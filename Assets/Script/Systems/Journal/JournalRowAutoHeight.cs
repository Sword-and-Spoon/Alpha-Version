using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class JournalRowAutoHeight : MonoBehaviour
{
    [SerializeField] private float minHeight = 50f;
    [SerializeField] private float verticalPadding = 8f;

    private RectTransform rootRect;
    private RectTransform amountHolderRect;
    private TMP_Text[] rowTexts;
    private LayoutElement layoutElement;
    private float lastHeight = -1f;
    private string lastTextSignature;

    private void OnEnable()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        Refresh();
    }

    private void OnValidate()
    {
        minHeight = Mathf.Max(1f, minHeight);
        verticalPadding = Mathf.Max(0f, verticalPadding);
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        ApplyHeight(false);
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
        amountHolderRect = transform.Find("AmountHolder") as RectTransform;
        rowTexts = GetComponentsInChildren<TMP_Text>(true);
        layoutElement = GetComponent<LayoutElement>();
        if (layoutElement == null)
        {
            layoutElement = gameObject.AddComponent<LayoutElement>();
        }
    }

    private void ApplyHeight(bool force)
    {
        if (rootRect == null || rowTexts == null || rowTexts.Length == 0)
        {
            ResolveReferences();
        }

        if (rootRect == null || rowTexts == null || rowTexts.Length == 0)
        {
            return;
        }

        string textSignature = string.Join("|", rowTexts.Select(t => t != null ? t.text : string.Empty));
        float rowHeight = CalculateRowHeight();
        if (!force && Mathf.Approximately(lastHeight, rowHeight) && lastTextSignature == textSignature)
        {
            return;
        }

        ApplyRectHeight(rootRect, rowHeight);
        ApplyLayoutHeight(rowHeight);
        ApplyTextHeight(rowHeight);
        ApplyAmountHolderHeight(rowHeight);

        lastHeight = rowHeight;
        lastTextSignature = textSignature;

        LayoutRebuilder.MarkLayoutForRebuild(rootRect);
        if (rootRect.parent is RectTransform parentRect)
        {
            LayoutRebuilder.MarkLayoutForRebuild(parentRect);
        }
    }

    private float CalculateRowHeight()
    {
        float preferredTextHeight = 0f;
        foreach (TMP_Text text in rowTexts)
        {
            if (text == null || string.IsNullOrEmpty(text.text))
            {
                continue;
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

            preferredTextHeight = Mathf.Max(preferredTextHeight, text.GetPreferredValues(text.text, width, 0f).y);
        }

        return Mathf.Ceil(Mathf.Max(minHeight, preferredTextHeight + verticalPadding));
    }

    private void ApplyTextHeight(float rowHeight)
    {
        foreach (TMP_Text text in rowTexts)
        {
            if (text == null)
            {
                continue;
            }

            RectTransform textRect = text.rectTransform;
            ApplyRectHeight(textRect, rowHeight);

            if (Mathf.Approximately(textRect.anchorMin.y, textRect.anchorMax.y))
            {
                textRect.anchoredPosition = new Vector2(textRect.anchoredPosition.x, -rowHeight * 0.5f);
            }
        }
    }

    private void ApplyAmountHolderHeight(float rowHeight)
    {
        if (amountHolderRect == null)
        {
            return;
        }

        ApplyRectHeight(amountHolderRect, rowHeight);
        if (Mathf.Approximately(amountHolderRect.anchorMin.y, amountHolderRect.anchorMax.y))
        {
            amountHolderRect.anchoredPosition = new Vector2(amountHolderRect.anchoredPosition.x, -rowHeight * 0.5f);
        }
    }

    private void ApplyRectHeight(RectTransform rect, float height)
    {
        if (rect == null)
        {
            return;
        }

        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
    }

    private void ApplyLayoutHeight(float rowHeight)
    {
        if (layoutElement == null)
        {
            return;
        }

        layoutElement.ignoreLayout = false;
        layoutElement.minHeight = rowHeight;
        layoutElement.preferredHeight = rowHeight;
        layoutElement.flexibleHeight = 0f;
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

#if UNITY_EDITOR
    private void MarkEditorObjectsDirty()
    {
        if (Application.isPlaying)
        {
            return;
        }

        MarkDirty(this);
        MarkDirty(rootRect);
        MarkDirty(amountHolderRect);
        MarkDirty(layoutElement);
        if (rowTexts == null)
        {
            return;
        }

        foreach (TMP_Text text in rowTexts)
        {
            MarkDirty(text);
            if (text != null)
            {
                MarkDirty(text.rectTransform);
            }
        }
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
