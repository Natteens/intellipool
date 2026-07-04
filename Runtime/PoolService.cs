using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.SceneManagement;

namespace IntelliPool
{
    public readonly struct PoolStats
    {
        public readonly int CountAll;
        public readonly int CountActive;
        public readonly int CountInactive;
        public readonly int MaxSize;

        public PoolStats(int countAll, int countActive, int countInactive, int maxSize)
        {
            CountAll = countAll;
            CountActive = countActive;
            CountInactive = countInactive;
            MaxSize = maxSize;
        }
    }

    internal sealed class PoolBucket
    {
        public PoolEntry Entry;
        public ObjectPool<PooledObject> Pool;
        public Transform Container;
        public readonly HashSet<PooledObject> Active = new HashSet<PooledObject>();
    }

    public sealed class PoolService : MonoBehaviour
    {
        [SerializeField] private PoolDatabase database;

        readonly Dictionary<string, PoolBucket> byId = new Dictionary<string, PoolBucket>();
        readonly Dictionary<GameObject, PoolBucket> byPrefab = new Dictionary<GameObject, PoolBucket>();
        readonly List<PoolBucket> buckets = new List<PoolBucket>();
        readonly List<PooledObject> scratch = new List<PooledObject>();
        bool initialized;

        public bool IsInitialized => initialized;
        public PoolDatabase Database => database;
        public IEnumerable<string> PoolIds => byId.Keys;

        void Awake()
        {
            Pool.RegisterIfUnset(this);
            if (database != null && !initialized)
                Initialize(database);
        }

        void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            for (int i = 0; i < buckets.Count; i++)
                buckets[i].Pool.Clear();
            Pool.ClearIfCurrent(this);
        }

        public void Initialize(PoolDatabase db)
        {
            if (initialized)
            {
                Debug.LogWarning("[IntelliPool] PoolService already initialized.", this);
                return;
            }

            database = db;
            initialized = true;
            SceneManager.sceneLoaded += OnSceneLoaded;

            if (db == null) return;

            for (int i = 0; i < db.entries.Count; i++)
            {
                var entry = db.entries[i];
                if (entry.prefab == null || string.IsNullOrEmpty(entry.id))
                {
                    Debug.LogError($"[IntelliPool] Invalid pool entry {i} ('{entry.id}') skipped.", db);
                    continue;
                }
                if (byId.ContainsKey(entry.id))
                {
                    Debug.LogError($"[IntelliPool] Duplicate pool id '{entry.id}' skipped.", db);
                    continue;
                }

                var bucket = CreateBucket(entry);
                byId.Add(entry.id, bucket);
                if (!byPrefab.ContainsKey(entry.prefab))
                    byPrefab.Add(entry.prefab, bucket);
                Prewarm(bucket);
            }
        }

        public GameObject Get(string id) => Get(id, Vector3.zero, Quaternion.identity, null);

        public GameObject Get(string id, Vector3 position, Quaternion rotation, Transform parent = null)
        {
            var bucket = GetBucket(id);
            return bucket == null ? null : Spawn(bucket, position, rotation, parent);
        }

        public GameObject Get(GameObject prefab) => Get(prefab, Vector3.zero, Quaternion.identity, null);

        public GameObject Get(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null)
        {
            if (prefab == null)
            {
                Debug.LogError("[IntelliPool] Get called with null prefab.", this);
                return null;
            }
            if (!byPrefab.TryGetValue(prefab, out var bucket))
                bucket = CreateRuntimeBucket(prefab);
            return Spawn(bucket, position, rotation, parent);
        }

        public T Get<T>(string id) where T : Component => Get<T>(id, Vector3.zero, Quaternion.identity, null);

        public T Get<T>(string id, Vector3 position, Quaternion rotation, Transform parent = null) where T : Component
        {
            var instance = Get(id, position, rotation, parent);
            if (instance == null) return null;
            if (instance.TryGetComponent(out T component)) return component;

            Debug.LogError($"[IntelliPool] Pool '{id}' instance has no {typeof(T).Name} component.", instance);
            Release(instance);
            return null;
        }

        public bool Release(GameObject instance)
        {
            if (instance == null) return false;
            if (instance.TryGetComponent(out PooledObject handle)) return Release(handle);

            Debug.LogWarning($"[IntelliPool] '{instance.name}' is not a pooled object.", instance);
            return false;
        }

        public bool Release(Component component)
        {
            return component != null && Release(component.gameObject);
        }

        internal bool Release(PooledObject handle)
        {
            if (handle == null || handle.Bucket == null) return false;
            if (!handle.IsSpawned)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning($"[IntelliPool] Double release of '{handle.name}' ignored.", handle);
#endif
                return false;
            }

