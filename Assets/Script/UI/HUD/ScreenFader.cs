using System.Collections;
using TMPro;
using UnityEngine;

public class ScreenFader : MonoBehaviour
{
    public static ScreenFader Instance { get; private set; }

    [SerializeField] private CanvasGroup fadeCanvasGroupPrefab;
    private CanvasGroup fadeCanvasGroup;
    [SerializeField] private float fadeDuration = 1f;
    private TMP_Text overlayMessageText;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        DontDestroyOnLoad(gameObject);

        if (fadeCanvasGroupPrefab != null)
        {
            CanvasGroup canvasGroup = Instantiate(fadeCanvasGroupPrefab, transform);
            canvasGroup.alpha = 0f;
            fadeCanvasGroup = canvasGroup;
        }
        else
        {
            Debug.LogWarning("[ScreenFader] fadeCanvasGroupPrefab is not assigned — creating fallback canvas.");
            fadeCanvasGroup = CreateFallbackCanvas();
        }
    }

    private CanvasGroup CreateFallbackCanvas()
    {
        GameObject canvasGO = new GameObject("FadeCanvas", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
        canvasGO.transform.SetParent(transform, false);
        Canvas canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999;
        DontDestroyOnLoad(canvasGO);

        GameObject panelGO = new GameObject("FadePanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
        panelGO.transform.SetParent(canvasGO.transform, false);
        RectTransform rect = panelGO.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        UnityEngine.UI.Image image = panelGO.GetComponent<UnityEngine.UI.Image>();
        image.color = Color.black;

        CanvasGroup cg = panelGO.AddComponent<CanvasGroup>();
        cg.alpha = 0f;
        cg.blocksRaycasts = false;
        return cg;
    }

    public IEnumerator Fade(float from, float to, float duration = -1f)
    {
        if (duration < 0f) duration = fadeDuration;
        if (fadeCanvasGroup == null) yield break;
        float elapsed = 0f;
        fadeCanvasGroup.alpha = from;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            fadeCanvasGroup.alpha = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }

        fadeCanvasGroup.alpha = to;
    }

    public IEnumerator FadeOutIn(System.Action action, float duration = -1f)
    {
        // Pause game
        float originalTimeScale = Time.timeScale;
        Time.timeScale = 0f;

        yield return Fade(0f, 1f, duration);

        action?.Invoke(); // execute action

        yield return Fade(1f, 0f, duration);

        // Resume game
        Time.timeScale = originalTimeScale;
    }

    public IEnumerator FadeOutInWithMessage(
        System.Action action,
        string message,
        float messageDuration,
        System.Action afterMessage = null,
        float duration = -1f)
    {
        float originalTimeScale = Time.timeScale;
        Time.timeScale = 0f;

        yield return Fade(0f, 1f, duration);

        if (!string.IsNullOrWhiteSpace(message))
        {
            ShowOverlayMessage(message);
            yield return new WaitForSecondsRealtime(Mathf.Max(0.1f, messageDuration));
            HideOverlayMessage();
        }

        try
        {
            action?.Invoke();
            afterMessage?.Invoke();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[ScreenFader] Sleep transition error: {e}");
        }

        yield return Fade(1f, 0f, duration);

        Time.timeScale = originalTimeScale;
    }

    private void ShowOverlayMessage(string message)
    {
        TMP_Text text = GetOverlayMessageText();
        if (text == null)
        {
            return;
        }

        text.text = message;
        text.gameObject.SetActive(true);
    }

    private void HideOverlayMessage()
    {
        if (overlayMessageText != null)
        {
            overlayMessageText.gameObject.SetActive(false);
        }
    }

    private TMP_Text GetOverlayMessageText()
    {
        if (overlayMessageText != null)
        {
            return overlayMessageText;
        }

        if (fadeCanvasGroup == null)
        {
            return null;
        }

        GameObject textObject = new GameObject(
            "SleepSummaryText",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        textObject.transform.SetParent(fadeCanvasGroup.transform, false);

        RectTransform rectTransform = textObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.sizeDelta = new Vector2(980f, 440f);

        overlayMessageText = textObject.GetComponent<TMP_Text>();
        overlayMessageText.alignment = TextAlignmentOptions.Center;
        overlayMessageText.enableWordWrapping = true;
        overlayMessageText.fontSize = 44f;
        overlayMessageText.lineSpacing = 16f;
        overlayMessageText.color = new Color(1f, 0.93f, 0.78f, 1f);
        overlayMessageText.raycastTarget = false;
        overlayMessageText.gameObject.SetActive(false);

        return overlayMessageText;
    }
}
