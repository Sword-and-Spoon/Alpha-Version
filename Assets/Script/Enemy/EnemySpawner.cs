using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum SpawnerTier { Tier1, Tier2, Tier3, Tier4, Tier5 }

public class EnemySpawner : MonoBehaviour
{
    [Header("Spawn Settings")]
    public SpawnerTier spawnerTier = SpawnerTier.Tier1; // เลือกระดับความโหด
    public GameObject[] enemyPrefabs; // รองรับมอนสเตอร์หลายแบบ
    public Vector2 spawnArea = new Vector2(5f, 5f);
    public Vector2 spawnOffset = Vector2.zero; // เลื่อนตำแหน่งพื้นที่เกิดได้อิสระ
    public int maxEnemies = 5;
    public float spawnInterval = 3f;

    [Header("One-Time Spawn")]
    [Tooltip("ถ้าเปิด: spawn ครบ maxEnemies ครั้งเดียวแล้วไม่ spawn อีก แม้กลับมาซีนใหม่ (สำหรับ tutorial village)")]
    [SerializeField] private bool spawnOnce = false;
    [Tooltip("ID เฉพาะของ spawner นี้ — ถ้าว่างจะ auto-generate จากชื่อและตำแหน่ง")]
    [SerializeField] private string spawnerId = "";

    [Header("Visual Settings")]
    public string sortingLayerName = "Default";
    public int baseSortingOrder = 10; // ตั้งค่าลำดับให้สูงกว่า Tilemap

    [Header("Status")]
    [SerializeField] private int currentEnemyCount;
    private List<GameObject> activeEnemies = new List<GameObject>();
    private float spawnTimer;

    // Track spawners ที่ spawn ครบแล้วตลอด session (ข้าม scene ได้)
    private static readonly HashSet<string> exhaustedSpawners = new HashSet<string>();

    private bool isExhausted;
    private bool initialSpawnDone;

    private string ResolvedId => string.IsNullOrWhiteSpace(spawnerId)
        ? $"{gameObject.scene.name}_{gameObject.name}_{transform.position.x:F1}_{transform.position.y:F1}"
        : spawnerId;

    private void Start()
    {
        if (spawnOnce)
        {
            isExhausted = exhaustedSpawners.Contains(ResolvedId);
            if (!isExhausted)
            {
                // Spawn ทั้งหมดทีเดียวโดยไม่รอ interval
                for (int i = 0; i < maxEnemies; i++)
                    SpawnEnemy();
                initialSpawnDone = true;
            }
        }
    }

    private void Update()
    {
        // 1. Cleanup: ลบมอนสเตอร์ที่ตายแล้วออก
        activeEnemies.RemoveAll(enemy => enemy == null);
        currentEnemyCount = activeEnemies.Count;

        if (spawnOnce)
        {
            // เมื่อ enemy ทุกตัวตายหมดแล้ว → mark exhausted ไม่ spawn อีก
            if (initialSpawnDone && !isExhausted && activeEnemies.Count == 0)
            {
                isExhausted = true;
                exhaustedSpawners.Add(ResolvedId);
            }
            return;
        }

        // 2. Spawn Logic (ปกติ)
        if (activeEnemies.Count < maxEnemies)
        {
            spawnTimer += Time.deltaTime;
            if (spawnTimer >= spawnInterval)
            {
                SpawnEnemy();
                spawnTimer = 0;
            }
        }
    }

    private void SpawnEnemy()
    {
        if (enemyPrefabs == null || enemyPrefabs.Length == 0) return;

        int randomIndex = Random.Range(0, enemyPrefabs.Length);
        GameObject prefabToSpawn = enemyPrefabs[randomIndex];

        // ป้องกัน Error หากใน Inspector มีช่องที่ขึ้นว่า "Missing" หรือเป็น null
        if (prefabToSpawn == null)
        {
            Debug.LogWarning($"[EnemySpawner] Enemy Prefab at index {randomIndex} is missing/null on {gameObject.name}. Please check the Inspector.");
            return;
        }

        // 3. Zone-based: สุ่มตำแหน่งในพื้นที่สี่เหลี่ยมพร้อม Offset
        Vector3 center = transform.position + (Vector3)spawnOffset;
        float randomX = Random.Range(-spawnArea.x / 2f, spawnArea.x / 2f);
        float randomY = Random.Range(-spawnArea.y / 2f, spawnArea.y / 2f);
        Vector3 spawnPosition = center + new Vector3(randomX, randomY, 0);

        GameObject newEnemy = Instantiate(prefabToSpawn, spawnPosition, Quaternion.identity);

        // 4. Auto Setup Visuals (เพื่อให้มอนสเตอร์ไม่จมดิน)
        if (newEnemy.TryGetComponent<SpriteRenderer>(out var sr))
        {
            sr.sortingLayerName = sortingLayerName;
            sr.sortingOrder = baseSortingOrder;
            sr.spriteSortPoint = SpriteSortPoint.Pivot; // เพื่อให้เดินบังกันได้ถูกต้อง
        }

        // เก็บอ้างอิงไว้เพื่อ Track จำนวน
        activeEnemies.Add(newEnemy);
    }

    // ช่วยให้เห็นขอบเขตจุดเกิดในหน้า Scene
    private void OnDrawGizmosSelected()
    {
        Vector3 center = transform.position + (Vector3)spawnOffset;

        // กำหนดสีตามระดับ Tier
        switch (spawnerTier)
        {
            case SpawnerTier.Tier1: Gizmos.color = Color.white; break;
            case SpawnerTier.Tier2: Gizmos.color = Color.cyan; break;
            case SpawnerTier.Tier3: Gizmos.color = Color.magenta; break;
            case SpawnerTier.Tier4: Gizmos.color = new Color(1f, 0.5f, 0f); break; // Orange
            case SpawnerTier.Tier5: Gizmos.color = Color.yellow; break;
            default: Gizmos.color = Color.white; break;
        }

        // วาดขอบเขตการเกิด
        Gizmos.DrawWireCube(center, new Vector3(spawnArea.x, spawnArea.y, 0));

        // วาดจุดกึ่งกลางของ Spawner (สีแดงใสเสมอเพื่อให้เห็นจุดติดตั้ง)
        Gizmos.color = new Color(1, 0, 0, 0.5f);
        Gizmos.DrawSphere(transform.position, 0.1f);
    }
}
