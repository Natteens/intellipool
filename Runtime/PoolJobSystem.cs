using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace IntelliPool
{
    public static class PoolJobSystem
    {
        private static bool isInitialized;
        private static NativeArray<float3> cachedPositions;
        private static NativeArray<quaternion> cachedRotations;
        
        public static bool IsInitialized => isInitialized;
        
        /// <summary>
        /// Inicializa o sistema de Jobs
        /// </summary>
        public static void Initialize()
        {
            if (isInitialized) return;
            
            isInitialized = true;
            
            if (Pool.EnableDebugMode)
                Debug.Log("[PoolJobSystem] Sistema inicializado");
        }
        
        /// <summary>
        /// Finaliza o sistema de Jobs
        /// </summary>
        public static void Shutdown()
        {
            if (!isInitialized) return;
            
            if (cachedPositions.IsCreated) cachedPositions.Dispose();
            if (cachedRotations.IsCreated) cachedRotations.Dispose();
            
            isInitialized = false;
            
            if (Pool.EnableDebugMode)
                Debug.Log("[PoolJobSystem] Sistema finalizado");
        }
        
        /// <summary>
        /// Calcula posições para PreWarm em paralelo
        /// </summary>
        public static JobHandle CalculatePreWarmPositions(int count, float spacing, out NativeArray<Vector3> positions, out NativeArray<Quaternion> rotations, JobHandle dependency = default)
        {
            if (!isInitialized) Initialize();
            
            positions = new NativeArray<Vector3>(count, Allocator.TempJob);
            rotations = new NativeArray<Quaternion>(count, Allocator.TempJob);
            
            var job = new CalculateTransformsJob
            {
                positions = positions,
                rotations = rotations,
                seed = (uint)UnityEngine.Random.Range(1, 100000),
                spacing = spacing,
                count = count
            };
            
            JobHandle handle = job.Schedule(count, math.max(1, count / 32), dependency);
            
            if (Pool.EnableDebugMode)
                Debug.Log($"[PoolJobSystem] Calculando {count} posições em paralelo");
            
            return handle;
        }
        
        /// <summary>
        /// Valida IDs de instância em lote
        /// </summary>
        public static JobHandle ValidateInstanceIds(NativeArray<int> instanceIds, NativeArray<bool> validationResults, JobHandle dependency = default)
        {
            if (!isInitialized || !instanceIds.IsCreated || instanceIds.Length == 0)
                return dependency;
            
            var job = new ValidateInstanceIdsJob
            {
                instanceIds = instanceIds,
                validationResults = validationResults,
                minValidId = 1,
                maxValidId = int.MaxValue
            };
            
            JobHandle handle = job.Schedule(instanceIds.Length, math.max(1, instanceIds.Length / 16), dependency);
            
            if (Pool.EnableDebugMode)
                Debug.Log($"[PoolJobSystem] Validando {instanceIds.Length} IDs de instância");
            
            return handle;
        }
        
        /// <summary>
        /// Obtém estatísticas reais do sistema
        /// </summary>
        public static void GetSystemStats(out bool initialized, out int cachedPositionsCount, out int cachedRotationsCount)
        {
            initialized = isInitialized;
            cachedPositionsCount = cachedPositions.IsCreated ? cachedPositions.Length : 0;
            cachedRotationsCount = cachedRotations.IsCreated ? cachedRotations.Length : 0;
        }
        
        /// <summary>
        /// Libera recursos temporários
        /// </summary>
        public static void ClearTempResources()
        {
            if (cachedPositions.IsCreated)
            {
                cachedPositions.Dispose();
                cachedPositions = default;
            }
            
            if (cachedRotations.IsCreated)
            {
                cachedRotations.Dispose();
                cachedRotations = default;
            }
        }
    }
    
    /// <summary>
    /// Job para calcular transforms
    /// </summary>
    public struct CalculateTransformsJob : IJobParallelFor
    {
        public NativeArray<Vector3> positions;
        public NativeArray<Quaternion> rotations;
        
        [ReadOnly] public uint seed;
        [ReadOnly] public float spacing;
        [ReadOnly] public int count;
        
        public void Execute(int index)
        {
            var random = new Unity.Mathematics.Random(seed + (uint)index);
            
            int gridSize = (int)math.ceil(math.sqrt(count));
            int x = index % gridSize;
            int z = index / gridSize;
            
            float3 basePosition = new float3(
                (x - gridSize * 0.5f) * spacing,
                0f,
                (z - gridSize * 0.5f) * spacing
            );
            
            float3 randomOffset = new float3(
                random.NextFloat(-spacing * 0.4f, spacing * 0.4f),
                random.NextFloat(-1f, 1f),
                random.NextFloat(-spacing * 0.4f, spacing * 0.4f)
            );
            
            positions[index] = basePosition + randomOffset;
            
            float3 eulerAngles = new float3(
                random.NextFloat(-10f, 10f),
                random.NextFloat(0f, 360f),
                random.NextFloat(-10f, 10f)
            );
            
            rotations[index] = Quaternion.Euler(eulerAngles);
        }
    }
    
    /// <summary>
    /// Job para validar ‘IDs’ de instâncias
    /// </summary>
    public struct ValidateInstanceIdsJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<int> instanceIds;
        public NativeArray<bool> validationResults;
        
        [ReadOnly] public int minValidId;
        [ReadOnly] public int maxValidId;
        
        public void Execute(int index)
        {
            int instanceId = instanceIds[index];
            bool isValid = instanceId >= minValidId && 
                          instanceId <= maxValidId && 
                          instanceId != 0;
            
            validationResults[index] = isValid;
        }
    }
}