using System;
using System.Collections;
using System.Collections.Specialized;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace PrefabLocker.Editor
{
    internal abstract class LockServiceClient
    {
        private static readonly Regex SlugRegex = new(@"^[a-z0-9._-]{1,64}$");
        private static bool _slugErrorDialogShown;

        // Replace with your actual cloud service URL.
        private static string ServiceUrl => PrefabLockerSettings.Get().GetServiceUrl();
        private static string Branch => GitProvider.GetBranch();
        private static string Origin => GitProvider.GetOrigin();

        private static bool isConfigValid()
        {
            string slug = PrefabLockerSettings.Get().ProjectSlug;
            if (!string.IsNullOrEmpty(slug) && SlugRegex.IsMatch(slug))
            {
                return true;
            }

            if (_slugErrorDialogShown == false)
            {
                _slugErrorDialogShown = true;
                Debug.LogError("Prefab Locker: ProjectSlug is missing or invalid. " +
                                "Set it in Tools/Prefab Locker/Settings (must match ^[a-z0-9._-]{1,64}$).");
                EditorUtility.DisplayDialog(
                    "Prefab Locker",
                    "ProjectSlug is missing or invalid.\n\nSet it in Tools/Prefab Locker/Settings.",
                    "OK");
            }

            return false;
        }

        private static WWWForm GetForm(string filePath)
        {
            WWWForm form = new();
            form.AddField("branch", Branch);
            form.AddField("origin", Origin);
            form.AddField("filePath", filePath);
            form.AddField("userName", UserNameProvider.GetUserName());
            return form;
        }

        private static NameValueCollection GetHeaders(string filePath)
        {
            NameValueCollection data = new()
            {
                ["branch"] = Branch,
                ["origin"] = Origin,
                ["filePath"] = filePath,
                ["userName"] = UserNameProvider.GetUserName()
            };
            return data;
        }

        private static string AddParamsToUrl(string url, string filePath = null)
        {
            url += $"?branch={Branch}&origin={Origin}";
            if (filePath != null)
            {
                url += $"&filePath={filePath}";
            }

            return url;
        }

        internal static IEnumerator LockAsset(string filePath, Action<bool, string> callback)
        {
            if (isConfigValid() == false)
            {
                callback(false, "Invalid ProjectSlug configuration");
                yield break;
            }

            UserNameProvider.EnsureUserNameExists(true);

            WWWForm form = GetForm(filePath);

            using UnityWebRequest www = UnityWebRequest.Post($"{ServiceUrl}/lock", form);
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.Log("Lock failed: " + www.error);
                callback(false, www.error);
            }
            else
            {
                // Optionally parse the response for more details.
                callback(true, www.downloadHandler.text);
                    
                PrefabLockOverlay.UpdateData();
            }
        }

        internal static IEnumerator UnlockAsset(string filePath, Action<bool, string> callback)
        {
            if (isConfigValid() == false)
            {
                callback(false, "Invalid ProjectSlug configuration");
                yield break;
            }

            WWWForm form = GetForm(filePath);

            using UnityWebRequest www = UnityWebRequest.Post($"{ServiceUrl}/unlock", form);
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.Log("Unlock failed: " + www.error);
                callback(false, www.error);
            }
            else
            {
                callback(true, www.downloadHandler.text);
                PrefabLockOverlay.UpdateData();
            }
        }

        internal static IEnumerator UpdateLockStatus(Action<LockDictionary> callback)
        {
            if (isConfigValid() == false)
            {
                yield break;
            }

            if (UserNameProvider.EnsureUserNameExists(false) == false)
            {
                Debug.Log("name not provided");
                yield break;
            }
            
            string url = AddParamsToUrl($"{ServiceUrl}/lockedAssets");
        
            // Use WebClient in a separate thread to avoid blocking the main thread
            bool requestCompleted = false;
            LockDictionary locks = null;
            string errorMessage = null;
            string json = null;
        
            System.Threading.ThreadPool.QueueUserWorkItem(_ => 
            {
                try
                {
                    using (WebClient client = new WebClient())
                    {
                        json = client.DownloadString(url);
                        locks = Newtonsoft.Json.JsonConvert.DeserializeObject<LockDictionary>(json);
                    }
                }
                catch (Exception ex)
                {
                    errorMessage = ex.Message;
                }
                finally
                {
                    requestCompleted = true;
                }
            });
        
            // Wait until the request completes
            yield return new WaitUntil(() => requestCompleted);
        
            if (errorMessage != null)
            {
                Debug.Log("Failed to update lock status: " + errorMessage);
                Debug.Log("Response: " + json);
            }
            else if (locks is { Locks: not null })
            {
                callback.Invoke(locks);
            }
        }
        
        internal static LockStatus GetLockStatus(string filePath)
        {
            if (isConfigValid() == false)
            {
                return null;
            }

            try
            {
                using WebClient client = new();
                string url = AddParamsToUrl($"{ServiceUrl}/status", filePath);
                string json = client.DownloadString(url);
                return Newtonsoft.Json.JsonConvert.DeserializeObject<LockStatus>(json);
            }
            catch (Exception ex)
            {
                Debug.Log("Error checking lock status for " + filePath + ": " + ex.Message);
                return null;
            }
        }
        
        internal static bool LockPrefab(string filePath)
        {
            if (isConfigValid() == false)
            {
                return false;
            }

            UserNameProvider.EnsureUserNameExists(true);
            try
            {
                using WebClient client = new();
                byte[] response = client.UploadValues($"{ServiceUrl}/lock", GetHeaders(filePath));
                string result = Encoding.UTF8.GetString(response);
                // Assume a successful response contains "locked successfully" or "already locked by you".
                if (!result.Contains("locked successfully") && !result.Contains("already locked by you"))
                {
                    return false;
                }

                PrefabLockOverlay.UpdateData();
                return true;
            }
            catch (Exception ex)
            {
                Debug.Log("Error locking prefab " + filePath + ": " + ex.Message);
                return false;
            }
        }
        
        // Add these methods to LockServiceClient class
        public static LockDictionary UpdateLockStatusSync()
        {
            if (isConfigValid() == false)
            {
                return null;
            }

            try
            {
                string url = AddParamsToUrl($"{ServiceUrl}/lockedAssets");

                using WebClient webClient = new();
                string response = webClient.DownloadString(url);
                return Newtonsoft.Json.JsonConvert.DeserializeObject<LockDictionary>(response);
            }
            catch (Exception e)
            {
                Debug.Log($"Failed to get locks: {e.Message}");
                return null;
            }
        }

        public static bool UnlockAssetSync(string assetPath)
        {
            if (isConfigValid() == false)
            {
                return false;
            }

            string endpoint = $"{ServiceUrl}/unlock";

            using WebClient webClient = new();
            byte[] responseBytes = webClient.UploadValues(endpoint, GetHeaders(assetPath));
            string response = Encoding.UTF8.GetString(responseBytes);
        
            bool success = !string.IsNullOrEmpty(response) && 
                           !response.Contains("error") && 
                           !response.Contains("fail");
            
            if (success)
            {
                PrefabLockOverlay.UpdateData();
            }
            
            return success;
        }
    }
}
