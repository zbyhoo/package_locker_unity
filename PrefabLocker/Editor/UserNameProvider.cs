using System;
using UnityEditor;

namespace PrefabLocker.Editor
{
    internal static class UserNameProvider
    {
        private const string USER_NAME_TAG = "PrefabLockerUserName";
        public static string GetUserName()
        {
            string name = EditorPrefs.GetString(USER_NAME_TAG, "");
            if (string.IsNullOrEmpty(name))
            {
                throw new Exception("missing user name");
            }

            return name;
        }

        public static void SetUserName(string newUserName)
        {
            EditorPrefs.SetString(USER_NAME_TAG, newUserName);
        }
    }
}
