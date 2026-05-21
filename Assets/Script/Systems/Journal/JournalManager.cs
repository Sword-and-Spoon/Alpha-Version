using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public struct AuditReport
{
    public bool isPassed;
    public int score;
    public int maxScore;
    public int structureScore;
    public int structureMaxScore;
    public int orderScore;
    public int orderMaxScore;
    public int sideScore;
    public int sideMaxScore;
    public int transactionScore;
    public int transactionMaxScore;
    public int attemptNumber;
    public int retryCount;
    public int mistakeCount;
    public bool earnedStructurePoint;
    public bool earnedTransactionPoint;
    public List<string> mistakeDetails;
    public List<string> lineMistakeDetails;
    public List<AuditLineHint> lineHints;
}

public struct AuditLineHint
{
    public string entryId;
    public int rowNumber;
    public int journalLineNumber;
    public string message;

    public bool HasEntry => !string.IsNullOrEmpty(entryId);
}

public class JournalManager : MonoBehaviour
{
    public static JournalManager Instance;
    public static event Action OnJournalEntryConfirmed;
    public static event Action<bool, AuditReport> OnJournalFinished;

    [Header("Journal UI")]
    public Transform itemListContainer;
    public GameObject itemListPrefab;
    public GameObject horizontalLinePrefab;

    [Header("Journal Scroll")]
    [SerializeField] private ScrollRect ledgerScrollRect;
    [SerializeField] private RectTransform ledgerViewport;

    private readonly List<EntryData> confirmedEntries = new List<EntryData>();
    private readonly List<string> penaltyLogs = new List<string>();

    private AuditReport lastAuditReport;
    private bool hasLastAuditReport;
    private int submitAttemptCount;
    private int lastFinishedFrame = -1;
    private bool journalCompletedForCurrentSession;

    private bool scrollViewportPrepared;
    private Coroutine pendingRebuildCoroutine;
    private bool pendingScrollToBottom;
    private bool journalButtonsBound;

