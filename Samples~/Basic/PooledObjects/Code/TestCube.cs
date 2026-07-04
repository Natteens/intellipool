using IntelliPool;
using UnityEngine;

public class TestCube : MonoBehaviour, IPoolable
{
    public float bounceForce = 200f;
    public Material[] randomMaterials;

    private Rigidbody rb;
    private Renderer cubeRenderer;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        cubeRenderer = GetComponent<Renderer>();
    }

    public void OnSpawnedFromPool()
    {
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        if (cubeRenderer != null && randomMaterials != null && randomMaterials.Length > 0)
            cubeRenderer.sharedMaterial = randomMaterials[Random.Range(0, randomMaterials.Length)];
    }

    public void OnReturnedToPool() { }

    void OnCollisionEnter(Collision collision)
    {
        if (rb == null) return;
        if (collision.gameObject.CompareTag("Ground") || collision.gameObject.name.Contains("Plane"))
            rb.AddForce((Vector3.up + Random.insideUnitSphere * 0.3f) * bounceForce);
    }
}
