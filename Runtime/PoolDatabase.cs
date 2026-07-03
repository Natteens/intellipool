using System.Collections.Generic;
using UnityEngine;

namespace IntelliPool
{
    [CreateAssetMenu(fileName = "PoolDatabase", menuName = "IntelliPool/Pool Database")]
    public sealed class PoolDatabase : ScriptableObject
    {
        public List<PoolEntry> entries = new List<PoolEntry>();
        public bool debugLogs;

        public bool Validate(List<string> problems)
        {
            bool valid = true;
            var seen = new HashSet<string>();
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry.prefab == null)
                {
                    problems?.Add($"Entry {i} ('{entry.id}'): missing prefab.");
                    valid = false;
                }
                if (string.IsNullOrEmpty(entry.id))
                {
                    problems?.Add($"Entry {i}: empty id.");
                    valid = false;
                }
                else if (!seen.Add(entry.id))
                {
                    problems?.Add($"Entry {i}: duplicate id '{entry.id}'.");
                    valid = false;
                }
            }
            return valid;
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (string.IsNullOrEmpty(entry.id) && entry.prefab != null)
                    entry.id = entry.prefab.name;
            }
        }
#endif
    }
}
