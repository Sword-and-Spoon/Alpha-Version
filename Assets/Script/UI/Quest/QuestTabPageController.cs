using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public struct NPCPortraitEntry
{
    public string npcName;
    public Sprite portrait;
}

public class QuestTabPageController : MonoBehaviour
{
    private enum QuestEntryType { AR, AP }

    private struct QuestEntryData
    {
        public QuestEntryType type;
        public ARQuestData arQuest;
        public APQuestData apDebt;

        public static QuestEntryData FromAR(ARQuestData quest) =>
            new QuestEntryData { type = QuestEntryType.AR, arQuest = quest, apDebt = null };

        public static QuestEntryData FromAP(APQuestData debt) =>
            new QuestEntryData { type = QuestEntryType.AP, arQuest = null, apDebt = debt };
    }

    [Header("Left Page — Quest List")]
    [SerializeField] private Transform leftListRoot;
    [SerializeField] private QuestTabEntryView questEntryPrefab;
    [SerializeField] private TMP_Text leftEmptyText;
    [SerializeField] private TMP_Text questCountText;

    [Header("Right Page — Detail")]
    [SerializeField] private GameObject rightDetailRoot;
    [SerializeField] private Image rightNpcPortrait;
    [SerializeField] private TMP_Text questTitleText;
    [SerializeField] private TMP_Text questStatusText;
    [SerializeField] private TMP_Text questDescriptionText;
    [SerializeField] private TMP_Text questObjectiveText;
    [SerializeField] private TMP_Text questProgressText;
    [SerializeField] private TMP_Text questRewardText;
    [SerializeField] private TMP_Text rewardLabelText;
    [SerializeField] private TMP_Text rightEmptyText;

    [Header("NPC Portraits")]
    [SerializeField] private NPCPortraitEntry[] npcPortraits;
    [SerializeField] private Sprite defaultPortrait;

    [Header("AR Description Templates — English (Deliver / Cook)")]
    [Tooltip("Placeholder: {npcName}, {food}, {dueDay}")]
    [SerializeField, TextArea(2, 3)]
    private string arDescInProgress_EN = "{npcName} placed an order.\nDeliver {food} by Day {dueDay}.";

    [Tooltip("Placeholder: {npcName}, {food}")]
    [SerializeField, TextArea(2, 3)]
    private string arDescDeliverPendingTurnIn_EN = "{npcName} is waiting for {food}.\nReturn to {npcName} to hand it over.";

    [Tooltip("Placeholder: {npcName}, {food}")]
    [SerializeField, TextArea(2, 3)]
    private string arDescCookInProgress_EN = "{npcName} wants {food} cooked fresh.\nBring it by Day {dueDay}.";

    [Tooltip("Placeholder: {npcName}, {food}")]
    [SerializeField, TextArea(2, 3)]
    private string arDescCookPendingTurnIn_EN = "Your {food} is ready!\nReturn to {npcName} to hand it over.";

    [Tooltip("Placeholder: {npcName}, {amount}")]
    [SerializeField, TextArea(2, 3)]
    private string arDescLetterSent_EN = "{npcName} has paid. Pick up {amount} G from your mailbox.";

    [Tooltip("Placeholder: {npcName}, {food}")]
    [SerializeField, TextArea(2, 3)]
    private string arDescOverdue_EN = "{npcName} is still waiting for {food}. Payment is overdue!";

    [Header("AR Description Templates — English (Kill / Collect)")]
    [Tooltip("Placeholder: {npcName}, {target}, {count}, {progress}, {tierRequirement}")]
    [SerializeField, TextArea(2, 3)]
    private string arDescKillInProgress_EN = "{npcName} needs you to defeat {count} {target}.\nProgress: {progress} / {count}\nRequired tier: {tierRequirement}";

    [Tooltip("Placeholder: {npcName}, {target}, {count}, {tierRequirement}")]
    [SerializeField, TextArea(2, 3)]
    private string arDescKillPendingTurnIn_EN = "You've defeated all {count} {target}!\nReturn to {npcName} to claim your reward.\nRequired tier: {tierRequirement}";

    [Tooltip("Placeholder: {npcName}, {target}, {count}")]
    [SerializeField, TextArea(2, 3)]
    private string arDescCollectInProgress_EN = "{npcName} needs {count} {target}.\nProgress: {progress} / {count}";

