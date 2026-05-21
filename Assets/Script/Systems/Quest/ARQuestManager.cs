using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// จัดการ AR Quest ทั้งหมด — สร้าง quest, ติดตามวันครบกำหนด, และจ่ายเงินตามประเภทเควส
/// รองรับ QuestType หลายประเภท: DeliverItem, CookAndDeliver, KillMonster, CollectItem
/// </summary>
public class ARQuestManager : MonoBehaviour
{
    public static ARQuestManager Instance;
    public static event Action<ARQuestData> OnQuestCreated;
    public static event Action<ARQuestData> OnQuestHandedIn;
    public static event Action<string>    OnQuestCompleted;
    public static event Action<ARQuestData> OnQuestProgressChanged;
    public static event Action<QuestType> OnFeaturedQuestChanged;

    [SerializeField] private MailboxInteractable mailbox;

    [Header("Weekly Featured Quest")]
    [Tooltip("เปิด/ปิดระบบ Featured Quest ประจำสัปดาห์")]
    [SerializeField] private bool enableWeeklyBonus = true;
    [Tooltip("ตัวคูณรางวัลสำหรับ featured quest type (1.5 = +50%)")]
    [SerializeField] private float weeklyBonusMultiplier = 1.5f;
    [Tooltip("วนรอบ quest type ตามลำดับนี้ทุกสัปดาห์ (7 วันเกม)")]
    [SerializeField] private QuestType[] weeklyRotation = new QuestType[]
    {
        QuestType.CookAndDeliver,
        QuestType.KillMonster,
        QuestType.DeliverItem,
        QuestType.CollectItem,
    };

    private QuestType featuredQuestType;
    private int lastFeaturedWeek = -1;

