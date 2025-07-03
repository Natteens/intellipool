using GameInit.PooledObjects;
using UnityEngine;

public class PoolTestSpawner : MonoBehaviour
{
    [Header("Configurações de Spawn")]
    [SerializeField] private float spawnForce = 500f;
    [SerializeField] private float spawnHeight = 2f;
    [SerializeField] private float despawnTime = 5f;
    [SerializeField] private float spawnRadius = 3f;
    
    [Header("Controle de Taxa (Opcional)")]
    [SerializeField] private bool useSpawnRate;
    [SerializeField] private float spawnRate;
    private float lastSpawnTime;

    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = true;
    [SerializeField] private bool showJobsStats = true;

    void Update()
    {
        if (Input.GetKey(KeyCode.Space))
        {
            if (!useSpawnRate || CanSpawn())
            {
                SpawnCube();
            }
        }
    }
    
    bool CanSpawn()
    {
        return Time.time >= lastSpawnTime + spawnRate;
    }
    void SpawnCube()
    {
        Vector3 randomOffset = new Vector3(
            Random.Range(-spawnRadius, spawnRadius),
            spawnHeight,
            Random.Range(-spawnRadius, spawnRadius)
        );

        Vector3 spawnPosition = transform.position + randomOffset;
        GameObject cube = Pool.SpawnByTag("TestCube", spawnPosition, Quaternion.identity);

        if (cube != null)
        {
            if (useSpawnRate)
            {
                lastSpawnTime = Time.time;
            }
            
            Rigidbody rb = cube.GetComponent<Rigidbody>();
            if (rb != null)
            {
                Vector3 forceDirection = new Vector3(
                    Random.Range(-100f, 100f),
                    spawnForce,
                    Random.Range(-100f, 100f)
                );
                rb.AddForce(forceDirection);

                Vector3 torque = new Vector3(
                    Random.Range(-50f, 50f),
                    Random.Range(-50f, 50f),
                    Random.Range(-50f, 50f)
                );
                rb.AddTorque(torque);
            }

            Pool.DespawnDelayed(cube, despawnTime);
            Debug.Log($"Cubo spawnado! Total ativo: {Pool.GetActiveCount("TestCube")}");
        }
        else
        {
            Debug.LogWarning("Falha ao spawnar cubo - Pool pode estar no limite!");
        }
    }
}