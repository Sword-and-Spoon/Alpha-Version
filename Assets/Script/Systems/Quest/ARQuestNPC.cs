using UnityEngine;

/// <summary>
/// NPC ที่ให้เควส AR — รองรับ 2 โหมด:
///   Fixed Mode  : questType / item / amount ตายตัวตาม Inspector (เหมือนเดิม)
///   Pool Mode   : ลาก QuestTemplate[] เข้า questPool → สุ่ม quest ใหม่ทุกครั้งที่ cooldown หมด
/// วาง Component นี้บน GameObject ที่มี InteractParent เป็น child
/// </summary>
public class ARQuestNPC : InteractableObject
{
    // ─────────────────────────────────────────────────────────────────────────
    // Inspector Fields
    // ─────────────────────────────────────────────────────────────────────────

    [Header("NPC Info")]
    [SerializeField] private string npcName = "Uncle Mike";
    [SerializeField] private Sprite portrait;

    public string NpcName => npcName;

    /// <summary>ล้าง cooldown ของ NPC นี้ทันที (ใช้โดย TutorialManager)</summary>
    public void ClearCooldown()
    {
        onCooldown = false;
        nextAvailableDay = 0;
        ARQuestManager.Instance?.SetNPCCooldown(npcName, 0);
        if (UsingPool && !questAccepted) RollNewQuest();
        RefreshIndicators();
    }

    [Header("Quest Pool (Pool Mode — ลาก QuestTemplate มาใส่เพื่อเปิด Pool Mode)")]
    [Tooltip("ถ้ามี template อย่างน้อย 1 รายการ จะสุ่ม quest จาก pool นี้แทน Fixed Mode\nสร้าง template ได้จาก Assets > Create > Quest > Quest Template")]
    [SerializeField] private QuestTemplate[] questPool;

    [Header("Quest Type (Fixed Mode — ใช้เมื่อ Quest Pool ว่าง)")]
    [SerializeField] private QuestType questType = QuestType.DeliverItem;

    [Header("Quest Settings — Deliver / Cook (Fixed Mode)")]
    [Tooltip("ลากไฟล์ ItemSO มาใส่ (ใช้กับ DeliverItem และ CookAndDeliver)")]
    [SerializeField] private ItemSO foodItem;

    [Header("Quest Settings — Kill Monster (Fixed Mode)")]
    [Tooltip("ลาก EnemySO ของมอนสเตอร์เป้าหมายมาใส่")]
    [SerializeField] private EnemySO targetEnemy;

    [Tooltip("ระดับขั้นต่ำที่จะนับ — Common = นับทุกระดับ")]
    [SerializeField] private MonsterTier minimumTier = MonsterTier.Common;

    [Header("Quest Settings — Collect Item (Fixed Mode)")]
    [Tooltip("ItemSO ที่ต้องเก็บ")]
    [SerializeField] private ItemSO collectItem;
    [SerializeField] private bool consumeCollectedItemsOnTurnIn = true;

    [Header("Quest Settings — General (Fixed Mode)")]
    [SerializeField] private int amount = 350;
    [SerializeField, Range(1, 7)] private int daysTillPayment = 1;
    [Tooltip("จำนวนที่ต้องทำ (KillMonster / CollectItem / CookAndDeliver)")]
    [SerializeField] private int requiredCount = 1;

    [Header("Quest Cooldown")]
    [Tooltip("วันบัญชีที่ NPC เริ่มให้เควสได้ครั้งแรก (1 = ตั้งแต่เริ่มเกม, 3 = เริ่มวันที่ 3)")]
    [SerializeField] private int questUnlockDay = 1;
    [Tooltip("ช่วงวันที่สุ่ม cooldown ก่อนรับเควสใหม่ได้\n(0,0) = รับได้ครั้งเดียวตลอดไป\n(2,5) = รอสุ่ม 2-5 วัน")]
    [SerializeField] private Vector2Int cooldownDaysRange = new Vector2Int(3, 6);

    [Header("Quest Indicators")]
    [SerializeField] private GameObject questAvailableIndicator;
    [SerializeField] private GameObject questActiveIndicator;
    [Tooltip("แสดงเมื่อ quest ของ NPC นี้ตรงกับ Featured Quest Type ของสัปดาห์ (ได้ bonus reward)")]
    [SerializeField] private GameObject questBonusIndicator;

    [Header("Language")]
    [SerializeField] private NPCLanguage language = NPCLanguage.English;

    [Header("English Dialogue (Fixed Mode)")]
    [SerializeField] private NPCDialogueSet englishDialogue = new NPCDialogueSet();

