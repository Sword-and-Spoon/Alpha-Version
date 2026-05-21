using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SmallReceipt : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public TMP_Text storeNameText;
    public TMP_Text transactionTypeText;

    public TransactionType transactionType;
    public StoreType storeType;
    public string counterpartyName;

    [Header("Visual State")]
    [SerializeField] private Color normalCardColor = new Color(0.81f, 0.67f, 0.55f, 1f);
    [SerializeField] private Color hoverCardColor = new Color(0.92f, 0.80f, 0.59f, 1f);
    [SerializeField] private Color selectedCardColor = new Color(0.97f, 0.86f, 0.62f, 1f);

    [Header("Hover Motion")]
    [SerializeField] private Vector2 selectedOffset = new Vector2(34f, 0f);
    [SerializeField] private Vector2 hoverOffset = new Vector2(18f, 8f);
    [SerializeField] private float selectedScale = 1.08f;
    [SerializeField] private float hoverScale = 1.04f;
    [SerializeField] private float hoverRotationOffset = -1.5f;
    [SerializeField] private float animationSpeed = 16f;

    private ReceiptManager receiptManager;
    private RectTransform rectTransform;
    private Image backgroundImage;
    private Vector2 deckPosition;
    private Vector3 deckRotation;
    private bool isHovered;
    private bool isSelected;
    private bool deckConfigured;

    private void Awake()
    {
        rectTransform = transform as RectTransform;
        receiptManager = FindObjectOfType<ReceiptManager>();

        Button button = GetComponent<Button>();
        if (button != null)
        {
            button.onClick.AddListener(ClickedHandler);
        }

        Transform background = transform.Find("Background");
        backgroundImage = background != null
            ? background.GetComponent<Image>()
            : GetComponent<Image>();
    }

    public void SetValue(StoreType storeType, TransactionType transactionType)
    {
        SetValue(storeType, transactionType, string.Empty, storeType.ToString(), transactionType.ToString());
    }

    public void SetValue(
        StoreType storeType,
        TransactionType transactionType,
        string counterpartyName,
        string displayName,
        string transactionDisplayName)
    {
        this.transactionType = transactionType;
        this.storeType = storeType;
        this.counterpartyName = string.IsNullOrWhiteSpace(counterpartyName) ? string.Empty : counterpartyName.Trim();

        if (storeNameText != null)
        {
            storeNameText.text = displayName.ToUpperInvariant();
            storeNameText.enableWordWrapping = false;
            // storeNameText.overflowMode = TextOverflowModes.Ellipsis;
        }

        if (transactionTypeText != null)
        {
            transactionTypeText.text = transactionDisplayName.ToUpperInvariant();
            transactionTypeText.enableWordWrapping = false;
            // transactionTypeText.overflowMode = TextOverflowModes.Ellipsis;
        }

        RefreshVisualState(true);
    }

    public void ConfigureDeckCard(Vector2 anchoredPosition, float rotationZ, int siblingIndex)
    {
        if (rectTransform == null)
        {
            rectTransform = transform as RectTransform;
        }

        deckPosition = anchoredPosition;
        deckRotation = new Vector3(0f, 0f, rotationZ);
        deckConfigured = true;
        if (siblingIndex >= 0)
        {
            transform.SetSiblingIndex(siblingIndex);
        }

        if (rectTransform != null)
        {
            rectTransform.anchoredPosition = deckPosition;
        }

        transform.localRotation = Quaternion.Euler(deckRotation);
        RefreshVisualState(true);
    }

    public void SetSelected(bool selected)
    {
        if (isSelected == selected)
        {
            return;
        }

        isSelected = selected;
        RefreshVisualState(false);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovered = true;
        transform.SetAsLastSibling();
        RefreshVisualState(false);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
        if (receiptManager != null)
        {
            receiptManager.RestoreSmallReceiptDeckOrder();
        }

        RefreshVisualState(false);
    }

    private void Update()
    {
        if (!deckConfigured || rectTransform == null)
        {
            return;
        }

        Vector2 lift = Vector2.zero;
        if (isSelected)
        {
            lift += selectedOffset;
        }

        if (isHovered)
        {
            lift += hoverOffset;
        }

        Vector2 targetPosition = deckPosition + lift;
        float targetScale = isSelected ? selectedScale : isHovered ? hoverScale : 1f;
        Vector3 targetRotation = deckRotation + new Vector3(0f, 0f, isHovered ? hoverRotationOffset : 0f);

        float t = Time.unscaledDeltaTime * Mathf.Max(0f, animationSpeed);
        rectTransform.anchoredPosition = Vector2.Lerp(rectTransform.anchoredPosition, targetPosition, t);
        transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one * targetScale, t);
        transform.localRotation = Quaternion.Lerp(transform.localRotation, Quaternion.Euler(targetRotation), t);
    }

    private void ClickedHandler()
    {
        if (receiptManager != null)
        {
            receiptManager.ViewReceipt(storeType, transactionType, counterpartyName);
        }
    }

    private void RefreshVisualState(bool instant)
    {
        if (backgroundImage != null)
        {
            backgroundImage.color = isSelected
                ? selectedCardColor
                : isHovered ? hoverCardColor : normalCardColor;
        }

        if (isSelected || isHovered)
        {
            transform.SetAsLastSibling();
        }

        if (instant)
        {
            transform.localScale = Vector3.one * (isSelected ? selectedScale : 1f);
            transform.localRotation = Quaternion.Euler(deckRotation);
        }
    }
}
