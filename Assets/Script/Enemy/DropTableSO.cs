using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewDropTable", menuName = "Enemy/Drop Table")]
public class DropTableSO : ScriptableObject
{
    [System.Serializable]
    public struct DropEntry
    {
        public ItemSO item;
        [Range(0, 100)] public float dropChance; // โอกาสดรอป 0-100%
        public int minAmount;
        public int maxAmount;

        [Header("Quality Override")]
        public bool useFixedQuality; // ถ้าติ๊ก จะใช้ Quality ที่กำหนดตายตัวด้านล่าง
        public ItemQuality fixedQuality;
    }

    public List<DropEntry> entries = new List<DropEntry>();
}