    [Header("Thai Dialogue (Fixed Mode)")]
    [SerializeField] private NPCDialogueSet thaiDialogue = new NPCDialogueSet();

    // ─────────────────────────────────────────────────────────────────────────
    // Pool Mode — Rolled State (ไม่ serialize — re-roll ทุก session)
    // ─────────────────────────────────────────────────────────────────────────

    private QuestType      _rolledQuestType;
    private ItemSO         _rolledFoodItem;
    private ItemQuality    _rolledFoodItemQuality;
    private EnemySO        _rolledTargetEnemy;
    private ItemSO         _rolledCollectItem;
    private ItemQuality    _rolledCollectItemQuality;
    private int            _rolledAmount;
    private int            _rolledDaysTillPayment;
    private int            _rolledRequiredCount;
    private MonsterTier    _rolledMinimumTier;
    private NPCDialogueSet _rolledEnglishDialogue;
    private NPCDialogueSet _rolledThaiDialogue;

    // ─────────────────────────────────────────────────────────────────────────
    // Active Value Accessors
    // ─────────────────────────────────────────────────────────────────────────

    private bool UsingPool => questPool != null && questPool.Length > 0;

    private QuestType      ActiveQuestType         => UsingPool ? _rolledQuestType         : questType;
    private ItemSO         ActiveFoodItem          => UsingPool ? _rolledFoodItem          : foodItem;
    private ItemQuality    ActiveFoodItemQuality   => UsingPool ? _rolledFoodItemQuality   : ItemQuality.Common;
    private EnemySO        ActiveTargetEnemy       => UsingPool ? _rolledTargetEnemy       : targetEnemy;
    private ItemSO         ActiveCollectItem       => UsingPool ? _rolledCollectItem       : collectItem;
    private ItemQuality    ActiveCollectItemQuality=> UsingPool ? _rolledCollectItemQuality: ItemQuality.Common;
    private int            ActiveAmount         => UsingPool ? _rolledAmount         : amount;
    private int            ActiveDaysTillPayment=> UsingPool ? _rolledDaysTillPayment: daysTillPayment;
    private int            ActiveRequiredCount  => UsingPool ? _rolledRequiredCount  : requiredCount;
    private MonsterTier    ActiveMinimumTier    => UsingPool ? _rolledMinimumTier    : minimumTier;
    private NPCDialogueSet ActiveEnglishDialogue=> UsingPool ? _rolledEnglishDialogue: englishDialogue;
    private NPCDialogueSet ActiveThaiDialogue   => UsingPool ? _rolledThaiDialogue   : thaiDialogue;
    private NPCDialogueSet ActiveDialogue       => language == NPCLanguage.Thai ? ActiveThaiDialogue : ActiveEnglishDialogue;

    // ─────────────────────────────────────────────────────────────────────────
    // Runtime State
    // ─────────────────────────────────────────────────────────────────────────

    private bool questAccepted;
    private bool onCooldown;
    private int  nextAvailableDay;
    private ARQuestData activeQuest;
    private NPCMovement npcMovement;

    // ─────────────────────────────────────────────────────────────────────────
    // Reset / Validate (Fixed Mode only)
    // ─────────────────────────────────────────────────────────────────────────

    private void Reset()
    {
        if (UsingPool) return;
        englishDialogue = NPCDialogueSet.DefaultEnglish(questType);
        thaiDialogue    = NPCDialogueSet.DefaultThai(questType);
    }

    private void OnValidate()
    {
        if (UsingPool) return;
        if (string.IsNullOrEmpty(englishDialogue?.offerMessage))
            englishDialogue = NPCDialogueSet.DefaultEnglish(questType);
        else if (string.IsNullOrEmpty(englishDialogue.objectiveText))
            englishDialogue.objectiveText = NPCDialogueSet.DefaultEnglish(questType).objectiveText;

        if (string.IsNullOrEmpty(thaiDialogue?.offerMessage))
            thaiDialogue = NPCDialogueSet.DefaultThai(questType);
        else if (string.IsNullOrEmpty(thaiDialogue.objectiveText))
            thaiDialogue.objectiveText = NPCDialogueSet.DefaultThai(questType).objectiveText;
    }

    /// <summary>เรียกจาก Editor เมื่อ QuestType เปลี่ยน — reset dialogue ให้ตรงกับประเภทใหม่ (Fixed Mode)</summary>
    public void ApplyQuestTypeDialogue()
    {
        if (UsingPool) return;
        englishDialogue = NPCDialogueSet.DefaultEnglish(questType);
        thaiDialogue    = NPCDialogueSet.DefaultThai(questType);
    }

