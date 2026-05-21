using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class RestaurantCounterDropZone : MonoBehaviour, IDropHandler
{
    public static event Action<RestaurantCounter, Item> OnFoodPlacedOnCounter;

    [HideInInspector][SerializeField] private RestaurantCounter counter;
    [HideInInspector][SerializeField] private RestaurantCounterSlotUI counterSlotUI;

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

    public void SetCounter(RestaurantCounter targetCounter)
    {
        counter = targetCounter;
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (eventData == null) return;

        ItemDragHandler dragHandler = eventData.pointerDrag != null ? eventData.pointerDrag.GetComponent<ItemDragHandler>() : null;
        if (dragHandler == null) return;

        TryPlaceFromDrag(dragHandler);
    }

    public bool TryPlaceFromDrag(ItemDragHandler dragHandler, bool moveSingleUnit = false)
    {
        if (dragHandler == null || counter == null) return false;
        if (dragHandler.IsFromCounterSource()) return false;

        if (TutorialManager.Instance != null
            && !TutorialManager.Instance.CanPlaceFoodOnCounter(out string blockedReason))
        {
            TutorialManager.Instance.ShowBlockedMessage(blockedReason, counter.transform.position);
            return false;
        }

        InventoryController inventory = dragHandler.inventoryController;
        if (inventory == null)
        {
            inventory = FindObjectOfType<InventoryController>();
        }

        if (inventory == null) return false;

        int sourceIndex = dragHandler.slotIndex;
        List<Item> items = inventory.GetItems();
        if (items == null || sourceIndex < 0 || sourceIndex >= items.Count) return false;

        Item sourceItem = items[sourceIndex];
        if (sourceItem == null || sourceItem.itemSO == null) return false;
        if (!counter.IsValidFood(sourceItem.itemSO)) return false;

        int sourceAmount = moveSingleUnit ? 1 : Mathf.Max(1, sourceItem.amount);

        if (!counter.HasFood)
        {
            int moveAmount = Mathf.Min(sourceAmount, counter.MaxStackAmount);
            return TryMoveStackToCounter(dragHandler, inventory, items, sourceIndex, moveAmount);
        }

        Item counterPlate = counter.GetStoredFoodCopy();
        if (counterPlate == null || counterPlate.itemSO == null)
        {
            return false;
        }

        bool sameDish = counterPlate.itemSO == sourceItem.itemSO && counterPlate.quality == sourceItem.quality;
        if (sameDish)
        {
            int capacity = Mathf.Max(0, counter.MaxStackAmount - counter.TotalFoodCount);
            if (capacity <= 0)
            {
                return false;
            }

            int moveAmount = Mathf.Min(sourceAmount, capacity);
            return TryMoveStackToCounter(dragHandler, inventory, items, sourceIndex, moveAmount);
        }

        if (moveSingleUnit)
        {
            // Shift+Click from inventory should only transfer one item to counter,
            // and should not trigger whole-stack swap with a different dish.
            return false;
        }

        int swapMoveAmount = Mathf.Min(sourceAmount, counter.MaxStackAmount);
        if (swapMoveAmount <= 0)
        {
            return false;
        }

        if (!TryTakeFromSourceSlot(items, sourceIndex, swapMoveAmount, out Item movedStack))
        {
            return false;
        }

        counter.ClearStoredFood();
        bool placed = counter.TryPlaceFood(movedStack, movedStack.amount);
        if (!placed)
        {
            counter.TryPlaceFood(counterPlate, counterPlate.amount);
            RestoreToSourceSlot(items, sourceIndex, movedStack);
            inventory.SwapItems(sourceIndex, sourceIndex);
            return false;
        }

        bool returnedOldCounterStack = inventory.AddItem(counterPlate);
        if (!returnedOldCounterStack)
        {
            counter.ClearStoredFood();
            counter.TryPlaceFood(counterPlate, counterPlate.amount);
            RestoreToSourceSlot(items, sourceIndex, movedStack);
            inventory.SwapItems(sourceIndex, sourceIndex);
            return false;
        }

        CompleteSuccessfulDrop(dragHandler, inventory, sourceIndex);
        NotifyFoodPlaced(movedStack);
        return true;
    }

    private bool TryMoveStackToCounter(
        ItemDragHandler dragHandler,
        InventoryController inventory,
        List<Item> items,
        int sourceIndex,
        int amount)
    {
        if (amount <= 0)
        {
            return false;
        }

        if (!TryTakeFromSourceSlot(items, sourceIndex, amount, out Item movedStack))
        {
            return false;
        }

        bool placed = counter.TryPlaceFood(movedStack, movedStack.amount);
        if (!placed)
        {
            RestoreToSourceSlot(items, sourceIndex, movedStack);
            inventory.SwapItems(sourceIndex, sourceIndex);
            return false;
        }

        CompleteSuccessfulDrop(dragHandler, inventory, sourceIndex);
        NotifyFoodPlaced(movedStack);
        return true;
    }

    private bool TryTakeFromSourceSlot(List<Item> items, int sourceIndex, int amount, out Item takenStack)
    {
        takenStack = null;

        if (items == null || sourceIndex < 0 || sourceIndex >= items.Count || amount <= 0)
        {
            return false;
        }

        Item source = items[sourceIndex];
        if (source == null || source.itemSO == null)
        {
            return false;
        }

        int takeAmount = Mathf.Min(amount, source.amount);
        if (takeAmount <= 0)
        {
            return false;
        }

        takenStack = new Item(source.itemSO, takeAmount, source.quality);

        source.amount -= takeAmount;
        if (source.amount <= 0)
        {
            items[sourceIndex] = null;
        }

        return true;
    }

    private void RestoreToSourceSlot(List<Item> items, int sourceIndex, Item stack)
    {
        if (items == null || stack == null || stack.itemSO == null)
        {
            return;
        }

        if (sourceIndex < 0 || sourceIndex >= items.Count)
        {
            return;
        }

        Item current = items[sourceIndex];
        if (current == null || current.itemSO == null)
        {
            items[sourceIndex] = new Item(stack.itemSO, stack.amount, stack.quality);
            return;
        }

        if (current.itemSO == stack.itemSO && current.quality == stack.quality && current.IsStackable())
        {
            current.amount += stack.amount;
            return;
        }

        // Fallback: preserve original stack in source slot when something else unexpectedly occupies the slot.
        items[sourceIndex] = new Item(stack.itemSO, stack.amount, stack.quality);
    }

    private void CompleteSuccessfulDrop(ItemDragHandler dragHandler, InventoryController inventory, int sourceIndex)
    {
        inventory.SwapItems(sourceIndex, sourceIndex);
        dragHandler.NotifyExternalDropConsumed();
        counterSlotUI?.RefreshNow();
    }

    private void AutoWire()
    {
        if (counterSlotUI == null)
        {
            counterSlotUI = GetComponentInParent<RestaurantCounterSlotUI>(true);
        }

        if (counter == null && counterSlotUI != null)
        {
            RestaurantCounter fallbackCounter = counterSlotUI.GetComponentInParent<RestaurantCounter>(true);
            if (fallbackCounter != null)
            {
                counter = fallbackCounter;
            }
        }
    }

    private void NotifyFoodPlaced(Item sourceStack)
    {
        if (counter == null || sourceStack == null || sourceStack.itemSO == null)
        {
            return;
        }

        Item placedSnapshot = new Item(sourceStack.itemSO, sourceStack.amount, sourceStack.quality);
        OnFoodPlacedOnCounter?.Invoke(counter, placedSnapshot);
    }
}