    [Header("Journal Total Footer")]
    [SerializeField] private GameObject journalTotalFooterPrefab;
    private RectTransform journalTotalFooter;
    private JournalTotalFooterUI journalTotalFooterUI;
    private GameObject loadedJournalTotalFooterPrefab;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        BindJournalButtons();
        EnsureScrollableLedger();
    }

    public List<EntryData> GetConfirmedEntries() => confirmedEntries;
    public List<string> GetPenaltyLogs() => penaltyLogs;
    public int GetSubmitAttemptCount() => submitAttemptCount;
    public bool IsJournalLocked => journalCompletedForCurrentSession;

    public bool TryGetLastAuditReport(out AuditReport report)
    {
        report = lastAuditReport;
        return hasLastAuditReport;
    }

    public void ApplyDailyJournalCompletionState(bool completed, AuditReport report, bool hasReport)
    {
        journalCompletedForCurrentSession = completed;

        if (hasReport)
        {
            lastAuditReport = report;
            hasLastAuditReport = true;
        }
        else if (!completed)
        {
            lastAuditReport = default;
            hasLastAuditReport = false;
        }

        BindJournalButtons();
        SetJournalActionButtonsVisible(!completed);
    }

    public void ResetAuditSession()
    {
        submitAttemptCount = 0;
        hasLastAuditReport = false;
        lastAuditReport = default;
        penaltyLogs.Clear();
        journalCompletedForCurrentSession = false;
        HideAuditSummary();
        UpdateJournalTotalFooter();
        SetJournalActionButtonsVisible(true);
        BindJournalButtons();
    }

    public void ResetForNewJournalDay()
    {
        ClearJournal();
        ResetAuditSession();
    }

    public void LoadConfirmedEntries(List<EntryData> entries)
    {
        confirmedEntries.Clear();
        if (entries != null)
        {
            confirmedEntries.AddRange(entries);
        }

        EnsureEntryIds();
        RebuildJournalUI();

        if (TransactionManager.Instance != null)
        {
            TransactionManager.Instance.SyncJournalManagerState();
        }
    }

    public void AddEntry(string droppedSide, ItemCategory category, TransactionType type, float amount, TimeManager.DateTime time)
    {
        if (journalCompletedForCurrentSession)
        {
            return;
        }

        AddEntry(
            droppedSide,
            string.Empty,
            category,
            type,
            StoreType.None,
            0,
            amount,
            time,
            category == GetBalancingCategoryForType(type));
    }

    public void AddEntry(
        string droppedSide,
        string itemName,
        ItemCategory category,
        TransactionType type,
        StoreType store,
        int quantity,
        float amount,
        TimeManager.DateTime time,
        bool isBalancingEntry)
    {
        if (journalCompletedForCurrentSession)
        {
            return;
        }

        EntryData entry = new EntryData(
            droppedSide,
            itemName,
            category,
            type,
            store,
            quantity,
            amount,
            time,
            isBalancingEntry);

        confirmedEntries.Add(entry);
        RefreshAuditPreviewIfDetailedFeedbackUnlocked();
        RebuildJournalUI(true);
    }

    public void HandleDrop(PointerEventData eventData, string side)
    {
        if (journalCompletedForCurrentSession)
        {
            Debug.Log("[JournalManager] Journal already completed for this session; drop ignored.");
            return;
        }

        if (eventData == null || eventData.pointerDrag == null || string.IsNullOrEmpty(side))
        {
            return;
        }

        ResolveItemListContainerIfNeeded();

        JournalEntryRowUI journalRow = eventData.pointerDrag.GetComponent<JournalEntryRowUI>();
        if (journalRow != null)
        {
            ChangeEntrySide(journalRow.EntryId, side);
            return;
        }

        DragGroupUI dragged = eventData.pointerDrag.GetComponent<DragGroupUI>();
        if (dragged == null)
        {
            return;
        }

        EntryData entry = CreateEntryFromDrag(dragged, side);
        confirmedEntries.Add(entry);
        RefreshAuditPreviewIfDetailedFeedbackUnlocked();
        RebuildJournalUI(true);
    }

    public void HandleInsertDrop(PointerEventData eventData, string targetEntryId, bool insertAfter, string side)
    {
        if (journalCompletedForCurrentSession)
        {
            Debug.Log("[JournalManager] Journal already completed for this session; insert drop ignored.");
            return;
        }

        if (eventData == null || eventData.pointerDrag == null || string.IsNullOrEmpty(targetEntryId))
        {
            return;
        }

        ResolveItemListContainerIfNeeded();

        JournalEntryRowUI journalRow = eventData.pointerDrag.GetComponent<JournalEntryRowUI>();
        if (journalRow != null)
        {
            MoveEntryRelativeTo(journalRow.EntryId, targetEntryId, insertAfter, side);
            return;
        }

        DragGroupUI dragged = eventData.pointerDrag.GetComponent<DragGroupUI>();
        if (dragged == null)
        {
            return;
        }

        EntryData entry = CreateEntryFromDrag(dragged, side);
        InsertEntryRelativeTo(entry, targetEntryId, insertAfter);
        RefreshAuditPreviewIfDetailedFeedbackUnlocked();
        ScheduleJournalRebuild();
    }

    public bool TryResolveDropSide(PointerEventData eventData, out string side)
    {
        side = string.Empty;
        if (eventData == null)
        {
            return false;
        }

        Camera eventCamera = eventData.pressEventCamera;
        if (eventCamera == null)
        {
            Canvas canvas = itemListContainer != null ? itemListContainer.GetComponentInParent<Canvas>() : null;
            if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                eventCamera = canvas.worldCamera;
            }
        }

        DropZone[] dropZones = FindObjectsOfType<DropZone>(true);
        foreach (DropZone dropZone in dropZones)
        {
            if (dropZone == null
                || string.IsNullOrEmpty(dropZone.side)
                || !dropZone.isActiveAndEnabled
                || !dropZone.gameObject.activeInHierarchy)
            {
                continue;
            }

            RectTransform rect = dropZone.GetComponent<RectTransform>();
            if (rect == null)
            {
                continue;
            }

            if (RectTransformUtility.RectangleContainsScreenPoint(rect, eventData.position, eventCamera))
            {
                side = dropZone.side;
                return true;
            }
        }

        if (ledgerViewport != null
            && RectTransformUtility.ScreenPointToLocalPointInRectangle(ledgerViewport, eventData.position, eventCamera, out Vector2 localPoint))
        {
            side = localPoint.x < 0f ? "Dr" : "Cr";
            return true;
        }

        return false;
    }

    public bool IsPointerInsideLedger(PointerEventData eventData)
    {
        if (eventData == null)
        {
            return false;
        }

        Camera eventCamera = ResolveEventCamera(eventData);

        if (ledgerViewport != null)
        {
            return RectTransformUtility.RectangleContainsScreenPoint(ledgerViewport, eventData.position, eventCamera);
        }

        RectTransform content = itemListContainer as RectTransform;
        return content != null && RectTransformUtility.RectangleContainsScreenPoint(content, eventData.position, eventCamera);
    }

    public void ChangeEntrySide(string entryId, string side)
    {
        if (journalCompletedForCurrentSession)
        {
            return;
        }

        EntryData entry = FindEntry(entryId);
        if (entry == null)
        {
            return;
        }

        entry.side = side;
        RefreshAuditPreviewIfDetailedFeedbackUnlocked();
        ScheduleJournalRebuild();
    }

    public void RemoveEntry(string entryId)
    {
        if (journalCompletedForCurrentSession)
        {
            return;
        }

        int index = confirmedEntries.FindIndex(e => e.entryId == entryId);
        if (index < 0)
        {
            return;
        }

        bool removedSeparator = confirmedEntries[index].separatorAfter;
        confirmedEntries.RemoveAt(index);

        if (removedSeparator && index > 0 && index - 1 < confirmedEntries.Count)
        {
            confirmedEntries[index - 1].separatorAfter = true;
        }

        RefreshAuditPreviewIfDetailedFeedbackUnlocked();
        ScheduleJournalRebuild();
    }

    public void SwapEntries(string firstEntryId, string secondEntryId)
    {
        if (journalCompletedForCurrentSession)
        {
            return;
        }

        if (firstEntryId == secondEntryId)
        {
            return;
        }

        int firstIndex = confirmedEntries.FindIndex(e => e.entryId == firstEntryId);
        int secondIndex = confirmedEntries.FindIndex(e => e.entryId == secondEntryId);

        if (firstIndex < 0 || secondIndex < 0)
        {
            return;
        }

        bool firstSeparator = confirmedEntries[firstIndex].separatorAfter;
        bool secondSeparator = confirmedEntries[secondIndex].separatorAfter;

        EntryData first = confirmedEntries[firstIndex];
        EntryData second = confirmedEntries[secondIndex];

        second.separatorAfter = firstSeparator;
        first.separatorAfter = secondSeparator;

        confirmedEntries[firstIndex] = second;
        confirmedEntries[secondIndex] = first;

        RefreshAuditPreviewIfDetailedFeedbackUnlocked();
        ScheduleJournalRebuild();
    }

    public string GetEntrySide(string entryId)
    {
        EntryData entry = FindEntry(entryId);
        return entry != null ? entry.side : string.Empty;
    }

    private EntryData CreateEntryFromDrag(DragGroupUI dragged, string side)
    {
        return new EntryData(
            side,
            dragged.itemName,
            dragged.category,
            dragged.transactionType,
            dragged.storeType,
            dragged.quantity,
            dragged.totalPrice,
            dragged.gameTime,
            dragged.isBalancingEntry);
    }

    private void InsertEntryRelativeTo(EntryData entry, string targetEntryId, bool insertAfter)
    {
        if (entry == null)
        {
            return;
        }

        int targetIndex = confirmedEntries.FindIndex(e => e.entryId == targetEntryId);
        if (targetIndex < 0)
        {
            confirmedEntries.Add(entry);
            return;
        }

        entry.separatorAfter = false;
        int insertIndex = targetIndex + (insertAfter ? 1 : 0);

        if (insertAfter && confirmedEntries[targetIndex].separatorAfter)
        {
            confirmedEntries[targetIndex].separatorAfter = false;
            entry.separatorAfter = true;
        }

        confirmedEntries.Insert(insertIndex, entry);
    }

    private void MoveEntryRelativeTo(string movingEntryId, string targetEntryId, bool insertAfter, string side)
    {
        if (string.IsNullOrEmpty(movingEntryId) || string.IsNullOrEmpty(targetEntryId))
        {
            return;
        }

        if (movingEntryId == targetEntryId)
        {
            ChangeEntrySide(movingEntryId, side);
            return;
        }

        int movingIndex = confirmedEntries.FindIndex(e => e.entryId == movingEntryId);
        if (movingIndex < 0)
        {
            return;
        }

        EntryData movingEntry = confirmedEntries[movingIndex];
        bool movingHadSeparator = movingEntry.separatorAfter;
        movingEntry.separatorAfter = false;
        confirmedEntries.RemoveAt(movingIndex);

        if (movingHadSeparator && movingIndex > 0 && movingIndex - 1 < confirmedEntries.Count)
        {
            confirmedEntries[movingIndex - 1].separatorAfter = true;
        }

        int targetIndex = confirmedEntries.FindIndex(e => e.entryId == targetEntryId);
        if (targetIndex < 0)
        {
            movingEntry.side = side;
            confirmedEntries.Add(movingEntry);
            RefreshAuditPreviewIfDetailedFeedbackUnlocked();
            ScheduleJournalRebuild();
            return;
        }

        movingEntry.side = side;
        InsertEntryRelativeTo(movingEntry, targetEntryId, insertAfter);
        RefreshAuditPreviewIfDetailedFeedbackUnlocked();
        ScheduleJournalRebuild();
    }

    public void OnFinished()
    {
        if (lastFinishedFrame == Time.frameCount)
        {
            return;
        }

        lastFinishedFrame = Time.frameCount;

        if (HasOpenLine())
        {
            ConfirmJournalLine();
        }

        submitAttemptCount++;
        AuditReport report = EvaluateJournal(submitAttemptCount);
        lastAuditReport = report;
        hasLastAuditReport = true;
        RebuildJournalUI();
        OnJournalFinished?.Invoke(report.isPassed, report);

        AuditUISummary summary = AuditUISummary.GetInstance();
        if (summary != null)
        {
            summary.DisplayAuditReport(report);
        }

        Debug.Log("=====================================");
        Debug.Log("[AUDIT REPORT] Journal Summary");
        Debug.Log("=====================================");
        Debug.Log($"Attempt {report.attemptNumber}.");
        Debug.Log($"Score {report.score} out of {report.maxScore}.");
        Debug.Log($"Found {report.mistakeCount} issues.");

        if (report.isPassed)
        {
            Debug.Log("Result: PASSED");
            CompleteJournalForCurrentSession();
            TransactionManager.Instance?.MarkJournalCompleted(report);
        }
        else
        {
            Debug.LogError("Result: FAILED");
            for (int i = 0; i < report.mistakeDetails.Count; i++)
            {
                Debug.LogWarning($"  {i + 1}. {report.mistakeDetails[i]}");
            }
        }

        Debug.Log("=====================================");
    }

    public void OnReset()
    {
        if (journalCompletedForCurrentSession)
        {
            return;
        }

        ClearJournal();
    }

    public void ConfirmJournalLine()
    {
        Debug.Log("1");
        if (journalCompletedForCurrentSession)
        {
            return;
        }
        Debug.Log("2");
        BindJournalButtons();

        Debug.Log("3");

        if (confirmedEntries.Count == 0)
        {
            return;
        }

        EntryData lastEntry = confirmedEntries[confirmedEntries.Count - 1];
        if (lastEntry.separatorAfter)
        {
            return;
        }

        lastEntry.separatorAfter = true;
        RebuildJournalUI();

        Debug.Log("4");
        OnJournalEntryConfirmed?.Invoke();
    }

    public bool CheckCompletedAll()
    {
        penaltyLogs.Clear();

        int attemptNumber = Mathf.Max(1, submitAttemptCount);
        AuditReport report = EvaluateJournal(attemptNumber);
        lastAuditReport = report;
        hasLastAuditReport = true;

        foreach (string detail in report.mistakeDetails)
        {
            penaltyLogs.Add($"ERROR: {detail}");
        }

        if (penaltyLogs.Count > 0)
        {
            foreach (string log in penaltyLogs)
            {
                Debug.LogError(log);
            }

            return false;
        }

        CompleteJournalForCurrentSession();
        TransactionManager.Instance?.MarkJournalCompleted(report);
        return true;
    }

    public void ClearJournal()
    {
        ResolveItemListContainerIfNeeded();

        if (itemListContainer != null)
        {
            foreach (Transform child in itemListContainer)
            {
                Destroy(child.gameObject);
            }
        }

        confirmedEntries.Clear();
        // penaltyLogs.Clear();
        // hasLastAuditReport = false;
        UpdateJournalTotalFooter();
        // HideAuditSummary();
        Debug.Log("Journal Cleared");
    }

    private void BindJournalButtons()
    {
        Button[] buttons = FindObjectsOfType<Button>(true);
        bool boundAny = false;

        foreach (Button button in buttons)
        {
            if (button == null)
            {
                continue;
            }

            if (button.name == "Button_Reset")
            {
                button.onClick.RemoveListener(OnReset);
                button.onClick.AddListener(OnReset);
                boundAny = true;
            }
            else if (button.name == "Button_EntryConfirm")
            {
                button.onClick.RemoveListener(ConfirmJournalLine);
                button.onClick.AddListener(ConfirmJournalLine);
                boundAny = true;
            }
            else if (IsDoneOrSubmitButton(button))
            {
                button.onClick.RemoveListener(OnFinished);
                button.onClick.AddListener(OnFinished);
                boundAny = true;
            }
        }

        journalButtonsBound = journalButtonsBound || boundAny;
        if (journalCompletedForCurrentSession)
        {
            SetJournalActionButtonsVisible(false);
        }
    }

    private bool IsDoneOrSubmitButton(Button button)
    {
        if (button == null)
        {
            return false;
        }

        string buttonName = button.name.ToLowerInvariant();
        return buttonName.Contains("done") || buttonName.Contains("submit");
    }

    private bool IsJournalActionButton(Button button)
    {
        if (button == null)
        {
            return false;
        }

        return button.name == "Button_Reset"
            || button.name == "Button_EntryConfirm"
            || IsDoneOrSubmitButton(button);
    }

    private void CompleteJournalForCurrentSession()
    {
        journalCompletedForCurrentSession = true;
        SetJournalActionButtonsVisible(false);
    }

    private void SetJournalActionButtonsVisible(bool visible)
    {
        Button[] buttons = FindObjectsOfType<Button>(true);
        foreach (Button button in buttons)
        {
            if (!IsJournalActionButton(button))
            {
                continue;
            }

            if (button.gameObject.activeSelf != visible)
            {
                button.gameObject.SetActive(visible);
            }
        }
    }

    private AuditReport EvaluateJournal(int attemptNumber)
    {
        List<string> structuralMistakes = new List<string>();
        List<string> transactionMistakes = new List<string>();
        List<string> lineMistakes = new List<string>();
        List<AuditLineHint> lineHints = new List<AuditLineHint>();
        int structureMaxScore = 1;
        int structureScore = 0;
        int orderScore = 0;
        int orderMaxScore = 0;
        int sideScore = 0;
        int sideMaxScore = 0;
        int transactionScore = 0;
        int transactionMaxScore = 0;

        if (confirmedEntries.Count == 0)
        {
            structuralMistakes.Add("The journal is empty.");
            transactionMistakes.Add("No journal entries were submitted for today's transactions.");
            transactionMaxScore = GetExpectedTransactionLineCount();
            return BuildAuditReport(
                attemptNumber,
                structuralMistakes,
                transactionMistakes,
                lineMistakes,
                lineHints,
                structureScore,
                structureMaxScore,
                orderScore,
                orderMaxScore,
                sideScore,
                sideMaxScore,
                transactionScore,
                transactionMaxScore);
        }

        float totalDebit = confirmedEntries.Where(e => e.side == "Dr").Sum(e => e.amount);
        float totalCredit = confirmedEntries.Where(e => e.side == "Cr").Sum(e => e.amount);
        if (!Mathf.Approximately(totalDebit, totalCredit))
        {
            structuralMistakes.Add($"Debit and credit totals do not match. Debit total is {FormatAmount(totalDebit)} and credit total is {FormatAmount(totalCredit)}.");
        }
        else
        {
            structureScore++;
        }

        for (int i = 0; i < confirmedEntries.Count; i++)
        {
            EntryData entry = confirmedEntries[i];
            string correctSide = GetCorrectSideForUser(entry.category, entry.type);
            if (correctSide == "Invalid")
            {
                string message = "This account is not used for this transaction.";
                structuralMistakes.Add(message);
                lineMistakes.Add(message);
                lineHints.Add(CreateLineHint(i, message));
                continue;
            }

            if (entry.side != correctSide)
            {
                string message = $"This account should be on {correctSide}.";
                structuralMistakes.Add(message);
                lineMistakes.Add(message);
                lineHints.Add(CreateLineHint(i, message));
            }
        }

        CalculateExpectedSideScore(out sideScore, out sideMaxScore);
        CalculateOrderScore(structuralMistakes, lineMistakes, lineHints, out orderScore, out orderMaxScore);

        ValidateAgainstTransactions(
            transactionMistakes,
            lineMistakes,
            lineHints,
            out transactionScore,
            out transactionMaxScore);

        return BuildAuditReport(
            attemptNumber,
            structuralMistakes,
            transactionMistakes,
            lineMistakes,
            lineHints,
            structureScore,
            structureMaxScore,
            orderScore,
            orderMaxScore,
            sideScore,
            sideMaxScore,
            transactionScore,
            transactionMaxScore);
    }

    private AuditReport BuildAuditReport(
        int attemptNumber,
        List<string> structuralMistakes,
        List<string> transactionMistakes,
        List<string> lineMistakes,
        List<AuditLineHint> lineHints,
        int structureScore,
        int structureMaxScore,
        int orderScore,
        int orderMaxScore,
        int sideScore,
        int sideMaxScore,
        int transactionScore,
        int transactionMaxScore)
    {
        List<string> allMistakes = new List<string>();
        allMistakes.AddRange(structuralMistakes);
        allMistakes.AddRange(transactionMistakes);

        lineHints = lineHints
            .OrderBy(h => h.HasEntry ? h.rowNumber : int.MaxValue)
            .ThenBy(h => h.journalLineNumber)
            .ThenBy(h => h.message)
            .ToList();

        lineMistakes = lineHints.Count > 0
            ? lineHints.Select(h => h.message).Distinct().ToList()
            : lineMistakes.Distinct().ToList();

        int normalizedStructureMaxScore = Mathf.Max(0, structureMaxScore);
        int normalizedOrderMaxScore = Mathf.Max(0, orderMaxScore);
        int normalizedSideMaxScore = Mathf.Max(0, sideMaxScore);
        int normalizedTransactionMaxScore = Mathf.Max(0, transactionMaxScore);
        int normalizedScore = Mathf.Clamp(structureScore, 0, normalizedStructureMaxScore)
            + Mathf.Clamp(orderScore, 0, normalizedOrderMaxScore)
            + Mathf.Clamp(sideScore, 0, normalizedSideMaxScore)
            + Mathf.Clamp(transactionScore, 0, normalizedTransactionMaxScore);
        int normalizedMaxScore = Mathf.Max(
            1,
            normalizedStructureMaxScore + normalizedOrderMaxScore + normalizedSideMaxScore + normalizedTransactionMaxScore);
        bool earnedStructurePoint =
            structureScore >= normalizedStructureMaxScore
            && orderScore >= normalizedOrderMaxScore
            && sideScore >= normalizedSideMaxScore;
        bool earnedTransactionPoint =
            normalizedTransactionMaxScore == 0
            || transactionScore >= normalizedTransactionMaxScore;
        int normalizedAttemptNumber = Mathf.Max(1, attemptNumber);

        AuditReport report = new AuditReport
        {
            attemptNumber = normalizedAttemptNumber,
            retryCount = Mathf.Max(0, normalizedAttemptNumber - 1),
            maxScore = normalizedMaxScore,
            earnedStructurePoint = earnedStructurePoint,
            earnedTransactionPoint = earnedTransactionPoint,
            structureScore = Mathf.Clamp(structureScore, 0, normalizedStructureMaxScore),
            structureMaxScore = normalizedStructureMaxScore,
            orderScore = Mathf.Clamp(orderScore, 0, normalizedOrderMaxScore),
            orderMaxScore = normalizedOrderMaxScore,
            sideScore = Mathf.Clamp(sideScore, 0, normalizedSideMaxScore),
            sideMaxScore = normalizedSideMaxScore,
            transactionScore = Mathf.Clamp(transactionScore, 0, normalizedTransactionMaxScore),
            transactionMaxScore = normalizedTransactionMaxScore,
            score = normalizedScore,
            mistakeDetails = allMistakes,
            lineMistakeDetails = lineMistakes,
            lineHints = lineHints,
            mistakeCount = allMistakes.Count,
        };

        report.isPassed = report.score == report.maxScore;
        return report;
    }

    private void CalculateOrderScore(
        List<string> structuralMistakes,
        List<string> lineMistakes,
        List<AuditLineHint> lineHints,
        out int orderScore,
        out int orderMaxScore)
    {
        orderScore = 0;
        orderMaxScore = 0;

        if (confirmedEntries.Count == 0)
        {
            return;
        }

        int groupStart = 0;
        for (int i = 0; i < confirmedEntries.Count; i++)
        {
            bool isGroupEnd = confirmedEntries[i].separatorAfter || i == confirmedEntries.Count - 1;
            if (!isGroupEnd)
            {
                continue;
            }

            orderMaxScore++;
            if (IsJournalGroupOrdered(groupStart, i, structuralMistakes, lineMistakes, lineHints))
            {
                orderScore++;
            }

            groupStart = i + 1;
        }
    }

    private bool IsJournalGroupOrdered(
        int groupStart,
        int groupEnd,
        List<string> structuralMistakes,
        List<string> lineMistakes,
        List<AuditLineHint> lineHints)
    {
        bool seenCredit = false;
        bool isOrdered = true;

        for (int i = groupStart; i <= groupEnd && i < confirmedEntries.Count; i++)
        {
            EntryData entry = confirmedEntries[i];
            if (entry.side == "Cr")
            {
                seenCredit = true;
                continue;
            }

            if (entry.side == "Dr" && seenCredit)
            {
                string message = "Debit rows should come before credit rows.";
                structuralMistakes.Add(message);
                lineMistakes.Add(message);
                lineHints.Add(CreateLineHint(i, message));
                isOrdered = false;
            }
        }

        return isOrdered;
    }

    private void CalculateExpectedSideScore(out int sideScore, out int sideMaxScore)
    {
        sideScore = 0;
        sideMaxScore = 0;

        List<TransactionRecord> actual = GetScorableTransactionRecords();

        if (actual == null || actual.Count == 0)
        {
            sideMaxScore = confirmedEntries.Count;
            sideScore = confirmedEntries.Count(entry => entry.side == GetCorrectSideForUser(entry.category, entry.type));
            return;
        }

        foreach (IGrouping<TransactionGroupKey, TransactionRecord> transactionGroup in actual.GroupBy(t => new TransactionGroupKey(t.store, t.type)))
        {
            StoreType store = transactionGroup.Key.Store;
            TransactionType type = transactionGroup.Key.Type;
            ItemCategory balancingCategory = GetBalancingCategoryForType(type);

            sideMaxScore++;
            if (HasCorrectSideEntryForExpectedLine(store, type, balancingCategory, string.Empty, true))
            {
                sideScore++;
            }

            foreach (IGrouping<TransactionItemKey, TransactionRecord> itemGroup in transactionGroup.GroupBy(t =>
                new TransactionItemKey(t.store, t.type, t.category, GetExpectedItemKey(t.type, t.category, t.itemName))))
            {
                sideMaxScore++;
                if (HasCorrectSideEntryForExpectedLine(
                    store,
                    type,
                    itemGroup.Key.Category,
                    itemGroup.Key.ItemName,
                    false))
                {
                    sideScore++;
                }
            }
        }
    }

    private bool HasCorrectSideEntryForExpectedLine(
        StoreType store,
        TransactionType type,
        ItemCategory category,
        string itemName,
        bool isBalancingEntry)
    {
        return confirmedEntries.Any(entry =>
            IsSameTransactionGroup(entry, store, type)
            && entry.category == category
            && IsBalancingEntry(entry) == isBalancingEntry
            && (isBalancingEntry || EntryMatchesExpectedItem(entry, itemName))
            && entry.side == GetCorrectSideForUser(entry.category, entry.type));
    }

    private void ValidateAgainstTransactions(
        List<string> transactionMistakes,
        List<string> lineMistakes,
        List<AuditLineHint> lineHints,
        out int transactionScore,
        out int transactionMaxScore)
    {
        transactionScore = 0;
        transactionMaxScore = 0;

        List<TransactionRecord> actual = GetScorableTransactionRecords();

        if (actual == null || actual.Count == 0)
        {
            if (confirmedEntries.Count > 0)
            {
                transactionMaxScore = confirmedEntries.Count;
                transactionMistakes.Add("The journal has entries, but there were no transactions to record today.");
                for (int i = 0; i < confirmedEntries.Count; i++)
                {
                    string message = "This entry is not from today's transactions.";
                    lineMistakes.Add(message);
                    lineHints.Add(CreateLineHint(i, message));
                }
            }

            return;
        }

        foreach (IGrouping<TransactionGroupKey, TransactionRecord> transactionGroup in actual.GroupBy(t => new TransactionGroupKey(t.store, t.type)))
        {
            StoreType store = transactionGroup.Key.Store;
            TransactionType type = transactionGroup.Key.Type;
            ItemCategory balancingCategory = GetBalancingCategoryForType(type);

            float expectedBalancing = transactionGroup.Sum(t => t.totalPrice);
            List<EntryData> balancingEntries = confirmedEntries
                .Where(e => IsSameTransactionGroup(e, store, type)
                    && e.category == balancingCategory
                    && IsBalancingEntry(e))
                .ToList();
            float enteredBalancing = balancingEntries.Sum(e => e.amount);
            transactionMaxScore++;

            if (!Mathf.Approximately(enteredBalancing, expectedBalancing))
            {
                string accountName = GetCategoryDisplayName(balancingCategory, type);
                transactionMistakes.Add(
                    $"{accountName} should be {FormatAmount(expectedBalancing)}, not {FormatAmount(enteredBalancing)}.");
                AddAmountLineHints(lineMistakes, lineHints, balancingEntries, accountName, expectedBalancing);
            }
            else
            {
                transactionScore++;
            }

            foreach (IGrouping<TransactionItemKey, TransactionRecord> itemGroup in transactionGroup.GroupBy(t =>
                new TransactionItemKey(store, type, t.category, GetExpectedItemKey(type, t.category, t.itemName))))
            {
                float expectedAmount = itemGroup.Sum(t => t.totalPrice);
                int expectedQuantity = itemGroup.Sum(t => t.quantity);
                string itemName = itemGroup.Key.ItemName;
                ItemCategory category = itemGroup.Key.Category;

                List<EntryData> matchingEntries = confirmedEntries
                    .Where(e => IsSameTransactionGroup(e, store, type)
                        && !IsBalancingEntry(e)
                        && e.category == category
                        && EntryMatchesExpectedItem(e, itemName))
                    .ToList();
                float enteredAmount = matchingEntries.Sum(e => e.amount);
                transactionMaxScore++;

                if (!Mathf.Approximately(enteredAmount, expectedAmount))
                {
                    string displayName = BuildExpectedItemDisplayName(itemName, expectedQuantity, category, type);
                    transactionMistakes.Add(
                        $"{displayName} should be {FormatAmount(expectedAmount)}, not {FormatAmount(enteredAmount)}.");
                    AddAmountLineHints(lineMistakes, lineHints, matchingEntries, displayName, expectedAmount);
                }
                else
                {
                    transactionScore++;
                }
            }
        }

        for (int i = 0; i < confirmedEntries.Count; i++)
        {
            EntryData entry = confirmedEntries[i];
            if (!MatchesAnyExpectedTransaction(entry, actual))
            {
                transactionMaxScore++;
                string message = "This entry does not match today's transactions.";
                transactionMistakes.Add(message);
                lineMistakes.Add(message);
                lineHints.Add(CreateLineHint(i, message));
            }
        }
    }

    private void AddAmountLineHints(
        List<string> lineMistakes,
        List<AuditLineHint> lineHints,
        List<EntryData> entries,
        string displayName,
        float expectedAmount)
    {
        if (entries == null || entries.Count == 0)
        {
            string message = $"Add {displayName} for {FormatAmount(expectedAmount)}.";
            lineMistakes.Add(message);
            lineHints.Add(CreateGeneralHint(message));
            return;
        }

        float enteredTotal = entries.Sum(e => e.amount);
        int acceptedExactMatchIndex = entries.FindIndex(e =>
            Mathf.Approximately(e.amount, expectedAmount)
            && e.side == GetCorrectSideForUser(e.category, e.type));

        if (acceptedExactMatchIndex < 0)
        {
            acceptedExactMatchIndex = entries.FindIndex(e => Mathf.Approximately(e.amount, expectedAmount));
        }

        for (int i = 0; i < entries.Count; i++)
        {
            EntryData entry = entries[i];
            if (acceptedExactMatchIndex >= 0 && i == acceptedExactMatchIndex)
            {
                continue;
            }

            int index = confirmedEntries.IndexOf(entry);
            string message = BuildAmountHintMessage(index, displayName, enteredTotal, expectedAmount, acceptedExactMatchIndex >= 0);
            lineMistakes.Add(message);
            lineHints.Add(CreateLineHint(index, message));
        }
    }

    private string BuildAmountHintMessage(int entryIndex, string displayName, float enteredTotal, float expectedAmount, bool hasAcceptedExactMatch)
    {
        string enteredText = FormatAmount(enteredTotal);
        string expectedText = FormatAmount(expectedAmount);

        if (hasAcceptedExactMatch && enteredTotal > expectedAmount)
        {
            return $"This is an extra {displayName} row.";
        }

        if (enteredTotal > expectedAmount)
        {
            return $"{displayName} should be {expectedText}, not {enteredText}.";
        }

        if (enteredTotal < expectedAmount)
        {
            return $"{displayName} should be {expectedText}, not {enteredText}.";
        }

        return $"This duplicates {displayName}.";
    }

    private bool MatchesAnyExpectedTransaction(EntryData entry, List<TransactionRecord> actual)
    {
        if (entry == null || actual == null)
        {
            return false;
        }

        if (IsBalancingEntry(entry))
        {
            return actual
                .GroupBy(t => new TransactionGroupKey(t.store, t.type))
                .Any(group => IsSameTransactionGroup(entry, group.Key.Store, group.Key.Type)
                    && entry.category == GetBalancingCategoryForType(group.Key.Type));
        }

        return actual.Any(record =>
            IsSameTransactionGroup(entry, record.store, record.type)
            && entry.category == record.category
            && EntryMatchesExpectedRecord(entry, record));
    }

    private void RebuildJournalUI(bool scrollToBottom = false)
    {
        if (!journalButtonsBound)
        {
            BindJournalButtons();
        }

        ResolveItemListContainerIfNeeded();

        if (itemListContainer == null)
        {
            Debug.LogWarning("[JournalManager] itemListContainer is not assigned and could not be found in the active JournalCanvas.");
            return;
        }

        EnsureScrollableLedger();
        EnsureJournalTotalFooter();

        foreach (Transform child in itemListContainer)
        {
            Destroy(child.gameObject);
        }

        EnsureEntryIds();

        foreach (EntryData entry in confirmedEntries)
        {
            GenerateJournalItem(entry);
            if (entry.separatorAfter && horizontalLinePrefab != null)
            {
                Instantiate(horizontalLinePrefab, itemListContainer);
            }
        }

        RectTransform content = itemListContainer as RectTransform;
        if (content != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        }

        if (scrollToBottom && ledgerScrollRect != null)
        {
            Canvas.ForceUpdateCanvases();
            ledgerScrollRect.verticalNormalizedPosition = 0f;
        }

        UpdateJournalTotalFooter();
    }

    private void ScheduleJournalRebuild(bool scrollToBottom = false)
    {
        pendingScrollToBottom = pendingScrollToBottom || scrollToBottom;
        if (pendingRebuildCoroutine != null)
        {
            return;
        }

        pendingRebuildCoroutine = StartCoroutine(RebuildJournalUINextFrame());
    }

    private IEnumerator RebuildJournalUINextFrame()
    {
        yield return null;

        bool shouldScrollToBottom = pendingScrollToBottom;
        pendingScrollToBottom = false;
        pendingRebuildCoroutine = null;
        RebuildJournalUI(shouldScrollToBottom);
    }

    private void GenerateJournalItem(EntryData entry)
    {
        if (itemListContainer == null || itemListPrefab == null || entry == null)
        {
            return;
        }

        GameObject itemObj = Instantiate(itemListPrefab, itemListContainer);
        string displayName = GetEntryDisplayName(entry);

        TMP_Text[] texts = itemObj.GetComponentsInChildren<TMP_Text>(true);
        TMP_Text nameDr = Array.Find(texts, t => t.name == "Text_Name_Dr");
        TMP_Text nameCr = Array.Find(texts, t => t.name == "Text_Name_Cr");
        TMP_Text valDr = Array.Find(texts, t => t.name == "Text_Dr");
        TMP_Text valCr = Array.Find(texts, t => t.name == "Text_Cr");

        if (nameDr == null)
        {
            Transform tr = itemObj.transform.Find("Text_Name_Dr");
            if (tr != null)
            {
                nameDr = tr.GetComponent<TMP_Text>();
            }
        }

        if (nameCr == null)
        {
            Transform tr = itemObj.transform.Find("Text_Name_Cr");
            if (tr != null)
            {
                nameCr = tr.GetComponent<TMP_Text>();
            }
        }

        if (valDr == null)
        {
            Transform tr = itemObj.transform.Find("AmountHolder/Text_Dr");
            if (tr != null)
            {
                valDr = tr.GetComponent<TMP_Text>();
            }
        }

        if (valCr == null)
        {
            Transform tr = itemObj.transform.Find("AmountHolder/Text_Cr");
            if (tr != null)
            {
                valCr = tr.GetComponent<TMP_Text>();
            }
        }

        if (nameDr != null) nameDr.text = string.Empty;
        if (nameCr != null) nameCr.text = string.Empty;
        if (valDr != null) valDr.text = string.Empty;
        if (valCr != null) valCr.text = string.Empty;

        string amountText = entry.amount.ToString("N0");
        if (entry.side == "Dr")
        {
            if (nameDr != null) nameDr.text = displayName;
            if (valDr != null) valDr.text = amountText;
        }
        else
        {
            if (nameCr != null) nameCr.text = displayName;
            if (valCr != null) valCr.text = amountText;
        }

        JournalRowAutoHeight autoHeight = itemObj.GetComponent<JournalRowAutoHeight>();
        if (autoHeight != null)
        {
            autoHeight.Refresh();
        }

        JournalEntryRowUI row = itemObj.GetComponent<JournalEntryRowUI>();
        if (row == null)
        {
            row = itemObj.AddComponent<JournalEntryRowUI>();
        }

        row.Initialize(this, entry);
        row.SetAuditHints(GetVisibleHintsForEntry(entry.entryId), ShouldShowDetailedAuditHints());
    }

    private void RefreshAuditPreviewIfDetailedFeedbackUnlocked()
    {
        if (!ShouldShowDetailedAuditHints())
        {
            return;
        }

        lastAuditReport = EvaluateJournal(submitAttemptCount);
        hasLastAuditReport = true;
    }

    private void EnsureScrollableLedger()
    {
        ResolveItemListContainerIfNeeded();

        if (scrollViewportPrepared || itemListContainer == null)
        {
            return;
        }

        RectTransform content = itemListContainer as RectTransform;
        if (content == null)
        {
            return;
        }

        if (ledgerScrollRect == null)
        {
            ledgerScrollRect = itemListContainer.GetComponentInParent<ScrollRect>(true);
        }

        if (ledgerScrollRect != null)
        {
            if (ledgerViewport == null)
            {
                ledgerViewport = ledgerScrollRect.viewport != null
                    ? ledgerScrollRect.viewport
                    : ledgerScrollRect.transform as RectTransform;
            }

            NormalizeLedgerViewportIfNeeded();
            NormalizeLedgerContentIfNeeded(content);
            EnsureLedgerContentSizing(content);

            if (ledgerScrollRect.content != content)
            {
                ledgerScrollRect.content = content;
            }
        }

        if (ledgerViewport != null)
        {
            Graphic viewportGraphic = ledgerViewport.GetComponent<Graphic>();
            if (viewportGraphic != null)
            {
                viewportGraphic.raycastTarget = true;
            }

            JournalLedgerDropArea dropArea = ledgerViewport.GetComponent<JournalLedgerDropArea>();
            if (dropArea == null)
            {
                dropArea = ledgerViewport.gameObject.AddComponent<JournalLedgerDropArea>();
            }

            dropArea.Initialize(this);
        }

        scrollViewportPrepared = true;
    }

    private void NormalizeLedgerContentIfNeeded(RectTransform content)
    {
        if (content == null || ledgerViewport == null || content.parent != ledgerViewport)
        {
            return;
        }

        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(0f, Mathf.Max(0f, content.sizeDelta.y));
    }

    private void EnsureLedgerContentSizing(RectTransform content)
    {
        if (content == null)
        {
            return;
        }

        VerticalLayoutGroup layoutGroup = content.GetComponent<VerticalLayoutGroup>();
        if (layoutGroup != null)
        {
            layoutGroup.childForceExpandWidth = true;
            layoutGroup.childForceExpandHeight = false;
        }

        ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
        if (fitter == null)
        {
            fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        }

        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    private void NormalizeLedgerViewportIfNeeded()
    {
        if (ledgerScrollRect == null || ledgerViewport == null)
        {
            return;
        }

        RectTransform scrollRectTransform = ledgerScrollRect.transform as RectTransform;
        if (scrollRectTransform == null || ledgerViewport.parent != scrollRectTransform)
        {
            return;
        }

        bool hasCollapsedAnchors = ledgerViewport.anchorMin == Vector2.zero
            && ledgerViewport.anchorMax == Vector2.zero;
        bool hasCollapsedSize = ledgerViewport.sizeDelta.sqrMagnitude < 0.01f;

        if (!hasCollapsedAnchors || !hasCollapsedSize)
        {
            return;
        }

        ledgerViewport.anchorMin = Vector2.zero;
        ledgerViewport.anchorMax = Vector2.one;
        ledgerViewport.anchoredPosition = Vector2.zero;
        ledgerViewport.sizeDelta = Vector2.zero;
        ledgerViewport.pivot = new Vector2(0f, 1f);
    }

    private void EnsureJournalTotalFooter()
    {
        ResolveItemListContainerIfNeeded();

        RectTransform ledgerRect = ledgerViewport != null
            ? ledgerViewport
            : itemListContainer as RectTransform;

        if (ledgerRect == null || ledgerRect.parent == null)
        {
            return;
        }

        RectTransform footerContainer = FindFooterContainer(ledgerRect);
        if (footerContainer == null)
        {
            return;
        }

        GameObject footerPrefab = GetJournalTotalFooterPrefab();
        if (footerPrefab == null)
        {
            Debug.LogWarning("[JournalManager] JournalTotalFooter prefab was not assigned and Resources/UI/JournalTotalFooter could not be loaded.");
            return;
        }

        if (journalTotalFooter == null)
        {
            Transform existingFooter = footerContainer.Find("JournalTotalFooter");
            if (existingFooter != null)
            {
                journalTotalFooter = existingFooter as RectTransform;
                journalTotalFooterUI = existingFooter.GetComponent<JournalTotalFooterUI>();
            }
            else
            {
                CreateJournalTotalFooter(footerContainer, ledgerRect, footerPrefab);
            }
        }
        else if (journalTotalFooter.parent != footerContainer)
        {
            journalTotalFooter.SetParent(footerContainer, false);
        }
    }

    private RectTransform FindFooterContainer(RectTransform start)
    {
        Transform current = start;
        while (current != null)
        {
            RectTransform footerContainer = current.Find("FooterContainer") as RectTransform;
            if (footerContainer != null)
            {
                return footerContainer;
            }

            current = current.parent;
        }

        return null;
    }

    private void CreateJournalTotalFooter(RectTransform footerContainer, RectTransform ledgerRect, GameObject footerPrefab)
    {
        GameObject footerObject = Instantiate(footerPrefab, footerContainer);
        footerObject.name = footerPrefab.name;
        journalTotalFooter = footerObject.transform as RectTransform;
        journalTotalFooter.SetSiblingIndex(Mathf.Min(ledgerRect.GetSiblingIndex() + 1, footerContainer.childCount - 1));

        journalTotalFooterUI = footerObject.GetComponent<JournalTotalFooterUI>();
        if (journalTotalFooterUI == null)
        {
            journalTotalFooterUI = footerObject.AddComponent<JournalTotalFooterUI>();
        }
    }

    private void UpdateJournalTotalFooter()
    {
        if (itemListContainer != null && journalTotalFooter == null)
        {
            EnsureJournalTotalFooter();
        }

        if (journalTotalFooterUI == null)
        {
            return;
        }

        float totalDebit = confirmedEntries.Where(e => e.side == "Dr").Sum(e => e.amount);
        float totalCredit = confirmedEntries.Where(e => e.side == "Cr").Sum(e => e.amount);
        journalTotalFooterUI.SetTotals(FormatAmount(totalDebit), FormatAmount(totalCredit));
    }

    private GameObject GetJournalTotalFooterPrefab()
    {
        if (journalTotalFooterPrefab != null)
        {
            return journalTotalFooterPrefab;
        }

        if (loadedJournalTotalFooterPrefab == null)
        {
            loadedJournalTotalFooterPrefab = Resources.Load<GameObject>("UI/JournalTotalFooter");
        }

        return loadedJournalTotalFooterPrefab;
    }

    private void HideAuditSummary()
    {
        if (AuditUISummary.Instance != null)
        {
            AuditUISummary.Instance.Hide();
        }
    }

    private void ResolveItemListContainerIfNeeded()
    {
        if (itemListContainer != null)
        {
            return;
        }

        DropZone[] dropZones = FindObjectsOfType<DropZone>(true);
        Transform fallback = null;

        foreach (DropZone dropZone in dropZones)
        {
            Transform journalRoot = dropZone != null ? dropZone.transform.parent : null;
            if (journalRoot == null)
            {
                continue;
            }

            Transform candidate = FindJournalItemContainer(journalRoot);
            if (candidate == null)
            {
                continue;
            }

            if (fallback == null)
            {
                fallback = candidate;
            }

            if (journalRoot.name == "Journal")
            {
                itemListContainer = candidate;
                return;
            }
        }

        itemListContainer = fallback;
    }

    private Transform FindJournalItemContainer(Transform journalRoot)
    {
        if (journalRoot == null)
        {
            return null;
        }

        Transform directHolder = journalRoot.Find("ItemListHolder");
        if (directHolder != null)
        {
            return directHolder;
        }

        Transform nestedHolder = FindDescendantByName(journalRoot, "ItemListHolder");
        if (nestedHolder != null)
        {
            return nestedHolder;
        }

        ScrollRect scrollRect = journalRoot.GetComponentInChildren<ScrollRect>(true);
        if (scrollRect != null && scrollRect.content != null)
        {
            return scrollRect.content;
        }

        return FindDescendantByName(journalRoot, "Content");
    }

    private Transform FindDescendantByName(Transform root, string targetName)
    {
        if (root == null || string.IsNullOrEmpty(targetName))
        {
            return null;
        }

        foreach (Transform descendant in root.GetComponentsInChildren<Transform>(true))
        {
            if (descendant != root && descendant.name == targetName)
            {
                return descendant;
            }
        }

        return null;
    }

    private Camera ResolveEventCamera(PointerEventData eventData)
    {
        Camera eventCamera = eventData != null ? eventData.pressEventCamera : null;
        if (eventCamera != null)
        {
            return eventCamera;
        }

        Canvas canvas = itemListContainer != null ? itemListContainer.GetComponentInParent<Canvas>() : null;
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            return canvas.worldCamera;
        }

        return null;
    }

    private bool HasOpenLine()
    {
        return confirmedEntries.Count > 0 && !confirmedEntries[confirmedEntries.Count - 1].separatorAfter;
    }

    private int GetJournalLineNumber(int entryIndex)
    {
        if (entryIndex < 0)
        {
            return 1;
        }

        int lineNumber = 1;
        for (int i = 0; i < entryIndex && i < confirmedEntries.Count; i++)
        {
            if (confirmedEntries[i].separatorAfter)
            {
                lineNumber++;
            }
        }

        return lineNumber;
    }

    private AuditLineHint CreateLineHint(int entryIndex, string message)
    {
        EntryData entry = entryIndex >= 0 && entryIndex < confirmedEntries.Count
            ? confirmedEntries[entryIndex]
            : null;

        return new AuditLineHint
        {
            entryId = entry != null ? entry.entryId : string.Empty,
            rowNumber = entryIndex >= 0 ? entryIndex + 1 : int.MaxValue,
            journalLineNumber = GetJournalLineNumber(entryIndex),
            message = message,
        };
    }

    private AuditLineHint CreateGeneralHint(string message)
    {
        return new AuditLineHint
        {
            entryId = string.Empty,
            rowNumber = int.MaxValue,
            journalLineNumber = int.MaxValue,
            message = message,
        };
    }

    private List<string> GetVisibleHintsForEntry(string entryId)
    {
        if (!ShouldShowDetailedAuditHints()
            || string.IsNullOrEmpty(entryId)
            || lastAuditReport.lineHints == null)
        {
            return new List<string>();
        }

        return lastAuditReport.lineHints
            .Where(h => h.entryId == entryId)
            .OrderBy(h => h.rowNumber)
            .Select(h => h.message)
            .Distinct()
            .ToList();
    }

    private bool ShouldShowDetailedAuditHints()
    {
        return hasLastAuditReport
            && !lastAuditReport.isPassed
            && lastAuditReport.attemptNumber >= 3;
    }

    private int GetExpectedTransactionLineCount()
    {
        List<TransactionRecord> actual = GetScorableTransactionRecords();

        if (actual == null || actual.Count == 0)
        {
            return 0;
        }

        int count = 0;
        foreach (IGrouping<TransactionGroupKey, TransactionRecord> transactionGroup in actual.GroupBy(t => new TransactionGroupKey(t.store, t.type)))
        {
            count++;
            count += transactionGroup
                .GroupBy(t => new TransactionItemKey(t.store, t.type, t.category, GetExpectedItemKey(t.type, t.category, t.itemName)))
                .Count();
        }

        return count;
    }

    private void EnsureEntryIds()
    {
        foreach (EntryData entry in confirmedEntries)
        {
            entry.EnsureEntryId();
        }
    }

    private EntryData FindEntry(string entryId)
    {
        if (string.IsNullOrEmpty(entryId))
        {
            return null;
        }

        return confirmedEntries.FirstOrDefault(e => e.entryId == entryId);
    }

    private bool IsSameTransactionGroup(EntryData entry, StoreType store, TransactionType type)
    {
        if (entry == null || entry.type != type)
        {
            return false;
        }

        return entry.store == store || entry.store == StoreType.None;
    }

    private bool IsBalancingEntry(EntryData entry)
    {
        if (entry == null)
        {
            return false;
        }

        if (entry.isBalancingEntry)
        {
            return true;
        }

        return string.IsNullOrWhiteSpace(entry.itemName)
            && entry.category == GetBalancingCategoryForType(entry.type);
    }

    private string GetEntryDisplayName(EntryData entry)
    {
        if (entry == null)
        {
            return "Unknown";
        }

        if (IsBalancingEntry(entry)
            || string.IsNullOrWhiteSpace(entry.itemName)
            || ShouldDisplayAccountName(entry.category, entry.type))
        {
            return GetCategoryDisplayName(entry.category, entry.type);
        }

        return entry.quantity > 1
            ? $"{entry.itemName} x{entry.quantity}"
            : entry.itemName;
    }

    private string BuildExpectedItemDisplayName(string itemName, int quantity, ItemCategory category, TransactionType type)
    {
        if (string.IsNullOrWhiteSpace(itemName) || ShouldDisplayAccountName(category, type))
        {
            return GetCategoryDisplayName(category, type);
        }

        return quantity > 1 ? $"{itemName} x{quantity}" : itemName;
    }

    private string NormalizeItemName(string itemName)
    {
        return string.IsNullOrWhiteSpace(itemName) ? string.Empty : itemName.Trim();
    }

    private string GetExpectedItemKey(TransactionType type, ItemCategory category, string itemName)
    {
        return ShouldDisplayAccountName(category, type) ? string.Empty : NormalizeItemName(itemName);
    }

    private bool EntryMatchesExpectedItem(EntryData entry, string expectedItemKey)
    {
        if (entry == null)
        {
            return false;
        }

        if (ShouldDisplayAccountName(entry.category, entry.type))
        {
            return true;
        }

        return NormalizeItemName(entry.itemName) == expectedItemKey;
    }

    private bool EntryMatchesExpectedRecord(EntryData entry, TransactionRecord record)
    {
        if (entry == null || record == null)
        {
            return false;
        }

        if (ShouldDisplayAccountName(record.category, record.type))
        {
            return true;
        }

        return NormalizeItemName(entry.itemName) == NormalizeItemName(record.itemName);
    }

    private List<TransactionRecord> GetScorableTransactionRecords()
    {
        List<TransactionRecord> records = TransactionManager.Instance != null
            ? TransactionManager.Instance.GetRecordsToday()
            : null;

        if (records == null)
        {
            return null;
        }

        return records
            .Where(record => record != null && record.category != GetBalancingCategoryForType(record.type))
            .ToList();
    }

    private string FormatAmount(float amount)
    {
        return amount.ToString("N0");
    }

    private string GetCorrectSideForUser(ItemCategory category, TransactionType type)
    {
        if (TryGetExplicitSideByType(category, type, out string side))
        {
            return side;
        }

        bool isCashOutflow = type == TransactionType.Buy || type == TransactionType.Bill;
        if (category == ItemCategory.Cash)
        {
            return isCashOutflow ? "Cr" : "Dr";
        }

        return isCashOutflow ? "Dr" : "Cr";
    }

    private ItemCategory GetBalancingCategoryForType(TransactionType type)
    {
        return type switch
        {
            TransactionType.BillAccrued => ItemCategory.AccruedExpenses,
            TransactionType.BillSettlement => ItemCategory.Cash,
            TransactionType.CreditSale => ItemCategory.AccountsReceivable,
            TransactionType.ReceivePayment => ItemCategory.Cash,
            TransactionType.CreditPurchase => ItemCategory.AccountsPayable,
            TransactionType.RepayAP => ItemCategory.Cash,
            TransactionType.AccrueInterest => ItemCategory.InterestPayable,
            _ => ItemCategory.Cash,
        };
    }

    private string GetCategoryDisplayName(ItemCategory category, TransactionType type)
    {
        string normal = category switch
        {
            ItemCategory.AccruedExpenses => "Accrued Expenses",
            ItemCategory.FoodSupplies => "Food Supplies",
            ItemCategory.KitchenEquipment => "Kitchen Equipment",
            ItemCategory.FoodSales => "Food Sales",
            ItemCategory.SalesRevenue => "Sales Revenue",
            ItemCategory.ServiceRevenue => "Service Revenue",
            ItemCategory.UtilitiesElectricity => "Electricity Expense",
            ItemCategory.UtilitiesWater => "Water Expense",
            ItemCategory.Cash => "Cash",
            ItemCategory.AccountsReceivable => "Accounts Receivable",
            ItemCategory.AccountsPayable => "Accounts Payable",
            ItemCategory.InterestExpense => "Interest Expense",
            ItemCategory.InterestPayable => "Interest Payable",
            _ => "Misc",
        };

        if (type == TransactionType.Sell || type == TransactionType.CreditSale)
        {
            return category switch
            {
                ItemCategory.FoodSales => "Food Sales Revenue",
                ItemCategory.SalesRevenue => "Sales Revenue",
                ItemCategory.ServiceRevenue => "Service Revenue",
                _ => normal,
            };
        }

        return normal;
    }

    private static bool TryGetExplicitSideByType(ItemCategory category, TransactionType type, out string side)
    {
        side = string.Empty;

        switch (type)
        {
            case TransactionType.BillAccrued:
                side = category == ItemCategory.AccruedExpenses ? "Cr" : "Dr";
                return true;

            case TransactionType.BillSettlement:
                if (category == ItemCategory.Cash)
                {
                    side = "Cr";
                }
                else if (category == ItemCategory.AccruedExpenses)
                {
                    side = "Dr";
                }
                else
                {
                    side = "Invalid";
                }
                return true;

            case TransactionType.CreditSale:
                if (category == ItemCategory.AccountsReceivable)
                {
                    side = "Dr";
                }
                else if (IsRevenueCategory(category))
                {
                    side = "Cr";
                }
                else
                {
                    side = "Invalid";
                }
                return true;

            case TransactionType.ReceivePayment:
                if (category == ItemCategory.Cash)
                {
                    side = "Dr";
                }
                else if (category == ItemCategory.AccountsReceivable)
                {
                    side = "Cr";
                }
                else
                {
                    side = "Invalid";
                }
                return true;

            case TransactionType.Sell:
                side = category == ItemCategory.Cash ? "Dr" : "Cr";
                return true;

            case TransactionType.CreditPurchase:
                if (category == ItemCategory.AccountsPayable)
                {
                    side = "Cr";
                }
                else if (category == ItemCategory.FoodSupplies || category == ItemCategory.KitchenEquipment)
                {
                    side = "Dr";
                }
                else
                {
                    side = "Invalid";
                }
                return true;

            case TransactionType.RepayAP:
                if (category == ItemCategory.Cash)
                {
                    side = "Cr";
                }
                else if (category == ItemCategory.AccountsPayable || category == ItemCategory.InterestPayable)
                {
                    side = "Dr";
                }
                else
                {
                    side = "Invalid";
                }
                return true;

            case TransactionType.AccrueInterest:
                if (category == ItemCategory.InterestExpense)
                {
                    side = "Dr";
                }
                else if (category == ItemCategory.InterestPayable)
                {
                    side = "Cr";
                }
                else
                {
                    side = "Invalid";
                }
                return true;
        }

        return false;
    }

    private static bool ShouldDisplayAccountName(ItemCategory category, TransactionType type)
    {
        return type == TransactionType.ReceivePayment
            || type == TransactionType.RepayAP
            || type == TransactionType.AccrueInterest
            || type == TransactionType.BillSettlement;
    }

    private static bool IsRevenueCategory(ItemCategory category)
    {
        return category == ItemCategory.FoodSales
            || category == ItemCategory.SalesRevenue
            || category == ItemCategory.ServiceRevenue;
    }

    private readonly struct TransactionGroupKey
    {
        public readonly StoreType Store;
        public readonly TransactionType Type;

        public TransactionGroupKey(StoreType store, TransactionType type)
        {
            Store = store;
            Type = type;
        }
    }

    private readonly struct TransactionItemKey
    {
        public readonly StoreType Store;
        public readonly TransactionType Type;
        public readonly ItemCategory Category;
        public readonly string ItemName;

        public TransactionItemKey(StoreType store, TransactionType type, ItemCategory category, string itemName)
        {
            Store = store;
            Type = type;
            Category = category;
            ItemName = itemName;
        }
    }
}

