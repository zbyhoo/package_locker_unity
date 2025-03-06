using System;
using System.Collections;
using System.Collections.Specialized;
using System.Net;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace PrefabLocker.Editor
{
    internal abstract class LockServiceClient
    {
        // Replace with your actual cloud service URL.
        private static string ServiceUrl => PrefabLockerSettings.Get().GetServiceUrl();
        private static string Branch => GitProvider.GetBranch();
        private static string Origin => GitProvider.GetOrigin();

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
            WWWForm form = GetForm(filePath);

            using (UnityWebRequest www = UnityWebRequest.Post($"{ServiceUrl}/lock", form))
            {
                yield return www.SendWebRequest();

                if (www.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError("Lock failed: " + www.error);
                    callback(false, www.error);
                }
                else
                {
                    // Optionally parse the response for more details.
                    callback(true, www.downloadHandler.text);
                    
                    PrefabLockOverlay.UpdateData();
                }
            }
        }

        internal static IEnumerator UnlockAsset(string filePath, Action<bool, string> callback)
        {
            WWWForm form = GetForm(filePath);

            using (UnityWebRequest www = UnityWebRequest.Post($"{ServiceUrl}/unlock", form))
            {
                yield return www.SendWebRequest();

                if (www.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError("Unlock failed: " + www.error);
                    callback(false, www.error);
                }
                else
                {
                    callback(true, www.downloadHandler.text);
                    
                    PrefabLockOverlay.UpdateData();
                }
            }
        }

        internal static IEnumerator UpdateLockStatus(Action<LockDictionary> callback)
        {
            if (UserNameProvider.EnsureUserNameExists(false) == false)
            {
                Debug.Log("name not provided");
                yield break;
            }
            
            string url = AddParamsToUrl($"{ServiceUrl}/lockedAssets");

            using (UnityWebRequest www = UnityWebRequest.Get(url))
            {
                yield return www.SendWebRequest();

                if (www.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError("Failed to update lock status: " + www.error);
                }
                else
                {
                    string json = www.downloadHandler.text;
                    LockDictionary locks = Newtonsoft.Json.JsonConvert.DeserializeObject<LockDictionary>(json);
                    if (locks is { Locks: not null })
                    {
                        callback.Invoke(locks);
                    }
                }
            }
        }
        
        internal static LockStatus GetLockStatus(string filePath)
        {
            try
            {
                using (WebClient client = new())
                {
                    string url = AddParamsToUrl($"{ServiceUrl}/status", filePath);
                    string json = client.DownloadString(url);
                    return Newtonsoft.Json.JsonConvert.DeserializeObject<LockStatus>(json);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("Error checking lock status for " + filePath + ": " + ex.Message);
                return null;
            }
        }
        
        internal static bool LockPrefab(string filePath)
        {
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
                Debug.LogError("Error locking prefab " + filePath + ": " + ex.Message);
                return false;
            }
        }
    }
}
