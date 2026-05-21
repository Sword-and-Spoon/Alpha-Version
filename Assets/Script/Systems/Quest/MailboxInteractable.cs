using System;
using System.Collections.Generic;
using UnityEngine;

public class MailboxInteractable : InteractableObject
{
    private sealed class MailEntry
    {
        public string key;
        public string sender;
        public string content;
        public Action onRead;
        public ARQuestData quest;
    }

    public static MailboxInteractable Instance;
    public static event Action<ARQuestData> OnFirstLetterReceived;
    public static event Action<ARQuestData> OnLetterRead;
    public static event Action OnUtilityBillLetterRead;

    [Header("Visual")]
    [SerializeField] private GameObject hasMailIndicator;

    [Header("No Mail Text")]
    [SerializeField] private string noMailTitle = "Mailbox";
    [SerializeField] private string noMailMessage = "No new letters.";

    private readonly Queue<MailEntry> pendingEntries = new();
    private readonly HashSet<string> pendingKeys = new();

    protected override void Awake()
    {
        base.Awake();
        Instance = this;
        ARQuestManager.Instance?.RefreshDueLettersForCurrentTime();
        RebuildPendingMail();
    }

    public override bool CanInteract() => true;

    public override void Interact()
    {
        if (!UI_StateManager.Instance.CanOpenInteractWindow()) return;

        RebuildPendingMail();

        if (pendingEntries.Count == 0)
        {
            OpenPanel();
            NPCDialoguePanel.Instance.ShowDialogue(null, noMailTitle, noMailMessage, ClosePanelFromDialogue);
            return;
        }

        OpenPanel();
        ShowNextLetter();
    }

    public void AddLetter(ARQuestData quest, bool emitFirstLetterEvent = true)
    {
        if (quest == null)
        {
            RefreshIndicator();
            return;
        }

        if (!TryEnqueueEntry(
                new MailEntry
                {
                    key = BuildQuestLetterKey(quest),
                    sender = ApplyQuestTemplate(quest.letterSenderTemplate, quest, "From: {npcName}"),
                    content = ApplyQuestTemplate(
                        quest.letterContentTemplate,
                        quest,
                        "Hello!\n\nThank you so much for the {food} the other day. Here is {amount} gold as promised.\n\nSincerely,\n{npcName}"),
                    onRead = () => ResolveQuestLetter(quest),
                    quest = quest,
                },
                out bool wasEmptyBeforeEnqueue))
        {
            RefreshIndicator();
            return;
        }

        if (emitFirstLetterEvent)
        {
            OnFirstLetterReceived?.Invoke(quest);
        }

        RefreshIndicator();
    }

    public void AddUtilityBillLetter(RestaurantServiceManager.UtilityBillStatement bill)
    {
        if (bill == null || string.IsNullOrWhiteSpace(bill.billId))
        {
            RefreshIndicator();
            return;
        }

        int electricityDue = Mathf.Max(0, bill.electricityOutstandingAmount);
        int waterDue = Mathf.Max(0, bill.waterOutstandingAmount);
        int totalDue = electricityDue + waterDue;
        if (totalDue <= 0 || bill.mailboxLetterRead)
        {
            RefreshIndicator();
            return;
        }

        TryEnqueueEntry(
            new MailEntry
            {
                key = BuildUtilityBillLetterKey(bill.billId),
                sender = "From: Lunaria Utilities Office",
                content =
                    $"Weekly utility bill for Week {bill.weekIndex}\n\n" +
                    $"Electricity: ${electricityDue:N0}\n" +
                    $"Water: ${waterDue:N0}\n" +
                    $"Total due: ${totalDue:N0}\n\n" +
                    "Please pay this bill at the utility payment counter.",
                onRead = () => RestaurantUtilityBillingCache.TryMarkMailboxLetterRead(bill.billId),
            },
            out _);

        RefreshIndicator();
    }

    public void RebuildPendingMail()
    {
        pendingEntries.Clear();
        pendingKeys.Clear();
        RebuildPendingLettersFromQuestManager();
        RebuildPendingLettersFromUtilityBills();
        RefreshIndicator();
    }

    private void ShowNextLetter()
    {
        if (MailboxPanel.Instance == null)
        {
            Debug.LogError("[Mailbox] MailboxPanel not found in scene!");
            ClosePanel();
            return;
        }

        if (pendingEntries.Count == 0)
        {
            ClosePanel();
            return;
        }

        MailEntry entry = pendingEntries.Peek();
        MailboxPanel.Instance.ShowLetter(entry.sender, entry.content, HandleLetterReadInternal);
    }

