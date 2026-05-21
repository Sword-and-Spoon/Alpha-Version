using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class JournalEntryRowUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
{
    [Header("Audit Hint UI")]
    [SerializeField] private GameObject hintMarkerPrefab;
    [SerializeField] private GameObject tooltipPrefab;
    [SerializeField] private Color hintHighlightColor = new Color(1f, 0.18f, 0.1f, 0.2f);
    [SerializeField] private Color hintHoverHighlightColor = new Color(1f, 0.78f, 0.18f, 0.32f);
    [SerializeField] private Color hintOutlineColor = new Color(0.9f, 0.18f, 0.1f, 0.9f);
    [SerializeField] private Color hintHoverOutlineColor = new Color(1f, 0.78f, 0.18f, 0.95f);

    private JournalManager journalManager;
    private Canvas canvas;
    private CanvasGroup canvasGroup;
    private GameObject dragGhost;
    private Image highlightImage;
    private Outline highlightOutline;
    private GameObject hintMarker;
    private GameObject loadedHintMarkerPrefab;
    private GameObject loadedTooltipPrefab;
    private readonly List<string> auditHints = new List<string>();
    private bool showAuditHints;
    private bool pointerOver;

    public string EntryId { get; private set; }

    public void Initialize(JournalManager manager, EntryData entry)
    {
        journalManager = manager;
        EntryId = entry != null ? entry.entryId : string.Empty;
        canvas = GetComponentInParent<Canvas>();

        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        EnsureHintVisuals();
    }

    public void SetAuditHints(List<string> hints, bool shouldShowHints)
    {
        auditHints.Clear();
        if (hints != null)
        {
            auditHints.AddRange(hints.Where(h => !string.IsNullOrWhiteSpace(h)).Distinct());
        }

        showAuditHints = shouldShowHints && auditHints.Count > 0;
        EnsureHintVisuals();
        ApplyHintVisualState();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
        {
            return;
        }

        JournalManager manager = journalManager != null ? journalManager : JournalManager.Instance;
        if (manager != null && manager.IsJournalLocked)
        {
            return;
        }

        JournalHintTooltipUI.Hide();
        pointerOver = false;
        ApplyHintVisualState();

        if (canvas == null)
        {
            canvas = GetComponentInParent<Canvas>();
        }

        if (canvas == null)
        {
            return;
        }

        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        canvasGroup.blocksRaycasts = false;
        canvasGroup.alpha = 0.65f;

        dragGhost = Instantiate(gameObject, canvas.transform);
        dragGhost.transform.position = transform.position;
        dragGhost.transform.SetAsLastSibling();

        CanvasGroup ghostGroup = dragGhost.GetComponent<CanvasGroup>();
        if (ghostGroup == null)
        {
            ghostGroup = dragGhost.AddComponent<CanvasGroup>();
        }

        ghostGroup.alpha = 0.65f;
        ghostGroup.blocksRaycasts = false;

        JournalEntryRowUI ghostRow = dragGhost.GetComponent<JournalEntryRowUI>();
        if (ghostRow != null)
        {
            ghostRow.enabled = false;
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
        {
            return;
        }

        if (dragGhost == null || canvas == null)
        {
            return;
        }

        dragGhost.transform.position += (Vector3)eventData.delta / canvas.scaleFactor;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (dragGhost != null)
        {
            Destroy(dragGhost);
        }

        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = true;
            canvasGroup.alpha = 1f;
        }

        JournalManager manager = journalManager != null ? journalManager : JournalManager.Instance;
        if (manager != null && manager.IsJournalLocked)
        {
            return;
        }

        if (manager == null || eventData == null)
        {
            return;
        }

        if (!manager.IsPointerInsideLedger(eventData))
        {
            manager.RemoveEntry(EntryId);
        }
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (eventData == null || eventData.pointerDrag == null)
        {
            return;
        }

        JournalManager manager = journalManager != null ? journalManager : JournalManager.Instance;
        if (manager == null || manager.IsJournalLocked)
        {
            return;
        }

        string side = ResolveDropSide(manager, eventData);
        bool insertAfter = ShouldInsertAfter(eventData);
        manager.HandleInsertDrop(eventData, EntryId, insertAfter, side);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!showAuditHints)
        {
            return;
        }

        pointerOver = true;
        ApplyHintVisualState();
        JournalHintTooltipUI.Show(canvas, ResolveTooltipPrefab(), BuildTooltipText(), eventData.position);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        pointerOver = false;
        ApplyHintVisualState();
        JournalHintTooltipUI.Hide();
    }

    public void OnPointerMove(PointerEventData eventData)
    {
        if (showAuditHints)
        {
            JournalHintTooltipUI.Move(eventData.position);
        }
    }

    private void OnDisable()
    {
        if (pointerOver)
        {
            pointerOver = false;
            JournalHintTooltipUI.Hide();
        }
    }

    private void EnsureHintVisuals()
    {
        if (highlightImage == null)
        {
            highlightImage = GetComponent<Image>();
            if (highlightImage == null)
            {
                highlightImage = gameObject.AddComponent<Image>();
            }

            highlightImage.raycastTarget = true;
            highlightImage.color = Color.clear;
        }

        if (highlightOutline == null)
        {
            highlightOutline = GetComponent<Outline>();
            if (highlightOutline == null)
            {
                highlightOutline = gameObject.AddComponent<Outline>();
            }

            highlightOutline.effectDistance = new Vector2(2f, -2f);
            highlightOutline.enabled = false;
        }

        LayoutElement layoutElement = GetComponent<LayoutElement>();
        if (layoutElement == null)
        {
            layoutElement = gameObject.AddComponent<LayoutElement>();
        }

        RectTransform rectTransform = transform as RectTransform;
        float currentHeight = rectTransform != null ? Mathf.Abs(rectTransform.sizeDelta.y) : 0f;
        float preferredHeight = Mathf.Max(48f, currentHeight);
        layoutElement.preferredHeight = preferredHeight;
        if (rectTransform != null)
        {
            rectTransform.sizeDelta = new Vector2(rectTransform.sizeDelta.x, preferredHeight);
        }

        if (hintMarker == null)
        {
            Transform existingMarker = transform.Find("JournalHintMarker");
            if (existingMarker != null)
            {
                hintMarker = existingMarker.gameObject;
            }
            else
            {
                GameObject prefab = ResolveHintMarkerPrefab();
                if (prefab != null)
                {
                    hintMarker = Instantiate(prefab, transform);
                    hintMarker.name = "JournalHintMarker";
                }
            }

            if (hintMarker != null)
            {
                foreach (Graphic graphic in hintMarker.GetComponentsInChildren<Graphic>(true))
                {
                    graphic.raycastTarget = false;
                }

                hintMarker.SetActive(false);
            }
        }
    }

    private void ApplyHintVisualState()
    {
        if (highlightImage == null || highlightOutline == null)
        {
            return;
        }

        if (!showAuditHints)
        {
            highlightImage.color = Color.clear;
            highlightOutline.enabled = false;
            if (hintMarker != null)
            {
                hintMarker.SetActive(false);
            }

            return;
        }

        highlightImage.color = pointerOver ? hintHoverHighlightColor : hintHighlightColor;
        highlightOutline.effectColor = pointerOver ? hintHoverOutlineColor : hintOutlineColor;
        highlightOutline.enabled = true;

        if (hintMarker != null)
        {
            hintMarker.SetActive(true);
        }
    }

    private string BuildTooltipText()
    {
        return string.Join("\n", auditHints.Select(StripRowPrefix));
    }

    private GameObject ResolveHintMarkerPrefab()
    {
        if (hintMarkerPrefab != null)
        {
            return hintMarkerPrefab;
        }

        if (loadedHintMarkerPrefab == null)
        {
            loadedHintMarkerPrefab = Resources.Load<GameObject>("UI/JournalHintMarker");
        }

        return loadedHintMarkerPrefab;
    }

    private GameObject ResolveTooltipPrefab()
    {
        if (tooltipPrefab != null)
        {
            return tooltipPrefab;
        }

        if (loadedTooltipPrefab == null)
        {
            loadedTooltipPrefab = Resources.Load<GameObject>("UI/JournalHintTooltip");
        }

        return loadedTooltipPrefab;
    }

    private string StripRowPrefix(string hint)
    {
        if (string.IsNullOrWhiteSpace(hint))
        {
            return string.Empty;
        }

        string trimmed = hint.Trim();
        if (!trimmed.StartsWith("Row "))
        {
            return trimmed;
        }

        int colonIndex = trimmed.IndexOf(": ");
        return colonIndex >= 0 && colonIndex + 2 < trimmed.Length
            ? trimmed.Substring(colonIndex + 2)
            : trimmed;
    }

    private string ResolveDropSide(JournalManager manager, PointerEventData eventData)
    {
        if (manager != null && manager.TryResolveDropSide(eventData, out string side))
        {
            return side;
        }

        if (manager != null)
        {
            string currentSide = manager.GetEntrySide(EntryId);
            if (!string.IsNullOrEmpty(currentSide))
            {
                return currentSide;
            }
        }

        return "Dr";
    }

    private bool ShouldInsertAfter(PointerEventData eventData)
    {
        RectTransform rectTransform = transform as RectTransform;
        if (rectTransform == null || eventData == null)
        {
            return true;
        }

        Camera eventCamera = eventData.pressEventCamera;
        if (eventCamera == null && canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            eventCamera = canvas.worldCamera;
        }

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rectTransform,
            eventData.position,
            eventCamera,
            out Vector2 localPoint))
        {
            return true;
        }

        return localPoint.y < 0f;
    }
}

public class JournalLedgerDropArea : MonoBehaviour, IDropHandler
{
    private JournalManager journalManager;

    public void Initialize(JournalManager manager)
    {
        journalManager = manager;
    }

    public void OnDrop(PointerEventData eventData)
    {
        JournalManager manager = journalManager != null ? journalManager : JournalManager.Instance;
        if (manager == null || eventData == null)
        {
            return;
        }

        if (!manager.TryResolveDropSide(eventData, out string side))
        {
            return;
        }

        manager.HandleDrop(eventData, side);
    }
}
