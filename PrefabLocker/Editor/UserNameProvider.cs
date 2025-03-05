using UnityEditor;
using UnityEngine;

namespace PrefabLocker.Editor
{
    internal static class UserNameProvider
    {
        private const string USER_NAME_TAG = "PrefabLockerUserName";

        public static string GetUserName()
        {
            return EditorPrefs.GetString(USER_NAME_TAG, "");
        }

        /// <summary>
        /// Ensures a username exists. If not, shows a dialog to enter one.
        /// </summary>
        /// <returns>True if a username exists or was successfully entered</returns>
        public static bool EnsureUserNameExists(bool showPrompt)
        {
            string name = GetUserName();
            if (string.IsNullOrEmpty(name))
            {
                if (showPrompt)
                {
                    ShowUserNameDialog();
                    name = GetUserName();
                }
            }
            return !string.IsNullOrEmpty(name);
        }

        private static void ShowUserNameDialog()
        {
            // Create and show a custom window with a text field
            UserNameInputWindow window = EditorWindow.GetWindow<UserNameInputWindow>(true, "Enter User Name", true);
            window.ShowModal();
        }

        public static void SetUserName(string newUserName)
        {
            EditorPrefs.SetString(USER_NAME_TAG, newUserName);
        }
    }

    // Custom window for username input
    internal class UserNameInputWindow : EditorWindow
    {
        private string _userName = "";

        private void OnEnable()
        {
            // Set minimum window size
            minSize = new Vector2(300, 100);
            maxSize = new Vector2(400, 120);

            // Center the window
            position = new Rect(
                (Screen.currentResolution.width - minSize.x) / 2,
                (Screen.currentResolution.height - minSize.y) / 2,
                minSize.x,
                minSize.y
            );
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Please enter your name:", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            _userName = EditorGUILayout.TextField("User Name:", _userName);

            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("OK", GUILayout.Width(100)))
            {
                if (!string.IsNullOrEmpty(_userName))
                {
                    UserNameProvider.SetUserName(_userName);
                    Close();
                }
                else
                {
                    EditorUtility.DisplayDialog("Error", "User name cannot be empty.", "OK");
                }
            }

            EditorGUILayout.EndHorizontal();
        }
    }
}