[Serializable]
public class EntryData
{
    public string entryId;
    public string side;
    public string itemName;
    public ItemCategory category;
    public TransactionType type;
    public StoreType store;
    public int quantity;
    public float amount;
    public TimeManager.DateTime time;
    public bool isBalancingEntry;
    public bool separatorAfter;

    public EntryData(string side, ItemCategory category, TransactionType type, float amount, TimeManager.DateTime time)
        : this(side, string.Empty, category, type, StoreType.None, 0, amount, time, false, false)
    {
    }

    public EntryData(
        string side,
        string itemName,
        ItemCategory category,
        TransactionType type,
        StoreType store,
        int quantity,
        float amount,
        TimeManager.DateTime time,
        bool isBalancingEntry,
        bool separatorAfter = false)
    {
        entryId = Guid.NewGuid().ToString("N");
        this.side = side;
        this.itemName = itemName;
        this.category = category;
        this.type = type;
        this.store = store;
        this.quantity = quantity;
        this.amount = amount;
        this.time = time;
        this.isBalancingEntry = isBalancingEntry;
        this.separatorAfter = separatorAfter;
    }

    public void EnsureEntryId()
    {
        if (string.IsNullOrEmpty(entryId))
        {
            entryId = Guid.NewGuid().ToString("N");
        }
    }
}