    [Tooltip("Placeholder: {npcName}, {target}, {count}")]
    [SerializeField, TextArea(2, 3)]
    private string arDescCollectPendingTurnIn_EN = "You've collected enough {target}!\nBring them to {npcName} to complete the quest.";

    [Header("AR Description Templates — Thai (Deliver / Cook)")]
    [Tooltip("Placeholder: {npcName}, {food}, {dueDay}")]
    [SerializeField, TextArea(2, 3)]
    private string arDescInProgress_TH = "{npcName} ได้สั่งอาหารไว้\nส่ง {food} ภายในวันที่ {dueDay}";

    [Tooltip("Placeholder: {npcName}, {food}")]
    [SerializeField, TextArea(2, 3)]
    private string arDescDeliverPendingTurnIn_TH = "{npcName} กำลังรอ {food}\nกลับไปหา {npcName} เพื่อส่งมอบ";

    [Tooltip("Placeholder: {npcName}, {food}, {dueDay}")]
    [SerializeField, TextArea(2, 3)]
    private string arDescCookInProgress_TH = "{npcName} ต้องการ {food} ที่ปรุงสด\nส่งภายในวันที่ {dueDay}";

    [Tooltip("Placeholder: {npcName}, {food}")]
    [SerializeField, TextArea(2, 3)]
    private string arDescCookPendingTurnIn_TH = "{food} ของคุณพร้อมแล้ว!\nกลับไปหา {npcName} เพื่อส่งมอบ";

    [Tooltip("Placeholder: {npcName}, {amount}")]
    [SerializeField, TextArea(2, 3)]
    private string arDescLetterSent_TH = "{npcName} ชำระเงินแล้ว รับ {amount} G ได้ที่ตู้จดหมาย";

    [Tooltip("Placeholder: {npcName}, {food}")]
    [SerializeField, TextArea(2, 3)]
    private string arDescOverdue_TH = "{npcName} ยังรอ {food} อยู่ เลยกำหนดชำระแล้ว!";

    [Header("AR Description Templates — Thai (Kill / Collect)")]
    [Tooltip("Placeholder: {npcName}, {target}, {count}, {progress}, {tierRequirement}")]
    [SerializeField, TextArea(2, 3)]
    private string arDescKillInProgress_TH = "{npcName} ต้องการให้คุณกำจัด {target} จำนวน {count} ตัว\nความคืบหน้า: {progress} / {count}\nระดับที่นับ: {tierRequirement}";

    [Tooltip("Placeholder: {npcName}, {target}, {count}, {tierRequirement}")]
    [SerializeField, TextArea(2, 3)]
    private string arDescKillPendingTurnIn_TH = "คุณกำจัด {target} ครบ {count} ตัวแล้ว!\nกลับไปหา {npcName} เพื่อรับรางวัล\nระดับที่นับ: {tierRequirement}";

    [Tooltip("Placeholder: {npcName}, {target}, {count}, {progress}")]
    [SerializeField, TextArea(2, 3)]
    private string arDescCollectInProgress_TH = "{npcName} ต้องการ {target} จำนวน {count} ชิ้น\nความคืบหน้า: {progress} / {count}";

    [Tooltip("Placeholder: {npcName}, {target}, {count}")]
    [SerializeField, TextArea(2, 3)]
    private string arDescCollectPendingTurnIn_TH = "คุณเก็บ {target} ได้ครบแล้ว!\nนำไปให้ {npcName} เพื่อทำเควสให้สำเร็จ";

    [Header("AP Objective Templates — English")]
    [Tooltip("Placeholder: {item}, {vendorName}")]
    [SerializeField, TextArea(1, 2)]
    private string apObjective_EN = "Repay {item} debt to {vendorName}.";

    [Header("AP Objective Templates — Thai")]
    [Tooltip("Placeholder: {item}, {vendorName}")]
    [SerializeField, TextArea(1, 2)]
    private string apObjective_TH = "ชำระหนี้ {item} คืนให้กับ {vendorName}";

    [Header("AP Language")]
    [SerializeField] private NPCLanguage apLanguage = NPCLanguage.English;