            handle.CancelScheduledRelease();
            handle.NotifyReturned();
            handle.MarkReturned();
            handle.Bucket.Active.Remove(handle);
            handle.Bucket.Pool.Release(handle);
            return true;
        }

        public void ReleaseDelayed(GameObject instance, float delay)
        {
            if (instance == null) return;
            if (delay <= 0f)
            {
                Release(instance);
                return;
            }
            if (instance.TryGetComponent(out PooledObject handle))
                handle.ScheduleRelease(delay);
            else
                Debug.LogWarning($"[IntelliPool] '{instance.name}' is not a pooled object.", instance);
        }

        public void ReleaseAll(string id)
        {
            if (byId.TryGetValue(id, out var bucket))
                ReleaseAll(bucket);
        }

        public void Clear(string id)
        {
            if (byId.TryGetValue(id, out var bucket))
                Clear(bucket);
        }

        public void ClearAll()
        {
            for (int i = 0; i < buckets.Count; i++)
                Clear(buckets[i]);
        }

        public bool TryGetStats(string id, out PoolStats stats)
        {
            if (!string.IsNullOrEmpty(id) && byId.TryGetValue(id, out var bucket))
            {
                stats = new PoolStats(bucket.Pool.CountAll, bucket.Active.Count, bucket.Pool.CountInactive, bucket.Entry.maxSize);
                return true;
            }
            stats = default;
            return false;
        }

        PoolBucket GetBucket(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                Debug.LogError("[IntelliPool] Pool id is null or empty.", this);
                return null;
            }
            if (byId.TryGetValue(id, out var bucket)) return bucket;

            Debug.LogError($"[IntelliPool] Unknown pool id '{id}'.", this);
            return null;
        }

        PoolBucket CreateBucket(PoolEntry entry)
        {
            var bucket = new PoolBucket { Entry = entry };

            var containerGO = new GameObject(string.IsNullOrEmpty(entry.containerName) ? $"Pool_{entry.id}" : entry.containerName);
            containerGO.transform.SetParent(transform, false);
            bucket.Container = containerGO.transform;

            bucket.Pool = new ObjectPool<PooledObject>(
                () => CreateInstance(bucket),
                null,
                handle => OnReleasedToPool(handle, bucket),
                handle => { if (handle != null) Destroy(handle.gameObject); },
                entry.collectionCheck,
                Mathf.Max(1, entry.defaultCapacity),
                Mathf.Max(1, entry.maxSize));

            buckets.Add(bucket);
            return bucket;
        }

        PoolBucket CreateRuntimeBucket(GameObject prefab)
        {
            var entry = new PoolEntry { id = prefab.name, prefab = prefab };
            var bucket = CreateBucket(entry);
            byPrefab.Add(prefab, bucket);
            if (!byId.ContainsKey(entry.id))
                byId.Add(entry.id, bucket);
            return bucket;
        }

        PooledObject CreateInstance(PoolBucket bucket)
        {
            var instance = Instantiate(bucket.Entry.prefab, bucket.Container);
            instance.SetActive(false);
            if (!instance.TryGetComponent(out PooledObject handle))
                handle = instance.AddComponent<PooledObject>();
            handle.Setup(this, bucket);
            return handle;
        }

        static void OnReleasedToPool(PooledObject handle, PoolBucket bucket)
        {
            handle.gameObject.SetActive(false);
            handle.transform.SetParent(bucket.Container, false);
        }

        GameObject Spawn(PoolBucket bucket, Vector3 position, Quaternion rotation, Transform parent)
        {
            var handle = bucket.Pool.Get();
            var t = handle.transform;
            t.SetParent(parent != null ? parent : bucket.Container, false);
            t.SetPositionAndRotation(position, rotation);
            bucket.Active.Add(handle);
            handle.MarkSpawned();
            handle.gameObject.SetActive(true);
            handle.NotifySpawned();
            return handle.gameObject;
        }

        void Prewarm(PoolBucket bucket)
        {
            int count = bucket.Entry.prewarmCount;
            if (bucket.Entry.maxSize > 0)
                count = Mathf.Min(count, bucket.Entry.maxSize);
            if (count <= 0) return;

            scratch.Clear();
            for (int i = 0; i < count; i++)
                scratch.Add(bucket.Pool.Get());
            for (int i = 0; i < count; i++)
                bucket.Pool.Release(scratch[i]);
            scratch.Clear();
        }

        void ReleaseAll(PoolBucket bucket)
        {
            if (bucket.Active.Count == 0) return;

            scratch.Clear();
            scratch.AddRange(bucket.Active);
            for (int i = 0; i < scratch.Count; i++)
                Release(scratch[i]);
            scratch.Clear();
        }

        void Clear(PoolBucket bucket)
        {
            ReleaseAll(bucket);
            bucket.Pool.Clear();
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            for (int i = 0; i < buckets.Count; i++)
            {
                if (buckets[i].Entry.clearOnSceneLoad)
                    ReleaseAll(buckets[i]);
            }
        }
    }
}
