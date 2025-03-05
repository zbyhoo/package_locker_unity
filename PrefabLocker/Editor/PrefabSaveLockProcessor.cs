using System.Collections.Generic;
using UnityEditor;

namespace PrefabLocker.Editor
{
    public class PrefabSaveLockProcessor : AssetModificationProcessor
    {
        /// <summary>
        /// This method is called before saving assets.
        /// We intercept prefab saves, check lock status, and automatically lock if possible.
        /// Return only the list of asset paths that should be saved.
        /// </summary>
        public static string[] OnWillSaveAssets(string[] paths)
        {
            List<string> allowedPaths = new List<string>();

            foreach (string path in paths)
            {
                // We only want to enforce lock checks for prefab files.
                if (path.EndsWith(".prefab"))
                {
                    LockStatus status = LockServiceClient.GetLockStatus(path);
                    if (status == null)
                    {
                        EditorUtility.DisplayDialog("Error", "Failed to check lock status for " + path, "OK");
                        continue; // Skip saving this prefab.
                    }
                    // If not locked, try to lock automatically.
                    if (!status.Locked)
                    {
                        bool lockedNow = LockServiceClient.LockPrefab(path);
                        if (!lockedNow)
                        {
                            EditorUtility.DisplayDialog("Lock Error", "Could not lock prefab " + path + " for saving.", "OK");
                            continue; // Do not save this prefab.
                        }
                    }
                    // If the prefab is locked but not by the current user, cancel its save.
                    else if (status.Locked && status.User != UserNameProvider.GetUserName())
                    {
                        EditorUtility.DisplayDialog("Lock Violation", "Prefab " + path + " is locked by " + status.User + ". Save aborted.", "OK");
                        continue;
                    }
                    // If we reach here, the prefab is either already locked by currentUser or was just locked.
                }
                allowedPaths.Add(path);
            }
            return allowedPaths.ToArray();
        }
    }
}