    [Header("AP Description Templates — English")]
    [Tooltip("Placeholder: {vendorName}, {item}, {dueDay}")]
    [SerializeField, TextArea(2, 3)]
    private string apDescInProgress_EN = "{vendorName} let you buy {item} on credit.\nRepay by Day {dueDay} to avoid more interest.";

    [Tooltip("Placeholder: {vendorName}, {item}")]
    [SerializeField, TextArea(2, 3)]
    private string apDescDueToday_EN = "{vendorName} let you buy {item} on credit.\nRepay today to avoid extra interest tomorrow.";

    [Tooltip("Placeholder: {vendorName}, {interest}")]
    [SerializeField, TextArea(2, 3)]
    private string apDescOverdue_EN = "Debt to {vendorName} is overdue.\nInterest accrued so far: {interest} G.";

    [Header("AP Description Templates — Thai")]
    [Tooltip("Placeholder: {vendorName}, {item}, {dueDay}")]
    [SerializeField, TextArea(2, 3)]
    private string apDescInProgress_TH = "{vendorName} ให้คุณซื้อ {item} แบบเครดิต\nชำระคืนภายในวันที่ {dueDay} เพื่อหลีกเลี่ยงดอกเบี้ย";

    [Tooltip("Placeholder: {vendorName}, {item}")]
    [SerializeField, TextArea(2, 3)]
    private string apDescDueToday_TH = "{vendorName} ให้คุณซื้อ {item} แบบเครดิต\nชำระคืนวันนี้เพื่อหลีกเลี่ยงดอกเบี้ยวันพรุ่งนี้";

    [Tooltip("Placeholder: {vendorName}, {interest}")]
    [SerializeField, TextArea(2, 3)]
    private string apDescOverdue_TH = "หนี้ที่ค้างชำระกับ {vendorName} เกินกำหนดแล้ว\nดอกเบี้ยสะสมจนถึงตอนนี้: {interest} G";

    [Header("AP Reward Labels")]
    [SerializeField] private string apRewardLabel_EN = "Amount Due:";
    [SerializeField] private string apRewardLabel_TH = "ยอดที่ต้องชำระ:";

    [Header("Entry Visuals")]
    [SerializeField] private Color selectedEntryColor = new Color(0.96f, 0.85f, 0.70f, 0.95f);
    [SerializeField] private Color normalEntryColor = new Color(1f, 1f, 1f, 0.35f);

    [Header("Status Colors")]
    [SerializeField] private Color inProgressColor = new Color(0.86f, 0.57f, 0.26f, 1f);
    [SerializeField] private Color dueTodayColor = new Color(0.95f, 0.45f, 0.18f, 1f);
    [SerializeField] private Color overdueColor = new Color(0.78f, 0.18f, 0.12f, 1f);
    [SerializeField] private Color letterSentColor = new Color(0.22f, 0.55f, 0.25f, 1f);
    [SerializeField] private Color pendingTurnInColor = new Color(0.25f, 0.55f, 0.85f, 1f);

    private readonly List<QuestTabEntryView> spawnedEntries = new();
    private readonly List<QuestEntryData> questEntries = new();
    private readonly Queue<QuestTabEntryView> entryPool = new();

    private int selectedIndex = -1;
    private bool isDirty = false;
    private int lastRenderedDay = -1;

    // ── Lifecycle ─────────────────────────────────────────────────────────

    private void OnEnable()
    {
        ARQuestManager.OnQuestCompleted += HandleARQuestCompleted;
        ARQuestManager.OnQuestProgressChanged += HandleARQuestProgressChanged;
        APQuestManager.OnAPQuestRepaid += HandleAPQuestRepaid;
        TimeManager.OnDateTimeChanged += HandleDateTimeChanged;
        isDirty = false;
        TryFindRewardLabel();
        Refresh();
    }

    private void OnDisable()
    {
        ARQuestManager.OnQuestCompleted -= HandleARQuestCompleted;
        ARQuestManager.OnQuestProgressChanged -= HandleARQuestProgressChanged;
        APQuestManager.OnAPQuestRepaid -= HandleAPQuestRepaid;
        TimeManager.OnDateTimeChanged -= HandleDateTimeChanged;
    }

