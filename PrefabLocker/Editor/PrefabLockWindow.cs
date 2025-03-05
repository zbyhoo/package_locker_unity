using System.Collections;
using System.Collections.Generic;
using Unity.EditorCoroutines.Editor;
using UnityEditor;
using UnityEngine;

namespace PrefabLocker.Editor
{
    public class PrefabLockWindow : EditorWindow
    {
        private string _filePath = "";
        private string _userName = "";
        private string _statusMessage = "";
        private Dictionary<string, string> _lockedFiles = new Dictionary<string, string>();
        private Vector2 _scrollPosition;
        private bool _isRefreshing = false;

        [MenuItem("Tools/Prefab Locker/Manager")]
        public static void ShowWindow()
        {
            PrefabLockWindow window = GetWindow<PrefabLockWindow>("Prefab Lock Manager");
            window._filePath = AssetDatabase.GetAssetPath(Selection.activeObject);
            window.RefreshLocksList();
            UserNameProvider.EnsureUserNameExists(true);
            window._userName = UserNameProvider.GetUserName();
        }
        
        private void OnEnable()
        {
            RefreshLocksList();
        }

        private void OnGUI()
        {
            GUILayout.Label("Lock/Unlock Prefab", EditorStyles.boldLabel);
            string newUser = EditorGUILayout.TextField("User Name", _userName);
            if (newUser != _userName)
            {
                UserNameProvider.SetUserName(newUser);
                _userName = newUser;
            }

            _filePath = EditorGUILayout.TextField("Prefab Path", _filePath);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Lock Prefab"))
            {
                EditorCoroutineUtility.StartCoroutineOwnerless(LockPrefab());
            }
            EditorGUILayout.EndHorizontal();

            GUILayout.Label("Status: " + _statusMessage);

            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("All Locked Files", EditorStyles.boldLabel);
            GUI.enabled = !_isRefreshing;
            if (GUILayout.Button("Refresh", GUILayout.Width(80)))
            {
                RefreshLocksList();
            }
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            DrawLockedFilesList();
        }

        private void DrawLockedFilesList()
        {
            EditorGUILayout.BeginVertical("box");
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, GUILayout.Height(300));
            
            if (_lockedFiles.Count == 0)
            {
                GUILayout.Label("No locked files found", EditorStyles.centeredGreyMiniLabel);
            }
            else
            {
                string currentUser = UserNameProvider.GetUserName();
                
                foreach (var pair in _lockedFiles)
                {
                    string filePath = pair.Key;
                    string lockedBy = pair.Value;
                    bool isMyLock = lockedBy == currentUser;

                    EditorGUILayout.BeginHorizontal("box");
                    
                    // Show the lock icon with appropriate color
                    Rect iconRect = GUILayoutUtility.GetRect(16, 16);
                    PrefabLockOverlay.DrawLockIcon(iconRect, lockedBy, true);
                    
                    // File path and owner info
                    EditorGUILayout.BeginVertical();
                    GUILayout.Label(filePath, EditorStyles.boldLabel);
                    GUILayout.Label("Locked by: " + lockedBy);
                    EditorGUILayout.EndVertical();
                    
                    // Only enable unlock button for files locked by current user
                    GUI.enabled = isMyLock;
                    if (GUILayout.Button("Unlock", GUILayout.Width(70)))
                    {
                        EditorCoroutineUtility.StartCoroutineOwnerless(UnlockSpecificFile(filePath));
                    }
                    GUI.enabled = true;

                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.Space(2);
                }
            }
            
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void RefreshLocksList()
        {
            _isRefreshing = true;
            EditorCoroutineUtility.StartCoroutineOwnerless(FetchAllLocks());
        }

        private IEnumerator FetchAllLocks()
        {
            yield return LockServiceClient.UpdateLockStatus((locks) =>
            {
                _lockedFiles = locks.Locks;
                _isRefreshing = false;
                Repaint();
            });
        }

