using System.Collections.Generic;
using Unity.EditorCoroutines.Editor;
using UnityEditor;
using UnityEngine;

namespace SocialWars.Editor.Scripts.FileLocker
{
    [InitializeOnLoad]
    public static class PrefabLockOverlay
    {
        // A dictionary mapping asset paths to the username of the locker.
        private static Dictionary<string, string> _lockedPrefabs = new Dictionary<string, string>();

        // How often (in seconds) to update lock status from the server.
        private const float UPDATE_INTERVAL = 2f;
        private static double _nextUpdateTime;

        static PrefabLockOverlay()
        {
            // Subscribe to the project window GUI callback.
            EditorApplication.projectWindowItemOnGUI += OnProjectWindowItemGUI;
            _nextUpdateTime = EditorApplication.timeSinceStartup + UPDATE_INTERVAL;
            // Start initial update.
            EditorApplication.update += PeriodicUpdate;
        }

        private static void PeriodicUpdate()
        {
            if (EditorApplication.timeSinceStartup > _nextUpdateTime)
            {
                UpdateData();
            }
        }

        public static void UpdateData()
        {
            _nextUpdateTime = EditorApplication.timeSinceStartup + UPDATE_INTERVAL;
            EditorCoroutineUtility.StartCoroutineOwnerless(LockServiceClient.UpdateLockStatus(OnLockedUpdated));
        }

        private static void OnLockedUpdated(LockDictionary locks)
        {
            _lockedPrefabs = locks.locks;
            // Refresh the project window so the icons are updated.
            EditorApplication.RepaintProjectWindow();
        }

        private static void OnProjectWindowItemGUI(string guid, Rect selectionRect)
        {
            // Convert GUID to asset path.
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            if (!assetPath.EndsWith(".prefab"))
            {
                return;
            }

            // Check if this prefab is locked.
            if (_lockedPrefabs.TryGetValue(assetPath, out string lockedBy))
            {
                DrawLockIcon(new Rect(selectionRect.xMax - 16, selectionRect.y, 16, 16), lockedBy, false);
            }
        }

        public static LockStatus GetStatus(string assetPath)
        {
            bool exists = _lockedPrefabs.TryGetValue(assetPath, out string lockedBy);
            return new LockStatus
            {
                locked = exists && lockedBy != null,
                user = lockedBy
            };
        }

        public static void DrawLockIcon(Rect iconRect, string lockedBy, bool label)
        {
            // Determine icon color based on ownership.
            bool isMyLock = lockedBy == UserNameProvider.GetUserName();
            Color iconColor = isMyLock ? Color.green : Color.red;

            // Try to load a lock icon. You can put your custom icon in an Editor Resources folder.
            Texture2D lockIcon = EditorGUIUtility.IconContent("LockIcon-On").image as Texture2D;
            if (lockIcon != null)
            {
                // Save original GUI color.
                Color originalColor = GUI.color;
                GUI.color = iconColor;

                if (label)
                {
                    GUILayout.Label(lockIcon, GUILayout.Width(16), GUILayout.Height(16));
                }
                else
                {
                    GUI.DrawTexture(iconRect, lockIcon);
                }

                // Restore original GUI color.
                GUI.color = originalColor;
            }
        }
    }
}