    private void LateUpdate()
    {
        if (!isDirty) return;
        isDirty = false;
        TryFindRewardLabel();
        Refresh();
    }

    // ── Event Handlers ────────────────────────────────────────────────────

    private void HandleARQuestCompleted(string _) => isDirty = true;
    private void HandleARQuestProgressChanged(ARQuestData _) => isDirty = true;
    private void HandleAPQuestRepaid(APQuestData _) => isDirty = true;

    private void HandleDateTimeChanged(TimeManager.DateTime dt)
    {
        int accountingDay = DailyJournalRules.GetAccountingDay(dt);
        if (accountingDay != lastRenderedDay)
        {
            lastRenderedDay = accountingDay;
            isDirty = true;
        }
    }

    // ── Refresh ───────────────────────────────────────────────────────────

    private void TryFindRewardLabel()
    {
        if (rewardLabelText == null && questRewardText != null)
        {
            // ลองหาจาก sibling ก่อน
            Transform parent = questRewardText.transform.parent;
            if (parent != null)
            {
                foreach (Transform child in parent)
                {
                    if (child == questRewardText.transform) continue;
                    TMP_Text text = child.GetComponent<TMP_Text>();
                    if (text != null)
                    {
                        string n = child.name.ToLower();
                        string t = text.text.ToLower();
                        if (n.Contains("label") || n.Contains("header") || n.Contains("title") ||
                            t.Contains("reward") || t.Contains("รางวัล") || t.Contains("pay"))
                        {
                            rewardLabelText = text;
                            break;
                        }
                    }
                }
            }

            // ถ้ายังไม่เจอ ลองหาจาก parent ของ parent (กรณีโครงสร้างซับซ้อนขึ้น)
            if (rewardLabelText == null && parent != null && parent.parent != null)
            {
                TMP_Text[] allTexts = parent.parent.GetComponentsInChildren<TMP_Text>(true);
                foreach (var t in allTexts)
                {
                    if (t == questRewardText) continue;
                    string n = t.gameObject.name.ToLower();
                    if (n.Contains("reward") && (n.Contains("label") || n.Contains("header") || n.Contains("title")))
                    {
                        rewardLabelText = t;
                        break;
                    }
                }
            }
        }
    }

    public void Refresh()
    {
        ReturnAllToPool();

        if (leftListRoot == null || questEntryPrefab == null)
        {
            Debug.LogWarning("[QuestTabPageController] Missing leftListRoot or questEntryPrefab.");
            ToggleEmptyState(true);
            return;
        }

        questEntries.Clear();

        List<ARQuestData> arQuests = ARQuestManager.Instance?.GetPendingQuests();
        if (arQuests != null)
            foreach (var q in arQuests)
                questEntries.Add(QuestEntryData.FromAR(q));

        List<APQuestData> apDebts = APQuestManager.Instance?.GetActiveDebts();
        if (apDebts != null)
            foreach (var d in apDebts)
                questEntries.Add(QuestEntryData.FromAP(d));

        if (questEntries.Count == 0)
        {
            selectedIndex = -1;
            ToggleEmptyState(true);
            SetLeftHeader(0);
            SetQuestCount(0);
            return;
        }

        ToggleEmptyState(false);
        SetLeftHeader(questEntries.Count);
        SetQuestCount(questEntries.Count);

        int currentDay = DailyJournalRules.GetCurrentAccountingDay();

        for (int i = 0; i < questEntries.Count; i++)
        {
            int capturedIndex = i;
            QuestEntryData questEntry = questEntries[i];

            QuestTabEntryView entry = entryPool.Count > 0
                ? entryPool.Dequeue()
                : Instantiate(questEntryPrefab, leftListRoot);

            entry.gameObject.SetActive(true);
            entry.transform.SetParent(leftListRoot);
            entry.transform.SetSiblingIndex(i);

            entry.Bind(
                title: BuildEntryTitle(questEntry),
                description: BuildEntryDescription(questEntry),
                dueText: BuildEntryDueText(questEntry, currentDay),
                dueColor: BuildEntryStatusColor(questEntry, currentDay),
                portrait: GetPortrait(GetActorName(questEntry)),
                onPressed: () => SelectQuest(capturedIndex)
            );
            spawnedEntries.Add(entry);
        }

        if (selectedIndex >= 0 && selectedIndex < questEntries.Count)
            SelectQuest(selectedIndex);
        else
            SelectQuest(0);
    }

