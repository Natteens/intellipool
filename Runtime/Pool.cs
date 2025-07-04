using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IntelliPool
{
    /// <summary>
    /// Sistema centralizado de gerenciamento de pools de objetos
    /// Implementa padrão Singleton estático
    /// </summary>
    public static class Pool
    {
        #region Campos Privados e Estado
        private static readonly Dictionary<string, PoolContainer> pools = new Dictionary<string, PoolContainer>();
        private static readonly Dictionary<int, PoolableObject> activeObjects = new Dictionary<int, PoolableObject>();
        private static readonly Dictionary<string, PoolPerformanceMetrics> performanceMetrics = new Dictionary<string, PoolPerformanceMetrics>();
        private static readonly object poolLock = new object();
        
        private static Transform poolManagerTransform;
        private static PoolDatabase database;
        private static PoolSystemState systemState = PoolSystemState.Uninitialized;
        private static float lastMetricsUpdate;
        private static int totalSpawnOperations;
        private static int totalDespawnOperations;
        
        private static PoolSystemConfiguration systemConfig;
        #endregion

        #region Estados e Enums
        /// <summary>
        /// Estados possíveis do sistema de pool
        /// </summary>
        public enum PoolSystemState
        {
            Uninitialized,
            Initializing,
            Ready,
            ShuttingDown,
            Error
        }

        /// <summary>
        /// Configuração do sistema de pool
        /// </summary>
        private struct PoolSystemConfiguration
        {
            public bool enableDebugMode;
            public bool useJobsForBatching;
            public int jobsThreshold;
            public bool enablePerformanceMetrics;
            public float metricsUpdateInterval;
            public int maxConcurrentPools;
        }

        /// <summary>
        /// Métricas de performance por pool
        /// </summary>
        public class PoolPerformanceMetrics
        {
            public int totalSpawns;
            public int totalDespawns;
            public float averageSpawnTime;
            public float averageDespawnTime;
            public int peakActiveObjects;
            public DateTime lastResetTime;
            
            public void RecordSpawn(float spawnTime)
            {
                totalSpawns++;
                averageSpawnTime = (averageSpawnTime * (totalSpawns - 1) + spawnTime) / totalSpawns;
            }
            
            public void RecordDespawn(float despawnTime)
            {
                totalDespawns++;
                averageDespawnTime = (averageDespawnTime * (totalDespawns - 1) + despawnTime) / totalDespawns;
            }
            
            public void UpdatePeakActiveObjects(int currentActive)
            {
                if (currentActive > peakActiveObjects)
                    peakActiveObjects = currentActive;
            }
        }
        #endregion

        #region Propriedades Públicas
        /// <summary>
        /// Estado atual do sistema de pool
        /// </summary>
        public static PoolSystemState SystemState => systemState;

        /// <summary>
        /// Indica se o modo debug está habilitado
        /// </summary>
        public static bool EnableDebugMode => systemConfig.enableDebugMode;

        /// <summary>
        /// Indica se o sistema usa Jobs para operações em lote
        /// </summary>
        public static bool UseJobsForBatching => systemConfig.useJobsForBatching;

        /// <summary>
        /// Limite de objetos para ativar Jobs System
        /// </summary>
        public static int JobsThreshold => systemConfig.jobsThreshold;

        /// <summary>
        /// Quantidade total de pools ativos
        /// </summary>
        public static int TotalActivePools => pools.Count;

        /// <summary>
        /// Quantidade total de objetos ativos em todos os pools
        /// </summary>
        public static int TotalActiveObjects => activeObjects.Count;

        /// <summary>
        /// Indica se o sistema está pronto para uso
        /// </summary>
        public static bool IsReady => systemState == PoolSystemState.Ready;
        #endregion

        #region Eventos Públicos
        /// <summary>
        /// Disparado quando um objeto é spawnado
        /// </summary>
        public static event Action<string, GameObject> OnObjectSpawned;

        /// <summary>
        /// Disparado quando um objeto é despawnado
        /// </summary>
        public static event Action<string, GameObject> OnObjectDespawned;

        /// <summary>
        /// Disparado quando um pool é criado
        /// </summary>
        public static event Action<string> OnPoolCreated;

        /// <summary>
        /// Disparado quando um pool é destruído
        /// </summary>
        public static event Action<string> OnPoolDestroyed;

        /// <summary>
        /// Disparado quando o sistema é inicializado
        /// </summary>
        public static event Action OnSystemInitialized;

        /// <summary>
        /// Disparado quando o sistema é finalizado
        /// </summary>
        public static event Action OnSystemShutdown;
        #endregion

        #region Inicialização do Editor
#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        static void InitializeOnLoad()
        {
            UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        static void OnPlayModeStateChanged(UnityEditor.PlayModeStateChange state)
        {
            switch (state)
            {
                case UnityEditor.PlayModeStateChange.ExitingEditMode:
                case UnityEditor.PlayModeStateChange.ExitingPlayMode:
                    ForceShutdown();
                    break;
                
                case UnityEditor.PlayModeStateChange.EnteredPlayMode:
                    if (systemState == PoolSystemState.Uninitialized)
                        Initialize();
                    break;
            }
        }
#endif
        #endregion

        #region Ciclo de Vida do Sistema
        
        /// <summary>
        /// Verifica se o sistema está habilitado na database
        /// </summary>
        static bool IsSystemEnabledInDatabase()
        {
            return database?.enablePoolSystem ?? false;
        }
        
        /// <summary>
        /// Inicialização primária do sistema
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Initialize()
        {
            if (systemState != PoolSystemState.Uninitialized) return;

            try
            {
                systemState = PoolSystemState.Initializing;
                LogDebug("[Pool] Verificando se sistema está habilitado...");

                LoadSystemConfiguration();
        
                if (!IsSystemEnabledInDatabase())
                {
                    systemState = PoolSystemState.Uninitialized;
                    LogDebug("[Pool] Sistema desabilitado - não inicializando");
                    return;
                }

                LogDebug("[Pool] Sistema habilitado - prosseguindo com inicialização...");

                CreatePoolManagerHierarchy();
                SetupConfiguredPools();
                RegisterSystemEvents();
                InitializeJobSystemIfRequired();
                ResetPerformanceMetrics();

                systemState = PoolSystemState.Ready;
                OnSystemInitialized?.Invoke();

                LogDebug("[Pool] Sistema de pool inicializado com sucesso");
            }
            catch (Exception ex)
            {
                systemState = PoolSystemState.Error;
                Debug.LogError($"[Pool] Erro crítico na inicialização: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// Inicialização pós-carregamento de cena
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void PostInitialize()
        {
            if (systemState == PoolSystemState.Uninitialized)
            {
                LogDebug("[Pool] Reinicializando sistema no PostInitialize");
                Initialize();
            }
            else if (systemState == PoolSystemState.Ready)
            {
                ValidateSystemIntegrity();
            }
        }

        /// <summary>
        /// Carrega configurações do sistema a partir da database
        /// </summary>
        static void LoadSystemConfiguration()
        {
            var databases = Resources.LoadAll<PoolDatabase>("");

            if (databases.Length == 0)
            {
                LogError("Nenhuma PoolDatabase encontrada! Criando configuração padrão.");
                CreateDefaultConfiguration();
                return;
            }

            if (databases.Length > 1)
            {
                LogWarning($"Múltiplas PoolDatabases encontradas! Usando: {databases[0].name}");
            }

            database = databases[0];
    
            if (!database.enablePoolSystem)
            {
                LogDebug("Pool System desabilitado na database - cancelando inicialização");
                systemState = PoolSystemState.Uninitialized;
                return;
            }

            systemConfig = new PoolSystemConfiguration
            {
                enableDebugMode = database.enableDebugMode,
                useJobsForBatching = database.useJobsForBatching,
                jobsThreshold = database.jobsThreshold,
                enablePerformanceMetrics = true,
                metricsUpdateInterval = 1.0f,
                maxConcurrentPools = 100
            };

            LogDebug($"Configuração carregada de: {database.name}");
        }

        /// <summary>
        /// Cria configuração padrão quando nenhuma database é encontrada
        /// </summary>
        static void CreateDefaultConfiguration()
        {
            systemConfig = new PoolSystemConfiguration
            {
                enableDebugMode = false,
                useJobsForBatching = false,
                jobsThreshold = 100,
                enablePerformanceMetrics = false,
                metricsUpdateInterval = 5.0f,
                maxConcurrentPools = 50
            };
        }

        /// <summary>
        /// Cria a hierarquia do gerenciador de pools
        /// </summary>
        static void CreatePoolManagerHierarchy()
        {
            if (poolManagerTransform)
            {
                SafeDestroyGameObject(poolManagerTransform.gameObject);
            }

            var poolManagerGO = new GameObject("PoolSystem_Manager")
            {
                hideFlags = HideFlags.DontSave
            };

            UnityEngine.Object.DontDestroyOnLoad(poolManagerGO);
            poolManagerTransform = poolManagerGO.transform;

            LogDebug("Hierarquia do PoolManager criada");
        }

        /// <summary>
        /// Configura todos os pools definidos na database
        /// </summary>
        static void SetupConfiguredPools()
        {
            if (database?.pools == null) return;

            int poolsCreated = 0;
            var errors = new List<string>();

            foreach (var poolConfig in database.pools)
            {
                try
                {
                    if (ValidatePoolConfiguration(poolConfig))
                    {
                        CreatePoolContainer(poolConfig);
                        poolsCreated++;
                    }
                }
                catch (Exception ex)
                {
                    errors.Add($"Pool '{poolConfig.poolTag}': {ex.Message}");
                }
            }

            LogDebug($"Setup concluído: {poolsCreated} pools criados");

            if (errors.Count > 0)
            {
                LogError($"Erros durante setup:\n{string.Join("\n", errors)}");
            }
        }

        /// <summary>
        /// Registra todos os eventos do sistema
        /// </summary>
        static void RegisterSystemEvents()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;

            Application.quitting -= OnApplicationQuitting;
            Application.quitting += OnApplicationQuitting;

            LogDebug("Eventos do sistema registrados");
        }

        /// <summary>
        /// Finalização forçada do sistema
        /// </summary>
        static void ForceShutdown()
        {
            if (systemState == PoolSystemState.ShuttingDown) return;

            var previousState = systemState;
            systemState = PoolSystemState.ShuttingDown;

            try
            {
                CleanupAllPools();

                CleanupActiveObjects();

                CleanupHierarchy();

                ShutdownJobSystem();

                ResetSystemState();

                systemState = PoolSystemState.Uninitialized;
                OnSystemShutdown?.Invoke();

                if (previousState != PoolSystemState.Uninitialized)
                {
                    LogDebug("Sistema finalizado completamente");
                }
            }
            catch (Exception ex)
            {
                systemState = PoolSystemState.Error;
                LogError($"Erro durante shutdown: {ex.Message}");
            }
        }
        #endregion

        #region Gerenciamento de Pools
        /// <summary>
        /// Valida configuração de pool
        /// </summary>
        static bool ValidatePoolConfiguration(PoolDatabase.PoolConfiguration config)
        {
            if (string.IsNullOrEmpty(config.poolTag))
            {
                LogWarning("Pool com tag vazia ignorado");
                return false;
            }

            if (!config.prefab)
            {
                LogWarning($"Pool '{config.poolTag}' com prefab nulo ignorado");
                return false;
            }

            if (pools.ContainsKey(config.poolTag))
            {
                LogWarning($"Pool '{config.poolTag}' já existe - ignorando duplicata");
                return false;
            }

            if (pools.Count >= systemConfig.maxConcurrentPools)
            {
                LogWarning($"Limite máximo de pools atingido: {systemConfig.maxConcurrentPools}");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Cria um container de pool com tratamento de erros
        /// </summary>
        static void CreatePoolContainer(PoolDatabase.PoolConfiguration config)
        {
            lock (poolLock)
            {
                try
                {
                    var container = new PoolContainer(config, poolManagerTransform);
                    pools.Add(config.poolTag, container);

                    performanceMetrics[config.poolTag] = new PoolPerformanceMetrics
                    {
                        lastResetTime = DateTime.Now
                    };

                    OnPoolCreated?.Invoke(config.poolTag);
                    LogDebug($"Pool criado: '{config.poolTag}'");
                }
                catch (Exception ex)
                {
                    LogError($"Falha ao criar pool '{config.poolTag}': {ex.Message}");
                    throw;
                }
            }
        }

        /// <summary>
        /// Remove um pool do sistema
        /// </summary>
        static bool RemovePool(string poolTag)
        {
            lock (poolLock)
            {
                if (!pools.TryGetValue(poolTag, out var container))
                    return false;

                try
                {
                    container.Dispose();
                    pools.Remove(poolTag);
                    performanceMetrics.Remove(poolTag);

                    OnPoolDestroyed?.Invoke(poolTag);
                    LogDebug($"Pool removido: '{poolTag}'");
                    return true;
                }
                catch (Exception ex)
                {
                    LogError($"Erro ao remover pool '{poolTag}': {ex.Message}");
                    return false;
                }
            }
        }
        #endregion

        #region API Pública - Spawn Operations
        /// <summary>
        /// Spawna objeto por tipo genérico
        /// </summary>
        public static T Spawn<T>() where T : Component
        {
            return Spawn<T>(Vector3.zero, Quaternion.identity, null);
        }

        /// <summary>
        /// Spawna objeto por tipo genérico com posição
        /// </summary>
        public static T Spawn<T>(Vector3 position) where T : Component
        {
            return Spawn<T>(position, Quaternion.identity, null);
        }

        /// <summary>
        /// Spawna objeto por tipo genérico com posição e rotação
        /// </summary>
        public static T Spawn<T>(Vector3 position, Quaternion rotation) where T : Component
        {
            return Spawn<T>(position, rotation, null);
        }

        /// <summary>
        /// Spawna objeto por tipo genérico com parâmetros completos
        /// </summary>
        public static T Spawn<T>(Vector3 position, Quaternion rotation, Transform parent) where T : Component
        {
            var poolTag = typeof(T).Name;
            var gameObject = SpawnByTag(poolTag, position, rotation, parent);
            return gameObject?.GetComponent<T>();
        }

        /// <summary>
        /// Spawna objeto por tag - sobrecargas
        /// </summary>
        public static GameObject SpawnByTag(string poolTag)
        {
            return SpawnByTag(poolTag, Vector3.zero, Quaternion.identity, null);
        }

        public static GameObject SpawnByTag(string poolTag, Vector3 position)
        {
            return SpawnByTag(poolTag, position, Quaternion.identity, null);
        }

        public static GameObject SpawnByTag(string poolTag, Vector3 position, Quaternion rotation)
        {
            return SpawnByTag(poolTag, position, rotation, null);
        }

        /// <summary>
        /// Spawna objeto por tag - implementação principal
        /// </summary>
        public static GameObject SpawnByTag(string poolTag, Vector3 position, Quaternion rotation, Transform parent)
        {
            if (!EnsureSystemReady()) return null;
            if (!ValidateSpawnParameters(poolTag)) return null;

            var startTime = systemConfig.enablePerformanceMetrics ? Time.realtimeSinceStartup : 0f;

            try
            {
                lock (poolLock)
                {
                    if (!pools.TryGetValue(poolTag, out var container))
                    {
                        LogError($"Pool '{poolTag}' não encontrado! Pools disponíveis: {string.Join(", ", pools.Keys)}");
                        return null;
                    }

                    var poolableObject = container.Spawn(position, rotation, parent);
                    if (!poolableObject)
                    {
                        LogWarning($"Falha ao spawnar de '{poolTag}' - Pool pode estar no limite");
                        return null;
                    }

                    activeObjects[poolableObject.InstanceID] = poolableObject;
                    totalSpawnOperations++;

                    if (systemConfig.enablePerformanceMetrics)
                    {
                        var spawnTime = Time.realtimeSinceStartup - startTime;
                        UpdateSpawnMetrics(poolTag, spawnTime);
                    }

                    OnObjectSpawned?.Invoke(poolTag, poolableObject.gameObject);

                    return poolableObject.gameObject;
                }
            }
            catch (Exception ex)
            {
                LogError($"Erro durante spawn de '{poolTag}': {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Spawna objeto com callback
        /// </summary>
        public static T Spawn<T>(Vector3 position, Action<T> callback) where T : Component
        {
            var obj = Spawn<T>(position);
            if (obj)
                callback?.Invoke(obj);
            return obj;
        }
        #endregion

        #region API Pública - Despawn Operations
        /// <summary>
        /// Retorna objeto para o pool
        /// </summary>
        public static bool Despawn(GameObject gameObject)
        {
            if (!ValidateDespawnParameters(gameObject)) return false;

            var poolableObject = gameObject.GetComponent<PoolableObject>();
            if (!poolableObject)
            {
                LogWarning($"Tentativa de despawn de objeto sem PoolableObject: {gameObject.name}");
                return false;
            }

            return DespawnByID(poolableObject.InstanceID);
        }

        /// <summary>
        /// Retorna objeto para o pool por ID
        /// </summary>
        public static bool DespawnByID(int instanceID)
        {
            if (!EnsureSystemReady()) return false;

            var startTime = systemConfig.enablePerformanceMetrics ? Time.realtimeSinceStartup : 0f;

            try
            {
                lock (poolLock)
                {
                    if (!activeObjects.TryGetValue(instanceID, out var poolableObject))
                        return false;

                    var poolTag = poolableObject.PoolTag;

                    if (pools.TryGetValue(poolTag, out var container))
                    {
                        var success = container.ReturnToPool(poolableObject);
                        if (success)
                        {
                            activeObjects.Remove(instanceID);
                            totalDespawnOperations++;

                            if (systemConfig.enablePerformanceMetrics)
                            {
                                var despawnTime = Time.realtimeSinceStartup - startTime;
                                UpdateDespawnMetrics(poolTag, despawnTime);
                            }

                            OnObjectDespawned?.Invoke(poolTag, poolableObject.gameObject);
                        }

                        return success;
                    }

                    return false;
                }
            }
            catch (Exception ex)
            {
                LogError($"Erro durante despawn ID {instanceID}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Adiciona timer de despawn automático
        /// </summary>
        public static void DespawnDelayed(GameObject gameObject, float delay)
        {
            if (!ValidateDespawnDelayedParameters(gameObject, delay)) return;

            var poolableObject = gameObject.GetComponent<PoolableObject>();
            if (!poolableObject)
                poolableObject = gameObject.AddComponent<PoolableObject>();

            if (!poolableObject.IsActive || poolableObject.IsInPool || !gameObject.activeInHierarchy)
                return;

            poolableObject.StartTimer(delay);
            LogDebug($"Timer iniciado: {gameObject.name} em {delay}s");
        }

        /// <summary>
        /// Despawna todos os objetos de um pool específico
        /// </summary>
        public static void DespawnAllByTag(string poolTag)
        {
            if (!EnsureSystemReady()) return;

            lock (poolLock)
            {
                if (pools.TryGetValue(poolTag, out var container))
                {
                    container.DespawnAll();
                }
            }
        }

        /// <summary>
        /// Despawna objetos dentro de um raio
        /// </summary>
        public static void DespawnInRadius<T>(Vector3 center, float radius) where T : Component
        {
            var poolTag = typeof(T).Name;
            DespawnInRadiusByTag(poolTag, center, radius);
        }

        /// <summary>
        /// Despawna objetos dentro de um raio por tag
        /// </summary>
        public static void DespawnInRadiusByTag(string poolTag, Vector3 center, float radius)
        {
            if (!EnsureSystemReady()) return;

            var objectsToRemove = new List<int>();

            lock (poolLock)
            {
                foreach (var kvp in activeObjects)
                {
                    if (kvp.Value.PoolTag == poolTag)
                    {
                        var distance = Vector3.Distance(kvp.Value.transform.position, center);
                        if (distance <= radius)
                            objectsToRemove.Add(kvp.Key);
                    }
                }
            }

            foreach (var instanceID in objectsToRemove)
                DespawnByID(instanceID);

            if (EnableDebugMode && objectsToRemove.Count > 0)
                LogDebug($"{objectsToRemove.Count} objetos despawnados em raio de {radius}m");
        }
        #endregion

        #region API Pública - Consultas e Informações
        /// <summary>
        /// Obtém quantidade de objetos ativos por tipo
        /// </summary>
        public static int GetActiveCount<T>() where T : Component
        {
            var poolTag = typeof(T).Name;
            return GetActiveCount(poolTag);
        }

        /// <summary>
        /// Obtém quantidade de objetos ativos por tag
        /// </summary>
        public static int GetActiveCount(string poolTag)
        {
            if (!pools.TryGetValue(poolTag, out var container))
                return 0;

            return container.ActiveCount;
        }

        /// <summary>
        /// Verifica se objeto está no pool
        /// </summary>
        public static bool IsInPool(GameObject gameObject)
        {
            if (!gameObject) return false;

            var poolableObject = gameObject.GetComponent<PoolableObject>();
            return poolableObject?.IsInPool ?? false;
        }

        /// <summary>
        /// Obtém informações detalhadas de um pool
        /// </summary>
        public static bool GetPoolInfo(string poolTag, out int active, out int available, out int total)
        {
            active = available = total = 0;

            if (!pools.TryGetValue(poolTag, out var container))
                return false;

            active = container.ActiveCount;
            available = container.AvailableCount;
            total = container.TotalCount;

            return true;
        }

        /// <summary>
        /// Lista todas as tags de pools disponíveis
        /// </summary>
        public static string[] GetAvailablePoolTags()
        {
            lock (poolLock)
            {
                return pools.Keys.ToArray();
            }
        }

        /// <summary>
        /// Obtém métricas de performance de um pool
        /// </summary>
        public static bool GetPoolMetrics(string poolTag, out PoolPerformanceMetrics metrics)
        {
            return performanceMetrics.TryGetValue(poolTag, out metrics);
        }
        #endregion

        #region API Pública - Gerenciamento de Pools
        /// <summary>
        /// PreWarm pool por tipo
        /// </summary>
        public static void PreWarmPool<T>(int count) where T : Component
        {
            var poolTag = typeof(T).Name;
            PreWarmPool(poolTag, count);
        }

        /// <summary>
        /// PreWarm pool por tag
        /// </summary>
        public static void PreWarmPool(string poolTag, int count)
        {
            if (!EnsureSystemReady() || count <= 0) return;

            lock (poolLock)
            {
                if (!pools.ContainsKey(poolTag))
                {
                    LogWarning($"Pool '{poolTag}' não encontrado para pré-aquecimento");
                    return;
                }

                var tempObjects = new List<GameObject>(count);

                for (int i = 0; i < count; i++)
                {
                    var gameObject = SpawnByTag(poolTag);
                    if (gameObject)
                        tempObjects.Add(gameObject);
                    else
                        break;
                }

                foreach (var gameObject in tempObjects)
                    Despawn(gameObject);

                LogDebug($"Pool '{poolTag}' pré-aquecido com {tempObjects.Count} objetos");
            }
        }

        /// <summary>
        /// Limpa pool por tipo
        /// </summary>
        public static void ClearPool<T>() where T : Component
        {
            var poolTag = typeof(T).Name;
            ClearPool(poolTag);
        }

        /// <summary>
        /// Limpa pool por tag
        /// </summary>
        public static void ClearPool(string poolTag)
        {
            if (!EnsureSystemReady()) return;

            if (RemovePool(poolTag))
            {
                LogDebug($"Pool '{poolTag}' removido");
            }
        }

        /// <summary>
        /// Limpa todos os pools
        /// </summary>
        public static void ClearAllPools()
        {
            if (!EnsureSystemReady()) return;

            var poolTags = GetAvailablePoolTags();
            foreach (var poolTag in poolTags)
            {
                RemovePool(poolTag);
            }

            LogDebug("Todos os pools foram limpos");
        }
        #endregion

        #region Eventos do Sistema
        /// <summary>
        /// Manipula carregamento de cena
        /// </summary>
        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (database?.pools == null || systemState != PoolSystemState.Ready)
                return;

            int poolsCleared = 0;

            foreach (var poolConfig in database.pools)
            {
                if (poolConfig.clearOnSceneLoad && pools.TryGetValue(poolConfig.poolTag, out var pool))
                {
                    pool.DespawnAll();
                    poolsCleared++;
                }
            }

            if (EnableDebugMode && poolsCleared > 0)
                LogDebug($"{poolsCleared} pools limpos após carregar cena '{scene.name}'");

            ValidateSystemIntegrity();
        }

        /// <summary>
        /// Manipula encerramento da aplicação
        /// </summary>
        static void OnApplicationQuitting()
        {
            LogDebug("Aplicação encerrando - iniciando shutdown do sistema");
            ForceShutdown();
        }
        #endregion

        #region Debug e Estatísticas
        /// <summary>
        /// Exibe estatísticas completas do sistema
        /// </summary>
        public static void LogPoolStats()
        {
            var stats = GenerateSystemReport();
            Debug.Log(stats);
        }

        /// <summary>
        /// Gera relatório completo do sistema
        /// </summary>
        private static string GenerateSystemReport()
        {
            var report = new StringBuilder();

            report.AppendLine("=== SISTEMA DE POOL - RELATÓRIO COMPLETO ===");
            report.AppendLine($"Estado do Sistema: {systemState}");
            report.AppendLine($"Total de Pools: {pools.Count}");
            report.AppendLine($"Total de Objetos Ativos: {activeObjects.Count}");
            report.AppendLine($"Total de Spawns: {totalSpawnOperations}");
            report.AppendLine($"Total de Despawns: {totalDespawnOperations}");
            report.AppendLine($"Jobs System Ativo: {PoolJobSystem.IsInitialized}");
            report.AppendLine();

            foreach (var kvp in pools)
            {
                var poolTag = kvp.Key;
                var container = kvp.Value;

                container.GetDetailedStats(out int active, out int available, out int total, out string nextObj);

                report.AppendLine($"[{poolTag}]");
                report.AppendLine($"  Ativos: {active} | Disponíveis: {available} | Total: {total}");
                report.AppendLine($"  Próximo: {nextObj}");

                if (performanceMetrics.TryGetValue(poolTag, out var metrics))
                {
                    report.AppendLine($"  Spawns: {metrics.totalSpawns} (Média: {metrics.averageSpawnTime:F4}s)");
                    report.AppendLine($"  Despawns: {metrics.totalDespawns} (Média: {metrics.averageDespawnTime:F4}s)");
                    report.AppendLine($"  Pico de Objetos: {metrics.peakActiveObjects}");
                }

                report.AppendLine();
            }

            if (PoolJobSystem.IsInitialized)
            {
                PoolJobSystem.GetSystemStats(out bool jobInit, out int cachedPositions, out int cachedRotations);
                report.AppendLine("=== JOBS SYSTEM ===");
                report.AppendLine($"Inicializado: {jobInit}");
                report.AppendLine($"Posições em Cache: {cachedPositions}");
                report.AppendLine($"Rotações em Cache: {cachedRotations}");
            }

            return report.ToString();
        }
        #endregion

        #region Métodos Auxiliares
        /// <summary>
        /// Garante que o sistema está pronto para uso
        /// </summary>
        static bool EnsureSystemReady()
        {
            if (systemState == PoolSystemState.Ready) return true;

            if (systemState == PoolSystemState.Uninitialized)
            {
                Initialize();
                return systemState == PoolSystemState.Ready;
            }

            return false;
        }

        /// <summary>
        /// Valida parâmetros de spawn
        /// </summary>
        static bool ValidateSpawnParameters(string poolTag)
        {
            if (string.IsNullOrEmpty(poolTag))
            {
                LogError("Tag do pool não pode ser vazia");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Valida parâmetros de despawn
        /// </summary>
        static bool ValidateDespawnParameters(GameObject gameObject)
        {
            if (!gameObject)
            {
                LogWarning("Tentativa de despawn de objeto nulo");
                return false;
            }

            return systemState == PoolSystemState.Ready;
        }

        /// <summary>
        /// Valida parâmetros de despawn com delay
        /// </summary>
        static bool ValidateDespawnDelayedParameters(GameObject gameObject, float delay)
        {
            if (!ValidateDespawnParameters(gameObject)) return false;

            if (delay <= 0f)
            {
                LogWarning("Delay deve ser maior que zero");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Inicializa Jobs System se necessário
        /// </summary>
        static void InitializeJobSystemIfRequired()
        {
            if (!systemConfig.useJobsForBatching) return;

            PoolJobSystem.Initialize();
            LogDebug("Jobs System inicializado");
        }

        /// <summary>
        /// Valida integridade do sistema
        /// </summary>
        static void ValidateSystemIntegrity()
        {
            if (systemState != PoolSystemState.Ready) return;

            try
            {
                var orphanedObjects = new List<int>();

                foreach (var kvp in activeObjects)
                {
                    if (!kvp.Value || !kvp.Value.gameObject)
                        orphanedObjects.Add(kvp.Key);
                }

                foreach (var orphanId in orphanedObjects)
                {
                    activeObjects.Remove(orphanId);
                }

                if (orphanedObjects.Count > 0)
                {
                    LogWarning($"Removidos {orphanedObjects.Count} objetos órfãos da lista de ativos");
                }

                UpdatePerformanceMetrics();
            }
            catch (Exception ex)
            {
                LogError($"Erro na validação de integridade: {ex.Message}");
            }
        }

        /// <summary>
        /// Atualiza métricas de spawn
        /// </summary>
        static void UpdateSpawnMetrics(string poolTag, float spawnTime)
        {
            if (performanceMetrics.TryGetValue(poolTag, out var metrics))
            {
                metrics.RecordSpawn(spawnTime);
                metrics.UpdatePeakActiveObjects(GetActiveCount(poolTag));
            }
        }

        /// <summary>
        /// Atualiza métricas de despawn
        /// </summary>
        static void UpdateDespawnMetrics(string poolTag, float despawnTime)
        {
            if (performanceMetrics.TryGetValue(poolTag, out var metrics))
            {
                metrics.RecordDespawn(despawnTime);
            }
        }

        /// <summary>
        /// Atualiza métricas de performance
        /// </summary>
        static void UpdatePerformanceMetrics()
        {
            if (!systemConfig.enablePerformanceMetrics) return;

            var currentTime = Time.realtimeSinceStartup;
            if (currentTime - lastMetricsUpdate < systemConfig.metricsUpdateInterval)
                return;

            lastMetricsUpdate = currentTime;

            foreach (var kvp in performanceMetrics)
            {
                var poolTag = kvp.Key;
                var metrics = kvp.Value;
                metrics.UpdatePeakActiveObjects(GetActiveCount(poolTag));
            }
        }

        /// <summary>
        /// Reset de métricas de performance
        /// </summary>
        static void ResetPerformanceMetrics()
        {
            totalSpawnOperations = 0;
            totalDespawnOperations = 0;
            lastMetricsUpdate = Time.realtimeSinceStartup;
            performanceMetrics.Clear();
        }
        #endregion

        #region Cleanup Methods
        /// <summary>
        /// Limpa todos os pools
        /// </summary>
        static void CleanupAllPools()
        {
            foreach (var container in pools.Values)
            {
                try
                {
                    container?.Dispose();
                }
                catch (Exception ex)
                {
                    LogError($"Erro ao limpar pool: {ex.Message}");
                }
            }

            pools.Clear();
        }

        /// <summary>
        /// Limpa objetos ativos
        /// </summary>
        static void CleanupActiveObjects()
        {
            activeObjects.Clear();
        }

        /// <summary>
        /// Limpa hierarquia
        /// </summary>
        static void CleanupHierarchy()
        {
            if (poolManagerTransform)
            {
                SafeDestroyGameObject(poolManagerTransform.gameObject);
                poolManagerTransform = null;
            }
        }

        /// <summary>
        /// Finaliza Jobs System
        /// </summary>
        static void ShutdownJobSystem()
        {
            try
            {
                PoolJobSystem.Shutdown();
            }
            catch (Exception ex)
            {
                LogError($"Erro ao finalizar Jobs System: {ex.Message}");
            }
        }

        /// <summary>
        /// Reset de estado do sistema
        /// </summary>
        static void ResetSystemState()
        {
            database = null;
            systemConfig = default;
            ResetPerformanceMetrics();
            
            OnObjectSpawned = null;
            OnObjectDespawned = null;
            OnPoolCreated = null;
            OnPoolDestroyed = null;
            OnSystemInitialized = null;
            OnSystemShutdown = null;
        }

        /// <summary>
        /// Destroi GameObject de forma segura
        /// </summary>
        static void SafeDestroyGameObject(GameObject gameObject)
        {
            if (!gameObject) return;

            try
            {
                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(gameObject);
                else
                    UnityEngine.Object.DestroyImmediate(gameObject);
            }
            catch (Exception ex)
            {
                LogError($"Erro ao destruir GameObject: {ex.Message}");
            }
        }
        #endregion

        #region Logging
        static void LogDebug(string message)
        {
            if (EnableDebugMode)
                Debug.Log($"[Pool] {message}");
        }

        static void LogWarning(string message)
        {
            if (EnableDebugMode)
                Debug.LogWarning($"[Pool] {message}");
        }

        static void LogError(string message)
        {
            if (EnableDebugMode)
                Debug.LogError($"[Pool] {message}");
        }
        #endregion
    }
}