using UnityEngine;

public class TestCube : MonoBehaviour
{
    [Header("Configurações do Cubo")]
    public float bounceForce = 200f;
    public Material[] randomMaterials;
    
    private Rigidbody rb;
    private Renderer cubeRenderer;
    private Color originalColor;
    private Vector3 originalScale;
    
    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        cubeRenderer = GetComponent<Renderer>();
        
        if (cubeRenderer != null)
        {
            originalColor = cubeRenderer.material.color;
        }
        
        originalScale = transform.localScale;
    }
    
    void OnEnable()
    {
        // Reset do cubo quando spawna
        ResetCube();
        
        // Cor aleatória
        if (cubeRenderer != null)
        {
            if (randomMaterials != null && randomMaterials.Length > 0)
            {
                cubeRenderer.material = randomMaterials[Random.Range(0, randomMaterials.Length)];
            }
            else
            {
                cubeRenderer.material.color = new Color(
                    Random.Range(0f, 1f),
                    Random.Range(0f, 1f),
                    Random.Range(0f, 1f),
                    1f
                );
            }
        }
        
        Debug.Log($"Cubo {name} ativado!");
    }
    
    void OnDisable()
    {
        // Cleanup quando retorna ao pool
        if (cubeRenderer != null)
        {
            cubeRenderer.material.color = originalColor;
        }
        
        transform.localScale = originalScale;
        
        Debug.Log($"Cubo {name} retornou ao pool!");
    }
    
    void ResetCube()
    {
        // Reset da física
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        
        // Reset da escala
        transform.localScale = originalScale;
    }
    
    void OnCollisionEnter(Collision collision)
    {
        // Bounce extra quando bate no chão
        if (collision.gameObject.CompareTag("Ground") || collision.gameObject.name.Contains("Plane"))
        {
            if (rb != null)
            {
                Vector3 bounceDirection = Vector3.up + Random.insideUnitSphere * 0.3f;
                rb.AddForce(bounceDirection * bounceForce);
            }
            
            // Efeito visual de bounce
            StartCoroutine(BounceEffect());
        }
    }
    
    System.Collections.IEnumerator BounceEffect()
    {
        Vector3 targetScale = originalScale * 1.2f;
        
        // Scale up
        float time = 0f;
        while (time < 0.1f)
        {
            time += Time.deltaTime;
            transform.localScale = Vector3.Lerp(originalScale, targetScale, time / 0.1f);
            yield return null;
        }
        
        // Scale down
        time = 0f;
        while (time < 0.1f)
        {
            time += Time.deltaTime;
            transform.localScale = Vector3.Lerp(targetScale, originalScale, time / 0.1f);
            yield return null;
        }
        
        transform.localScale = originalScale;
    }
}