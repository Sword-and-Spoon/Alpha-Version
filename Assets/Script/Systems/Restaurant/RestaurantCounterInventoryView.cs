using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class RestaurantCounterInventoryView : MonoBehaviour
{
    [Header("Layout")]
    [SerializeField] private Transform slotContainer;

    [HideInInspector] [SerializeField] private InventoryController inventoryController;
    [HideInInspector] [SerializeField] private GameObject slotPrefab;
    [HideInInspector] [SerializeField] private GameObject uiItemPrefab;

    private readonly List<Slot> uiSlots = new();
    private bool subscribed;

    private const int MinVisibleSlots = 9;

    private void Awake()
    {
        AutoWire();
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        AutoWire();
    }

    private void OnDisable()
    {
        Unbind();
    }

    public void Bind()
    {
        AutoWire();

        if (inventoryController != null && !subscribed)
        {
            inventoryController.OnInventoryUpdated += Refresh;
            subscribed = true;
        }

        Refresh();
    }

    public void Unbind()
    {
        if (subscribed && inventoryController != null)
        {
            inventoryController.OnInventoryUpdated -= Refresh;
        }

        subscribed = false;
        ClearRenderedSlots();
    }

    public void Refresh()
    {
        AutoWire();
        if (inventoryController == null || slotContainer == null || slotPrefab == null || uiItemPrefab == null)
        {
            Debug.LogWarning("[RestaurantCounterInventoryView] Missing refs. Check InventoryController/slotPrefab/uiItemPrefab/slotContainer.");
            return;
        }

        List<int> displayIndices = BuildDisplayIndices();
        EnsureSlotCount(displayIndices.Count);

        for (int uiIndex = 0; uiIndex < uiSlots.Count; uiIndex++)
        {
            Slot slot = uiSlots[uiIndex];
            if (slot == null) continue;

            int inventoryIndex = displayIndices[uiIndex];
            Item item = inventoryController.GetItemAt(inventoryIndex);
            BindSlot(slot, inventoryIndex, item);
        }
    }

    private void AutoWire()
    {
        if (slotContainer == null)
        {
            slotContainer = transform;
        }

        if (inventoryController == null)
        {
            inventoryController = FindObjectOfType<InventoryController>();
        }

        if (inventoryController != null)
        {
            if (slotPrefab == null)
            {
                slotPrefab = inventoryController.slotPrefab;
            }

            if (uiItemPrefab == null)
            {
                uiItemPrefab = inventoryController.uiItemPrefab;
            }
        }
    }

    private List<int> BuildDisplayIndices()
    {
        List<int> cookedFoodIndices = new List<int>();
        List<int> emptyIndices = new List<int>();

        if (inventoryController == null)
        {
            return cookedFoodIndices;
        }

        List<Item> items = inventoryController.GetItems();
        if (items == null)
        {
            return cookedFoodIndices;
        }

        for (int i = 0; i < items.Count; i++)
        {
            Item item = items[i];
            if (item == null || item.itemSO == null)
            {
                emptyIndices.Add(i);
                continue;
            }

            if (IsCookedFood(item))
            {
                cookedFoodIndices.Add(i);
            }
        }

        int targetCount = Mathf.Max(cookedFoodIndices.Count, MinVisibleSlots);
        int emptyCursor = 0;

        while (cookedFoodIndices.Count < targetCount && emptyCursor < emptyIndices.Count)
        {
            cookedFoodIndices.Add(emptyIndices[emptyCursor]);
            emptyCursor++;
        }

        return cookedFoodIndices;
    }

    // Rule from user: show only cooked/sellable dishes that are recipe-based (requiredItems > 0)
    private bool IsCookedFood(Item item)
    {
        if (item == null || item.itemSO == null)
        {
            return false;
        }

        return item.itemSO.requiredItems != null && item.itemSO.requiredItems.Count > 0;
    }

    private void EnsureSlotCount(int requiredCount)
    {
        while (uiSlots.Count < requiredCount)
        {
            GameObject slotObj = Instantiate(slotPrefab, slotContainer);
            Slot slot = slotObj.GetComponent<Slot>();
            if (slot == null)
            {
                Destroy(slotObj);
                break;
            }

            slot.Setup(0, null, true);
            uiSlots.Add(slot);
        }

        for (int i = uiSlots.Count - 1; i >= requiredCount; i--)
        {
            if (uiSlots[i] != null)
            {
                Destroy(uiSlots[i].gameObject);
            }

            uiSlots.RemoveAt(i);
        }
    }

    private void BindSlot(Slot slot, int inventoryIndex, Item item)
    {
        slot.index = inventoryIndex;
        slot.Setup(inventoryIndex, null, true);

        if (slot.currentItem != null)
        {
            Destroy(slot.currentItem);
            slot.currentItem = null;
        }

        Image qualityBackground = GetQualityBackground(slot);
        if (qualityBackground != null)
        {
            qualityBackground.color = Color.clear;
        }

        if (item == null || item.itemSO == null)
        {
            slot.SetHighlight(false);
            return;
        }

        GameObject uiItemObj = Instantiate(uiItemPrefab, slot.transform);
        RectTransform rect = uiItemObj.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.anchoredPosition = Vector2.zero;
            rect.localScale = Vector3.one;
        }

        slot.currentItem = uiItemObj;

        ItemDragHandler dragHandler = uiItemObj.GetComponent<ItemDragHandler>();
        if (dragHandler != null)
        {
            dragHandler.ConfigureAsInventorySource(inventoryController, inventoryIndex);
            dragHandler.SetUseVisualOnlyInventorySwap(true);
        }

        ItemUI itemUI = uiItemObj.GetComponent<ItemUI>();
        if (itemUI != null)
        {
            itemUI.Set(item);
        }

        if (qualityBackground != null)
        {
            qualityBackground.color = item.GetQualityColor();
        }
    }

    private void ClearRenderedSlots()
    {
        for (int i = 0; i < uiSlots.Count; i++)
        {
            Slot slot = uiSlots[i];
            if (slot == null)
            {
                continue;
            }

            if (slot.currentItem != null)
            {
                Destroy(slot.currentItem);
                slot.currentItem = null;
            }

            Image qualityBackground = GetQualityBackground(slot);
            if (qualityBackground != null)
            {
                qualityBackground.color = Color.clear;
            }
        }
    }

    private Image GetQualityBackground(Slot slot)
    {
        Transform qualityPath = slot.transform.Find("BackgroundQualityParent/BackgroundQuality");
        return qualityPath != null ? qualityPath.GetComponent<Image>() : null;
    }
}
