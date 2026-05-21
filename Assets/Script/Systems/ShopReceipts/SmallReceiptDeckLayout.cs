using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[ExecuteAlways]
public class SmallReceiptDeckLayout : MonoBehaviour
{
    [SerializeField] private bool previewInEditor = true;
    [SerializeField] private bool applyChildSize = true;
    [SerializeField] private Vector2 cardSize = new Vector2(126f, 154f);
    [FormerlySerializedAs("firstCardAnchoredPosition")]
    [SerializeField] private Vector2 deckCenterAnchoredPosition = new Vector2(70f, 120f);
    [SerializeField] private Vector2 perCardOffset = new Vector2(0f, -42f);
    [SerializeField] private Vector2 alternatingCardOffset = new Vector2(7f, 0f);
    [SerializeField] private float firstCardRotation = -3f;
    [SerializeField] private float lastCardRotation = 3f;
    [SerializeField] private bool applySiblingOrder = true;

    public void ApplyLayout(IReadOnlyList<SmallReceipt> cards)
    {
        if (cards == null)
        {
            return;
        }

        for (int i = 0; i < cards.Count; i++)
        {
            SmallReceipt card = cards[i];
            if (card == null)
            {
                continue;
            }

            ApplySlot(card.transform as RectTransform, i, cards.Count);
            card.ConfigureDeckCard(GetAnchoredPosition(i, cards.Count), GetRotation(i, cards.Count), applySiblingOrder ? i : -1);
        }
    }

    [ContextMenu("Refresh Deck Preview")]
    public void RefreshPreview()
    {
        int count = transform.childCount;
        for (int i = 0; i < count; i++)
        {
            ApplySlot(transform.GetChild(i) as RectTransform, i, count);
        }
    }

    private void OnValidate()
    {
        if (!Application.isPlaying && previewInEditor)
        {
            RefreshPreview();
        }
    }

    private void OnTransformChildrenChanged()
    {
        if (!Application.isPlaying && previewInEditor)
        {
            RefreshPreview();
        }
    }

    private void ApplySlot(RectTransform rect, int index, int count)
    {
        if (rect == null)
        {
            return;
        }

        rect.anchoredPosition = GetAnchoredPosition(index, count);
        rect.localRotation = Quaternion.Euler(0f, 0f, GetRotation(index, count));

        if (applyChildSize)
        {
            rect.sizeDelta = cardSize;
        }

        if (applySiblingOrder)
        {
            rect.SetSiblingIndex(index);
        }
    }

    private Vector2 GetAnchoredPosition(int index, int count)
    {
        if (count <= 1)
        {
            return deckCenterAnchoredPosition;
        }

        float centeredIndex = index - ((count - 1) * 0.5f);
        Vector2 alternatingOffset = index % 2 == 1 ? alternatingCardOffset : Vector2.zero;
        int alternatingSlotCount = count / 2;
        Vector2 averageAlternatingOffset = alternatingCardOffset * (alternatingSlotCount / (float)count);

        return deckCenterAnchoredPosition + (perCardOffset * centeredIndex) + alternatingOffset - averageAlternatingOffset;
    }

    private float GetRotation(int index, int count)
    {
        if (count <= 1)
        {
            return (firstCardRotation + lastCardRotation) * 0.5f;
        }

        return Mathf.Lerp(firstCardRotation, lastCardRotation, index / (float)(count - 1));
    }
}
