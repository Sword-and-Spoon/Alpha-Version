using System;

public enum QuestType
{
    DeliverItem,    // ส่งของที่มีใน inventory ทันที — AR บันทึกตอนรับเควส
    CookAndDeliver, // ทำอาหารก่อนแล้วค่อยส่ง — AR บันทึกตอนส่ง
    KillMonster,    // ตีมอนครบจำนวน — AR บันทึกตอน progress ครบ
    CollectItem,    // เก็บไอเทมครบแล้วส่ง — AR บันทึกตอนส่ง
}

public enum QuestStatus
{
    Active,        // กำลังทำอยู่
    PendingTurnIn, // รอ player กลับไปรายงาน/ส่งมอบที่ NPC
    LetterSent,    // (DeliverItem) ส่งจดหมายจ่ายเงินแล้ว รอ player เก็บที่ mailbox
}

[Serializable]
public class ARQuestData
{
    public string npcName;
    public string foodName;     // display name (ใช้กับทุก QuestType)
    public int amount;
    public int dueTotalDay;
    public int createdTotalDay;
    public int paymentTermDays = 1;
    public bool isLetterSent;
    public bool isARRecorded;

    public NPCLanguage language;
    public string objectiveText;

    // Templates จาก ARQuestNPC — ใช้ placeholder: {npcName}, {food}, {amount}
    public string letterSenderTemplate;
    public string letterContentTemplate;

    // ── Quest Type System ──────────────────────────────────────────────────
    public QuestType questType;
    public QuestStatus status;
    public string targetTag;      // itemId (CollectItem) หรือ enemyId (KillMonster)
    public int requiredCount;     // จำนวนที่ต้องทำ (default 1)
    public int currentProgress;   // progress ปัจจุบัน
    public MonsterTier minimumTier;  // KillMonster: ระดับขั้นต่ำที่นับ (default Common = นับทุกระดับ)
    public ItemQuality minimumItemQuality; // DeliverItem/CookAndDeliver/CollectItem: คุณภาพขั้นต่ำที่ยอมรับ
    public bool consumeRequiredItemsOnTurnIn = true;
}
