using UnityEngine;
using System.Collections.Generic;

public class CropSpawner : MonoBehaviour
{
    private BoxCollider2D spawnArea;

    public GameObject cropPrefab;
    public int numberOfCrops = 10;
    public float minDistanceBetweenCrops = 1f;

    private List<Vector2> usedPositions = new List<Vector2>();

    void Awake()
    {
        spawnArea = GetComponent<BoxCollider2D>();
    }

    void Start()
    {
        SpawnCrops();
    }

    void SpawnCrops()
    {
        int spawned = 0;
        int attempts = 0;
        int maxAttempts = 500;

        while (spawned < numberOfCrops && attempts < maxAttempts)
        {
            Vector2 spawnPosition = new Vector2(
                Random.Range(spawnArea.bounds.min.x, spawnArea.bounds.max.x),
                Random.Range(spawnArea.bounds.min.y, spawnArea.bounds.max.y)
            );

            if (IsFarEnough(spawnPosition))
            {
                GameObject obj = Instantiate(cropPrefab, spawnPosition, Quaternion.identity, transform);
                SpriteRenderer sr = obj.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    sr.sortingOrder = 1;
                }

                usedPositions.Add(spawnPosition);
                spawned++;
            }

            attempts++;
        }

        if (spawned < numberOfCrops)
        {
            Debug.LogWarning("Spawn ไม่ครบ อาจพื้นที่เล็กเกินไป!");
        }
    }

    bool IsFarEnough(Vector2 pos)
    {
        foreach (var p in usedPositions)
        {
            if (Vector2.Distance(pos, p) < minDistanceBetweenCrops)
                return false;
        }
        return true;
    }
}