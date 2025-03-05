using System;

namespace PrefabLocker.Editor
{
    internal static class UserNameProvider
    {
        public static string GetUserName()
        {
            string name = PrefabLockerSettings.GetOrCreateSettings().UserName;
            if (string.IsNullOrEmpty(name))
            {
                throw new Exception("missing user name");
            }

            return name;
        }
    }
}
