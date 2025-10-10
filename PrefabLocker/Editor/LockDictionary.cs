using System.Collections.Generic;

namespace PrefabLocker.Editor
{
    [System.Serializable]
    public class LockDictionary
    {
        public class LockEntry
        {
            public string User;
            public string Branch;
        }
        public Dictionary<string, LockEntry> Locks;
    }
}