    // ── Selection ─────────────────────────────────────────────────────────

    private void SelectQuest(int index)
    {
        if (questEntries.Count == 0) return;
        if (index < 0 || index >= questEntries.Count) return;

        selectedIndex = index;
        UpdateEntrySelection();
        UpdateRightDetail(questEntries[index]);
    }

    private void UpdateEntrySelection()
    {
        for (int i = 0; i < spawnedEntries.Count; i++)
            spawnedEntries[i].SetSelected(i == selectedIndex, selectedEntryColor, normalEntryColor);
    }

    // ── Right Panel ───────────────────────────────────────────────────────

    private void UpdateRightDetail(QuestEntryData questEntry)
    {
        if ((questEntry.type == QuestEntryType.AR && questEntry.arQuest == null) ||
            (questEntry.type == QuestEntryType.AP && questEntry.apDebt == null))
        {
            if (rightDetailRoot != null) rightDetailRoot.SetActive(false);
            if (rightEmptyText != null) rightEmptyText.gameObject.SetActive(true);
            return;
        }

        if (rightDetailRoot != null) rightDetailRoot.SetActive(true);
        if (rightEmptyText != null) rightEmptyText.gameObject.SetActive(false);

        int currentDay = DailyJournalRules.GetCurrentAccountingDay();

        Sprite portrait = GetPortrait(GetActorName(questEntry));
        if (rightNpcPortrait != null)
        {
            rightNpcPortrait.sprite = portrait;
            rightNpcPortrait.gameObject.SetActive(true);
        }

        if (questEntry.type == QuestEntryType.AR)
        {
            ARQuestData quest = questEntry.arQuest;
            int remaining = quest.dueTotalDay - currentDay;

            if (questTitleText != null) questTitleText.text = quest.npcName;

            if (questStatusText != null)
            {
                questStatusText.text = BuildARStatusText(quest, remaining);
                questStatusText.color = BuildARStatusColor(quest, remaining);
            }

            if (questDescriptionText != null)
                questDescriptionText.text = BuildARDescription(quest, remaining);

            if (questObjectiveText != null)
                questObjectiveText.text = string.IsNullOrEmpty(quest.objectiveText)
                    ? BuildFallbackObjective(quest)
                    : quest.objectiveText;

            if (questProgressText != null)
                questProgressText.text = BuildARProgressText(quest);

            if (questRewardText != null)
                questRewardText.text = $"{quest.amount:N0} G";

            if (rewardLabelText != null)
            {
                bool isThai = quest.language == NPCLanguage.Thai;
                rewardLabelText.text = isThai ? "รางวัล:" : "Reward:";
            }
            return;
        }

        // ── AP ────────────────────────────────────────────────────────────
        APQuestData debt = questEntry.apDebt;
        int apRemaining = debt.dueTotalDay - currentDay;
        int totalOwed = debt.principalAmount + debt.accruedInterest;

        APQuestManager.Instance?.NotifyAPQuestViewed(debt);

        if (questTitleText != null) questTitleText.text = debt.vendorName;

        if (questStatusText != null)
        {
            questStatusText.text = BuildAPStatusText(apRemaining);
            questStatusText.color = BuildAPStatusColor(apRemaining);
        }

        if (questDescriptionText != null)
            questDescriptionText.text = BuildAPDescription(debt, apRemaining);

        if (questObjectiveText != null)
        {
            string objTemplate = apLanguage == NPCLanguage.Thai ? apObjective_TH : apObjective_EN;
            questObjectiveText.text = objTemplate
                .Replace("{item}", debt.itemName)
                .Replace("{vendorName}", debt.vendorName);
        }

        if (questProgressText != null) questProgressText.text = string.Empty;

        if (questRewardText != null) questRewardText.text = $"{totalOwed:N0} G";

        if (rewardLabelText != null)
        {
            bool isThai = apLanguage == NPCLanguage.Thai;
            rewardLabelText.text = isThai ? apRewardLabel_TH : apRewardLabel_EN;
        }
    }
    // ── AR Status / Description helpers ───────────────────────────────────

