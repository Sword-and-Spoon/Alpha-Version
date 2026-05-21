using UnityEngine;

public class EnvironmentGenerator : MonoBehaviour
{
    [Header("Flower Prefabs")]
    public GameObject[] prefabs;
    public int amountToSpawn = 50;
    public Vector2 spawnArea = new Vector2(20, 20);
    public float minDistance = 0.5f;

    [Header("Sorting Settings")]
    public string sortingLayerName = "Default";
    public int sortingOrder = 1;

    void Start()
    {
        GenerateFlowers();
    }

    public void GenerateFlowers()
    {
        int spawnedCount = 0;
        int attempts = 0;
        int maxAttempts = amountToSpawn * 20;

        while (spawnedCount < amountToSpawn && attempts < maxAttempts)
        {
            attempts++;

            Vector3 spawnPos = transform.position + new Vector3(
                Random.Range(-spawnArea.x / 2, spawnArea.x / 2),
                Random.Range(-spawnArea.y / 2, spawnArea.y / 2),
                0
            );

            if (!Physics2D.OverlapCircle(spawnPos, minDistance))
            {
                int randomIndex = Random.Range(0, prefabs.Length);
                GameObject flower = Instantiate(prefabs[randomIndex], spawnPos, Quaternion.identity, transform);

                SetupFlower(flower);
                spawnedCount++;
            }
        }
    }

    private void SetupFlower(GameObject obj)
    {
        if (obj.TryGetComponent<SpriteRenderer>(out var sr))
        {
            sr.sortingLayerName = sortingLayerName;
            sr.sortingOrder = sortingOrder;
            sr.spriteSortPoint = SpriteSortPoint.Pivot;
        }

        Collider2D[] colliders = obj.GetComponentsInChildren<Collider2D>();
        foreach (var c in colliders) Destroy(c);

        Rigidbody2D rb = obj.GetComponent<Rigidbody2D>();
        if (rb != null) Destroy(rb);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(transform.position, new Vector3(spawnArea.x, spawnArea.y, 0));
    }
}
