using UnityEngine;

public class Enemy_Drop : MonoBehaviour
{
    [Header("Drop Settings")]
    [SerializeField] private DropTableSO dropTable;

    private Enemy_Stats enemyStats;

    private void Awake()
    {
        enemyStats = GetComponent<Enemy_Stats>();
    }

    // ฟังก์ชันนี้ถูกเรียกโดย Health.cs เมื่อมอนสเตอร์ตาย
    public void DropItem()
    {
        if (enemyStats == null)
        {
            enemyStats = GetComponent<Enemy_Stats>();
        }

        string monsterTag = (enemyStats?.enemyData != null) ? enemyStats.enemyData.enemyId : gameObject.tag;
        MonsterTier tier  = enemyStats != null ? enemyStats.tier : MonsterTier.Common;
        ARQuestManager.Instance?.NotifyMonsterKilled(monsterTag, tier);

        if (dropTable == null || dropTable.entries == null || dropTable.entries.Count == 0)
        {
            Debug.LogWarning("Drop Table is missing or empty on " + gameObject.name);
            return;
        }

        foreach (var entry in dropTable.entries)
        {
            if (entry.item == null) continue;

            // ทอยลูกเต๋าเช็คโอกาสดรอป (%)
            float roll = Random.Range(0f, 100f);
            if (roll <= entry.dropChance)
            {
                // สุ่มจำนวนชิ้นที่จะดรอป
                int amount = Random.Range(entry.minAmount, entry.maxAmount + 1);

                // กำหนดคุณภาพไอเทม (Quality)
                ItemQuality q;
                if (entry.useFixedQuality)
                {
                    q = entry.fixedQuality; // ใช้ค่าที่กำหนดตายตัวใน Drop Table
                }
                else if (enemyStats != null && enemyStats.tierSettings != null)
                {
                    // ใช้คุณภาพไอเทมอัตโนมัติตาม Tier ของมอนสเตอร์
                    var settings = enemyStats.tierSettings.GetSettings(enemyStats.tier);
                    q = settings.defaultQuality;
                }
                else
                {
                    q = ItemQuality.Common; // Fallback
                }

                // สร้างไอเทมและดรอปลงพื้น
                Item item = new Item(entry.item, amount, q);
                ItemScene.SpawnItemScene(transform.position, item);
            }
        }
    }
}