    private string BuildARStatusText(ARQuestData quest, int remaining)
    {
        if (quest.status == QuestStatus.LetterSent) return "CHECK MAILBOX";
        if (IsARReadyToTurnIn(quest)) return "RETURN TO NPC";
        if (quest.isARRecorded) return "WAITING FOR PAYMENT";
        if (remaining > 1) return $"DUE IN {remaining} DAYS";
        if (remaining == 1) return "DUE IN 1 DAY";
        if (remaining == 0) return "DUE TODAY!";
        return $"OVERDUE BY {Mathf.Abs(remaining)} DAY(S)";
    }

    private Color BuildARStatusColor(ARQuestData quest, int remaining)
    {
        if (quest.status == QuestStatus.LetterSent) return letterSentColor;
        if (IsARReadyToTurnIn(quest)) return pendingTurnInColor;
        if (quest.isARRecorded) return inProgressColor;
        if (remaining > 0) return inProgressColor;
        if (remaining == 0) return dueTodayColor;
        return overdueColor;
    }

    private string BuildARDescription(ARQuestData quest, int remaining)
    {
        bool isThai = quest.language == NPCLanguage.Thai;

        if (quest.status == QuestStatus.LetterSent)
        {
            string tpl = isThai ? arDescLetterSent_TH : arDescLetterSent_EN;
            return ApplyARPlaceholders(tpl, quest, remaining);
        }

        if (quest.isARRecorded)
        {
            return isThai
                ? $"{quest.npcName} รับทราบงานแล้ว\nรอชำระเงินภายในวันที่ {quest.dueTotalDay}"
                : $"{quest.npcName} accepted the completed job.\nPayment is due on Day {quest.dueTotalDay}.";
        }

        switch (quest.questType)
        {
            case QuestType.CookAndDeliver:
                if (IsARReadyToTurnIn(quest))
                {
                    string tpl = isThai ? arDescCookPendingTurnIn_TH : arDescCookPendingTurnIn_EN;
                    return ApplyARPlaceholders(tpl, quest, remaining);
                }
                else
                {
                    string tpl = remaining <= 0
                        ? (isThai ? arDescOverdue_TH : arDescOverdue_EN)
                        : (isThai ? arDescCookInProgress_TH : arDescCookInProgress_EN);
                    return ApplyARPlaceholders(tpl, quest, remaining);
                }

            case QuestType.KillMonster:
                if (IsARReadyToTurnIn(quest))
                {
                    string tpl = isThai ? arDescKillPendingTurnIn_TH : arDescKillPendingTurnIn_EN;
                    return AppendKillTierRequirementIfMissing(ApplyARPlaceholders(tpl, quest, remaining), tpl, quest);
                }
                else
                {
                    string tpl = isThai ? arDescKillInProgress_TH : arDescKillInProgress_EN;
                    return AppendKillTierRequirementIfMissing(ApplyARPlaceholders(tpl, quest, remaining), tpl, quest);
                }

            case QuestType.CollectItem:
                if (IsARReadyToTurnIn(quest))
                {
                    string tpl = isThai ? arDescCollectPendingTurnIn_TH : arDescCollectPendingTurnIn_EN;
                    return ApplyARPlaceholders(tpl, quest, remaining);
                }
                else
                {
                    string tpl = isThai ? arDescCollectInProgress_TH : arDescCollectInProgress_EN;
                    return ApplyARPlaceholders(tpl, quest, remaining);
                }

            default: // DeliverItem
                if (IsARReadyToTurnIn(quest))
                {
                    string tpl = isThai ? arDescDeliverPendingTurnIn_TH : arDescDeliverPendingTurnIn_EN;
                    return ApplyARPlaceholders(tpl, quest, remaining);
                }

                string deliverTpl = remaining <= 0
                    ? (isThai ? arDescOverdue_TH : arDescOverdue_EN)
                    : (isThai ? arDescInProgress_TH : arDescInProgress_EN);
                return ApplyARPlaceholders(deliverTpl, quest, remaining);
        }
    }

