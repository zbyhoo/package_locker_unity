using UnityEditor;
using UnityEngine;

namespace PrefabLocker.Editor
{
    [CustomEditor(typeof(GameObject))]
    public class PrefabLockInspector : UnityEditor.Editor
    {
        private bool _isPrefabAsset;
        private string _assetPath = "";

        private void OnEnable()
        {
            _assetPath = AssetDatabase.GetAssetPath(target);
            _isPrefabAsset = !string.IsNullOrEmpty(_assetPath) && _assetPath.EndsWith(".prefab");
        }

        public override void OnInspectorGUI()
        {
            if (_isPrefabAsset)
            {
                LockStatus status = PrefabLockOverlay.GetStatus(_assetPath);
                if (status != null && status.Locked)
                {
                    EditorGUILayout.BeginHorizontal();
                    PrefabLockOverlay.DrawLockIcon(new Rect(), status.User, true);
                    
                    bool isMyLock = status.User == UserNameProvider.GetUserName();
                    Color iconColor = isMyLock ? Color.green : Color.red;
                    Color originalColor = GUI.color;
                    GUI.color = iconColor;
                    GUILayout.Label($"Locked by: {status.User}", EditorStyles.boldLabel);
                    GUI.color = originalColor;
                    
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.Space();
                }

                
            }
            
            DrawDefaultInspector();
        }
    }
}
