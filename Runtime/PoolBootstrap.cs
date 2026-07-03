using UnityEngine;

namespace IntelliPool
{
    internal static class PoolBootstrap
    {
        public const string DatabaseResourcesPath = "IntelliPool/PoolDatabase";

        public static PoolDatabase FindDefaultDatabase()
        {
            var db = Resources.Load<PoolDatabase>(DatabaseResourcesPath);
            if (db != null) return db;

            var all = Resources.LoadAll<PoolDatabase>("");
            if (all.Length > 0) return all[0];

            Debug.LogWarning($"[IntelliPool] No PoolDatabase found in Resources ('{DatabaseResourcesPath}'). Pools by id are unavailable; Get(prefab) still works. Use Pool.Initialize(database) or place a PoolService for manual setup.");
            return null;
        }

        public static PoolService CreateService(PoolDatabase database)
        {
            var go = new GameObject("IntelliPool");
            Object.DontDestroyOnLoad(go);
            var service = go.AddComponent<PoolService>();
            service.Initialize(database);
            return service;
        }
    }
}