    private string ApplyARPlaceholders(string template, ARQuestData quest, int remaining)
    {
        return template
            .Replace("{npcName}", quest.npcName)
            .Replace("{food}", quest.foodName)
            .Replace("{target}", quest.foodName)
            .Replace("{amount}", quest.amount.ToString("N0"))
            .Replace("{dueDay}", quest.dueTotalDay.ToString())
            .Replace("{count}", quest.requiredCount.ToString())
            .Replace("{progress}", quest.currentProgress.ToString())
            .Replace("{tierRequirement}", BuildTierRequirementText(quest.minimumTier, quest.language));
    }

    private static bool IsARReadyToTurnIn(ARQuestData quest)
    {
        if (quest == null || quest.isARRecorded)
        {
            return false;
        }

        if (quest.status == QuestStatus.PendingTurnIn)
        {
            return true;
        }

        return (quest.questType == QuestType.KillMonster || quest.questType == QuestType.CollectItem)
            && quest.currentProgress >= quest.requiredCount;
    }

    private string BuildARProgressText(ARQuestData quest)
    {
        if (quest.status == QuestStatus.LetterSent) return string.Empty;

        return quest.questType switch
        {
            QuestType.KillMonster => $"{quest.currentProgress} / {quest.requiredCount}",
            QuestType.CollectItem => $"{quest.currentProgress} / {quest.requiredCount}",
            _ => string.Empty,
        };
    }

    private string BuildFallbackObjective(ARQuestData quest)
    {
        return quest.questType switch
        {
            QuestType.DeliverItem => $"Deliver {quest.foodName} to {quest.npcName}.",
            QuestType.CookAndDeliver => $"Cook and deliver {quest.foodName} to {quest.npcName}.",
            QuestType.KillMonster => $"Defeat {quest.requiredCount} {quest.foodName} for {quest.npcName}. {BuildTierRequirementLine(quest.minimumTier, quest.language)}",
            QuestType.CollectItem => $"Collect {quest.requiredCount} {quest.foodName} for {quest.npcName}.",
            _ => $"Complete the quest for {quest.npcName}.",
        };
    }

    // ── Left List Entry builders ──────────────────────────────────────────

    private string GetActorName(QuestEntryData entry) =>
        entry.type == QuestEntryType.AR ? entry.arQuest?.npcName : entry.apDebt?.vendorName;

    private string BuildEntryTitle(QuestEntryData entry) =>
        entry.type == QuestEntryType.AR ? entry.arQuest.npcName : entry.apDebt.vendorName;

    private string BuildEntryDescription(QuestEntryData entry)
    {
        if (entry.type == QuestEntryType.AP)
            return $"Repay {entry.apDebt.itemName} x{entry.apDebt.quantity}.";

        ARQuestData q = entry.arQuest;
        if (q.isARRecorded)
            return $"Awaiting payment for {q.foodName}.";

        return q.questType switch
        {
            QuestType.DeliverItem => $"Deliver {q.foodName}.",
            QuestType.CookAndDeliver => q.requiredCount > 1
                ? $"Cook & deliver ({q.currentProgress}/{q.requiredCount}) {q.foodName}."
                : $"Cook & deliver {q.foodName}.",
            QuestType.KillMonster => $"Defeat {q.currentProgress}/{q.requiredCount} {q.foodName}. ({BuildTierRequirementText(q.minimumTier, q.language)})",
            QuestType.CollectItem => $"Collect {q.currentProgress}/{q.requiredCount} {q.foodName}.",
            _ => q.foodName,
        };
    }

    private string BuildTierRequirementText(MonsterTier minimumTier, NPCLanguage language)
    {
        bool isThai = language == NPCLanguage.Thai;
        if (minimumTier == MonsterTier.Common) return isThai ? "ทุกระดับ" : "any tier";
        if (minimumTier == MonsterTier.Mythic) return isThai ? "Mythic เท่านั้น" : "Mythic only";
        return isThai ? $"{minimumTier} ขึ้นไป" : $"{minimumTier}+";
    }

    private string BuildTierRequirementLine(MonsterTier minimumTier, NPCLanguage language)
    {
        return language == NPCLanguage.Thai
            ? $"ระดับที่นับ: {BuildTierRequirementText(minimumTier, language)}"
            : $"Required tier: {BuildTierRequirementText(minimumTier, language)}";
    }

