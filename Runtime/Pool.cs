using System;
using System.Collections.Generic;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IntelliPool
{
    /// <summary>
    /// Sistema centralizado de gerenciamento de pools de objetos
    /// API simplificada e dinâmica
    /// </summary>
    public static class Pool
    {
        #region Campos Privados
        private static readonly Dictionary<string, PoolContainer> pools = new Dictionary<string, PoolContainer>();
        private static readonly Dictionary<int, PoolableObject> activeObjects = new Dictionary<int, PoolableObject>();
        private static readonly Dictionary<string, PoolMetrics> metrics = new Dictionary<string, PoolMetrics>();
        
        private static Transform poolRoot;
        private static PoolDatabase database;
        private static bool isInitialized;
        private static bool systemEnabled = true;
        private static string databasePath;
        #endregion

        #region Propriedades Públicas
        public static bool IsInitialized => isInitialized;
        public static bool SystemEnabled => systemEnabled;
        public static bool EnableDebugMode => database?.enableDebugMode ?? false;
        public static int TotalActivePools => pools.Count;
        public static int TotalActiveObjects => activeObjects.Count;
        public static PoolDatabase Database => database;
        #endregion

        #region Eventos
        public static event Action<string, GameObject> OnObjectSpawned;
        public static event Action<string, GameObject> OnObjectDespawned;
        public static event Action<string> OnPoolCreated;
        public static event Action OnSystemInitialized;
        #endregion

        #region Inicialização
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Initialize()
        {
            if (isInitialized) return;

            try
            {
                LoadDatabase();
                
                if (!systemEnabled)
                {
                    LogDebug("Pool System disabled - not initializing");
                    return;
                }

                CreatePoolRoot();
                SetupPreconfiguredPools();
                RegisterEvents();
                
                isInitialized = true;
                OnSystemInitialized?.Invoke();
                
                LogDebug("Pool System initialized successfully");
            }
            catch (Exception ex)
            {
                LogError($"Initialization error: {ex.Message}");
            }
        }

        static void LoadDatabase()
        {
            if (LoadDatabaseFromSavedPath())
                return;

            var databases = Resources.LoadAll<PoolDatabase>("");
            if (databases.Length > 0)
            {
                database = databases[0];
                systemEnabled = database.enablePoolSystem;
                LogDebug($"Database loaded from Resources: {database.name}");
                return;
            }

            systemEnabled = true;
            LogDebug("No database found - system enabled by default");
        }

        static bool LoadDatabaseFromSavedPath()
        {
#if UNITY_EDITOR
            databasePath = EditorPrefs.GetString("IntelliPool.DatabasePath", "");
            
            if (string.IsNullOrEmpty(databasePath))
                return false;

            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<PoolDatabase>(databasePath);
            if (asset != null)
            {
                database = asset;
                systemEnabled = database.enablePoolSystem;
                LogDebug($"Database loaded from saved path: {databasePath}");
                return true;
            }
#endif
            return false;
        }

        public static void SetDatabasePath(string path)
        {
#if UNITY_EDITOR
            databasePath = path;
            EditorPrefs.SetString("IntelliPool.DatabasePath", path);
#endif
        }

        static void CreatePoolRoot()
        {
            var rootGO = new GameObject("PoolSystem")
            {
                hideFlags = HideFlags.DontSave
            };
            
            UnityEngine.Object.DontDestroyOnLoad(rootGO);
            poolRoot = rootGO.transform;
        }

        static void SetupPreconfiguredPools()
        {
            if (database?.pools == null) return;

            int created = 0;
            foreach (var config in database.pools)
            {
                if (CreatePool(config.poolTag, config))
                    created++;
            }

            LogDebug($"Pre-configured pools created: {created}");
        }

        static void RegisterEvents()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            
            Application.quitting -= OnApplicationQuit;
            Application.quitting += OnApplicationQuit;
        }
        #endregion

        #region API Principal - Spawn
        /// <summary>
        /// Spawna objeto por tipo genérico
        /// </summary>
        public static T Spawn<T>(Vector3 position = default, Quaternion rotation = default, Transform parent = null) where T : Component
        {
            var poolTag = typeof(T).Name;
            var gameObject = SpawnByTag(poolTag, position, rotation, parent);
            return gameObject?.GetComponent<T>();
        }

        /// <summary>
        /// Spawna objeto por tag - sobrecargas para compatibilidade
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
        /// Spawna objeto por tag - método principal
        /// </summary>
        public static GameObject SpawnByTag(string poolTag, Vector3 position, Quaternion rotation, Transform parent)
        {
            if (!EnsureSystemReady()) return null;
            if (string.IsNullOrEmpty(poolTag)) return null;

            if (!pools.ContainsKey(poolTag))
            {
                if (!TryAutoCreatePool(poolTag))
                {
                    LogError($"Pool '{poolTag}' not found and could not auto-create");
                    return null;
                }
            }

            var container = pools[poolTag];
            var poolableObject = container.Spawn(position, rotation, parent);
            
            if (poolableObject != null)
            {
                activeObjects[poolableObject.InstanceID] = poolableObject;
                RecordSpawn(poolTag);
                OnObjectSpawned?.Invoke(poolTag, poolableObject.gameObject);
            }

            return poolableObject?.gameObject;
        }

        /// <summary>
        /// Spawna objeto por string (método alternativo)
        /// </summary>
        public static GameObject Spawn(string poolTag, Vector3 position = default, Quaternion rotation = default, Transform parent = null)
        {
            return SpawnByTag(poolTag, position, rotation, parent);
        }

        /// <summary>
        /// Tenta criar pool automaticamente baseado no nome
        /// </summary>
        static bool TryAutoCreatePool(string poolTag)
        {
#if UNITY_EDITOR
            var guids = UnityEditor.AssetDatabase.FindAssets($"t:GameObject {poolTag}");
            
            if (guids.Length == 0) return false;

            var assetPath = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            
            if (prefab == null) return false;

            var config = new PoolDatabase.PoolConfiguration
            {
                poolTag = poolTag,
                prefab = prefab,
                initialSize = 5,
                maxSize = 100,
                preWarm = true,
                clearOnSceneLoad = false
            };

            return CreatePool(poolTag, config);
#else
            return false;
#endif
        }
        #endregion

        #region API Principal - Despawn
        /// <summary>
        /// Retorna objeto para o pool
        /// </summary>
        public static bool Despawn(GameObject gameObject)
        {
            if (!gameObject) return false;

            var poolableObject = gameObject.GetComponent<PoolableObject>();
            if (!poolableObject)
            {
                LogWarning($"Trying to despawn object without PoolableObject: {gameObject.name}");
                return false;
            }

            return DespawnByID(poolableObject.InstanceID);
        }

        /// <summary>
        /// Retorna objeto por ID
        /// </summary>
        public static bool DespawnByID(int instanceID)
        {
            if (!activeObjects.TryGetValue(instanceID, out var poolableObject))
                return false;

            var poolTag = poolableObject.PoolTag;
            
            if (pools.TryGetValue(poolTag, out var container))
            {
                if (container.ReturnToPool(poolableObject))
                {
                    activeObjects.Remove(instanceID);
                    RecordDespawn(poolTag);
                    OnObjectDespawned?.Invoke(poolTag, poolableObject.gameObject);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Despawn com delay
        /// </summary>
        public static void DespawnDelayed(GameObject gameObject, float delay)
        {
            if (!gameObject || delay <= 0f) return;

            var poolableObject = gameObject.GetComponent<PoolableObject>();
            if (!poolableObject)
                poolableObject = gameObject.AddComponent<PoolableObject>();

            poolableObject.StartDespawnTimer(delay);
        }

        /// <summary>
        /// Despawna todos os objetos de um pool
        /// </summary>
        public static void DespawnAll(string poolTag)
        {
            if (!pools.TryGetValue(poolTag, out var container))
                return;

            var objectsToRemove = activeObjects.Values
                .Where(obj => obj.PoolTag == poolTag)
                .ToList();

            foreach (var obj in objectsToRemove)
            {
                DespawnByID(obj.InstanceID);
            }
        }
        #endregion

        #region Gerenciamento de Pools
        /// <summary>
        /// Cria um novo pool
        /// </summary>
        public static bool CreatePool(string poolTag, PoolDatabase.PoolConfiguration config)
        {
            if (pools.ContainsKey(poolTag))
            {
                LogWarning($"Pool '{poolTag}' already exists");
                return false;
            }

            try
            {
                var container = new PoolContainer(config, poolRoot);
                pools[poolTag] = container;
                metrics[poolTag] = new PoolMetrics();
                
                OnPoolCreated?.Invoke(poolTag);
                LogDebug($"Pool created: '{poolTag}'");
                return true;
            }
            catch (Exception ex)
            {
                LogError($"Error creating pool '{poolTag}': {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Pre-aquece um pool
        /// </summary>
        public static void PreWarm(string poolTag, int count)
        {
            if (!pools.TryGetValue(poolTag, out var container))
                return;

            var tempObjects = new List<GameObject>();
            
            for (int i = 0; i < count; i++)
            {
                var obj = SpawnByTag(poolTag);
                if (obj) tempObjects.Add(obj);
                else break;
            }

            foreach (var obj in tempObjects)
                Despawn(obj);

            LogDebug($"Pool '{poolTag}' pre-warmed with {tempObjects.Count} objects");
        }

        /// <summary>
        /// Remove um pool
        /// </summary>
        public static bool RemovePool(string poolTag)
        {
            if (!pools.TryGetValue(poolTag, out var container))
                return false;

            container.Dispose();
            pools.Remove(poolTag);
            metrics.Remove(poolTag);
            
            LogDebug($"Pool removed: '{poolTag}'");
            return true;
        }
        #endregion

        #region Consultas e Informações
        /// <summary>
        /// Obtém contagem de objetos ativos
        /// </summary>
        public static int GetActiveCount(string poolTag)
        {
            return pools.TryGetValue(poolTag, out var container) ? container.ActiveCount : 0;
        }

        /// <summary>
        /// Lista todos os pools disponíveis
        /// </summary>
        public static string[] GetAvailablePoolTags()
        {
            return pools.Keys.ToArray();
        }

        /// <summary>
        /// Obtém informações de um pool
        /// </summary>
        public static bool GetPoolInfo(string poolTag, out PoolInfo info)
        {
            info = default;
            
            if (!pools.TryGetValue(poolTag, out var container))
                return false;

            info = new PoolInfo
            {
                poolTag = poolTag,
                activeCount = container.ActiveCount,
                totalCount = container.TotalCount,
                maxSize = container.MaxSize
            };

            return true;
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
        #endregion

        #region Eventos do Sistema
        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!isInitialized) return;

            if (database?.pools != null)
            {
                var poolsToClean = pools.Where(kvp => 
                    database.pools.Any(config => 
                        config.poolTag == kvp.Key && config.clearOnSceneLoad)
                ).ToList();

                foreach (var kvp in poolsToClean)
                {
                    DespawnAll(kvp.Key);
                }
            }

            LogDebug($"Scene loaded: {scene.name}");
        }

        static void OnApplicationQuit()
        {
            Shutdown();
        }
        #endregion

        #region Métricas e Debug
        public static void LogPoolStats()
        {
            if (!isInitialized) return;

            var stats = $"=== POOL SYSTEM STATS ===\n" +
                       $"Pools: {pools.Count}\n" +
                       $"Active Objects: {activeObjects.Count}\n";

            foreach (var kvp in pools)
            {
                var container = kvp.Value;
                stats += $"[{kvp.Key}] Active: {container.ActiveCount}, Total: {container.TotalCount}\n";
            }

            Debug.Log(stats);
        }

        static void RecordSpawn(string poolTag)
        {
            if (metrics.TryGetValue(poolTag, out var metric))
                metric.totalSpawns++;
        }

        static void RecordDespawn(string poolTag)
        {
            if (metrics.TryGetValue(poolTag, out var metric))
                metric.totalDespawns++;
        }
        #endregion

        #region Utilitários
        static bool EnsureSystemReady()
        {
            if (isInitialized && systemEnabled) return true;
            
            if (!isInitialized)
            {
                Initialize();
                return isInitialized && systemEnabled;
            }
            
            return false;
        }

        static void Shutdown()
        {
            if (!isInitialized) return;

            foreach (var container in pools.Values)
                container?.Dispose();

            pools.Clear();
            activeObjects.Clear();
            metrics.Clear();

            if (poolRoot)
                UnityEngine.Object.Destroy(poolRoot.gameObject);

            isInitialized = false;
            LogDebug("System shutdown");
        }

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
            Debug.LogError($"[Pool] {message}");
        }
        #endregion

        #region Estruturas de Dados
        public struct PoolInfo
        {
            public string poolTag;
            public int activeCount;
            public int totalCount;
            public int maxSize;
        }

        public class PoolMetrics
        {
            public int totalSpawns;
            public int totalDespawns;
        }
        #endregion
    }
}