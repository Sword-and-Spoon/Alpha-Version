using System;
using UnityEngine;

/// <summary>
/// คู่ ItemSO + คุณภาพขั้นต่ำที่ยอมรับสำหรับเควส
/// </summary>
[Serializable]
public struct QuestItemEntry
{
    public ItemSO item;
    [Tooltip("คุณภาพขั้นต่ำที่ยอมรับ — Common = ยอมรับทุกระดับ")]
    public ItemQuality minimumQuality;
}

/// <summary>
/// ข้อมูล template สำหรับเควสที่สุ่มได้ — ลาก ScriptableObject นี้เข้า questPool ของ ARQuestNPC
/// สร้างได้จาก Assets > Create > Quest > Quest Template
/// </summary>
[CreateAssetMenu(fileName = "QuestTemplate", menuName = "Quest/Quest Template")]
public class QuestTemplate : ScriptableObject
{
    [Header("Quest Type")]
    public QuestType questType = QuestType.DeliverItem;

    [Header("Items / Enemies (สุ่มเลือก 1 จาก list)")]
    [Tooltip("ใช้กับ DeliverItem และ CookAndDeliver — สุ่มเลือก 1 รายการทุกครั้งที่ quest ถูกสร้าง")]
    [NonReorderable] public QuestItemEntry[] foodItems;

    [Tooltip("ใช้กับ KillMonster — สุ่มเลือก 1 ชนิดเป็นเป้าหมาย")]
    [NonReorderable] public EnemySO[] targetEnemies;

    [Tooltip("ใช้กับ CollectItem — สุ่มเลือก 1 รายการ")]
    [NonReorderable] public QuestItemEntry[] collectItems;

    [Header("Reward Range (สุ่มในช่วงนี้)")]
    [Tooltip("จำนวนเงินรางวัลขั้นต่ำ–สูงสุด")]
    public Vector2Int amountRange = new Vector2Int(300, 600);

    [Tooltip("จำนวนวันรอรับเงิน")]
    public Vector2Int paymentTermRange = new Vector2Int(1, 3);

    [Header("Count Range (KillMonster / CollectItem / CookAndDeliver)")]
    [Tooltip("จำนวนที่ต้องทำ ขั้นต่ำ–สูงสุด")]
    public Vector2Int requiredCountRange = new Vector2Int(1, 1);

    [Header("KillMonster Only")]
    public MonsterTier minimumTier = MonsterTier.Common;

    [Header("Unlock Condition")]
    [Tooltip("วันบัญชีขั้นต่ำที่ template นี้จะถูกสุ่มได้\n1 = ตั้งแต่เริ่มเกม (Common)\n4 = เริ่มวันที่ 4 (Rare)\n8 = เริ่มวันที่ 8 (Epic)")]
    public int minUnlockDay = 1;

    [Header("Weight (น้ำหนักการสุ่ม)")]
    [Tooltip("ยิ่งสูงยิ่งมีโอกาสถูกสุ่มเลือกมากกว่า template อื่น")]
    [Min(0f)]
    public float weight = 1f;

    [Header("Dialogue Override (เว้นว่าง = ใช้ default อัตโนมัติ)")]
    [Tooltip("ถ้าใส่ข้อความ offerMessage จะใช้ชุด dialogue นี้แทน default")]
    public NPCDialogueSet englishDialogueOverride;
    public NPCDialogueSet thaiDialogueOverride;
}
