using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace IntelliPool
{
    public sealed class PoolContainer
    {
        #region Campos Privados
        private readonly List<PoolableObject> allObjects;
        private readonly Queue<PoolableObject> availableObjects;
        private readonly PoolDatabase.PoolConfiguration config;
        private Transform containerTransform; // Removido readonly
        
        private bool isDisposed;
        #endregion

        #region Propriedades
        public int ActiveCount => allObjects.Count - availableObjects.Count;
        public int TotalCount => allObjects.Count;
        public int AvailableCount => availableObjects.Count;
        public int MaxSize => config.maxSize;
        public string PoolTag => config.poolTag;
        #endregion

        #region Construtor
        public PoolContainer(PoolDatabase.PoolConfiguration configuration, Transform parent)
        {
            config = configuration;
            allObjects = new List<PoolableObject>();
            availableObjects = new Queue<PoolableObject>();
            
            CreateContainer(parent);
            
            if (config.preWarm && config.initialSize > 0)
                PreWarmPool();
        }

        void CreateContainer(Transform parent)
        {
            var containerGO = new GameObject($"Pool_{config.poolTag}")
            {
                hideFlags = HideFlags.DontSave
            };
            
            containerGO.transform.SetParent(parent, false);
            containerTransform = containerGO.transform;
        }

        void PreWarmPool()
        {
            for (int i = 0; i < config.initialSize; i++)
            {
                var poolableObject = CreateNewObject();
                if (poolableObject != null)
                {
                    poolableObject.gameObject.SetActive(false);
                    poolableObject.SetInPool(true);
                    availableObjects.Enqueue(poolableObject);
                }
            }
        }
        #endregion

        #region Spawn e Despawn
        public PoolableObject Spawn(Vector3 position, Quaternion rotation, Transform parent)
        {
            var poolableObject = GetAvailableObject();
            if (poolableObject == null) return null;

            // Configura posição e hierarquia
            var transform = poolableObject.transform;
            transform.SetParent(parent ?? containerTransform, false);
            transform.position = position;
            transform.rotation = rotation;

            // Ativa objeto
            poolableObject.gameObject.SetActive(true);
            poolableObject.SetInPool(false);

            return poolableObject;
        }

        PoolableObject GetAvailableObject()
        {
            // Tenta reutilizar objeto existente
            while (availableObjects.Count > 0)
            {
                var poolableObject = availableObjects.Dequeue();
                if (poolableObject && poolableObject.gameObject)
                    return poolableObject;
            }

            // Cria novo se permitido (sempre pode expandir)
            return CreateNewObject();
        }

        PoolableObject CreateNewObject()
        {
            // Verifica limite máximo apenas se configurado (> 0)
            if (config.maxSize > 0 && allObjects.Count >= config.maxSize)
            {
                Debug.LogWarning($"Pool '{config.poolTag}' atingiu limite máximo: {config.maxSize}");
                return null;
            }

            try
            {
                var newObj = Object.Instantiate(config.prefab, containerTransform);
                newObj.name = $"{config.prefab.name}_{allObjects.Count + 1:D3}";
                newObj.hideFlags = HideFlags.DontSave;

                var poolableObject = EnsurePoolableComponent(newObj);
                poolableObject.PoolTag = config.poolTag;
                
                allObjects.Add(poolableObject);
                
                return poolableObject;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Erro ao criar objeto para pool '{config.poolTag}': {ex.Message}");
                return null;
            }
        }

        PoolableObject EnsurePoolableComponent(GameObject obj)
        {
            var poolableObject = obj.GetComponent<PoolableObject>();
            if (!poolableObject)
                poolableObject = obj.AddComponent<PoolableObject>();
            
            return poolableObject;
        }

        public bool ReturnToPool(PoolableObject poolableObject)
        {
            if (!poolableObject || poolableObject.IsInPool || isDisposed)
                return false;

            try
            {
                poolableObject.transform.SetParent(containerTransform, false);
                poolableObject.gameObject.SetActive(false);
                poolableObject.SetInPool(true);
                availableObjects.Enqueue(poolableObject);
                
                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Erro ao retornar objeto ao pool: {ex.Message}");
                return false;
            }
        }
        #endregion

        #region Dispose
        public void Dispose()
        {
            if (isDisposed) return;

            foreach (var obj in allObjects.Where(obj => obj && obj.gameObject))
            {
                Object.Destroy(obj.gameObject);
            }

            allObjects.Clear();
            availableObjects.Clear();

            if (containerTransform)
                Object.Destroy(containerTransform.gameObject);

            isDisposed = true;
        }
        #endregion
    }
}