    private readonly List<ARQuestData> pendingQuests = new();
    private readonly Dictionary<string, int> npcCooldowns = new();
    private int lastCheckedDay = -1;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        TimeManager.OnDateTimeChanged += OnDateTimeChanged;
        RefreshDueLettersForCurrentTime();
    }

    private void OnDisable()
    {
        TimeManager.OnDateTimeChanged -= OnDateTimeChanged;
    }

    // ── Quest Creation ────────────────────────────────────────────────────

    public void CreateQuest(string npcName, string foodName, int amount, int daysTillPayment,
                            string letterSenderTemplate = null, string letterContentTemplate = null,
                            NPCLanguage language = NPCLanguage.English, string objectiveText = null,
                            QuestType questType = QuestType.DeliverItem, string targetTag = null,
                            int requiredCount = 1, MonsterTier minimumTier = MonsterTier.Common,
                            ItemQuality minimumItemQuality = ItemQuality.Common,
                            bool consumeRequiredItemsOnTurnIn = true)
    {
        int currentDay = DailyJournalRules.GetCurrentAccountingDay();
        int paymentTermDays = Mathf.Max(1, daysTillPayment);

        var quest = new ARQuestData
        {
            npcName               = npcName,
            foodName              = foodName,
            amount                = amount,
            dueTotalDay           = currentDay + paymentTermDays,
            createdTotalDay       = currentDay,
            paymentTermDays       = paymentTermDays,
            isLetterSent          = false,
            isARRecorded          = false,
            language              = language,
            objectiveText         = objectiveText,
            letterSenderTemplate  = letterSenderTemplate,
            letterContentTemplate = letterContentTemplate,
            questType             = questType,
            status                = questType == QuestType.DeliverItem ? QuestStatus.PendingTurnIn : QuestStatus.Active,
            targetTag             = targetTag ?? foodName,
            requiredCount         = Mathf.Max(1, requiredCount),
            currentProgress       = 0,
            minimumTier           = minimumTier,
            minimumItemQuality    = minimumItemQuality,
            consumeRequiredItemsOnTurnIn = consumeRequiredItemsOnTurnIn,
        };
        pendingQuests.Add(quest);

        if (quest.questType == QuestType.CollectItem)
            SyncCollectProgressFromInventory(quest);

        OnQuestCreated?.Invoke(quest);
        Debug.Log($"[ARQuestManager] สร้าง {questType} Quest: {npcName} จะจ่าย {amount} บาท");
    }

    private static void SyncCollectProgressFromInventory(ARQuestData quest)
    {
        var inv = GameManager.instance?.player?.GetComponent<Player>()?.GetInventoryController();
        if (inv == null) return;
        var db = Resources.Load<ItemDatabaseSO>("Database/ItemDatabase");
        if (db == null) return;
        var itemSO = db.allItems.Find(i => i.itemId == quest.targetTag || i.GetDisplayName() == quest.targetTag);
        if (itemSO == null) return;
        
        int existing = inv.GetTotalAmount(itemSO);
        quest.currentProgress = Mathf.Min(quest.requiredCount, existing);
        
        if (quest.currentProgress >= quest.requiredCount)
        {
            if (quest.status == QuestStatus.Active) quest.status = QuestStatus.PendingTurnIn;
        }
        else
        {
            if (quest.status == QuestStatus.PendingTurnIn) quest.status = QuestStatus.Active;
        }
        
        Debug.Log($"[ARQuestManager] Synced collect progress from inventory: {quest.npcName} {quest.currentProgress}/{quest.requiredCount}");
    }

    // ── Progress Tracking (KillMonster / CollectItem) ────────────────────

    public void NotifyMonsterKilled(string enemyId, MonsterTier tier = MonsterTier.Common)
    {
        string normalizedEnemyId = NormalizeQuestId(enemyId);
        foreach (var q in pendingQuests)
        {
            if (q.questType != QuestType.KillMonster) continue;
            if (q.status != QuestStatus.Active) continue;
            if (!IsQuestTargetMatch(q, normalizedEnemyId)) continue;
            if (tier < q.minimumTier) continue; // ระดับต่ำกว่าที่กำหนด — ไม่นับ

            q.currentProgress = Mathf.Min(q.requiredCount, q.currentProgress + 1);
            Debug.Log($"[ARQuestManager] Kill progress: {q.npcName} {q.currentProgress}/{q.requiredCount} (tier {tier} >= {q.minimumTier})");

            if (q.currentProgress >= q.requiredCount)
                OnKillQuestComplete(q);
            else
                OnQuestProgressChanged?.Invoke(q);
        }
    }

    public void NotifyItemCollected(string itemId, int amount = 1)
    {
        if (string.IsNullOrEmpty(itemId)) return;

        foreach (var q in pendingQuests)
        {
            if (q.questType != QuestType.CollectItem) continue;
            if (q.status != QuestStatus.Active && q.status != QuestStatus.PendingTurnIn) continue;
            if (q.targetTag != itemId) continue;

            SyncCollectProgressFromInventory(q);
            OnQuestProgressChanged?.Invoke(q);
        }
    }

    public void NotifyItemRemoved(string itemId, int amount = 1)
    {
        if (string.IsNullOrEmpty(itemId)) return;

        foreach (var q in pendingQuests)
        {
            if (q.questType != QuestType.CollectItem) continue;
            if (q.status != QuestStatus.Active && q.status != QuestStatus.PendingTurnIn) continue;
            if (q.targetTag != itemId) continue;

            SyncCollectProgressFromInventory(q);
            OnQuestProgressChanged?.Invoke(q);
        }
    }

    // KillMonster ครบ → รอกลับมารายงานที่ NPC
    private void OnKillQuestComplete(ARQuestData quest)
    {
        quest.status = QuestStatus.PendingTurnIn;
        OnQuestProgressChanged?.Invoke(quest);
    }

    public bool CanHandIn(ARQuestData quest)
    {
        return quest != null && CanHandInNow(quest);
    }

    // Player กลับมา hand-in ที่ NPC
    public bool TryHandIn(ARQuestData quest)
    {
        if (!CanHandIn(quest)) return false;

        int currentDay = DailyJournalRules.GetCurrentAccountingDay();
        if (!quest.isARRecorded)
        {
            quest.createdTotalDay = currentDay;
            quest.paymentTermDays = GetPaymentTermDays(quest);
            quest.dueTotalDay = currentDay + quest.paymentTermDays;
            RecordAREntry(quest);
            quest.isARRecorded = true;
        }

        if (currentDay >= quest.dueTotalDay)
        {
            SendLetterToMailbox(quest);
        }
        else
        {
            // ส่งมอบงานแล้ว รอถึงกำหนดค่อยส่งจดหมายจ่ายเงิน
            quest.status = QuestStatus.Active;
        }

        OnQuestHandedIn?.Invoke(quest);
        OnQuestProgressChanged?.Invoke(quest);
        return true;
    }

    private static bool CanHandInNow(ARQuestData quest)
    {
        if (quest.isARRecorded)
        {
            return false;
        }

        return quest.questType switch
        {
            QuestType.DeliverItem    => quest.status == QuestStatus.PendingTurnIn,
            QuestType.KillMonster    => quest.status == QuestStatus.PendingTurnIn || quest.currentProgress >= quest.requiredCount,
            QuestType.CookAndDeliver => quest.status == QuestStatus.Active || quest.status == QuestStatus.PendingTurnIn,
            QuestType.CollectItem    => quest.status == QuestStatus.PendingTurnIn || quest.currentProgress >= quest.requiredCount,
            _                        => false,
        };
    }

    // ── Date Tick ────────────────────────────────────────────────────────

    private void OnDateTimeChanged(TimeManager.DateTime dt)
    {
        RefreshDueLetters(dt);
    }

    public void RefreshDueLettersForCurrentTime()
    {
        if (!TimeManager.TryGetCurrentDateTime(out TimeManager.DateTime currentDateTime))
        {
            return;
        }

        RefreshDueLetters(currentDateTime);
    }

    private void RefreshDueLetters(TimeManager.DateTime dt)
    {
        int accountingDay = DailyJournalRules.GetAccountingDay(dt);
        if (accountingDay == lastCheckedDay) return;
        lastCheckedDay = accountingDay;

        UpdateFeaturedQuestForDay(accountingDay);

        foreach (var quest in new List<ARQuestData>(pendingQuests))
        {
            // งานที่ส่งมอบแล้วและยังไม่ส่งจดหมาย: ตรวจ due date ตอน 6 โมงเช้าของวันบัญชี
            if (!quest.isLetterSent
                && quest.isARRecorded
                && quest.status == QuestStatus.Active
                && accountingDay >= quest.dueTotalDay)
            {
                SendLetterToMailbox(quest);
            }
        }
    }

    // ── Weekly Featured Quest ─────────────────────────────────────────────

    private void UpdateFeaturedQuestForDay(int accountingDay)
    {
        if (!enableWeeklyBonus || weeklyRotation == null || weeklyRotation.Length == 0) return;

        int week = (accountingDay - 1) / 7;
        if (week == lastFeaturedWeek) return;
        lastFeaturedWeek = week;

        QuestType newFeatured = weeklyRotation[week % weeklyRotation.Length];
        if (newFeatured == featuredQuestType) return;

        featuredQuestType = newFeatured;
        OnFeaturedQuestChanged?.Invoke(featuredQuestType);
        Debug.Log($"[ARQuestManager] สัปดาห์ {week}: Featured = {featuredQuestType} (+{weeklyBonusMultiplier:F1}x bonus)");
    }

    /// <summary>Quest type ที่ได้ bonus สัปดาห์นี้</summary>
    public QuestType GetFeaturedQuestType() => featuredQuestType;

    /// <summary>ตัวคูณ bonus สำหรับ featured quest (เช่น 1.5 = +50%)</summary>
    public float GetWeeklyBonusMultiplier() => enableWeeklyBonus ? weeklyBonusMultiplier : 1f;

    /// <summary>คืน true ถ้า type นี้เป็น featured quest type ของสัปดาห์ปัจจุบัน</summary>
    public bool IsQuestTypeFeatured(QuestType type) => enableWeeklyBonus && type == featuredQuestType;

    // ── Letter ───────────────────────────────────────────────────────────

    private void SendLetterToMailbox(ARQuestData quest)
    {
        MailboxInteractable targetMailbox = mailbox != null ? mailbox : MailboxInteractable.Instance;
        if (targetMailbox == null)
        {
            // ยังไม่มี mailbox ในซีนปัจจุบัน (เช่น ตอนเพิ่งตื่นในบ้าน)
            // เก็บ quest ไว้ก่อนและบังคับให้ retry ได้อีกครั้งในวันเดิม
            lastCheckedDay = -1;
            Debug.LogWarning("[ARQuestManager] ไม่พบ MailboxInteractable");
            return;
        }

        quest.isLetterSent = true;
        quest.status = QuestStatus.LetterSent;
        targetMailbox.AddLetter(quest);
    }

    // ── Transaction Recording ─────────────────────────────────────────────

    private void RecordAREntry(ARQuestData quest)
    {
        if (TransactionManager.Instance == null)
        {
            Debug.LogWarning("[ARQuestManager] TransactionManager ยังไม่ initialize — ข้าม AR entry");
            return;
        }

        ItemCategory revenueCategory = GetRevenueCategoryForQuest(quest.questType);
        string lineItemName = GetQuestLineItemName(quest);

        TransactionManager.Instance.AddRecord(new TransactionRecord(
            lineItemName,
            revenueCategory,
            1,
            quest.amount,
            TransactionType.CreditSale,
            StoreType.Restaurant,
            quest.npcName
        ));
    }

    private static ItemCategory GetRevenueCategoryForQuest(QuestType questType)
    {
        return questType switch
        {
            QuestType.KillMonster    => ItemCategory.ServiceRevenue,
            QuestType.CookAndDeliver => ItemCategory.FoodSales,
            QuestType.DeliverItem    => ItemCategory.SalesRevenue,
            QuestType.CollectItem    => ItemCategory.SalesRevenue,
            _                        => ItemCategory.SalesRevenue,
        };
    }

    public static string GetQuestLineItemName(ARQuestData quest)
    {
        if (quest == null)
        {
            return string.Empty;
        }

        if (quest.questType == QuestType.KillMonster)
            return $"Extermination Service: {quest.foodName}";

        return quest.foodName;
    }

    private static int GetPaymentTermDays(ARQuestData quest)
    {
        if (quest == null)
        {
            return 1;
        }

        if (quest.paymentTermDays > 0)
        {
            return quest.paymentTermDays;
        }

        return Mathf.Max(1, quest.dueTotalDay - quest.createdTotalDay);
    }

    private static bool IsQuestTargetMatch(ARQuestData quest, string normalizedEnemyId)
    {
        if (quest == null)
        {
            return false;
        }

        string targetTag = NormalizeQuestId(quest.targetTag);
        string foodName = NormalizeQuestId(quest.foodName);
        return targetTag == normalizedEnemyId
            || foodName == normalizedEnemyId
            || (!string.IsNullOrEmpty(targetTag) && normalizedEnemyId.Contains(targetTag))
            || (!string.IsNullOrEmpty(normalizedEnemyId) && targetTag.Contains(normalizedEnemyId));
    }

    private static string NormalizeQuestId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.Trim()
            .Replace(" ", string.Empty)
            .Replace("_", string.Empty)
            .Replace("-", string.Empty)
            .ToLowerInvariant();
    }

    private bool ReceivePaymentNow(ARQuestData quest)
    {
        var inv = GameManager.instance?.player?.GetComponent<Player>()?.GetInventoryController();
        if (inv == null)
        {
            Debug.LogError("[ARQuestManager] ไม่พบ InventoryController สำหรับรับเงินเควส");
            return false;
        }

        inv.AddMoney(quest.amount);

        if (TransactionManager.Instance == null)
        {
            Debug.LogWarning("[ARQuestManager] TransactionManager ยังไม่ initialize — ข้ามบันทึกรับชำระ");
            return true;
        }

        TransactionManager.Instance.AddRecord(new TransactionRecord(
            GetQuestLineItemName(quest),
            ItemCategory.AccountsReceivable,
            1,
            quest.amount,
            TransactionType.ReceivePayment,
            StoreType.Restaurant,
            quest.npcName
        ));
        return true;
    }

    // ── Quest Completion ───────────────────────────────────────────────────

    public List<ARQuestData> GetPendingQuests() => pendingQuests;

    public void CompleteQuest(ARQuestData quest)
    {
        pendingQuests.Remove(quest);
        OnQuestCompleted?.Invoke(quest.npcName);
    }

    // ── NPC Cooldown ──────────────────────────────────────────────────────

    public void SetNPCCooldown(string npcName, int nextAvailableDay)
        => npcCooldowns[npcName] = nextAvailableDay;

    /// <summary>คืนค่า -1 = ไม่มี cooldown เคยตั้งไว้</summary>
    public int GetNPCCooldown(string npcName)
        => npcCooldowns.TryGetValue(npcName, out int day) ? day : -1;

    // ── Save / Load ───────────────────────────────────────────────────────

    public List<QuestDTO> CaptureState()
    {
        var dtos = new List<QuestDTO>();
        foreach (var q in pendingQuests)
        {
            dtos.Add(new QuestDTO
            {
                npcName               = q.npcName,
                foodName              = q.foodName,
                amount                = q.amount,
                dueTotalDay           = q.dueTotalDay,
                createdTotalDay       = q.createdTotalDay,
                paymentTermDays       = GetPaymentTermDays(q),
                isLetterSent          = q.isLetterSent,
                isARRecorded          = q.isARRecorded,
                language              = (int)q.language,
                objectiveText         = q.objectiveText,
                letterSenderTemplate  = q.letterSenderTemplate,
                letterContentTemplate = q.letterContentTemplate,
                questType             = (int)q.questType,
                questStatus           = (int)q.status,
                targetTag             = q.targetTag,
                requiredCount         = q.requiredCount,
                currentProgress       = q.currentProgress,
                minimumTier           = (int)q.minimumTier,
                minimumItemQuality    = (int)q.minimumItemQuality,
                consumeRequiredItemsOnTurnIn = q.consumeRequiredItemsOnTurnIn ? 1 : 0,
            });
        }
        return dtos;
    }

    public List<NPCCooldownDTO> CaptureCooldowns()
    {
        var dtos = new List<NPCCooldownDTO>();
        foreach (var kv in npcCooldowns)
            dtos.Add(new NPCCooldownDTO { npcName = kv.Key, nextAvailableDay = kv.Value });
        return dtos;
    }

    public void RestoreState(List<QuestDTO> dtos)
    {
        pendingQuests.Clear();
        lastCheckedDay = -1; // force OnDateTimeChanged to re-evaluate on next tick after restore
        if (dtos == null) return;
        foreach (var dto in dtos)
        {
            ARQuestData quest = new ARQuestData
            {
                npcName               = dto.npcName,
                foodName              = dto.foodName,
                amount                = dto.amount,
                dueTotalDay           = dto.dueTotalDay,
                createdTotalDay       = dto.createdTotalDay,
                paymentTermDays       = dto.paymentTermDays > 0 ? dto.paymentTermDays : Mathf.Max(1, dto.dueTotalDay - dto.createdTotalDay),
                isLetterSent          = dto.isLetterSent,
                isARRecorded          = dto.isARRecorded,
                language              = (NPCLanguage)dto.language,
                objectiveText         = dto.objectiveText,
                letterSenderTemplate  = dto.letterSenderTemplate,
                letterContentTemplate = dto.letterContentTemplate,
                questType             = (QuestType)dto.questType,
                status                = (QuestStatus)dto.questStatus,
                targetTag             = dto.targetTag,
                requiredCount         = dto.requiredCount,
                currentProgress       = dto.currentProgress,
                minimumTier           = (MonsterTier)dto.minimumTier,
                minimumItemQuality    = dto.minimumItemQuality <= 0 ? ItemQuality.Common : (ItemQuality)dto.minimumItemQuality,
                consumeRequiredItemsOnTurnIn = dto.consumeRequiredItemsOnTurnIn != 0,
            };

            if (!quest.isARRecorded
                && quest.status == QuestStatus.Active
                && (quest.questType == QuestType.KillMonster || quest.questType == QuestType.CollectItem)
                && quest.currentProgress >= quest.requiredCount)
            {
                quest.status = QuestStatus.PendingTurnIn;
            }

            pendingQuests.Add(quest);
        }
        Debug.Log($"[ARQuestManager] Restored {pendingQuests.Count} pending quests.");
        RefreshDueLettersForCurrentTime();
    }

    public void RestoreCooldowns(List<NPCCooldownDTO> dtos)
    {
        npcCooldowns.Clear();
        if (dtos == null) return;
        foreach (var dto in dtos)
            npcCooldowns[dto.npcName] = dto.nextAvailableDay;
    }
}
