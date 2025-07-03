using System;
using System.Collections;
using UnityEngine;

namespace IntelliPool
{
    public class PoolableObject : MonoBehaviour
    {
        public int InstanceID { get; private set; }
        public bool IsActive { get; private set; }
        public bool IsInPool { get; private set; }
        public string PoolTag { get; set; }
    
        private Vector3 originalPosition;
        private Quaternion originalRotation;
        private Vector3 originalScale;
        private Coroutine timerCoroutine;
        private bool hasActiveTimer;
        private float timerStartTime;
        private float timerDuration;

        public Action OnSpawnFromPool;
        public Action OnReturnToPool;

        public bool HasActiveTimer => hasActiveTimer;
        private float TimeLeft { get; set; }
    
        void Awake()
        {
            InstanceID = GetInstanceID();
            StoreOriginalTransform();
        }
    
        /// <summary>
        /// Armazena a transformação original do objeto
        /// </summary>
        void StoreOriginalTransform()
        {
            originalPosition = transform.localPosition;
            originalRotation = transform.localRotation;
            originalScale = transform.localScale;
        }
    
        /// <summary>
        /// Define o estado ativo do objeto no pool
        /// </summary>
        public void SetActiveState(bool active)
        {
            IsActive = active;
            IsInPool = !active;
        
            if (active)
            {
                OnSpawnFromPool?.Invoke();
            }
            else
            {
                ForceStopTimer();
                OnReturnToPool?.Invoke();
                ResetTransform();
            }
        }
    
        /// <summary>
        /// Reseta a transformação para o estado original
        /// </summary>
        void ResetTransform()
        {
            transform.localPosition = originalPosition;
            transform.localRotation = originalRotation;
            transform.localScale = originalScale;
        }
    
        /// <summary>
        /// Inicia um temporizador para auto-despawn
        /// </summary>
        public void StartTimer(float duration)
        {
            if (!IsActive || IsInPool || duration <= 0f)
                return;
            
            ForceStopTimer();
            
            timerDuration = duration;
            timerStartTime = Time.time;
            TimeLeft = duration;
            hasActiveTimer = true;
            timerCoroutine = StartCoroutine(TimerCoroutine());
        }
    
        /// <summary>
        /// Para o timer atual
        /// </summary>
        public void StopTimer()
        {
            if (hasActiveTimer)
                ForceStopTimer();
        }
        
        /// <summary>
        /// Para o temporizador de forma forçada
        /// </summary>
        void ForceStopTimer()
        {
            if (timerCoroutine != null)
            {
                StopCoroutine(timerCoroutine);
                timerCoroutine = null;
            }
            hasActiveTimer = false;
            TimeLeft = 0f;
        }
    
        /// <summary>
        /// Adiciona tempo extra ao timer atual
        /// </summary>
        public void AddTime(float extraTime)
        {
            if (hasActiveTimer)
            {
                timerDuration += extraTime;
                TimeLeft += extraTime;
            }
        }
    
        /// <summary>
        /// Coroutine que gerencia o countdown do timer
        /// </summary>
        IEnumerator TimerCoroutine()
        {
            float elapsed = 0f;
            
            while (elapsed < timerDuration)
            {
                if (!hasActiveTimer || IsInPool || !IsActive || !gameObject || !gameObject.activeInHierarchy)
                    yield break;
                
                elapsed = Time.time - timerStartTime;
                TimeLeft = Mathf.Max(0f, timerDuration - elapsed);
                
                yield return null;
            }
            
            if (hasActiveTimer && IsActive && !IsInPool && gameObject && gameObject.activeInHierarchy)
            {
                hasActiveTimer = false;
                Pool.Despawn(gameObject);
            }
        }
        
        /// <summary>
        /// Detecta objetos órfãos sem temporizador
        /// </summary>
        void Update()
        {
            if (IsActive && !IsInPool && !hasActiveTimer && gameObject.activeInHierarchy)
            {
                if (Pool.EnableDebugMode)
                    Debug.LogWarning($"Objeto órfão detectado: {name} - Forçando despawn");
                Pool.Despawn(gameObject);
            }
        }
    
        void OnDisable() => ForceStopTimer();
        void OnDestroy() => ForceStopTimer();
    }
}