    private static string ApplyQuestTemplate(string template, ARQuestData quest, string fallback)
    {
        string text = string.IsNullOrEmpty(template) ? fallback : template;
        return text
            .Replace("{npcName}", quest.npcName)
            .Replace("{food}", quest.foodName)
            .Replace("{amount}", quest.amount.ToString("N0"));
    }

    private void HandleLetterReadInternal()
    {
        if (pendingEntries.Count == 0)
        {
            ClosePanel();
            return;
        }

        MailEntry entry = pendingEntries.Dequeue();
        if (!string.IsNullOrWhiteSpace(entry.key))
        {
            pendingKeys.Remove(entry.key);
        }

        bool isUtilityBill = entry.key != null && entry.key.StartsWith("UTIL::");
        entry.onRead?.Invoke();

        if (isUtilityBill)
        {
            OnUtilityBillLetterRead?.Invoke();
        }

        RefreshIndicator();

        if (pendingEntries.Count > 0)
        {
            ShowNextLetter();
        }
        else
        {
            ClosePanel();
        }
    }

    private void RebuildPendingLettersFromQuestManager()
    {
        if (ARQuestManager.Instance == null)
        {
            return;
        }

        foreach (ARQuestData quest in ARQuestManager.Instance.GetPendingQuests())
        {
            if (quest != null && quest.isLetterSent && quest.status == QuestStatus.LetterSent)
            {
                AddLetter(quest, emitFirstLetterEvent: false);
            }
        }
    }

    private void RebuildPendingLettersFromUtilityBills()
    {
        IReadOnlyList<RestaurantServiceManager.UtilityBillStatement> unreadBills
            = RestaurantUtilityBillingCache.GetUnreadMailboxBillsSnapshot();

        if (unreadBills == null)
        {
            return;
        }

        for (int i = 0; i < unreadBills.Count; i++)
        {
            AddUtilityBillLetter(unreadBills[i]);
        }
    }

    private void ResolveQuestLetter(ARQuestData quest)
    {
        if (quest == null)
        {
            return;
        }

        var inv = GameManager.instance.player.GetComponent<Player>().GetInventoryController();
        inv.AddMoney(quest.amount);

        TransactionManager.Instance.AddRecord(new TransactionRecord(
            ARQuestManager.GetQuestLineItemName(quest),
            ItemCategory.AccountsReceivable,
            1,
            quest.amount,
            TransactionType.ReceivePayment,
            StoreType.Restaurant,
            quest.npcName
        ));

        if (ARQuestManager.Instance != null)
        {
            ARQuestManager.Instance.CompleteQuest(quest);
        }

        OnLetterRead?.Invoke(quest);
    }

    private bool TryEnqueueEntry(MailEntry entry, out bool wasEmptyBeforeEnqueue)
    {
        wasEmptyBeforeEnqueue = pendingEntries.Count == 0;

        if (entry == null || string.IsNullOrWhiteSpace(entry.key) || pendingKeys.Contains(entry.key))
        {
            return false;
        }

        pendingEntries.Enqueue(entry);
        pendingKeys.Add(entry.key);
        return true;
    }

    private static string BuildQuestLetterKey(ARQuestData quest)
    {
        if (quest == null)
        {
            return string.Empty;
        }

        return $"AR::{quest.npcName}::{quest.foodName}::{quest.amount}::{quest.dueTotalDay}";
    }

    private static string BuildUtilityBillLetterKey(string billId)
    {
        return string.IsNullOrWhiteSpace(billId) ? string.Empty : $"UTIL::{billId}";
    }

    private void ClosePanel()
    {
        UI_StateManager.Instance.interactWindowOpen = false;
        Time.timeScale = 1f;
    }

    private void ClosePanelFromDialogue()
    {
        NPCDialoguePanel.Instance.Close();
        ClosePanel();
    }

    private void OpenPanel()
    {
        UI_StateManager.Instance.interactWindowOpen = true;
        Time.timeScale = 0f;
    }

    private void RefreshIndicator()
    {
        if (hasMailIndicator != null)
        {
            hasMailIndicator.SetActive(pendingEntries.Count > 0);
        }
    }
}