    [ContextMenu("Reset Dialogue to Quest Type Defaults")]
    private void ResetDialogueToDefaults()
    {
        if (UsingPool) return;
        englishDialogue = NPCDialogueSet.DefaultEnglish(questType);
        thaiDialogue    = NPCDialogueSet.DefaultThai(questType);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Pool Logic
    // ─────────────────────────────────────────────────────────────────────────

    private void RollNewQuest()
    {
        if (!UsingPool || questPool.Length == 0) return;

        int currentDay = DailyJournalRules.GetCurrentAccountingDay();

        // กรองเฉพาะ template ที่ถึงวัน unlock แล้ว
        float totalWeight = 0f;
        foreach (var t in questPool)
            if (t != null && currentDay >= t.minUnlockDay) totalWeight += Mathf.Max(0f, t.weight);

        // ถ้าไม่มี template ที่ unlock แล้ว ใช้ template แรกที่ไม่ null เป็น fallback
        QuestTemplate selected = null;
        foreach (var t in questPool) { if (t != null) { selected = t; break; } }

        if (totalWeight > 0f)
        {
            float roll = Random.Range(0f, totalWeight);
            float cumulative = 0f;
            foreach (var t in questPool)
            {
                if (t == null || currentDay < t.minUnlockDay) continue;
                cumulative += Mathf.Max(0f, t.weight);
                if (roll <= cumulative) { selected = t; break; }
            }
        }

        if (selected == null) return;

        _rolledQuestType = selected.questType;
        _rolledMinimumTier = selected.minimumTier;

        // Pick random item/enemy
        (_rolledFoodItem,    _rolledFoodItemQuality)    = PickRandomEntry(selected.foodItems);
        _rolledTargetEnemy = PickRandom(selected.targetEnemies);
        (_rolledCollectItem, _rolledCollectItemQuality) = PickRandomEntry(selected.collectItems);

        // Roll amounts
        _rolledAmount          = Random.Range(selected.amountRange.x, selected.amountRange.y + 1);
        _rolledDaysTillPayment = Random.Range(selected.paymentTermRange.x, selected.paymentTermRange.y + 1);
        _rolledRequiredCount   = Mathf.Max(1, Random.Range(selected.requiredCountRange.x, selected.requiredCountRange.y + 1));

        // Dialogue: use override if filled in, otherwise auto-generate from quest type
        _rolledEnglishDialogue = !string.IsNullOrEmpty(selected.englishDialogueOverride?.offerMessage)
            ? selected.englishDialogueOverride
            : NPCDialogueSet.DefaultEnglish(selected.questType);
        _rolledThaiDialogue = !string.IsNullOrEmpty(selected.thaiDialogueOverride?.offerMessage)
            ? selected.thaiDialogueOverride
            : NPCDialogueSet.DefaultThai(selected.questType);

        Debug.Log($"[ARQuestNPC] {npcName} rolled: {_rolledQuestType} x{_rolledRequiredCount} for {_rolledAmount}G");
    }

    /// <summary>หลังโหลด save — หา ItemSO/EnemySO กลับมาจาก pool โดยใช้ข้อมูลใน ARQuestData</summary>
    private void RestoreRolledRefsFromPool()
    {
        if (!UsingPool || activeQuest == null) return;

        _rolledQuestType     = activeQuest.questType;
        _rolledRequiredCount = activeQuest.requiredCount;
        _rolledMinimumTier   = activeQuest.minimumTier;

        foreach (var template in questPool)
        {
            if (template == null || template.questType != activeQuest.questType) continue;

            switch (activeQuest.questType)
            {
                case QuestType.DeliverItem:
                case QuestType.CookAndDeliver:
                    _rolledFoodItem         = FindItemSO(template.foodItems, activeQuest.targetTag, activeQuest.foodName);
                    _rolledFoodItemQuality  = FindItemQuality(template.foodItems, activeQuest.targetTag, activeQuest.foodName);
                    break;
                case QuestType.CollectItem:
                    _rolledCollectItem         = FindItemSO(template.collectItems, activeQuest.targetTag, activeQuest.foodName);
                    _rolledCollectItemQuality  = FindItemQuality(template.collectItems, activeQuest.targetTag, activeQuest.foodName);
                    break;
                case QuestType.KillMonster:
                    _rolledTargetEnemy = FindEnemySO(template.targetEnemies, activeQuest.targetTag);
                    break;
            }

            if (_rolledFoodItem != null || _rolledCollectItem != null || _rolledTargetEnemy != null) break;
        }

        _rolledEnglishDialogue = NPCDialogueSet.DefaultEnglish(activeQuest.questType);
        _rolledThaiDialogue    = NPCDialogueSet.DefaultThai(activeQuest.questType);
    }

    private static T PickRandom<T>(T[] arr) where T : Object
    {
        if (arr == null || arr.Length == 0) return null;
        return arr[Random.Range(0, arr.Length)];
    }

    private static (ItemSO item, ItemQuality quality) PickRandomEntry(QuestItemEntry[] arr)
    {
        if (arr == null || arr.Length == 0) return (null, ItemQuality.Common);
        var entry = arr[Random.Range(0, arr.Length)];
        return (entry.item, entry.minimumQuality);
    }

    private static ItemSO FindItemSO(QuestItemEntry[] arr, string itemId, string displayName)
    {
        if (arr == null) return null;
        foreach (var entry in arr)
            if (entry.item != null && (entry.item.itemId == itemId || entry.item.GetDisplayName() == displayName))
                return entry.item;
        return null;
    }

    private static ItemQuality FindItemQuality(QuestItemEntry[] arr, string itemId, string displayName)
    {
        if (arr == null) return ItemQuality.Common;
        foreach (var entry in arr)
            if (entry.item != null && (entry.item.itemId == itemId || entry.item.GetDisplayName() == displayName))
                return entry.minimumQuality;
        return ItemQuality.Common;
    }

    private static EnemySO FindEnemySO(EnemySO[] arr, string enemyId)
    {
        if (arr == null) return null;
        foreach (var enemy in arr)
            if (enemy != null && enemy.enemyId == enemyId)
                return enemy;
        return null;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    private void Start()
    {
        if (ARQuestManager.Instance != null)
        {
            foreach (var q in ARQuestManager.Instance.GetPendingQuests())
            {
                if (q.npcName == npcName && q.status != QuestStatus.LetterSent)
                {
                    questAccepted = true;
                    activeQuest   = q;
                    break;
                }
            }

            int savedCoolday = ARQuestManager.Instance.GetNPCCooldown(npcName);
            if (savedCoolday >= 0)
            {
                int currentDay = DailyJournalRules.GetCurrentAccountingDay();
                if (savedCoolday == int.MaxValue)
                {
                    onCooldown = true;
                    nextAvailableDay = int.MaxValue;
                }
                else if (currentDay < savedCoolday)
                {
                    onCooldown = true;
                    nextAvailableDay = savedCoolday;
                }
            }
        }

        // ถ้าวันปัจจุบันยังไม่ถึง questUnlockDay ให้ lock ไว้ก่อน
        if (!questAccepted && !onCooldown)
        {
            int currentDay = DailyJournalRules.GetCurrentAccountingDay();
            if (currentDay < questUnlockDay)
            {
                onCooldown       = true;
                nextAvailableDay = questUnlockDay;
            }
        }

        // Pool Mode initialisation
        if (UsingPool)
        {
            if (questAccepted && activeQuest != null)
                RestoreRolledRefsFromPool();
            else if (!onCooldown)
                RollNewQuest();
        }

        npcMovement = GetComponent<NPCMovement>();
        RefreshIndicators();
    }

    private void OnEnable()
    {
        ARQuestManager.OnQuestCompleted       += HandleQuestCompleted;
        ARQuestManager.OnFeaturedQuestChanged += HandleFeaturedQuestChanged;
        TimeManager.OnDateTimeChanged         += OnDateTimeChanged;
    }

    private void OnDisable()
    {
        ARQuestManager.OnQuestCompleted       -= HandleQuestCompleted;
        ARQuestManager.OnFeaturedQuestChanged -= HandleFeaturedQuestChanged;
        TimeManager.OnDateTimeChanged         -= OnDateTimeChanged;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Event Handlers
    // ─────────────────────────────────────────────────────────────────────────

    private void HandleQuestCompleted(string completedNpcName)
    {
        if (completedNpcName != npcName) return;

        questAccepted = false;
        activeQuest   = null;

        bool isOneTime = cooldownDaysRange.x <= 0 && cooldownDaysRange.y <= 0;
        if (isOneTime)
        {
            onCooldown = true;
            nextAvailableDay = int.MaxValue;
        }
        else
        {
            onCooldown = true;
            int currentDay = DailyJournalRules.GetCurrentAccountingDay();
            int roll = Random.Range(cooldownDaysRange.x, cooldownDaysRange.y + 1);
            nextAvailableDay = currentDay + roll;
            Debug.Log($"[ARQuestNPC] {npcName} cooldown {roll} วัน (เปิดวันที่ {nextAvailableDay})");
        }

        ARQuestManager.Instance?.SetNPCCooldown(npcName, nextAvailableDay);
        RefreshIndicators();
    }

    private void OnDateTimeChanged(TimeManager.DateTime dt)
    {
        if (!onCooldown) return;
        if (DailyJournalRules.GetAccountingDay(dt) < nextAvailableDay) return;

        onCooldown = false;
        if (UsingPool) RollNewQuest(); // สุ่ม quest ใหม่เมื่อ cooldown หมด
        RefreshIndicators();
        Debug.Log($"[ARQuestNPC] {npcName} พร้อมรับเควสใหม่");
    }

    private void HandleFeaturedQuestChanged(QuestType featured)
    {
        RefreshIndicators();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Interaction
    // ─────────────────────────────────────────────────────────────────────────

    public override bool CanInteract() => true;

    private static readonly string[] SmallTalkThai =
    {
        "วันนี้อากาศดีจังเลยนะ",
        "ยังไม่มีอะไรให้ทำวันนี้ แต่ถ้ามีงานจะบอกนะ",
        "ระวังดูแลตัวเองด้วยล่ะ",
        "ขอบคุณที่แวะมาทักทายนะ",
    };

    private static readonly string[] SmallTalkEnglish =
    {
        "Nice weather today, isn't it?",
        "Nothing for you right now, but I'll let you know if something comes up.",
        "Take care of yourself out there.",
        "Thanks for stopping by!",
    };

    public override void Interact()
    {
        if (!UI_StateManager.Instance.CanOpenInteractWindow()) return;

        var d = ActiveDialogue;

        if (d == null)
        {
            var lines = language == NPCLanguage.Thai ? SmallTalkThai : SmallTalkEnglish;
            string msg = lines[Random.Range(0, lines.Length)];
            OpenPanel();
            NPCDialoguePanel.Instance.ShowDialogue(portrait, npcName, msg, ClosePanel);
            return;
        }

        if (onCooldown)
        {
            OpenPanel();
            NPCDialoguePanel.Instance.ShowDialogue(portrait, npcName, d.onCooldownMessage, ClosePanel, d.okButtonLabel);
            return;
        }

        if (questAccepted && activeQuest != null)
        {
            HandleActiveQuestInteract(d);
            return;
        }

        string targetName  = GetTargetDisplayName();
        int    finalAmount = GetFinalAmount();

        string offer = d.offerMessage
            .Replace("{food}",    targetName)
            .Replace("{target}",  targetName)
            .Replace("{monster}", targetName)
            .Replace("{count}",   ActiveRequiredCount.ToString())
            .Replace("{amount}",  finalAmount.ToString())
            .Replace("{days}",    ActiveDaysTillPayment.ToString())
            .Replace("{tierRequirement}", GetTierRequirementText(ActiveMinimumTier, language));

        if (ActiveQuestType == QuestType.KillMonster && !d.offerMessage.Contains("{tierRequirement}"))
            offer = $"{offer}\n{GetTierRequirementLine(ActiveMinimumTier, language)}";

        OpenPanel();
        NPCDialoguePanel.Instance.ShowChoice(portrait, npcName, offer, OnAccepted, OnDeclined,
            d.acceptButtonLabel, d.declineButtonLabel);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Active Quest Interact
    // ─────────────────────────────────────────────────────────────────────────

    private void HandleActiveQuestInteract(NPCDialogueSet d)
    {
        OpenPanel();

        switch (activeQuest.questType)
        {
            case QuestType.DeliverItem:
                if (activeQuest.isARRecorded)
                {
                    NPCDialoguePanel.Instance.ShowDialogue(portrait, npcName, BuildWaitingPaymentMessage(), ClosePanel, d.okButtonLabel);
                    break;
                }
                if (activeQuest.status == QuestStatus.PendingTurnIn)
                    TryHandInItem(d, ActiveFoodItem);
                else
                    NPCDialoguePanel.Instance.ShowDialogue(portrait, npcName, ApplyActiveQuestPlaceholders(d.alreadyAcceptedMessage), ClosePanel, d.okButtonLabel);
                break;

            case QuestType.CookAndDeliver:
                if (activeQuest.isARRecorded)
                {
                    NPCDialoguePanel.Instance.ShowDialogue(portrait, npcName, BuildWaitingPaymentMessage(), ClosePanel, d.okButtonLabel);
                    break;
                }
                TryHandInItem(d, ActiveFoodItem ?? FindItemInDatabase(activeQuest.targetTag));
                break;

            case QuestType.CollectItem:
                if (activeQuest.isARRecorded)
                {
                    NPCDialoguePanel.Instance.ShowDialogue(portrait, npcName, BuildWaitingPaymentMessage(), ClosePanel, d.okButtonLabel);
                    break;
                }
                TryHandInItem(d, ActiveCollectItem ?? FindItemInDatabase(activeQuest.targetTag));
                break;

            case QuestType.KillMonster:
                if (activeQuest.isARRecorded)
                {
                    NPCDialoguePanel.Instance.ShowDialogue(portrait, npcName, BuildWaitingPaymentMessage(), ClosePanel, d.okButtonLabel);
                    break;
                }
                var qm = ARQuestManager.Instance;
                if (qm != null && qm.CanHandIn(activeQuest))
                {
                    if (qm.TryHandIn(activeQuest))
                        NPCDialoguePanel.Instance.ShowDialogue(portrait, npcName, BuildHandInAcceptedMessage(d), ClosePanel, d.okButtonLabel);
                    else
                        NPCDialoguePanel.Instance.ShowDialogue(portrait, npcName, ApplyActiveQuestPlaceholders(d.alreadyAcceptedMessage), ClosePanel, d.okButtonLabel);
                }
                else
                {
                    NPCDialoguePanel.Instance.ShowDialogue(portrait, npcName, ApplyActiveQuestPlaceholders(d.alreadyAcceptedMessage), ClosePanel, d.okButtonLabel);
                }
                break;
        }
    }

    private void TryHandInItem(NPCDialogueSet d, ItemSO item)
    {
        if (item == null) { ClosePanel(); return; }
        if (activeQuest == null) { ClosePanel(); return; }

        var qm = ARQuestManager.Instance;
        if (qm == null) { Debug.LogError($"[ARQuestNPC] {npcName}: ไม่พบ ARQuestManager"); ClosePanel(); return; }

        var inventory = GameManager.instance?.player?.GetComponent<Player>()?.GetInventoryController();
        if (inventory == null) { Debug.LogError($"[ARQuestNPC] {npcName}: ไม่สามารถเข้าถึง InventoryController"); ClosePanel(); return; }

        if (!qm.CanHandIn(activeQuest))
        {
            ShowNotEnoughMessage(d, item.GetDisplayName());
            return;
        }

        int count = activeQuest.requiredCount;
        ItemQuality minQuality = activeQuest.minimumItemQuality;
        if (inventory.GetTotalAmount(item, minQuality) >= count)
        {
            bool shouldConsume = activeQuest.consumeRequiredItemsOnTurnIn || activeQuest.questType != QuestType.CollectItem;
            bool handInSucceeded = false;

            if (shouldConsume)
            {
                bool removed = inventory.RemoveItem(new Item(item, count, minQuality));
                handInSucceeded = removed && qm.TryHandIn(activeQuest);
            }
            else
            {
                handInSucceeded = qm.TryHandIn(activeQuest);
            }

            if (handInSucceeded)
            {
                NPCDialoguePanel.Instance.ShowDialogue(portrait, npcName, d.acceptedMessage, ClosePanel, d.okButtonLabel);
            }
            else
            {
                ShowNotEnoughMessage(d, item.GetDisplayName());
            }
        }
        else
        {
            ShowNotEnoughMessage(d, item.GetDisplayName());
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Accept / Decline
    // ─────────────────────────────────────────────────────────────────────────

    private void OnAccepted()
    {
        var d = ActiveDialogue;
        switch (ActiveQuestType)
        {
            case QuestType.DeliverItem:
                AcceptDeliverQuest(d);
                break;
            case QuestType.CookAndDeliver:
            case QuestType.KillMonster:
            case QuestType.CollectItem:
                AcceptNonDeliverQuest(d);
                break;
        }
    }

    private void AcceptDeliverQuest(NPCDialogueSet d)
    {
        var item = ActiveFoodItem;
        if (item == null) { Debug.LogError($"[ARQuestNPC] {npcName}: foodItem ไม่ได้ตั้งค่า"); return; }

        CreateAndAcceptQuest(d, item.GetDisplayName(), item.itemId);
        string acceptedMsg = language == NPCLanguage.Thai
            ? $"รับเควสแล้ว! นำ {item.GetDisplayName()} มาให้ฉันเมื่อพร้อม"
            : $"Quest accepted! Bring me {item.GetDisplayName()} when you're ready.";
        NPCDialoguePanel.Instance.ShowDialogue(portrait, npcName, acceptedMsg, ClosePanel, d.okButtonLabel);
    }

    private void AcceptNonDeliverQuest(NPCDialogueSet d)
    {
        CreateAndAcceptQuest(d, GetTargetDisplayName(), GetTargetId());
        NPCDialoguePanel.Instance.ShowDialogue(portrait, npcName, d.acceptedMessage, ClosePanel, d.okButtonLabel);
    }

    private void CreateAndAcceptQuest(NPCDialogueSet d, string displayName, string targetId)
    {
        string objectiveTemplate = d.objectiveText ?? string.Empty;
        string resolvedObjective = objectiveTemplate
            .Replace("{food}",    displayName)
            .Replace("{target}",  displayName)
            .Replace("{monster}", displayName)
            .Replace("{npcName}", npcName)
            .Replace("{count}",   ActiveRequiredCount.ToString())
            .Replace("{tierRequirement}", GetTierRequirementText(ActiveMinimumTier, language));

        if (ActiveQuestType == QuestType.KillMonster && !objectiveTemplate.Contains("{tierRequirement}"))
            resolvedObjective = $"{resolvedObjective} ({GetTierRequirementLine(ActiveMinimumTier, language)})";

        ItemQuality itemQuality = ActiveQuestType switch
        {
            QuestType.CookAndDeliver => ActiveFoodItemQuality,
            QuestType.DeliverItem    => ActiveFoodItemQuality,
            QuestType.CollectItem    => ActiveCollectItemQuality,
            _                        => ItemQuality.Common,
        };

        ARQuestManager.Instance.CreateQuest(
            npcName, displayName, GetFinalAmount(), ActiveDaysTillPayment,
            d.letterSenderTemplate, d.letterContentTemplate,
            language, resolvedObjective,
            ActiveQuestType, targetId, ActiveRequiredCount,
            ActiveQuestType == QuestType.KillMonster ? ActiveMinimumTier : MonsterTier.Common,
            itemQuality,
            ActiveQuestType == QuestType.CollectItem ? consumeCollectedItemsOnTurnIn : true
        );

        foreach (var q in ARQuestManager.Instance.GetPendingQuests())
        {
            if (q.npcName == npcName) { activeQuest = q; break; }
        }

        questAccepted = true;
        RefreshIndicators();
    }

    private void OnDeclined()
    {
        NPCDialoguePanel.Instance.ShowDialogue(portrait, npcName, ActiveDialogue.declinedMessage, ClosePanel, ActiveDialogue.okButtonLabel);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>คำนวณรางวัลจริงหลังรวม Featured Quest Bonus (ถ้ามี)</summary>
    private int GetFinalAmount()
    {
        int baseAmount = ActiveAmount;
        if (ARQuestManager.Instance != null && ARQuestManager.Instance.IsQuestTypeFeatured(ActiveQuestType))
            return Mathf.RoundToInt(baseAmount * ARQuestManager.Instance.GetWeeklyBonusMultiplier());
        return baseAmount;
    }

    private string GetTargetDisplayName()
    {
        return ActiveQuestType switch
        {
            QuestType.DeliverItem    => ActiveFoodItem    != null ? ActiveFoodItem.GetDisplayName()    : "item",
            QuestType.CookAndDeliver => ActiveFoodItem    != null ? ActiveFoodItem.GetDisplayName()    : "dish",
            QuestType.KillMonster    => ActiveTargetEnemy != null ? ActiveTargetEnemy.displayName      : "monsters",
            QuestType.CollectItem    => ActiveCollectItem != null ? ActiveCollectItem.GetDisplayName() : "items",
            _                        => "item",
        };
    }

    private string GetTargetId()
    {
        return ActiveQuestType switch
        {
            QuestType.KillMonster    => ActiveTargetEnemy != null ? ActiveTargetEnemy.enemyId          : "",
            QuestType.CollectItem    => ActiveCollectItem != null ? ActiveCollectItem.itemId            : "",
            QuestType.CookAndDeliver => ActiveFoodItem    != null ? ActiveFoodItem.itemId              : "",
            _                        => "",
        };
    }

    private string ApplyActiveQuestPlaceholders(string template)
    {
        if (activeQuest == null) return template ?? string.Empty;

        string targetName   = GetTargetDisplayName();
        MonsterTier reqTier = activeQuest.minimumTier;
        return (template ?? string.Empty)
            .Replace("{food}",    targetName)
            .Replace("{target}",  targetName)
            .Replace("{monster}", targetName)
            .Replace("{npcName}", npcName)
            .Replace("{amount}",  activeQuest.amount.ToString("N0"))
            .Replace("{days}",    activeQuest.paymentTermDays.ToString())
            .Replace("{progress}", Mathf.Min(activeQuest.currentProgress, activeQuest.requiredCount).ToString())
            .Replace("{count}",   activeQuest.requiredCount.ToString())
            .Replace("{tierRequirement}", GetTierRequirementText(reqTier, language));
    }

    private string BuildHandInAcceptedMessage(NPCDialogueSet d)
    {
        if (language == NPCLanguage.Thai)
            return $"ขอบคุณ งานเสร็จเรียบร้อยแล้ว! ฉันจะจ่ายเงินภายใน {activeQuest.paymentTermDays} วัน";
        return $"Thank you. The job is complete! I'll send your payment in {activeQuest.paymentTermDays} day(s).";
    }

    private string BuildWaitingPaymentMessage()
    {
        if (activeQuest == null) return string.Empty;
        if (language == NPCLanguage.Thai)
            return $"ขอบคุณอีกครั้ง ฉันจะชำระเงินภายในวันที่ {activeQuest.dueTotalDay}";
        return $"Thanks again. I'll send your payment by Day {activeQuest.dueTotalDay}.";
    }

    private void ShowNotEnoughMessage(NPCDialogueSet d, string itemName)
    {
        MonsterTier reqTier = activeQuest != null ? activeQuest.minimumTier : ActiveMinimumTier;
        int count = activeQuest?.requiredCount ?? ActiveRequiredCount;
        string msg = d.notEnoughMessage
            .Replace("{food}",     itemName)
            .Replace("{target}",   itemName)
            .Replace("{monster}",  itemName)
            .Replace("{progress}", activeQuest?.currentProgress.ToString() ?? "0")
            .Replace("{count}",    count.ToString())
            .Replace("{tierRequirement}", GetTierRequirementText(reqTier, language));

        if (ActiveQuestType == QuestType.KillMonster && !d.notEnoughMessage.Contains("{tierRequirement}"))
            msg = $"{msg}\n{GetTierRequirementLine(reqTier, language)}";

        NPCDialoguePanel.Instance.ShowDialogue(portrait, npcName, msg, ClosePanel, d.okButtonLabel);
    }

    private string GetTierRequirementText(MonsterTier tier, NPCLanguage lang)
    {
        bool isThai = lang == NPCLanguage.Thai;
        if (tier == MonsterTier.Common) return isThai ? "ทุกระดับ" : "any tier";
        if (tier == MonsterTier.Mythic) return isThai ? "Mythic เท่านั้น" : "Mythic only";
        return isThai ? $"{tier} ขึ้นไป" : $"{tier}+";
    }

    private string GetTierRequirementLine(MonsterTier tier, NPCLanguage lang)
    {
        return lang == NPCLanguage.Thai
            ? $"ระดับที่นับ: {GetTierRequirementText(tier, lang)}"
            : $"Required tier: {GetTierRequirementText(tier, lang)}";
    }

    private static ItemSO FindItemInDatabase(string tag)
    {
        if (string.IsNullOrEmpty(tag)) return null;
        var db = Resources.Load<ItemDatabaseSO>("Database/ItemDatabase");
        return db?.allItems?.Find(i => i.itemId == tag || i.GetDisplayName() == tag);
    }

    private void OpenPanel()
    {
        UI_StateManager.Instance.interactWindowOpen = true;
        Time.timeScale = 0f;
    }

    private void ClosePanel()
    {
        UI_StateManager.Instance.interactWindowOpen = false;
        Time.timeScale = 1f;
    }

    private void RefreshIndicators()
    {
        bool available = !questAccepted && !onCooldown;
        bool isBonus   = available
            && ARQuestManager.Instance != null
            && ARQuestManager.Instance.IsQuestTypeFeatured(ActiveQuestType);

        // Bonus indicator แสดงแทน available indicator (ไม่ซ้อนกัน)
        if (questAvailableIndicator != null) questAvailableIndicator.SetActive(available && !isBonus);
        if (questBonusIndicator     != null) questBonusIndicator.SetActive(isBonus);
        if (questActiveIndicator    != null) questActiveIndicator.SetActive(questAccepted);

        // แจ้ง NPCMovement ให้เดินหา player เมื่อมี quest พร้อม
        npcMovement?.SetQuestAvailable(available);
    }
}