        private IEnumerator UnlockSpecificFile(string filePath)
        {
            yield return LockServiceClient.UnlockAsset(filePath, (success, response) =>
            {
                if (success)
                {
                    _statusMessage = $"Unlocked: {filePath}";
                    // Update the local list immediately for better UX
                    if (_lockedFiles.ContainsKey(filePath))
                    {
                        _lockedFiles.Remove(filePath);
                    }
                }
                else
                {
                    _statusMessage = $"Failed to unlock: {response}";
                }
                Repaint();
            });
            
            // Refresh the list after a short delay to ensure server has processed the change
            yield return new EditorWaitForSeconds(0.5f);
            RefreshLocksList();
        }

        [MenuItem("Assets/Prefab Locker/Manager")]
        public static void ShowFromAsset()
        {
            ShowWindow();
        }

        [MenuItem("Assets/Prefab Locker/Lock Asset")]
        public static void LockSelectedAsset()
        {
            // Ensure username exists before attempting to lock
            if (!UserNameProvider.EnsureUserNameExists(true))
            {
                EditorUtility.DisplayDialog("Cannot Lock Asset", "You must enter a username to lock assets.", "OK");
                return;
            }
    
            string path = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (!path.EndsWith(".prefab") && !path.EndsWith(".unity"))
            {
                EditorUtility.DisplayDialog("Error", "Selected asset is not a prefab or scene.", "OK");
                return;
            }

            EditorCoroutineUtility.StartCoroutineOwnerless(LockAssetCoroutine(path));
        }

        [MenuItem("Assets/Prefab Locker/Lock Asset", true)]
        public static bool ValidateLockSelectedAsset()
        {
            string path = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (!path.EndsWith(".prefab") && !path.EndsWith(".unity"))
                return false;

            // Check if asset is already locked
            LockStatus status = PrefabLockOverlay.GetStatus(path);
            if (status.Locked)
                return false;
        
            // Don't verify username here - just check if path is valid
            return true;
        }

        [MenuItem("Assets/Prefab Locker/Unlock Asset")]
        public static void UnlockSelectedAsset()
        {
            string path = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (!path.EndsWith(".prefab") && !path.EndsWith(".unity"))
            {
                EditorUtility.DisplayDialog("Error", "Selected asset is not a prefab or scene.", "OK");
                return;
            }

            EditorCoroutineUtility.StartCoroutineOwnerless(UnlockAssetCoroutine(path));
        }

        [MenuItem("Assets/Prefab Locker/Unlock Asset", true)]
        public static bool ValidateUnlockSelectedAsset()
        {
            string path = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (!path.EndsWith(".prefab") && !path.EndsWith(".unity"))
                return false;

            try
            {
                string userName = UserNameProvider.GetUserName();
                LockStatus status = PrefabLockOverlay.GetStatus(path);
                return !string.IsNullOrEmpty(userName) && status.Locked && status.User == userName;
            }
            catch
            {
                return false;
            }
        }

        private static IEnumerator LockAssetCoroutine(string path)
        {
            yield return LockServiceClient.LockAsset(path, (success, response) =>
            {
                string message = success ? "Lock successful." : "Lock failed: " + response;
                EditorUtility.DisplayDialog("Lock Prefab", message, "OK");
                PrefabLockOverlay.UpdateData();
            });
        }

        private static IEnumerator UnlockAssetCoroutine(string path)
        {
            yield return LockServiceClient.UnlockAsset(path, (success, response) =>
            {
                string message = success ? "Unlock successful." : "Unlock failed: " + response;
                EditorUtility.DisplayDialog("Unlock Prefab", message, "OK");
                PrefabLockOverlay.UpdateData();
            });
        }

        private IEnumerator LockPrefab()
        {
            yield return LockServiceClient.LockAsset(_filePath, (success, response) =>
            {
                _statusMessage = success ? "Lock successful." : "Lock failed: " + response;
                Repaint();
            });
        }

        private IEnumerator UnlockPrefab()
        {
            yield return LockServiceClient.UnlockAsset(_filePath, (success, response) =>
            {
                _statusMessage = success ? "Unlock successful." : "Unlock failed: " + response;
                Repaint();
            });
        }
    }
}
