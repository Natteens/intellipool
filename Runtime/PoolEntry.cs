using System;
using UnityEngine;

namespace IntelliPool
{
    [Serializable]
    public sealed class PoolEntry
    {
        [Tooltip("Stable id used by Pool.Get(id). Keep it unique.")]
        public string id;
        [Tooltip("Prefab instantiated and reused by this pool.")]
        public GameObject prefab;
        [Tooltip("Number of inactive instances created during initialization.")]
        public int prewarmCount;
        [Tooltip("Initial internal capacity used by Unity ObjectPool.")]
        public int defaultCapacity = 10;
        [Tooltip("Maximum number of inactive instances retained by Unity ObjectPool.")]
        public int maxSize = 100;
        [Tooltip("Detects invalid double releases in development/editor builds. Disable for maximum release performance if needed.")]
        public bool collectionCheck = true;
        [Tooltip("Returns active instances from this pool when a new scene is loaded.")]
        public bool clearOnSceneLoad;
        [Tooltip("Optional hierarchy container name for pooled objects.")]
        public string containerName;
    }
}
