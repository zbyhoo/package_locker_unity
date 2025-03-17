using System.Collections;
using System.Collections.Generic;
using Unity.EditorCoroutines.Editor;
using UnityEditor;
using UnityEngine;

namespace PrefabLocker.Editor
{
    public class PrefabLockWindow : EditorWindow
    {
        private string _userName = "";
        private string _statusMessage = "";
        private Dictionary<string, string> _lockedFiles = new();
        private Vector2 _scrollPosition;
        private bool _isRefreshing;
        private SortField _currentSortField = SortField.FilePath;
        private bool _sortAscending = true;
        
        private enum SortField
        {
            FilePath,
            LockedBy
        }

        [MenuItem("Tools/Prefab Locker/Manager")]
        public static void ShowWindow()
        {
            PrefabLockWindow window = GetWindow<PrefabLockWindow>("Prefab Lock Manager");
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
            string newUser = EditorGUILayout.TextField("User Name", _userName);
            if (newUser != _userName)
            {
                UserNameProvider.SetUserName(newUser);
                _userName = newUser;
            }

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
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            
            if (_lockedFiles.Count == 0)
            {
                GUILayout.Label("No locked files found", EditorStyles.centeredGreyMiniLabel);
            }
            else
            {
                string currentUser = UserNameProvider.GetUserName();
                
                // Headers row
                EditorGUILayout.BeginHorizontal("box");
                EditorGUILayout.BeginHorizontal(GUILayout.Width(20));
                Rect iconRect = GUILayoutUtility.GetRect(16, 16, GUILayout.Width(20));
                PrefabLockOverlay.DrawLockIcon(iconRect, null, true, Color.grey);
                EditorGUILayout.EndHorizontal();

// File Path header with sorting button
                if (GUILayout.Button("File Path" + (_currentSortField == SortField.FilePath ? (_sortAscending ? " ↑" : " ↓") : ""), 
                        EditorStyles.boldLabel, GUILayout.MinWidth(200)))
                {
                    if (_currentSortField == SortField.FilePath)
                        _sortAscending = !_sortAscending;
                    else
                    {
                        _currentSortField = SortField.FilePath;
                        _sortAscending = true;
                    }
                }

                if (GUILayout.Button("Locked By" + (_currentSortField == SortField.LockedBy ? (_sortAscending ? " ↑" : " ↓") : ""), 
                        EditorStyles.boldLabel, GUILayout.Width(100)))
                {
                    if (_currentSortField == SortField.LockedBy)
                        _sortAscending = !_sortAscending;
                    else
                    {
                        _currentSortField = SortField.LockedBy;
                        _sortAscending = true;
                    }
                }

                EditorGUILayout.LabelField("Unlock", EditorStyles.boldLabel, GUILayout.Width(70));
                EditorGUILayout.EndHorizontal();

                List<string> sortedFiles = new(_lockedFiles.Keys);
                switch (_currentSortField)
                {
                    case SortField.FilePath:
                        sortedFiles.Sort((a, b) => _sortAscending ? 
                            string.Compare(a, b, System.StringComparison.OrdinalIgnoreCase) : 
                            string.Compare(b, a, System.StringComparison.OrdinalIgnoreCase));
                        break;
                    case SortField.LockedBy:
                        sortedFiles.Sort((a, b) => _sortAscending ? 
                            string.Compare(_lockedFiles[a], _lockedFiles[b], System.StringComparison.OrdinalIgnoreCase) : 
                            string.Compare(_lockedFiles[b], _lockedFiles[a], System.StringComparison.OrdinalIgnoreCase));
                        break;
                }
                
                foreach (string filePath in sortedFiles)
                {
                    string lockedBy = _lockedFiles[filePath];
                    bool isMyLock = lockedBy == currentUser;
                    
                    EditorGUILayout.BeginHorizontal("box");
                    
                    // Column 1: Lock icon
                    EditorGUILayout.BeginHorizontal(GUILayout.Width(20));
                    iconRect = GUILayoutUtility.GetRect(16, 16, GUILayout.Width(20));
                    PrefabLockOverlay.DrawLockIcon(iconRect, lockedBy, true);
                    EditorGUILayout.EndHorizontal();
                    
                    // Column 2: File path
                    // Replace the simple label with a clickable object field
                    Object asset = AssetDatabase.LoadAssetAtPath<Object>(filePath);
                    if (asset != null)
                    {
                        EditorGUI.BeginDisabledGroup(true); // Make it read-only
                        EditorGUILayout.ObjectField(asset, typeof(Object), false, GUILayout.MinWidth(200));
                        EditorGUI.EndDisabledGroup();
                    }
                    else
                    {
                        // Fallback to regular label if asset doesn't exist
                        EditorGUILayout.LabelField(filePath, GUILayout.MinWidth(200));
                    }
                    
                    // Column 3: Locked by user
                    EditorGUILayout.LabelField(lockedBy, GUILayout.Width(100));
                    
                    // Column 4: Unlock button
                    GUI.enabled = isMyLock;
                    if (GUILayout.Button("Unlock", GUILayout.Width(70)))
                    {
                        EditorCoroutineUtility.StartCoroutineOwnerless(UnlockSpecificFile(filePath));
                    }
                    GUI.enabled = true;
                    
                    EditorGUILayout.EndHorizontal();
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
    }
}
