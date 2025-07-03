using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace IntelliPool
{
    /// <summary>
    /// Container responsável por gerenciar um pool específico de objetos
    /// </summary>
    public sealed class PoolContainer
    {
        #region Campos Privados
        private readonly List<PoolableObject> allObjects;
        private readonly HashSet<int> availableIndices;
        private readonly Queue<int> sequentialQueue;
        private readonly PoolDatabase.PoolConfiguration config;
        private Transform parentContainer;
        private int nextSequentialIndex;
        private bool isDisposed;
        #endregion

        #region Propriedades Públicas
        public int ActiveCount => allObjects.Count - availableIndices.Count;
        public int TotalCount => allObjects.Count;
        public int AvailableCount => availableIndices.Count;
        public string PoolTag => config.poolTag;
        public bool IsDisposed => isDisposed;
        #endregion

        #region Construtor
        /// <summary>
        /// Inicializa um novo container de pool com a configuração especificada
        /// </summary>
        /// <param name="configuration">Configuração do pool</param>
        /// <param name="parent">Transform pai para organização hierárquica</param>
        public PoolContainer(PoolDatabase.PoolConfiguration configuration, Transform parent)
        {
            ValidateConfiguration(configuration);
            ValidateParent(parent);

            config = configuration;
            allObjects = new List<PoolableObject>(config.maxSize);
            availableIndices = new HashSet<int>(config.maxSize);
            sequentialQueue = new Queue<int>(config.maxSize);
            nextSequentialIndex = 0;
            isDisposed = false;

            CreateParentContainer(parent);

            if (config is { preWarm: true, initialSize: > 0 })
                PreWarmPool();
        }
        #endregion

        #region Validação
        /// <summary>
        /// Valida a configuração do pool
        /// </summary>
        void ValidateConfiguration(PoolDatabase.PoolConfiguration configuration)
        {
            if (string.IsNullOrEmpty(configuration.poolTag))
                throw new System.ArgumentException("[PoolContainer] Tag do pool não pode ser vazia");

            if (!configuration.prefab)
                throw new System.ArgumentNullException(nameof(configuration));

            if (configuration.maxSize <= 0)
                throw new System.ArgumentException("[PoolContainer] Tamanho máximo deve ser maior que zero");

            if (configuration.initialSize < 0)
                throw new System.ArgumentException("[PoolContainer] Tamanho inicial não pode ser negativo");

            if (configuration.initialSize > configuration.maxSize)
                throw new System.ArgumentException("[PoolContainer] Tamanho inicial não pode ser maior que o máximo");
        }

        /// <summary>
        /// Valida o transform pai
        /// </summary>
        void ValidateParent(Transform parent)
        {
            if (!parent)
                throw new System.ArgumentNullException(nameof(parent));
        }
        #endregion

        #region Inicialização
        /// <summary>
        /// Cria o container pai para organização hierárquica dos objetos
        /// </summary>
        void CreateParentContainer(Transform parent)
        {
            var containerGO = new GameObject($"Pool_{config.poolTag}")
            {
                hideFlags = HideFlags.DontSave
            };
            
            containerGO.transform.SetParent(parent, false);
            parentContainer = containerGO.transform;

            if (Pool.EnableDebugMode)
                Debug.Log($"[PoolContainer] Container criado para '{config.poolTag}'");
        }

        /// <summary>
        /// Executa o PreWarm do pool baseado na configuração
        /// </summary>
        void PreWarmPool()
        {
            if (ShouldUseJobsForPreWarm())
                PreWarmWithJobs();
            else
                PreWarmTraditional();
        }

        /// <summary>
        /// Determina se deve usar Jobs para o PreWarm
        /// </summary>
        bool ShouldUseJobsForPreWarm()
        {
            return config.initialSize > Pool.JobsThreshold && 
                   Pool.UseJobsForBatching && 
                   PoolJobSystem.IsInitialized;
        }

        /// <summary>
        /// PreWarm tradicional para pools pequenos
        /// </summary>
        void PreWarmTraditional()
        {
            var createdObjects = new List<PoolableObject>(config.initialSize);

            for (int i = 0; i < config.initialSize; i++)
            {
                var poolableObject = CreateNewObject();
                if (poolableObject)
                    createdObjects.Add(poolableObject);
                else
                    break;
            }

            OptimizeHierarchy();

            if (Pool.EnableDebugMode)
            {
                Debug.Log($"[PoolContainer] PreWarm tradicional concluído: " +
                         $"{createdObjects.Count}/{config.initialSize} objetos criados para '{config.poolTag}'");
            }
        }

        /// <summary>
        /// PreWarm com Jobs System para pools grandes
        /// </summary>
        void PreWarmWithJobs()
        {
            var positionsHandle = PoolJobSystem.CalculatePreWarmPositions(
                config.initialSize,
                CalculateOptimalSpacing(),
                out NativeArray<Vector3> positions,
                out NativeArray<Quaternion> rotations
            );

            var createdObjects = new List<PoolableObject>(config.initialSize);
            for (int i = 0; i < config.initialSize; i++)
            {
                var poolableObject = CreateNewObject();
                if (poolableObject)
                    createdObjects.Add(poolableObject);
                else
                    break;
            }

            positionsHandle.Complete();

            ApplyCalculatedPositions(createdObjects, positions);

            SafeDisposeNativeArray(ref positions);
            SafeDisposeNativeArray(ref rotations);

            OptimizeHierarchy();

            if (Pool.EnableDebugMode)
            {
                Debug.Log($"[PoolContainer] PreWarm com Jobs concluído: " +
                         $"{createdObjects.Count}/{config.initialSize} objetos criados para '{config.poolTag}'");
            }
        }

        /// <summary>
        /// Calcula o espaço baseado no tamanho do pool
        /// </summary>
        float CalculateOptimalSpacing()
        {
            return Mathf.Max(1f, Mathf.Sqrt(config.initialSize) * 0.5f);
        }

        /// <summary>
        /// Aplica as posições calculadas aos objetos criados
        /// </summary>
        void ApplyCalculatedPositions(List<PoolableObject> objects, NativeArray<Vector3> positions)
        {
            if (!positions.IsCreated) return;

            int count = Mathf.Min(objects.Count, positions.Length);
            for (int i = 0; i < count; i++)
            {
                if (objects[i] && objects[i].gameObject)
                {
                    objects[i].transform.localPosition = positions[i];
                }
            }
        }

        /// <summary>
        /// Otimiza a hierarquia desabilitando objetos desnecessários
        /// </summary>
        void OptimizeHierarchy()
        {
            if (parentContainer)
            {
                parentContainer.gameObject.SetActive(false);
                parentContainer.gameObject.SetActive(true);
            }
        }
        #endregion

        #region Criação de Objetos
        /// <summary>
        /// Cria um objeto para o pool com configuração completa
        /// </summary>
        PoolableObject CreateNewObject()
        {
            if (allObjects.Count >= config.maxSize)
            {
                Debug.LogWarning($"[PoolContainer] Limite máximo atingido para '{config.poolTag}': {config.maxSize}");
                return null;
            }

            try
            {
                var newObj = Object.Instantiate(config.prefab, parentContainer);
                ConfigureNewObject(newObj);

                var poolable = EnsurePoolableComponent(newObj);
                SetupPoolableObject(poolable);

                RegisterObject(poolable);

                return poolable;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[PoolContainer] Erro ao criar objeto para '{config.poolTag}': {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Configura propriedades básicas do novo objeto
        /// </summary>
        void ConfigureNewObject(GameObject newObj)
        {
            newObj.name = GenerateObjectName();
            newObj.SetActive(false);
            newObj.hideFlags = HideFlags.DontSave;
        }

        /// <summary>
        /// Gera nome único para o objeto
        /// </summary>
        string GenerateObjectName()
        {
            return $"{config.prefab.name}_{allObjects.Count + 1:D4}";
        }

        /// <summary>
        /// Garante que o objeto possui o componente PoolableObject
        /// </summary>
        PoolableObject EnsurePoolableComponent(GameObject obj)
        {
            var poolable = obj.GetComponent<PoolableObject>();
            if (!poolable)
            {
                poolable = obj.AddComponent<PoolableObject>();
                if (Pool.EnableDebugMode)
                    Debug.Log($"[PoolContainer] PoolableObject adicionado automaticamente a '{obj.name}'");
            }
            return poolable;
        }

        /// <summary>
        /// Configura o componente PoolableObject
        /// </summary>
        void SetupPoolableObject(PoolableObject poolable)
        {
            poolable.PoolTag = config.poolTag;
            poolable.SetActiveState(false);
        }

        /// <summary>
        /// Registra o objeto nas estruturas de dados do container
        /// </summary>
        void RegisterObject(PoolableObject poolable)
        {
            int index = allObjects.Count;
            allObjects.Add(poolable);
            availableIndices.Add(index);
            sequentialQueue.Enqueue(index);
        }
        #endregion

        #region Spawn e Despawn
        /// <summary>
        /// Spawna um objeto do pool na posição e rotação especificadas
        /// </summary>
        public PoolableObject Spawn(Vector3 position, Quaternion rotation, Transform parent = null)
        {
            if (isDisposed)
            {
                Debug.LogError($"[PoolContainer] Tentativa de spawn em container descartado: '{config.poolTag}'");
                return null;
            }

            var poolableObject = GetAvailableObject();
            if (!poolableObject) return null;

            ConfigureSpawnedObject(poolableObject, position, rotation, parent);
            ActivateObject(poolableObject);

            return poolableObject;
        }

        /// <summary>
        /// Configura posição, rotação e hierarquia do objeto spawnado
        /// </summary>
        void ConfigureSpawnedObject(PoolableObject poolableObject, Vector3 position, Quaternion rotation, Transform parent)
        {
            var transform = poolableObject.transform;
            transform.SetParent(parent ?? parentContainer, false);
            transform.position = position;
            transform.rotation = rotation;
        }

        /// <summary>
        /// Ativa o objeto e define o seu estado
        /// </summary>
        void ActivateObject(PoolableObject poolableObject)
        {
            poolableObject.gameObject.SetActive(true);
            poolableObject.SetActiveState(true);
        }

        /// <summary>
        /// Obtém o próximo objeto disponível seguindo ordem sequencial
        /// </summary>
        PoolableObject GetAvailableObject()
        {
            if (TryGetFromSequentialQueue(out var poolableObject))
                return poolableObject;

            if (TryGetFromLinearSearch(out poolableObject))
                return poolableObject;

            if (CanCreateNewObject())
                return CreateAndRegisterNewObject();

            return null;
        }

        /// <summary>
        /// Tenta obter objeto da fila sequencial
        /// </summary>
        bool TryGetFromSequentialQueue(out PoolableObject poolableObject)
        {
            while (sequentialQueue.Count > 0)
            {
                int index = sequentialQueue.Dequeue();
                
                if (IsValidAvailableObject(index))
                {
                    availableIndices.Remove(index);
                    poolableObject = allObjects[index];
                    return true;
                }
            }

            poolableObject = null;
            return false;
        }

        /// <summary>
        /// Tenta obter objeto através de busca linear
        /// </summary>
        bool TryGetFromLinearSearch(out PoolableObject poolableObject)
        {
            for (int i = nextSequentialIndex; i < allObjects.Count; i++)
            {
                if (IsValidAvailableObject(i))
                {
                    availableIndices.Remove(i);
                    nextSequentialIndex = (i + 1) % allObjects.Count;
                    poolableObject = allObjects[i];
                    return true;
                }
            }

            for (int i = 0; i < nextSequentialIndex; i++)
            {
                if (IsValidAvailableObject(i))
                {
                    availableIndices.Remove(i);
                    nextSequentialIndex = (i + 1) % allObjects.Count;
                    poolableObject = allObjects[i];
                    return true;
                }
            }

            poolableObject = null;
            return false;
        }

        /// <summary>
        /// Verifica se é possível criar um objeto
        /// </summary>
        bool CanCreateNewObject()
        {
            return allObjects.Count < config.maxSize;
        }

        /// <summary>
        /// Cria e registra um novo objeto imediatamente
        /// </summary>
        PoolableObject CreateAndRegisterNewObject()
        {
            var newObj = CreateNewObject();
            if (newObj)
            {
                int newIndex = allObjects.Count - 1;
                availableIndices.Remove(newIndex);
                nextSequentialIndex = (newIndex + 1) % allObjects.Count;
            }
            return newObj;
        }

        /// <summary>
        /// Verifica se o objeto no índice especificado está disponível e válido
        /// </summary>
        bool IsValidAvailableObject(int index)
        {
            return index >= 0 && 
                   index < allObjects.Count &&
                   availableIndices.Contains(index) &&
                   allObjects[index] &&
                   allObjects[index].IsInPool &&
                   allObjects[index].gameObject &&
                   !allObjects[index].gameObject.activeInHierarchy;
        }

        /// <summary>
        /// Retorna um objeto para o pool
        /// </summary>
        public bool ReturnToPool(PoolableObject poolableObject)
        {
            if (!poolableObject || poolableObject.IsInPool || isDisposed)
                return false;

            int objIndex = FindObjectIndex(poolableObject);
            if (objIndex == -1)
            {
                Debug.LogWarning($"[PoolContainer] Objeto não pertence a este pool: '{poolableObject.name}'");
                return false;
            }

            return ExecuteReturnToPool(poolableObject, objIndex);
        }

        /// <summary>
        /// Executa o retorno do objeto ao pool
        /// </summary>
        bool ExecuteReturnToPool(PoolableObject poolableObject, int objIndex)
        {
            try
            {
                poolableObject.transform.SetParent(parentContainer, false);
                poolableObject.gameObject.SetActive(false);
                poolableObject.SetActiveState(false);
                availableIndices.Add(objIndex);
                sequentialQueue.Enqueue(objIndex);

                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[PoolContainer] Erro ao retornar objeto '{poolableObject.name}' ao pool: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Encontra o índice de um objeto na lista de forma otimizada
        /// </summary>
        int FindObjectIndex(PoolableObject poolableObject)
        {
            for (int i = 0; i < allObjects.Count; i++)
            {
                if (ReferenceEquals(allObjects[i], poolableObject))
                    return i;
            }
            return -1;
        }
        #endregion

        #region Gerenciamento em Lote
        /// <summary>
        /// Despawna todos os objetos ativos do pool
        /// </summary>
        public void DespawnAll()
        {
            if (isDisposed) return;

            var activeObjectsList = CollectActiveObjects();
            
            foreach (var poolableObject in activeObjectsList)
            {
                ReturnToPool(poolableObject);
            }

            if (Pool.EnableDebugMode)
            {
                Debug.Log($"[PoolContainer] {activeObjectsList.Count} objetos despawnados de '{config.poolTag}'");
            }
        }

        /// <summary>
        /// Coleta todos os objetos ativos do pool
        /// </summary>
        List<PoolableObject> CollectActiveObjects()
        {
            var activeObjects = new List<PoolableObject>();

            foreach (var poolableObject in allObjects)
            {
                if (poolableObject && poolableObject.IsActive)
                    activeObjects.Add(poolableObject);
            }

            return activeObjects;
        }

        /// <summary>
        /// Limpa completamente o pool destruindo todos os objetos
        /// </summary>
        private void ClearPool()
        {
            if (isDisposed) return;

            if (ShouldUseJobsForClear())
                ClearPoolWithJobs();
            else
                ClearPoolTraditional();

            ResetContainerState();
        }

        /// <summary>
        /// Determina se deve usar Jobs para limpeza
        /// </summary>
        bool ShouldUseJobsForClear()
        {
            return allObjects.Count > Pool.JobsThreshold &&
                   Pool.UseJobsForBatching &&
                   PoolJobSystem.IsInitialized;
        }

        /// <summary>
        /// Limpeza tradicional do pool
        /// </summary>
        void ClearPoolTraditional()
        {
            int destroyedCount = 0;

            foreach (var poolableObject in allObjects)
            {
                if (poolableObject && poolableObject.gameObject)
                {
                    Object.Destroy(poolableObject.gameObject);
                    destroyedCount++;
                }
            }

            if (Pool.EnableDebugMode)
            {
                Debug.Log($"[PoolContainer] Pool '{config.poolTag}' limpo tradicionalmente: " +
                         $"{destroyedCount} objetos destruídos");
            }
        }

        /// <summary>
        /// Limpeza otimizada com Jobs System
        /// </summary>
        void ClearPoolWithJobs()
        {
            var instanceIds = CreateInstanceIdsArray();
            var validationResults = new NativeArray<bool>(allObjects.Count, Allocator.TempJob);

            // Executa validação em paralelo
            var jobHandle = PoolJobSystem.ValidateInstanceIds(instanceIds, validationResults);
            jobHandle.Complete();

            // Processa resultados e destrói objetos
            int destroyedCount = ProcessValidationAndDestroy(validationResults);

            // Cleanup de recursos
            SafeDisposeNativeArray(ref instanceIds);
            SafeDisposeNativeArray(ref validationResults);

            if (Pool.EnableDebugMode)
            {
                Debug.Log($"[PoolContainer] Pool '{config.poolTag}' limpo com Jobs: " +
                         $"{destroyedCount} objetos destruídos");
            }
        }

        /// <summary>
        /// Cria array de ‘IDs’ de instância para processamento em Jobs
        /// </summary>
        NativeArray<int> CreateInstanceIdsArray()
        {
            var instanceIds = new NativeArray<int>(allObjects.Count, Allocator.TempJob);

            for (int i = 0; i < allObjects.Count; i++)
            {
                instanceIds[i] = allObjects[i]?.InstanceID ?? 0;
            }

            return instanceIds;
        }

        /// <summary>
        /// Processa resultados da validação e destrói objetos
        /// </summary>
        int ProcessValidationAndDestroy(NativeArray<bool> validationResults)
        {
            int destroyedCount = 0;

            for (int i = 0; i < allObjects.Count; i++)
            {
                var poolableObject = allObjects[i];
                if (poolableObject && 
                    poolableObject.gameObject && 
                    validationResults[i])
                {
                    Object.Destroy(poolableObject.gameObject);
                    destroyedCount++;
                }
            }

            return destroyedCount;
        }

        /// <summary>
        /// Reseta o estado do container após limpeza
        /// </summary>
        void ResetContainerState()
        {
            allObjects.Clear();
            availableIndices.Clear();
            sequentialQueue.Clear();
            nextSequentialIndex = 0;
        }
        #endregion

        #region Estatísticas e Debug
        /// <summary>
        /// Obtém estatísticas detalhadas do pool para debug e monitoramento
        /// </summary>
        public void GetDetailedStats(out int active, out int available, out int total, out string nextObject)
        {
            active = ActiveCount;
            available = AvailableCount;
            total = TotalCount;

            nextObject = GetNextObjectName();
        }

        /// <summary>
        /// Obtém o nome do próximo objeto na sequência
        /// </summary>
        string GetNextObjectName()
        {
            if (sequentialQueue.Count > 0)
            {
                var nextIndex = sequentialQueue.Peek();
                if (nextIndex < allObjects.Count && allObjects[nextIndex])
                    return allObjects[nextIndex].name;
            }

            if (nextSequentialIndex < allObjects.Count && allObjects[nextSequentialIndex])
                return allObjects[nextSequentialIndex].name;

            return "None";
        }

        /// <summary>
        /// Gera relatório completo do estado do pool
        /// </summary>
        public string GenerateStatusReport()
        {
            var report = new System.Text.StringBuilder();
            
            report.AppendLine($"=== Pool Status Report: {config.poolTag} ===");
            report.AppendLine($"Total Objects: {TotalCount}/{config.maxSize}");
            report.AppendLine($"Active Objects: {ActiveCount}");
            report.AppendLine($"Available Objects: {AvailableCount}");
            report.AppendLine($"Sequential Queue Count: {sequentialQueue.Count}");
            report.AppendLine($"Next Sequential Index: {nextSequentialIndex}");
            report.AppendLine($"Is Disposed: {isDisposed}");

            return report.ToString();
        }
        #endregion

        #region Utilities
        /// <summary>
        /// Descarta um NativeArray de forma segura
        /// </summary>
        void SafeDisposeNativeArray<T>(ref NativeArray<T> array) where T : struct
        {
            if (array.IsCreated)
            {
                array.Dispose();
                array = default;
            }
        }
        #endregion

        #region Disposable Pattern
        /// <summary>
        /// Libera todos os recursos do container
        /// </summary>
        public void Dispose()
        {
            if (isDisposed) return;

            ClearPool();

            if (parentContainer)
            {
                Object.Destroy(parentContainer.gameObject);
                parentContainer = null;
            }

            isDisposed = true;

            if (Pool.EnableDebugMode)
                Debug.Log($"[PoolContainer] Container '{config.poolTag}' descartado");
        }

        /// <summary>
        /// Destrutor para garantir limpeza de recursos
        /// </summary>
        ~PoolContainer()
        {
            if (!isDisposed)
            {
                Debug.LogWarning($"[PoolContainer] Container '{config.poolTag}' não foi descartado adequadamente");
            }
        }
        #endregion
    }
}