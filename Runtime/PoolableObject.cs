using System;
using System.Collections;
using UnityEngine;

namespace IntelliPool
{
    public class PoolableObject : MonoBehaviour
    {
        #region Propriedades
        public int InstanceID { get; private set; }
        public bool IsInPool { get; private set; }
        public string PoolTag { get; set; }
        #endregion

        #region Eventos
        public event Action OnSpawnFromPool;
        public event Action OnReturnToPool;
        #endregion

        #region Campos Privados
        private Coroutine despawnTimer;
        private Vector3 originalPosition;
        private Quaternion originalRotation;
        private Vector3 originalScale;
        #endregion

        #region Unity Lifecycle
        void Awake()
        {
            InstanceID = GetInstanceID();
            StoreOriginalTransform();
        }

        void OnEnable()
        {
            if (!IsInPool)
            {
                OnSpawnFromPool?.Invoke();
            }
        }

        void OnDisable()
        {
            StopDespawnTimer();
            
            if (IsInPool)
            {
                ResetToOriginalState();
                OnReturnToPool?.Invoke();
            }
        }
        #endregion

        #region Configuração
        void StoreOriginalTransform()
        {
            originalPosition = transform.localPosition;
            originalRotation = transform.localRotation;
            originalScale = transform.localScale;
        }

        void ResetToOriginalState()
        {
            transform.localPosition = originalPosition;
            transform.localRotation = originalRotation;
            transform.localScale = originalScale;
        }

        public void SetInPool(bool inPool)
        {
            IsInPool = inPool;
        }
        #endregion

        #region Timer de Despawn
        public void StartDespawnTimer(float delay)
        {
            if (IsInPool || delay <= 0f) return;
            
            StopDespawnTimer();
            despawnTimer = StartCoroutine(DespawnTimerCoroutine(delay));
        }

        public void StopDespawnTimer()
        {
            if (despawnTimer != null)
            {
                StopCoroutine(despawnTimer);
                despawnTimer = null;
            }
        }

        IEnumerator DespawnTimerCoroutine(float delay)
        {
            yield return new WaitForSeconds(delay);
            
            if (!IsInPool && gameObject.activeInHierarchy)
            {
                Pool.Despawn(gameObject);
            }
        }
        #endregion
    }
}