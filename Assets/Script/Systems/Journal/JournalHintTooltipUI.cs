using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class JournalHintTooltipUI : MonoBehaviour
{
    private static JournalHintTooltipUI instance;
    private static GameObject instancePrefab;

    [SerializeField] private TMP_Text label;
    [SerializeField] private Vector2 pointerOffset = new Vector2(18f, -18f);
    [SerializeField] private float canvasMargin = 8f;

    private Canvas canvas;
    private RectTransform rectTransform;

    public static void Show(Canvas targetCanvas, GameObject tooltipPrefab, string message, Vector2 screenPosition)
    {
        if (targetCanvas == null || tooltipPrefab == null || string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        EnsureInstance(targetCanvas, tooltipPrefab);
        if (instance == null || instance.label == null)
        {
            return;
        }

        instance.gameObject.SetActive(true);
        instance.transform.SetAsLastSibling();
        instance.label.text = message;
        LayoutRebuilder.ForceRebuildLayoutImmediate(instance.rectTransform);
        Move(screenPosition);
    }

    public static void Move(Vector2 screenPosition)
    {
        if (instance == null || !instance.gameObject.activeSelf)
        {
            return;
        }

        instance.MoveInternal(screenPosition);
    }

    public static void Hide()
    {
        if (instance != null)
        {
            instance.gameObject.SetActive(false);
        }
    }

    private static void EnsureInstance(Canvas targetCanvas, GameObject tooltipPrefab)
    {
        if (instance != null && instance.canvas == targetCanvas && instancePrefab == tooltipPrefab)
        {
            return;
        }

        if (instance != null)
        {
            Destroy(instance.gameObject);
            instance = null;
            instancePrefab = null;
        }

        GameObject tooltipObject = Instantiate(tooltipPrefab, targetCanvas.transform, false);
        tooltipObject.name = "JournalHintTooltip";

        instance = tooltipObject.GetComponent<JournalHintTooltipUI>();
        if (instance == null)
        {
            instance = tooltipObject.AddComponent<JournalHintTooltipUI>();
        }

        instance.canvas = targetCanvas;
        instancePrefab = tooltipPrefab;
        instance.ResolveReferences();
        tooltipObject.SetActive(false);
    }

    private void ResolveReferences()
    {
        rectTransform = transform as RectTransform;
        if (rectTransform == null)
        {
            rectTransform = gameObject.AddComponent<RectTransform>();
        }

        if (label == null)
        {
            label = GetComponentInChildren<TMP_Text>(true);
        }

        if (label != null)
        {
            label.raycastTarget = false;
        }

        CanvasGroup canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }

        foreach (Graphic graphic in GetComponentsInChildren<Graphic>(true))
        {
            graphic.raycastTarget = false;
        }
    }

    private void MoveInternal(Vector2 screenPosition)
    {
        if (canvas == null || rectTransform == null)
        {
            return;
        }

        RectTransform canvasRect = canvas.transform as RectTransform;
        if (canvasRect == null)
        {
            return;
        }

        Camera eventCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : canvas.worldCamera;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, eventCamera, out Vector2 localPoint))
        {
            return;
        }

        Vector2 desired = localPoint + pointerOffset;
        Vector2 size = rectTransform.rect.size;
        Rect canvasBounds = canvasRect.rect;

        float minX = canvasBounds.xMin + canvasMargin;
        float maxX = Mathf.Max(minX, canvasBounds.xMax - size.x - canvasMargin);
        float minY = canvasBounds.yMin + size.y + canvasMargin;
        float maxY = Mathf.Max(minY, canvasBounds.yMax - canvasMargin);

        desired.x = Mathf.Clamp(desired.x, minX, maxX);
        desired.y = Mathf.Clamp(desired.y, minY, maxY);
        rectTransform.anchoredPosition = desired;
    }
}
