using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class AuditUISummary : MonoBehaviour
{
    private struct AuditLogLine
    {
        public string Message;
        public AuditLogTone Tone;

        public AuditLogLine(string message, AuditLogTone tone)
        {
            Message = message;
            Tone = tone;
        }
    }

    public static AuditUISummary Instance;

    [Header("UI References")]
    public Transform logContainer;
    public GameObject logItemPrefab;

    private GameObject runtimeLogItemPrefab;
    private bool isDisplayingMessages;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[AuditUISummary] Duplicate instance found, destroying extra.");
            Destroy(gameObject);
            return;
        }

        Instance = this;
        Debug.Log("[AuditUISummary] Awake called, Instance set.");

        if (!isDisplayingMessages)
        {
            Hide();
        }
    }

    public static AuditUISummary GetInstance()
    {
        if (Instance == null)
        {
            Instance = FindObjectOfType<AuditUISummary>(true);
        }

        if (Instance == null)
        {
            Debug.LogError("[AuditUISummary] Instance is NULL! Make sure the AuditUISummary script is attached to a GameObject in the scene.");
        }

        return Instance;
    }

    public void UpdateUI()
    {
        if (JournalManager.Instance == null)
        {
            Debug.LogError("[AuditUISummary] JournalManager.Instance is NULL! Cannot retrieve penalty logs.");
            return;
        }

        if (JournalManager.Instance.TryGetLastAuditReport(out AuditReport report))
        {
            Debug.Log("[AuditUISummary] UpdateUI called, displaying latest audit report.");
            DisplayAuditReport(report);
            return;
        }

        if (IsCurrentButtonDoneOrSubmit())
        {
            Debug.Log("[AuditUISummary] UpdateUI called from Done/Submit before an audit report existed; submitting journal now.");
            JournalManager.Instance.OnFinished();
            if (JournalManager.Instance.TryGetLastAuditReport(out report))
            {
                DisplayAuditReport(report);
                return;
            }
        }

        List<string> logs = JournalManager.Instance.GetPenaltyLogs();
        Debug.Log($"[AuditUISummary] UpdateUI called, retrieved {logs.Count} logs from JournalManager.");
        DisplayErrors(logs);
    }

    public void DisplayAuditReport(AuditReport report)
    {
        List<AuditLogLine> lines = new List<AuditLogLine>
        {
            new AuditLogLine(BuildAttemptMessage(report), AuditLogTone.Neutral),
        };

        if (report.isPassed)
        {
            lines.Add(new AuditLogLine("Everything is correct.", AuditLogTone.Success));
        }
        else
        {
            List<string> detailedHints = report.attemptNumber >= 3
                ? BuildDetailedHintMessages(report)
                : new List<string>();

            if (detailedHints.Count > 0)
            {
                AddDetailedHintLines(lines, detailedHints);
            }
            else
            {
                List<string> issueMessages = BuildAuditIssueMessages(report);
                if (issueMessages.Count == 0)
                {
                    issueMessages.Add("Something is still off. Check the journal again.");
                }

                foreach (string issueMessage in issueMessages)
                {
                    lines.Add(new AuditLogLine(issueMessage, AuditLogTone.Error));
                }

                if (report.attemptNumber < 3)
                {
                    lines.Add(new AuditLogLine("Try again. Row hints unlock on attempt 3.", AuditLogTone.Warning));
                }
            }
        }

        DisplayLines(lines, false);
    }

    private string BuildAttemptMessage(AuditReport report)
    {
        int attempt = Mathf.Max(1, report.attemptNumber);
        return $"Attempt {attempt}.";
    }

    private List<string> BuildAuditIssueMessages(AuditReport report)
    {
        List<string> messages = new List<string>();

        if (report.structureMaxScore > 0 && report.structureScore < report.structureMaxScore)
        {
            messages.Add("Debit and credit totals do not match.");
        }

        if (report.orderMaxScore > 0 && report.orderScore < report.orderMaxScore)
        {
            messages.Add("Debit rows should come before credit rows.");
        }

        if (report.sideMaxScore > 0 && report.sideScore < report.sideMaxScore)
        {
            messages.Add("Some accounts are on the wrong Dr or Cr side.");
        }

        if (report.transactionMaxScore > 0 && report.transactionScore < report.transactionMaxScore)
        {
            messages.Add("Some items or amounts do not match today's transactions.");
        }

        return messages;
    }

    private void AddDetailedHintLines(List<AuditLogLine> lines, List<string> detailedHints)
    {
        const int maxVisibleHints = 4;
        int visibleCount = Mathf.Min(maxVisibleHints, detailedHints.Count);

        for (int i = 0; i < visibleCount; i++)
        {
            lines.Add(new AuditLogLine(detailedHints[i], AuditLogTone.Error));
        }

        if (detailedHints.Count > visibleCount)
        {
            lines.Add(new AuditLogLine("More hints are on the highlighted rows.", AuditLogTone.Warning));
        }
    }

    public void DisplayErrors(List<string> errors)
    {
        DisplayMessages(errors, true, AuditLogTone.Error);
    }

    public void Hide()
    {
        ClearMessages();
        gameObject.SetActive(false);
    }

    private void DisplayMessages(List<string> messages, bool numbered, AuditLogTone tone)
    {
        List<AuditLogLine> lines = messages != null
            ? messages.Select(message => new AuditLogLine(message, tone)).ToList()
            : null;

        DisplayLines(lines, numbered);
    }

    private void DisplayLines(List<AuditLogLine> lines, bool numbered)
    {
        ResolveReferencesIfNeeded();

        ClearMessages();

        if (lines == null || lines.Count == 0)
        {
            Debug.Log("[AuditUISummary] No messages to display.");
            gameObject.SetActive(false);
            return;
        }

        isDisplayingMessages = true;
        gameObject.SetActive(true);
        isDisplayingMessages = false;

        GameObject prefab = GetAuditLogItemPrefab();
        if (prefab == null)
        {
            Debug.LogError("[AuditUISummary] Audit log item prefab is missing. Expected Assets/Resources/UI/AuditLogItem.prefab.");
            return;
        }

        if (logContainer == null)
        {
            Debug.LogError("[AuditUISummary] logContainer is missing, so audit messages cannot be displayed.");
            return;
        }

        int itemNumber = 1;
        foreach (AuditLogLine line in lines)
        {
            string renderedMessage = numbered ? $"{itemNumber++}. {line.Message}" : line.Message;
            GameObject item = Instantiate(prefab, logContainer);
            item.name = "AuditLogItem";

            SetAuditItem(item, renderedMessage, line.Tone);
        }

        Debug.Log($"[AuditUISummary] UI Refresh Complete. Rendered {lines.Count} prefab rows.");
    }

    private void ClearMessages()
    {
        ResolveReferencesIfNeeded();

        if (logContainer == null)
        {
            return;
        }

        int childCount = logContainer.childCount;
        for (int i = childCount - 1; i >= 0; i--)
        {
            Destroy(logContainer.GetChild(i).gameObject);
        }
    }

    private void SetAuditItem(GameObject item, string message, AuditLogTone tone)
    {
        AuditLogItemUI itemUI = item.GetComponent<AuditLogItemUI>();
        if (itemUI != null)
        {
            itemUI.SetMessage(message, tone);
            return;
        }

        bool foundText = false;

        TMP_Text[] tmpTexts = item.GetComponentsInChildren<TMP_Text>(true);
        foreach (TMP_Text tmpText in tmpTexts)
        {
            tmpText.text = message;
            foundText = true;
        }

        Text[] fallbackTexts = item.GetComponentsInChildren<Text>(true);
        foreach (Text fallbackText in fallbackTexts)
        {
            fallbackText.text = message;
            foundText = true;
        }

        if (!foundText)
        {
            Debug.LogWarning($"[AuditUISummary] Audit log item prefab '{item.name}' has no Text or TMP_Text child.");
        }
    }

    private List<string> BuildDetailedHintMessages(AuditReport report)
    {
        if (report.lineHints != null && report.lineHints.Count > 0)
        {
            return report.lineHints
                .OrderBy(h => h.HasEntry ? h.rowNumber : int.MaxValue)
                .ThenBy(h => h.journalLineNumber)
                .ThenBy(h => h.message)
                .Select(BuildAuditLogHintMessage)
                .Distinct()
                .ToList();
        }

        if (report.lineMistakeDetails != null && report.lineMistakeDetails.Count > 0)
        {
            return report.lineMistakeDetails.Distinct().ToList();
        }

        return report.mistakeDetails != null
            ? report.mistakeDetails.Distinct().ToList()
            : new List<string>();
    }

    private string BuildAuditLogHintMessage(AuditLineHint hint)
    {
        if (!hint.HasEntry || hint.rowNumber == int.MaxValue)
        {
            return hint.message;
        }

        string row = $"Row {hint.rowNumber}";
        string message = hint.message;

        if (message.StartsWith("This account should be on "))
        {
            return $"{row} should be on {message.Substring("This account should be on ".Length)}";
        }

        if (message == "This account is not used for this transaction.")
        {
            return $"{row} uses the wrong account.";
        }

        if (message == "This entry is not from today's transactions.")
        {
            return $"{row} is not from today's transactions.";
        }

        if (message == "This entry does not match today's transactions.")
        {
            return $"{row} does not match today's transactions.";
        }

        if (message.StartsWith("This is an extra "))
        {
            return $"{row} is an extra {message.Substring("This is an extra ".Length)}";
        }

        if (message.StartsWith("This duplicates "))
        {
            return $"{row} duplicates {message.Substring("This duplicates ".Length)}";
        }

        return $"{row}: {message}";
    }

    private GameObject GetAuditLogItemPrefab()
    {
        if (logItemPrefab != null)
        {
            return logItemPrefab;
        }

        if (runtimeLogItemPrefab == null)
        {
            runtimeLogItemPrefab = Resources.Load<GameObject>("UI/AuditLogItem");
        }

        if (runtimeLogItemPrefab != null)
        {
            return runtimeLogItemPrefab;
        }

        return logItemPrefab;
    }

    private void ResolveReferencesIfNeeded()
    {
        if (logContainer != null)
        {
            return;
        }

        Transform content = transform.Find("Viewport/Content");
        if (content == null)
        {
            content = transform.Find("Content");
        }

        if (content != null)
        {
            logContainer = content;
            return;
        }

        Debug.LogError("[AuditUISummary] Cannot find AuditErrorLog content. Assign logContainer or create Viewport/Content in the prefab/scene.");
    }

    private bool IsCurrentButtonDoneOrSubmit()
    {
        GameObject selected = EventSystem.current != null
            ? EventSystem.current.currentSelectedGameObject
            : null;

        if (selected == null)
        {
            return false;
        }

        string selectedName = selected.name.ToLowerInvariant();
        return selectedName.Contains("done") || selectedName.Contains("submit");
    }

}
