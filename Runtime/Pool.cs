using UnityEngine;

namespace IntelliPool
{
    public static class Pool
    {
        static PoolService service;

        public static bool IsReady => service != null && service.IsInitialized;
        public static PoolService Current => service;
        public static PoolService Service => Resolve();

        public static PoolService Initialize(PoolDatabase database)
        {
            if (service != null)
            {
                Debug.LogWarning("[IntelliPool] Pool already has a service. Ignoring Initialize.", service);
                return service;
            }
            service = PoolBootstrap.CreateService(database);
            return service;
        }

        public static void Use(PoolService instance) => service = instance;

        public static GameObject Get(string id, Vector3 position = default, Quaternion rotation = default, Transform parent = null)
            => Resolve().Get(id, position, Normalize(rotation), parent);

        public static T Get<T>(string id, Vector3 position = default, Quaternion rotation = default, Transform parent = null) where T : Component
            => Resolve().Get<T>(id, position, Normalize(rotation), parent);

        public static GameObject Get(GameObject prefab, Vector3 position = default, Quaternion rotation = default, Transform parent = null)
            => Resolve().Get(prefab, position, Normalize(rotation), parent);

        public static bool Release(GameObject instance) => service != null && service.Release(instance);

        public static bool Release(Component component) => service != null && service.Release(component);

        public static void ReleaseDelayed(GameObject instance, float delay)
        {
            if (service != null) service.ReleaseDelayed(instance, delay);
        }

        internal static void RegisterIfUnset(PoolService instance)
        {
            if (service == null) service = instance;
        }

        internal static void ClearIfCurrent(PoolService instance)
        {
            if (service == instance) service = null;
        }

        static PoolService Resolve()
        {
            if (service == null) service = PoolBootstrap.CreateService(PoolBootstrap.FindDefaultDatabase());
            return service;
        }

        static Quaternion Normalize(Quaternion rotation)
            => rotation.x == 0f && rotation.y == 0f && rotation.z == 0f && rotation.w == 0f ? Quaternion.identity : rotation;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => service = null;
    }
}
