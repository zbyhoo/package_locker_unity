using System;
using System.IO;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;

namespace PrefabLocker.Editor
{
    public class PrefabLockerSettings : ScriptableObject
    {
        private const string PATH = "Assets/PrefabLocker/Editor/PrefabLocker/";
        private const string FILE = "PrefabLockerSettings.asset";
        
        public string Url;
        public int Port;

        internal string GetServiceUrl()
        {
            return "http://" + Url + ":" + Port;
        }

        [MenuItem("Tools/Prefab Locker/Settings")]
        public static void SelectSettings()
        {
            PrefabLockerSettings settings = GetOrCreateSettings();
            Selection.activeObject = settings;
        }

        [NotNull]
        internal static PrefabLockerSettings GetOrCreateSettings()
        {
            PrefabLockerSettings settings = AssetDatabase.LoadAssetAtPath<PrefabLockerSettings>($"{PATH}{FILE}");
            if (settings == null)
            {
                settings = CreateSettings();
            }

            if (settings == null)
            {
                throw new Exception("cannot get or create settings");
            }

            return settings;
        }

        [CanBeNull]
        private static PrefabLockerSettings CreateSettings()
        {
            ScriptableObject asset = CreateInstance<PrefabLockerSettings>();

            if (Directory.Exists(PATH) == false)
            {
                Directory.CreateDirectory(PATH);
            }

            AssetDatabase.CreateAsset(asset, $"{PATH}{FILE}");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return (PrefabLockerSettings) asset;
        }
    }
}
