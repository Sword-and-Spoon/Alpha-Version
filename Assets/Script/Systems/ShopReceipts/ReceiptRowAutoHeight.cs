using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ReceiptRowAutoHeight : MonoBehaviour
{
    [SerializeField] private float minHeight = 48f;
    [SerializeField] private float verticalPadding = 8f;
    [SerializeField] private float lineSpacing = 8f;
    [SerializeField] private float lineHeight = 3f;
    [SerializeField] private float fallbackRowWidth = 451f;
    [SerializeField] private float backgroundInset = 0f;

    private RectTransform rootRect;
    private RectTransform bodyRect;
    private RectTransform textHolderRect;
    private RectTransform backgroundRect;
    private RectTransform lineRect;
    private TMP_Text nameText;
    private TMP_Text priceText;
    private VerticalLayoutGroup rootVerticalLayout;
    private LayoutElement rootLayout;
    private LayoutElement bodyLayout;
    private LayoutElement textHolderLayout;
    private float lastAppliedHeight = -1f;
    private Vector2 lastRootSize;
    private string lastName;
    private string lastPrice;

    private void OnEnable()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        ResolveReferences();
        ApplyHeight(true);
    }

    private void OnValidate()
    {
        minHeight = Mathf.Max(1f, minHeight);
        verticalPadding = Mathf.Max(0f, verticalPadding);
        lineSpacing = Mathf.Max(0f, lineSpacing);
        lineHeight = Mathf.Max(0f, lineHeight);
        fallbackRowWidth = Mathf.Max(1f, fallbackRowWidth);
        backgroundInset = Mathf.Max(0f, backgroundInset);
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        ApplyHeight(false);
    }

    private void OnRectTransformDimensionsChange()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        ApplyHeight(true);
    }

    [ContextMenu("Refresh Row Height")]
    public void Refresh()
    {
        ResolveReferences();
        ApplyHeight(true);
        ForceRebuildNow();
#if UNITY_EDITOR
        MarkEditorObjectsDirty();
#endif
    }

    private void ResolveReferences()
    {
        rootRect = transform as RectTransform;
        bodyRect = FindDirectChildRect("Draggable");
        if (bodyRect == null)
        {
            bodyRect = FindDirectChildRect("NonDraggable");
        }

        textHolderRect = bodyRect != null
            ? bodyRect.Find("TextHolder") as RectTransform
            : transform.Find("TextHolder") as RectTransform;

        nameText = textHolderRect != null
            ? textHolderRect.Find("Text_Name")?.GetComponent<TMP_Text>()
            : GetComponentInChildren<TMP_Text>(true);

        priceText = textHolderRect != null
            ? textHolderRect.Find("Text_Price")?.GetComponent<TMP_Text>()
            : null;

        backgroundRect = bodyRect != null
            ? bodyRect.Find("Background") as RectTransform
            : transform.Find("Background") as RectTransform;

        lineRect = FindDirectChildRect("Line_H");
        rootVerticalLayout = rootRect != null ? rootRect.GetComponent<VerticalLayoutGroup>() : null;
        rootLayout = EnsureLayoutElement(rootRect);
        bodyLayout = EnsureLayoutElement(bodyRect);
        textHolderLayout = EnsureLayoutElement(textHolderRect);
    }

    private RectTransform FindDirectChildRect(string childName)
    {
        Transform child = transform.Find(childName);
        return child as RectTransform;
    }

    private LayoutElement EnsureLayoutElement(RectTransform rect)
    {
        if (rect == null)
        {
            return null;
        }

        LayoutElement layoutElement = rect.GetComponent<LayoutElement>();
        if (layoutElement == null)
        {
            layoutElement = rect.gameObject.AddComponent<LayoutElement>();
        }

        return layoutElement;
    }

    private void ApplyHeight(bool force)
    {
        if (rootRect == null || nameText == null)
        {
            ResolveReferences();
        }

        if (rootRect == null || nameText == null)
        {
            return;
        }

        string currentName = nameText.text;
        string currentPrice = priceText != null ? priceText.text : string.Empty;
        Vector2 currentSize = rootRect.rect.size;
        ApplyRootLayoutSettings();
        float rowWidth = CalculateRowWidth();
        ApplyLineSize(rowWidth);
        float bodyHeight = CalculateBodyHeight();
        float rootHeight = CalculateRootHeight(bodyHeight);

        if (!force
            && Mathf.Approximately(lastAppliedHeight, rootHeight)
            && lastRootSize == currentSize
            && lastName == currentName
            && lastPrice == currentPrice)
        {
            return;
        }

        float textAreaHeight = Mathf.Max(1f, bodyHeight - GetTextHolderVerticalPadding());
        ApplyRectWidth(rootRect, rowWidth);
        ApplyRectWidth(bodyRect, rowWidth);
        ApplyRectHeight(rootRect, rootHeight);
        ApplyRectHeight(bodyRect, bodyHeight);
        StretchRectToParent(textHolderRect);
        ApplyRectHeight(textHolderRect, bodyHeight);
        ApplyRectHeight(nameText.rectTransform, textAreaHeight);
        if (priceText != null)
        {
            ApplyRectHeight(priceText.rectTransform, textAreaHeight);
        }

        ApplyLayoutHeight(rootLayout, rootHeight);
        ApplyLayoutHeight(bodyLayout, bodyHeight);
        ApplyLayoutHeight(textHolderLayout, bodyHeight);
        ApplyLayoutWidth(rootLayout, rowWidth);
        ApplyLayoutWidth(bodyLayout, rowWidth);
        ApplyLayoutWidth(textHolderLayout, rowWidth);
        FitBackgroundToBody(rowWidth, bodyHeight);

        lastAppliedHeight = rootHeight;
        lastRootSize = currentSize;
        lastName = currentName;
        lastPrice = currentPrice;

        LayoutRebuilder.MarkLayoutForRebuild(rootRect);
        if (rootRect.parent is RectTransform parentRect)
        {
            LayoutRebuilder.MarkLayoutForRebuild(parentRect);
        }
    }

    private void ForceRebuildNow()
    {
        if (rootRect == null)
        {
            return;
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(rootRect);
        if (rootRect.parent is RectTransform parentRect)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect);
        }
    }

    private float CalculateBodyHeight()
    {
        float holderPadding = GetTextHolderVerticalPadding();
        float nameHeight = GetTextPreferredHeight(nameText);
        float priceHeight = GetTextPreferredHeight(priceText);
        float textHeight = Mathf.Max(nameHeight, priceHeight);
        return Mathf.Ceil(Mathf.Max(minHeight, textHeight + holderPadding + verticalPadding));
    }

    private float CalculateRootHeight(float bodyHeight)
    {
        float rootHeight = bodyHeight;
        if (rootVerticalLayout != null)
        {
            rootHeight += rootVerticalLayout.padding.top + rootVerticalLayout.padding.bottom;
        }

        if (lineRect != null && lineRect.gameObject.activeSelf)
        {
            rootHeight += Mathf.Max(lineHeight, Mathf.Abs(lineRect.rect.height));
            if (rootVerticalLayout != null && bodyRect != null && bodyRect.gameObject.activeSelf)
            {
                rootHeight += lineSpacing;
            }
        }

        return Mathf.Ceil(rootHeight);
    }

    private float CalculateRowWidth()
    {
        float width = fallbackRowWidth;

        if (lineRect != null)
        {
            width = Mathf.Max(width, Mathf.Abs(lineRect.rect.width), Mathf.Abs(lineRect.sizeDelta.x));
        }

        if (backgroundRect != null)
        {
            width = Mathf.Max(width, Mathf.Abs(backgroundRect.rect.width), Mathf.Abs(backgroundRect.sizeDelta.x));
        }

        if (textHolderRect != null)
        {
            width = Mathf.Max(width, Mathf.Abs(textHolderRect.rect.width), Mathf.Abs(textHolderRect.sizeDelta.x));
        }

        if (width <= 1f && rootRect != null)
        {
            width = Mathf.Max(width, Mathf.Abs(rootRect.rect.width), Mathf.Abs(rootRect.sizeDelta.x));
        }

        return Mathf.Ceil(Mathf.Max(1f, width));
    }

    private void ApplyRootLayoutSettings()
    {
        if (rootVerticalLayout == null)
        {
            return;
        }

        rootVerticalLayout.spacing = lineSpacing;
        rootVerticalLayout.childControlHeight = false;
        rootVerticalLayout.childForceExpandHeight = false;
        rootVerticalLayout.childScaleHeight = false;
    }

    private void ApplyLineSize(float rowWidth)
    {
        if (lineRect == null)
        {
            return;
        }

        lineRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, rowWidth);
        lineRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, lineHeight);
    }

    private float GetTextHolderVerticalPadding()
    {
        if (textHolderRect == null)
        {
            return 0f;
        }

        HorizontalLayoutGroup layoutGroup = textHolderRect.GetComponent<HorizontalLayoutGroup>();
        return layoutGroup != null
            ? layoutGroup.padding.top + layoutGroup.padding.bottom
            : 0f;
    }

    private float GetTextPreferredHeight(TMP_Text text)
    {
        if (text == null)
        {
            return 0f;
        }

        text.enableWordWrapping = true;
        RectTransform textRect = text.rectTransform;
        float width = textRect.rect.width;
        if (width <= 1f)
        {
            width = Mathf.Abs(textRect.sizeDelta.x);
        }

        if (width <= 1f)
        {
            width = text.preferredWidth;
        }

        return text.GetPreferredValues(text.text, width, 0f).y;
    }

    private void ApplyRectHeight(RectTransform rect, float height)
    {
        if (rect == null)
        {
            return;
        }

        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
    }

    private void ApplyRectWidth(RectTransform rect, float width)
    {
        if (rect == null)
        {
            return;
        }

        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
    }

    private void ApplyLayoutHeight(LayoutElement layoutElement, float height)
    {
        if (layoutElement == null)
        {
            return;
        }

        layoutElement.ignoreLayout = false;
        layoutElement.minHeight = height;
        layoutElement.preferredHeight = height;
        layoutElement.flexibleHeight = 0f;
    }

    private void ApplyLayoutWidth(LayoutElement layoutElement, float width)
    {
        if (layoutElement == null)
        {
            return;
        }

        layoutElement.ignoreLayout = false;
        layoutElement.minWidth = width;
        layoutElement.preferredWidth = width;
        layoutElement.flexibleWidth = 0f;
    }

    private void FitBackgroundToBody(float rowWidth, float bodyHeight)
    {
        if (backgroundRect == null)
        {
            return;
        }

        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.zero;
        backgroundRect.pivot = new Vector2(0.5f, 0.5f);
        backgroundRect.anchoredPosition = new Vector2(rowWidth * 0.5f, bodyHeight * 0.5f);
        backgroundRect.sizeDelta = new Vector2(
            Mathf.Max(1f, rowWidth - (backgroundInset * 2f)),
            Mathf.Max(1f, bodyHeight - (backgroundInset * 2f)));
    }

    private void StretchRectToParent(RectTransform rect)
    {
        StretchRectToParent(rect, 0f);
    }

    private void StretchRectToParent(RectTransform rect, float inset)
    {
        if (rect == null)
        {
            return;
        }

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
    }

#if UNITY_EDITOR
    private void MarkEditorObjectsDirty()
    {
        if (Application.isPlaying)
        {
            return;
        }

        MarkDirty(this);
        MarkDirty(rootRect);
        MarkDirty(bodyRect);
        MarkDirty(textHolderRect);
        MarkDirty(backgroundRect);
        MarkDirty(lineRect);
        MarkDirty(nameText);
        MarkDirty(priceText);
        MarkDirty(rootLayout);
        MarkDirty(bodyLayout);
        MarkDirty(textHolderLayout);
        MarkDirty(rootVerticalLayout);
    }

    private static void MarkDirty(Object target)
    {
        if (target != null)
        {
            UnityEditor.EditorUtility.SetDirty(target);
        }
    }
#endif
}
