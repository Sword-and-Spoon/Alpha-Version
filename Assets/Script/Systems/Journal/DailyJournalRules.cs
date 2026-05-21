using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class DailyJournalRules
{
    private const string HomeSceneName = "InsideHome";
    private const string SleepBlockedMessage = "You need to finish today's journal before going to sleep.";
    private const string SleepBlockedMistakesMessage = "Your journal still has mistakes. Correct every entry before going to sleep.";
    private const string LeaveHomeBlockedMessage = "You've finished today's journal. You're too tired to go back out; you should sleep now.";

    private static GameObject activeToast;

    public static bool ShouldBlockSleep(out string message)
    {
        TransactionManager transactionManager = TransactionManager.Instance;
        if (transactionManager != null && transactionManager.HasTransactionsNeedingJournal())
        {
            if (JournalManager.Instance != null
                && JournalManager.Instance.TryGetLastAuditReport(out AuditReport report)
                && !report.isPassed)
            {
                message = SleepBlockedMistakesMessage;
                return true;
            }

            message = SleepBlockedMessage;
            return true;
        }

        message = null;
        return false;
    }

    public static bool ShouldBlockLeavingHome(string targetScene, out string message)
    {
        message = null;

        if (string.IsNullOrEmpty(targetScene))
        {
            return false;
        }

        Scene currentScene = SceneManager.GetActiveScene();
        if (currentScene.name != HomeSceneName || targetScene == HomeSceneName)
        {
            return false;
        }

        TransactionManager transactionManager = TransactionManager.Instance;
        if (transactionManager != null && transactionManager.IsJournalCompletedForActiveDay)
        {
            message = LeaveHomeBlockedMessage;
            return true;
        }

        return false;
    }

    public static string BuildSleepSummary()
    {
        TransactionManager transactionManager = TransactionManager.Instance;
        if (transactionManager == null)
        {
            return "Daily Journal Summary\nNo transaction records were found today.";
        }

        return transactionManager.BuildSleepJournalSummary();
    }

    public static void StartNewDayAfterSleep()
    {
        TransactionManager transactionManager = TransactionManager.Instance;
        if (transactionManager != null)
        {
            transactionManager.CompleteDailyRolloverAfterSleep();
        }
    }

    public static void RefreshNewDaySystemsAfterWake()
    {
        RestaurantUtilityBillingCache.RefreshTimeState();
        ARQuestManager.Instance?.RefreshDueLettersForCurrentTime();
        APQuestManager.Instance?.RefreshDebtStatusForCurrentTime();
    }

    public static int GetCurrentAccountingDay()
    {
        if (TimeManager.Instance == null)
        {
            return 1;
        }

        return GetAccountingDay(TimeManager.Instance.dateTime);
    }

    public static int GetAccountingDay(TimeManager.DateTime dateTime)
    {
        int newDayHour = TimeManager.Instance != null ? TimeManager.Instance.newDayHour : 6;
        int day = dateTime.TotalNumDays;
        if (dateTime.Hour < newDayHour)
        {
            day--;
        }

        return Mathf.Max(1, day);
    }

    public static void ShowMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        MonoBehaviour runner = ScreenFader.Instance != null
            ? ScreenFader.Instance
            : TimeManager.Instance;

        if (runner == null)
        {
            Debug.Log(message);
            return;
        }

        runner.StartCoroutine(ShowToastRoutine(message));
    }

    private static IEnumerator ShowToastRoutine(string message)
    {
        if (activeToast != null)
        {
            UnityEngine.Object.Destroy(activeToast);
            activeToast = null;
        }

        activeToast = CreateToast(message);
        yield return new WaitForSecondsRealtime(2.5f);

        if (activeToast != null)
        {
            UnityEngine.Object.Destroy(activeToast);
            activeToast = null;
        }
    }

    private static GameObject CreateToast(string message)
    {
        GameObject canvasObject = new GameObject(
            "DailyJournalMessage",
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 2500;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        GameObject panelObject = new GameObject(
            "Panel",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        panelObject.transform.SetParent(canvasObject.transform, false);

        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 1f);
        panelRect.anchorMax = new Vector2(0.5f, 1f);
        panelRect.pivot = new Vector2(0.5f, 1f);
        panelRect.anchoredPosition = new Vector2(0f, -96f);
        panelRect.sizeDelta = new Vector2(860f, 118f);

        Image panelImage = panelObject.GetComponent<Image>();
        panelImage.color = new Color(0.08f, 0.06f, 0.04f, 0.86f);

        GameObject textObject = new GameObject(
            "Text",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        textObject.transform.SetParent(panelObject.transform, false);

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(34f, 18f);
        textRect.offsetMax = new Vector2(-34f, -18f);

        TMP_Text text = textObject.GetComponent<TMP_Text>();
        text.text = message;
        text.alignment = TextAlignmentOptions.Center;
        text.enableWordWrapping = true;
        text.fontSize = 30f;
        text.color = new Color(1f, 0.93f, 0.78f, 1f);
        text.raycastTarget = false;

        return canvasObject;
    }
}
