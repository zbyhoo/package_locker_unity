using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.EditorCoroutines.Editor;
using UnityEditor;
using UnityEngine;

namespace PrefabLocker.Editor
{
    [InitializeOnLoad]
    public static class AutoUnlockService
    {
        private const float CHECK_INTERVAL_SECONDS = 60f;
        private static DateTime _lastCheckTime = DateTime.MinValue;
        private static bool _isCheckingNow;

        // Store recently unlocked assets to show in notification
        private static readonly List<string> RecentlyUnlockedAssets = new();

        static AutoUnlockService()
        {
            EditorApplication.update += PeriodicCheck;
            
            // Subscribe to Unity's quitting event to perform final unlock check
            EditorApplication.quitting += OnEditorQuitting;
        }
        
        private static void OnEditorQuitting()
        {
            // When Unity is closing, perform a synchronous check for auto-unlock
            // to ensure we don't leave things locked unnecessarily
            CheckLockedAssetsSync();
        }

        private static void PeriodicCheck()
        {
            if (_isCheckingNow)
                return;

            if ((DateTime.Now - _lastCheckTime).TotalSeconds >= CHECK_INTERVAL_SECONDS)
            {
                _lastCheckTime = DateTime.Now;
                _isCheckingNow = true;
                EditorCoroutineUtility.StartCoroutineOwnerless(CheckLockedAssets());
            }
        }

        private static IEnumerator CheckLockedAssets()
        {
            // Clear the list for this check cycle
            RecentlyUnlockedAssets.Clear();

            // Get all locked files
            Dictionary<string, string> lockedFiles = new();
            bool gotLocks = false;

            yield return LockServiceClient.UpdateLockStatus((locks) =>
            {
                lockedFiles = locks.Locks;
                gotLocks = true;
            });

            // Wait until we get the locks
            yield return new WaitUntil(() => gotLocks);

            yield return ProcessLockedFiles(lockedFiles);
        }
        
        // Synchronous version of the check for editor quitting
        private static void CheckLockedAssetsSync()
        {
            try
            {
                // Clear the list for this check cycle
                RecentlyUnlockedAssets.Clear();
                
                // Get all locked files directly (synchronous call)
                LockDictionary locks = LockServiceClient.UpdateLockStatusSync();
                if (locks != null)
                {
                    // Process locked files synchronously
                    ProcessLockedFilesSync(locks.Locks);
                    
                    // Log the results without showing a dialog (since editor is quitting)
                    if (RecentlyUnlockedAssets.Count > 0)
                    {
                        Debug.Log($"[Prefab Locker] Auto-unlocked {RecentlyUnlockedAssets.Count} assets on editor exit:\n{string.Join("\n", RecentlyUnlockedAssets)}");
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[Prefab Locker] Error during exit auto-unlock: {e.Message}");
            }
        }

        // Shared processing logic for both async and sync paths
        private static IEnumerator ProcessLockedFiles(Dictionary<string, string> lockedFiles)
        {
            // Only check files locked by current user
            string currentUser = UserNameProvider.GetUserName();
            List<string> myLockedFiles = lockedFiles
                .Where(kvp => kvp.Value == currentUser)
                .Select(kvp => kvp.Key)
                .ToList();

            if (myLockedFiles.Count == 0)
            {
                _isCheckingNow = false;
                yield break;
            }

            // Check each locked file
            foreach (string assetPath in myLockedFiles)
            {
                // Skip files that don't exist
                if (!System.IO.File.Exists(assetPath))
                    continue;

                // Check if file has been committed and pushed
                bool canUnlock = CheckIfCommittedAndPushed(assetPath);

                if (canUnlock)
                {
                    // Unlock the asset
                    bool unlocked = false;
                    yield return LockServiceClient.UnlockAsset(assetPath, (success, _) =>
                    {
                        unlocked = success;
                    });

                    if (unlocked)
                    {
                        RecentlyUnlockedAssets.Add(assetPath);
                    }
                }

                // Short delay between files to not overwhelm the system
                yield return new EditorWaitForSeconds(0.1f);
            }

            // Show notification if any files were unlocked
            if (RecentlyUnlockedAssets.Count > 0)
            {
                ShowUnlockedAssetsNotification();
            }

            // Update overlay
            PrefabLockOverlay.UpdateData();

            _isCheckingNow = false;
        }
        
        // Synchronous version of the processing logic for editor quit
        private static void ProcessLockedFilesSync(Dictionary<string, string> lockedFiles)
        {
            // Only check files locked by current user
            string currentUser = UserNameProvider.GetUserName();
            List<string> myLockedFiles = lockedFiles
                .Where(kvp => kvp.Value == currentUser)
                .Select(kvp => kvp.Key)
                .ToList();

            if (myLockedFiles.Count == 0)
                return;

            // Check each locked file
            foreach (string assetPath in myLockedFiles)
            {
                // Skip files that don't exist
                if (!System.IO.File.Exists(assetPath))
                    continue;

                // Check if file has been committed and pushed
                bool canUnlock = CheckIfCommittedAndPushed(assetPath);

                if (canUnlock)
                {
                    // Unlock the asset synchronously
                    if (LockServiceClient.UnlockAssetSync(assetPath))
                    {
                        RecentlyUnlockedAssets.Add(assetPath);
                    }
                }
            }
        }

        private static bool CheckIfCommittedAndPushed(string assetPath)
        {
            // Check if file has local changes
            bool hasLocalChanges = GitProvider.HasLocalChanges(assetPath);
            if (hasLocalChanges)
            {
                return false;
            }
            
            bool pushed = GitProvider.IsLastCommitPushedToRemote();
            return pushed;
        }

        private static void ShowUnlockedAssetsNotification()
        {
            string message;
            if (RecentlyUnlockedAssets.Count == 1)
            {
                string filename = System.IO.Path.GetFileName(RecentlyUnlockedAssets[0]);
                message = $"Auto-unlocked asset: {filename}";
            }
            else
            {
                message = $"Auto-unlocked {RecentlyUnlockedAssets.Count} assets that were committed and pushed";
            }

            EditorUtility.DisplayDialog("Auto-Unlock Service",
                $"{message}\n\nThese assets had no local changes and were committed to the repository.",
                "OK");

            // Log to console as well for reference
            Debug.Log($"[Prefab Locker] {message}:\n{string.Join("\n", RecentlyUnlockedAssets)}");
        }

        // Public method to trigger a check manually
        public static void CheckNow()
        {
            if (!_isCheckingNow)
            {
                _isCheckingNow = true;
                EditorCoroutineUtility.StartCoroutineOwnerless(CheckLockedAssets());
            }
        }
    }

    // Add menu item to manually trigger the check
    public static class AutoUnlockMenu
    {
        [MenuItem("Tools/Prefab Locker/Check for Auto-Unlock")]
        public static void CheckForAutoUnlock()
        {
            AutoUnlockService.CheckNow();
        }
    }
}