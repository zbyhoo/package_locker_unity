using UnityEngine;
using UnityEditor;

namespace SocialWars.Editor.Scripts.FileLocker
{


    [CustomEditor(typeof(GameObject))]
    public class PrefabLockInspector : UnityEditor.Editor
    {
        private bool isPrefabAsset = false;
        private string assetPath = "";

        private void OnEnable()
        {
            assetPath = AssetDatabase.GetAssetPath(target);
            isPrefabAsset = !string.IsNullOrEmpty(assetPath) && assetPath.EndsWith(".prefab");
        }

        public override void OnInspectorGUI()
        {
            if (isPrefabAsset)
            {
                LockStatus status = PrefabLockOverlay.GetStatus(assetPath);
                if (status != null && status.locked)
                {
                    // Draw a horizontal block with the lock icon and status.
                    EditorGUILayout.BeginHorizontal();
                    PrefabLockOverlay.DrawLockIcon(new Rect(), status.user, true);
                    
                    bool isMyLock = status.user == UserNameProvider.GetUserName();
                    Color iconColor = isMyLock ? Color.green : Color.red;
                    Color originalColor = GUI.color;
                    GUI.color = iconColor;
                    GUILayout.Label($"Locked by: {status.user}", EditorStyles.boldLabel);
                    GUI.color = originalColor;
                    
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.Space();
                }

                
            }

            // Draw the rest of the inspector normally.
            DrawDefaultInspector();
        }
    }
}
