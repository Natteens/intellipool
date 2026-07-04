using IntelliPool;
using UnityEngine;

public class PoolTestSpawner : MonoBehaviour
{
    [SerializeField] private float spawnForce = 500f;
    [SerializeField] private float spawnHeight = 2f;
    [SerializeField] private float releaseDelay = 1.5f;
    [SerializeField] private float spawnRadius = 3f;
    [SerializeField] private bool autoSpawnOnStart;
    [SerializeField] private float spawnEverySeconds = 0.15f;

    void Start()
    {
        if (autoSpawnOnStart)
            _ = AutoSpawnLoop();
    }

    async Awaitable AutoSpawnLoop()
    {
        try
        {
            while (autoSpawnOnStart)
            {
                SpawnOnce();
                await Awaitable.WaitForSecondsAsync(spawnEverySeconds, destroyCancellationToken);
            }
        }
        catch (System.OperationCanceledException) { }
    }

    [ContextMenu("Spawn Once")]
    public void SpawnOnce()
    {
        var offset = new Vector3(
            Random.Range(-spawnRadius, spawnRadius),
            spawnHeight,
            Random.Range(-spawnRadius, spawnRadius));

        var cube = Pool.Get("TestCube", transform.position + offset);
        if (cube == null) return;

        if (cube.TryGetComponent(out Rigidbody rb))
        {
            rb.AddForce(new Vector3(Random.Range(-100f, 100f), spawnForce, Random.Range(-100f, 100f)));
            rb.AddTorque(new Vector3(Random.Range(-50f, 50f), Random.Range(-50f, 50f), Random.Range(-50f, 50f)));
        }

        Pool.ReleaseDelayed(cube, releaseDelay);
    }

    public void SpawnBurst(int count)
    {
        for (int i = 0; i < count; i++)
            SpawnOnce();
    }
}