    private string AppendKillTierRequirementIfMissing(string description, string template, ARQuestData quest)
    {
        if (template.Contains("{tierRequirement}")) return description;
        return $"{description}\n{BuildTierRequirementLine(quest.minimumTier, quest.language)}";
    }

    private string BuildEntryDueText(QuestEntryData entry, int currentDay)
    {
        if (entry.type == QuestEntryType.AR)
        {
            ARQuestData quest = entry.arQuest;
            if (quest.status == QuestStatus.LetterSent) return "COLLECT AT MAILBOX";
            if (IsARReadyToTurnIn(quest)) return "RETURN TO NPC";
            if (quest.isARRecorded) return "WAITING FOR PAYMENT";
            int remaining = quest.dueTotalDay - currentDay;
            if (remaining > 1) return $"DUE: {remaining} DAYS";
            if (remaining == 1) return "DUE: 1 DAY";
            if (remaining == 0) return "DUE: TODAY";
            return $"OVERDUE: {Mathf.Abs(remaining)} DAY(S)";
        }

        int apRemaining = entry.apDebt.dueTotalDay - currentDay;
        if (apRemaining > 1) return $"DUE: {apRemaining} DAYS";
        if (apRemaining == 1) return "DUE: 1 DAY";
        if (apRemaining == 0) return "DUE: TODAY";
        return $"OVERDUE: {Mathf.Abs(apRemaining)} DAY(S)";
    }

    private Color BuildEntryStatusColor(QuestEntryData entry, int currentDay)
    {
        if (entry.type == QuestEntryType.AR)
            return BuildARStatusColor(entry.arQuest, entry.arQuest.dueTotalDay - currentDay);

        return BuildAPStatusColor(entry.apDebt.dueTotalDay - currentDay);
    }

    // ── AP helpers ────────────────────────────────────────────────────────

    private string BuildAPStatusText(int remaining)
    {
        if (remaining > 1) return $"DUE IN {remaining} DAYS";
        if (remaining == 1) return "DUE IN 1 DAY";
        if (remaining == 0) return "DUE TODAY!";
        return $"OVERDUE BY {Mathf.Abs(remaining)} DAY(S)";
    }

    private Color BuildAPStatusColor(int remaining)
    {
        if (remaining > 0) return inProgressColor;
        if (remaining == 0) return dueTodayColor;
        return overdueColor;
    }

    private string BuildAPDescription(APQuestData debt, int remaining)
    {
        bool isThai = apLanguage == NPCLanguage.Thai;
        string template;

        if (remaining > 0) template = isThai ? apDescInProgress_TH : apDescInProgress_EN;
        else if (remaining == 0) template = isThai ? apDescDueToday_TH : apDescDueToday_EN;
        else template = isThai ? apDescOverdue_TH : apDescOverdue_EN;

        return template
            .Replace("{vendorName}", debt.vendorName)
            .Replace("{item}", debt.itemName)
            .Replace("{dueDay}", debt.dueTotalDay.ToString())
            .Replace("{interest}", debt.accruedInterest.ToString("N0"));
    }

    // ── Utilities ─────────────────────────────────────────────────────────

    private Sprite GetPortrait(string npcName)
    {
        if (npcPortraits != null)
            foreach (var entry in npcPortraits)
                if (entry.npcName == npcName) return entry.portrait;
        return defaultPortrait;
    }

    private void SetQuestCount(int count)
    {
        if (questCountText == null) return;
        questCountText.text = count > 0 ? $"  {count}  QUEST(S)  " : string.Empty;
    }

    private void SetLeftHeader(int count)
    {
        if (leftEmptyText == null) return;
        leftEmptyText.gameObject.SetActive(true);
        leftEmptyText.text = count switch { 0 => "No active quests.", 1 => "Quest Log", _ => "Quest Log" };
    }

    private void ToggleEmptyState(bool empty)
    {
        if (rightEmptyText != null) rightEmptyText.gameObject.SetActive(empty);
        if (rightDetailRoot != null) rightDetailRoot.SetActive(!empty);
    }

    private void ReturnAllToPool()
    {
        foreach (var entry in spawnedEntries)
        {
            if (entry == null) continue;
            entry.gameObject.SetActive(false);
            entryPool.Enqueue(entry);
        }
        spawnedEntries.Clear();
    }
}
