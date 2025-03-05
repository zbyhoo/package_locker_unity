using System.Collections;
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

        [MenuItem("Tools/Prefab Locker/Manager")]
        public static void ShowWindow()
        {
            PrefabLockWindow window = GetWindow<PrefabLockWindow>("Prefab Lock Manager");
            window._filePath = AssetDatabase.GetAssetPath(Selection.activeObject);
            window._userName = EditorPrefs.GetString("PrefabLockerUserName", "");
        }

        [MenuItem("Assets/Prefab Locker Manager")]
        public static void ShowFromAsset()
        {
            ShowWindow();
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

            if (GUILayout.Button("Lock Prefab"))
            {
                EditorCoroutineUtility.StartCoroutineOwnerless(LockPrefab());
            }

            if (GUILayout.Button("Unlock Prefab"))
            {
                EditorCoroutineUtility.StartCoroutineOwnerless(UnlockPrefab());
            }

            GUILayout.Label("Status: " + _statusMessage);
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
