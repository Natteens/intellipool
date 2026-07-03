using System.Collections;
using UnityEngine;

namespace IntelliPool
{
    [DisallowMultipleComponent]
    public sealed class PooledObject : MonoBehaviour
    {
        PoolService service;
        IPoolable[] poolables;
        Coroutine delayed;

        internal PoolBucket Bucket { get; private set; }

        public string PoolId => Bucket?.Entry.id;
        public bool IsSpawned { get; private set; }

        internal void Setup(PoolService owner, PoolBucket bucket)
        {
            service = owner;
            Bucket = bucket;
            poolables = GetComponentsInChildren<IPoolable>(true);
        }

        public bool Release()
        {
            return service != null && service.Release(this);
        }

        public void ReleaseDelayed(float delay)
        {
            if (delay <= 0f)
            {
                Release();
                return;
            }
            ScheduleRelease(delay);
        }

        internal void ScheduleRelease(float delay)
        {
            if (!IsSpawned) return;
            if (!isActiveAndEnabled)
            {
                Release();
                return;
            }
            CancelScheduledRelease();
            delayed = StartCoroutine(DelayedRelease(delay));
        }

        internal void CancelScheduledRelease()
        {
            if (delayed == null) return;
            StopCoroutine(delayed);
            delayed = null;
        }

        IEnumerator DelayedRelease(float delay)
        {
            yield return new WaitForSeconds(delay);
            delayed = null;
            Release();
        }

        internal void MarkSpawned() => IsSpawned = true;
        internal void MarkReturned() => IsSpawned = false;

        internal void NotifySpawned()
        {
            for (int i = 0; i < poolables.Length; i++)
                poolables[i].OnSpawnedFromPool();
        }

        internal void NotifyReturned()
        {
            for (int i = 0; i < poolables.Length; i++)
                poolables[i].OnReturnedToPool();
        }

        void OnDestroy()
        {
            Bucket?.Active.Remove(this);
        }
    }
}
