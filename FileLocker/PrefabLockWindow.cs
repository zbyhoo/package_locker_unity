using System.Collections;
using Unity.EditorCoroutines.Editor;
using UnityEditor;
using UnityEngine;

namespace SocialWars.Editor.Scripts.FileLocker
{
    public class PrefabLockWindow : EditorWindow
    {
        private string filePath = "";
        private string statusMessage = "";

        [MenuItem("Tools/Prefab Lock Manager")]
        public static void ShowWindow()
        {
            GetWindow<PrefabLockWindow>("Prefab Lock Manager");
        }

        private void OnGUI()
        {
            GUILayout.Label("Lock/Unlock Prefab", EditorStyles.boldLabel);
            filePath = EditorGUILayout.TextField("Prefab Path", filePath);

            if (GUILayout.Button("Lock Prefab"))
            {
                EditorCoroutineUtility.StartCoroutineOwnerless(LockPrefab());
            }

            if (GUILayout.Button("Unlock Prefab"))
            {
                EditorCoroutineUtility.StartCoroutineOwnerless(UnlockPrefab());
            }

            GUILayout.Label("Status: " + statusMessage);
        }

        private IEnumerator LockPrefab()
        {
            yield return LockServiceClient.LockAsset(filePath, (success, response) =>
            {
                statusMessage = success ? "Lock successful." : "Lock failed: " + response;
                Repaint();
            });
        }

        private IEnumerator UnlockPrefab()
        {
            yield return LockServiceClient.UnlockAsset(filePath, (success, response) =>
            {
                statusMessage = success ? "Unlock successful." : "Unlock failed: " + response;
                Repaint();
            });
        }
    }
}
