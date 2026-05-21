using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class ItemDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    private enum DragSourceKind
    {
        Inventory,
        Counter,
    }

    private Transform originalParent;
    private CanvasGroup canvasGroup;

    public int slotIndex;
    public InventoryController inventoryController;

    private bool externalDropConsumed;
    private DragSourceKind sourceKind = DragSourceKind.Inventory;
    private RestaurantCounter sourceCounter;
    private bool useVisualOnlyInventorySwap;
    private Vector3 dragLocalScale = Vector3.one;
    private Vector3 defaultLocalScale = Vector3.one;

    private static readonly List<RaycastResult> RaycastResultsBuffer = new List<RaycastResult>(32);

    private void Start()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        if (inventoryController == null)
        {
            inventoryController = FindObjectOfType<InventoryController>();
        }

        Image img = GetComponent<Image>();
        if (img != null)
        {
            img.raycastTarget = true;
        }

        RectTransform selfRect = GetComponent<RectTransform>();
        if (selfRect != null)
        {
            defaultLocalScale = selfRect.localScale;
            dragLocalScale = defaultLocalScale;
        }
    }

    public void ConfigureAsInventorySource(InventoryController inventory, int inventorySlotIndex)
    {
        sourceKind = DragSourceKind.Inventory;
        sourceCounter = null;
        inventoryController = inventory;
        slotIndex = inventorySlotIndex;
        useVisualOnlyInventorySwap = false;
        enabled = true;
    }

    public void SetUseVisualOnlyInventorySwap(bool enabled)
    {
        useVisualOnlyInventorySwap = enabled;
    }

    public void ConfigureAsCounterSource(RestaurantCounter counter, InventoryController inventory)
    {
        sourceKind = DragSourceKind.Counter;
        sourceCounter = counter;
        inventoryController = inventory != null ? inventory : inventoryController;
        slotIndex = -1;
        useVisualOnlyInventorySwap = false;
        enabled = true;
    }

    public bool IsFromCounterSource()
    {
        return sourceKind == DragSourceKind.Counter;
    }

    public void NotifyExternalDropConsumed()
    {
        externalDropConsumed = true;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;

        if (sourceKind == DragSourceKind.Counter && sourceCounter == null)
        {
            return;
        }

        externalDropConsumed = false;
        originalParent = transform.parent;
        dragLocalScale = defaultLocalScale;

        GameObject canvas = GameObject.Find("DragCanvas");
        if (canvas != null)
        {
            transform.SetParent(canvas.transform, true);
        }

        transform.SetAsLastSibling();

        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = false;
            canvasGroup.alpha = 0.5f;
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        transform.position = eventData.position;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;

        RestaurantCounterDropZone counterDropZone = ResolveCounterDropZone(eventData);
        Slot dropSlot = ResolveDropSlot(eventData);
        bool droppedOnCounterSlot = IsCounterSlot(dropSlot);

        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = true;
            canvasGroup.alpha = 1f;
        }

        if (externalDropConsumed)
        {
            externalDropConsumed = false;
            CleanupConsumedSourceAndDestroy();
            return;
        }

        if (sourceKind == DragSourceKind.Counter)
        {
            if (dropSlot != null && !droppedOnCounterSlot)
            {
                int targetInventoryIndex = ResolveInventoryIndexFromDropSlot(dropSlot);
                if (TryMoveCounterItemToInventory(targetInventoryIndex))
                {
                    CleanupConsumedSourceAndDestroy();
                    return;
                }
            }

            ReturnToOriginal();
            return;
        }

        if (counterDropZone != null)
        {
            if (counterDropZone.TryPlaceFromDrag(this))
            {
                CleanupConsumedSourceAndDestroy();
            }
            else
            {
                ReturnToOriginal();
            }

            return;
        }

        Slot originalSlot = originalParent != null ? originalParent.GetComponent<Slot>() : null;
        if (originalSlot == null)
        {
            ReturnToOriginal();
            return;
        }

        int originalDataIndex = slotIndex;

        if (dropSlot != null)
        {
            if (droppedOnCounterSlot || dropSlot == originalSlot)
            {
                ReturnToOriginal();
                return;
            }

            if (useVisualOnlyInventorySwap && IsCounterInventorySlot(originalSlot) && IsCounterInventorySlot(dropSlot))
            {
                int counterTargetDataIndex = ResolveInventoryIndexFromDropSlot(dropSlot);
                if (originalDataIndex == counterTargetDataIndex)
                {
                    ReturnToOriginal();
                    return;
                }

                if (TryMergeOrSwapCounterInventoryIndices(originalDataIndex, counterTargetDataIndex))
                {
                    CleanupConsumedSourceAndDestroy();
                }
                else
                {
                    ReturnToOriginal();
                }

                return;
            }

            int targetDataIndex = ResolveInventoryIndexFromDropSlot(dropSlot);
            if (!IsValidInventoryIndex(originalDataIndex) || !IsValidInventoryIndex(targetDataIndex))
            {
                ReturnToOriginal();
                return;
            }

            if (inventoryController != null)
            {
                inventoryController.SwapItems(originalDataIndex, targetDataIndex);
            }

            if (this != null && gameObject != null)
            {
                ReturnToOriginal();
            }

            return;
        }

        if (useVisualOnlyInventorySwap)
        {
            ReturnToOriginal();
            return;
        }

        if (!IsWithinInventory(eventData.position))
        {
            DropItem(originalSlot);
            ClearSlotQualityBackground(originalSlot);
        }
        else
        {
            ReturnToOriginal();
        }

        RectTransform selfEndRect = GetComponent<RectTransform>();
        if (selfEndRect != null)
        {
            NormalizeRectTransform(selfEndRect);
            selfEndRect.localScale = dragLocalScale;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left && IsShiftPressed())
        {
            if (sourceKind == DragSourceKind.Inventory)
            {
                RestaurantCounterDropZone counterDropZone = FindActiveCounterDropZone();
                if (counterDropZone != null)
                {
                    counterDropZone.TryPlaceFromDrag(this, true);
                }

                return;
            }

            if (sourceKind == DragSourceKind.Counter)
            {
                TryMoveCounterItemToInventoryAnywhere();
                return;
            }
        }

        if (eventData.button == PointerEventData.InputButton.Right)
        {
            if (sourceKind != DragSourceKind.Inventory) return;
            if (inventoryController == null) return;

            inventoryController.TryConsumeItemAt(slotIndex);
        }
    }

    private bool TryMoveCounterItemToInventory(int targetIndex)
    {
        if (sourceCounter == null) return false;

        if (inventoryController == null)
        {
            inventoryController = FindObjectOfType<InventoryController>();
        }

        if (inventoryController == null) return false;
        if (!IsValidInventoryIndex(targetIndex)) return false;

        Item counterItem = sourceCounter.GetStoredFoodCopy();
        if (counterItem == null || counterItem.itemSO == null) return false;

        List<Item> items = inventoryController.GetItems();
        Item targetItem = items[targetIndex];

        if (targetItem == null || targetItem.itemSO == null)
        {
            items[targetIndex] = counterItem;
            sourceCounter.ClearStoredFood();
            inventoryController.SwapItems(targetIndex, targetIndex);
            return true;
        }

        if (targetItem.itemSO == counterItem.itemSO && targetItem.quality == counterItem.quality && targetItem.IsStackable())
        {
            targetItem.amount += counterItem.amount;
            sourceCounter.ClearStoredFood();
            inventoryController.SwapItems(targetIndex, targetIndex);
            return true;
        }

        if (targetItem.amount == 1 && sourceCounter.IsValidFood(targetItem.itemSO))
        {
            Item targetCopy = new Item(targetItem.itemSO, 1, targetItem.quality);
            items[targetIndex] = counterItem;

            sourceCounter.ClearStoredFood();
            bool placed = sourceCounter.TryPlaceFood(targetCopy, 1);
            if (!placed)
            {
                items[targetIndex] = targetItem;
                sourceCounter.ClearStoredFood();
                sourceCounter.TryPlaceFood(counterItem, 1);
                inventoryController.SwapItems(targetIndex, targetIndex);
                return false;
            }

            inventoryController.SwapItems(targetIndex, targetIndex);
            return true;
        }

        return false;
    }

    private bool TryMoveCounterItemToInventoryAnywhere()
    {
        if (sourceCounter == null) return false;

        if (inventoryController == null)
        {
            inventoryController = FindObjectOfType<InventoryController>();
        }

        if (inventoryController == null) return false;

        if (!sourceCounter.TryTakeOneFood(out Item takenOne) || takenOne == null || takenOne.itemSO == null)
        {
            return false;
        }

        bool added = inventoryController.AddItem(takenOne);
        if (!added)
        {
            sourceCounter.TryPlaceFood(takenOne, 1);
            return false;
        }

        return true;
    }

    private int ResolveInventoryIndexFromDropSlot(Slot slot)
    {
        if (slot == null)
        {
            return -1;
        }

        if (slot.currentItem != null)
        {
            ItemDragHandler targetHandler = slot.currentItem.GetComponent<ItemDragHandler>();
            if (targetHandler != null && !targetHandler.IsFromCounterSource())
            {
                return targetHandler.slotIndex;
            }
        }

        return slot.index;
    }

    private bool TryMergeOrSwapCounterInventoryIndices(int sourceIndex, int targetIndex)
    {
        if (!IsValidInventoryIndex(sourceIndex) || !IsValidInventoryIndex(targetIndex))
        {
            return false;
        }

        if (sourceIndex == targetIndex)
        {
            return false;
        }

        List<Item> items = inventoryController != null ? inventoryController.GetItems() : null;
        if (items == null)
        {
            return false;
        }

        Item sourceItem = items[sourceIndex];
        if (sourceItem == null || sourceItem.itemSO == null)
        {
            return false;
        }

        Item targetItem = items[targetIndex];

        if (targetItem == null || targetItem.itemSO == null)
        {
            items[targetIndex] = sourceItem;
            items[sourceIndex] = null;
            inventoryController.SwapItems(targetIndex, targetIndex);
            return true;
        }

        if (targetItem.itemSO == sourceItem.itemSO && targetItem.quality == sourceItem.quality && sourceItem.IsStackable())
        {
            targetItem.amount += sourceItem.amount;
            items[sourceIndex] = null;
            inventoryController.SwapItems(targetIndex, targetIndex);
            return true;
        }

        inventoryController.SwapItems(sourceIndex, targetIndex);
        return true;
    }

    private bool TrySwapVisualOnly(Slot originalSlot, Slot dropSlot)
    {
        if (originalSlot == null || dropSlot == null)
        {
            return false;
        }

        if (originalSlot == dropSlot)
        {
            ReturnToOriginal();
            return true;
        }

        GameObject targetItem = dropSlot.currentItem;

        Image originalQuality = GetQualityImage(originalSlot);
        Image targetQuality = GetQualityImage(dropSlot);

        Color originalColor = originalQuality != null ? originalQuality.color : Color.clear;
        Color targetColor = targetQuality != null ? targetQuality.color : Color.clear;

        if (targetItem != null && targetItem != gameObject)
        {
            targetItem.transform.SetParent(originalSlot.transform, false);
            RectTransform targetRect = targetItem.GetComponent<RectTransform>();
            if (targetRect != null)
            {
                NormalizeRectTransform(targetRect);
            }

            originalSlot.currentItem = targetItem;
        }
        else
        {
            originalSlot.currentItem = null;
            ClearSlotQualityBackground(originalSlot);
        }

        transform.SetParent(dropSlot.transform, false);
        RectTransform selfRect = GetComponent<RectTransform>();
        if (selfRect != null)
        {
            NormalizeRectTransform(selfRect);
            selfRect.localScale = dragLocalScale;
        }

        dropSlot.currentItem = gameObject;

        if (targetQuality != null)
        {
            targetQuality.color = originalColor;
        }

        if (originalQuality != null)
        {
            originalQuality.color = (targetItem != null && targetItem != gameObject) ? targetColor : Color.clear;
        }

        return true;
    }

    private bool IsCounterInventorySlot(Slot slot)
    {
        return slot != null && slot.GetComponentInParent<RestaurantCounterInventoryView>() != null;
    }

    private Image GetQualityImage(Slot slot)
    {
        if (slot == null)
        {
            return null;
        }

        Transform qualityTransform = slot.transform.Find("BackgroundQualityParent/BackgroundQuality");
        return qualityTransform != null ? qualityTransform.GetComponent<Image>() : null;
    }

    private bool IsValidInventoryIndex(int index)
    {
        if (inventoryController == null) return false;

        List<Item> items = inventoryController.GetItems();
        if (items == null) return false;

        return index >= 0 && index < items.Count;
    }

    private void CleanupConsumedSourceAndDestroy()
    {
        if (originalParent != null)
        {
            Slot originalSlotCleanup = originalParent.GetComponent<Slot>();
            if (originalSlotCleanup != null && originalSlotCleanup.currentItem == gameObject)
            {
                originalSlotCleanup.currentItem = null;
            }
        }

        Destroy(gameObject);
    }

    private void ReturnToOriginal()
    {
        if (originalParent != null)
        {
            transform.SetParent(originalParent, false);
        }

        RectTransform rect = GetComponent<RectTransform>();
        if (rect != null)
        {
            NormalizeRectTransform(rect);
            rect.localScale = dragLocalScale;
        }
    }

    private bool IsWithinInventory(Vector2 mousePosition)
    {
        if (originalParent == null || originalParent.parent == null) return true;

        RectTransform inventoryRect = originalParent.parent.GetComponent<RectTransform>();
        if (inventoryRect == null) return true;

        return RectTransformUtility.RectangleContainsScreenPoint(inventoryRect, mousePosition);
    }

    private void DropItem(Slot originalSlot)
    {
        if (inventoryController == null) return;
        if (!IsValidInventoryIndex(slotIndex)) return;

        Item slotItem = inventoryController.GetItems()[slotIndex];
        if (slotItem == null || slotItem.itemSO == null) return;

        int amount = slotItem.amount;
        ItemSO itemSO = slotItem.itemSO;
        ItemQuality quality = slotItem.quality;

        Item dropCopy = new Item(itemSO, amount, quality);

        if (GameManager.instance != null && GameManager.instance.player != null)
        {
            ItemScene.SpawnItemScene(GameManager.instance.player.transform.position, dropCopy);
        }

        inventoryController.RemoveItem(new Item(itemSO, amount, quality));

        if (originalSlot != null)
        {
            originalSlot.currentItem = null;
        }

        Destroy(gameObject);
    }

    private void ClearSlotQualityBackground(Slot slot)
    {
        if (slot == null) return;

        Image qualityImage = GetQualityImage(slot);
        if (qualityImage != null)
        {
            qualityImage.color = Color.clear;
        }
    }

    private Slot ResolveDropSlot(PointerEventData eventData)
    {
        if (eventData == null) return null;

        GameObject currentRaycast = eventData.pointerCurrentRaycast.gameObject;
        Slot raycastSlot = ResolveSlotFrom(currentRaycast);
        if (raycastSlot != null) return raycastSlot;

        if (eventData.pointerEnter != null)
        {
            Slot pointerSlot = ResolveSlotFrom(eventData.pointerEnter);
            if (pointerSlot != null) return pointerSlot;
        }

        if (eventData.hovered != null)
        {
            for (int i = 0; i < eventData.hovered.Count; i++)
            {
                Slot hoveredSlot = ResolveSlotFrom(eventData.hovered[i]);
                if (hoveredSlot != null) return hoveredSlot;
            }
        }

        if (EventSystem.current != null)
        {
            RaycastResultsBuffer.Clear();
            EventSystem.current.RaycastAll(eventData, RaycastResultsBuffer);
            for (int i = 0; i < RaycastResultsBuffer.Count; i++)
            {
                Slot slot = ResolveSlotFrom(RaycastResultsBuffer[i].gameObject);
                if (slot != null) return slot;
            }
        }

        return ResolveSlotByScreenPoint(eventData.position);
    }

    private RestaurantCounterDropZone ResolveCounterDropZone(PointerEventData eventData)
    {
        if (eventData == null) return null;

        GameObject currentRaycast = eventData.pointerCurrentRaycast.gameObject;
        RestaurantCounterDropZone raycastZone = ResolveCounterDropZoneFrom(currentRaycast);
        if (raycastZone != null) return raycastZone;

        if (eventData.pointerEnter != null)
        {
            RestaurantCounterDropZone pointerDropZone = ResolveCounterDropZoneFrom(eventData.pointerEnter);
            if (pointerDropZone != null) return pointerDropZone;
        }

        if (eventData.hovered != null)
        {
            for (int i = 0; i < eventData.hovered.Count; i++)
            {
                RestaurantCounterDropZone hoveredDropZone = ResolveCounterDropZoneFrom(eventData.hovered[i]);
                if (hoveredDropZone != null) return hoveredDropZone;
            }
        }

        if (EventSystem.current != null)
        {
            RaycastResultsBuffer.Clear();
            EventSystem.current.RaycastAll(eventData, RaycastResultsBuffer);
            for (int i = 0; i < RaycastResultsBuffer.Count; i++)
            {
                RestaurantCounterDropZone zone = ResolveCounterDropZoneFrom(RaycastResultsBuffer[i].gameObject);
                if (zone != null) return zone;
            }
        }

        return ResolveCounterDropZoneByScreenPoint(eventData.position);
    }

    private Slot ResolveSlotFrom(GameObject source)
    {
        if (source == null) return null;
        if (IsSelfOrChild(source)) return null;

        return source.GetComponentInParent<Slot>();
    }

    private RestaurantCounterDropZone ResolveCounterDropZoneFrom(GameObject source)
    {
        if (source == null) return null;
        if (IsSelfOrChild(source)) return null;

        return source.GetComponentInParent<RestaurantCounterDropZone>();
    }

    private Slot ResolveSlotByScreenPoint(Vector2 screenPosition)
    {
        Slot[] slots = FindObjectsOfType<Slot>(true);
        for (int i = 0; i < slots.Length; i++)
        {
            Slot slot = slots[i];
            if (slot == null || !slot.gameObject.activeInHierarchy) continue;

            RectTransform rect = slot.GetComponent<RectTransform>();
            if (rect == null) continue;

            Canvas canvas = slot.GetComponentInParent<Canvas>();
            Camera eventCamera = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                ? canvas.worldCamera
                : null;

            if (RectTransformUtility.RectangleContainsScreenPoint(rect, screenPosition, eventCamera))
            {
                return slot;
            }
        }

        return null;
    }

    private RestaurantCounterDropZone ResolveCounterDropZoneByScreenPoint(Vector2 screenPosition)
    {
        RestaurantCounterDropZone[] zones = FindObjectsOfType<RestaurantCounterDropZone>(true);
        for (int i = 0; i < zones.Length; i++)
        {
            RestaurantCounterDropZone zone = zones[i];
            if (zone == null || !zone.isActiveAndEnabled || !zone.gameObject.activeInHierarchy) continue;

            RectTransform rect = zone.GetComponent<RectTransform>();
            if (rect == null) continue;

            Canvas canvas = zone.GetComponentInParent<Canvas>();
            Camera eventCamera = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                ? canvas.worldCamera
                : null;

            if (RectTransformUtility.RectangleContainsScreenPoint(rect, screenPosition, eventCamera))
            {
                return zone;
            }
        }

        return null;
    }

    private bool IsSelfOrChild(GameObject target)
    {
        if (target == null) return false;
        if (target == gameObject) return true;

        Transform targetTransform = target.transform;
        return targetTransform != null && targetTransform.IsChildOf(transform);
    }

    private bool IsCounterSlot(Slot slot)
    {
        return slot != null && slot.GetComponentInParent<RestaurantCounterSlotUI>() != null;
    }

    private RestaurantCounterDropZone FindActiveCounterDropZone()
    {
        RestaurantCounterDropZone[] zones = FindObjectsOfType<RestaurantCounterDropZone>(true);
        for (int i = 0; i < zones.Length; i++)
        {
            RestaurantCounterDropZone zone = zones[i];
            if (zone == null) continue;
            if (!zone.isActiveAndEnabled) continue;
            if (!zone.gameObject.activeInHierarchy) continue;

            return zone;
        }

        return null;
    }

    private void NormalizeRectTransform(RectTransform rect)
    {
        if (rect == null) return;

        rect.anchoredPosition = Vector2.zero;
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one;
    }

    private bool IsShiftPressed()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            return keyboard.shiftKey.isPressed || keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
        }
#endif

        return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
    }
}

