using System;
using UnityEngine;

namespace IntelliPool
{
    [Serializable]
    public sealed class PoolEntry
    {
        public string id;
        public GameObject prefab;
        public int prewarmCount;
        public int defaultCapacity = 10;
        public int maxSize = 100;
        public bool collectionCheck = true;
        public bool clearOnSceneLoad;
        public string containerName;
    }
}
