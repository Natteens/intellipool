using IntelliPool;
using UnityEngine;

public class PoolTestSpawner : MonoBehaviour
{
    [SerializeField] private PoolDatabase database;
    [SerializeField] private float spawnForce = 500f;
    [SerializeField] private float spawnHeight = 2f;
    [SerializeField] private float releaseDelay = 1.5f;
    [SerializeField] private float spawnRadius = 3f;
    [SerializeField] private float spawnRate = 0.15f;

    private float lastSpawnTime;

    void Awake()
    {
        if (database != null && !Pool.IsReady)
            Pool.Initialize(database);
    }

    void Update()
    {
        if (Input.GetKey(KeyCode.Space) && Time.time >= lastSpawnTime + spawnRate)
            SpawnCube();
    }

    void SpawnCube()
    {
        var offset = new Vector3(
            Random.Range(-spawnRadius, spawnRadius),
            spawnHeight,
            Random.Range(-spawnRadius, spawnRadius));

        var cube = Pool.Get("TestCube", transform.position + offset);
        if (cube == null) return;

        lastSpawnTime = Time.time;

        if (cube.TryGetComponent(out Rigidbody rb))
        {
            rb.AddForce(new Vector3(Random.Range(-100f, 100f), spawnForce, Random.Range(-100f, 100f)));
            rb.AddTorque(new Vector3(Random.Range(-50f, 50f), Random.Range(-50f, 50f), Random.Range(-50f, 50f)));
        }

        Pool.ReleaseDelayed(cube, releaseDelay);
    }
}
