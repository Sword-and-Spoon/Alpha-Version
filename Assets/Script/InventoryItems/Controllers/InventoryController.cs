using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class InventoryController : MonoBehaviour
{
    [Header("References")]
    public GameObject inventoryPanel;
    public GameObject slotPrefab;
    public GameObject uiItemPrefab;

    public event Action OnInventoryUpdated;
    public event Action OnMoneyChanged;

    private List<Slot> slots = new();

    // Reference to the persistent items from GameManager
    public List<Item> items => GameManager.instance != null && GameManager.instance.inventoryData != null ? GameManager.instance.inventoryData.items : new List<Item>();
    public int money
    {
        get => GameManager.instance != null && GameManager.instance.inventoryData != null ? GameManager.instance.inventoryData.money : 0;
        set { if (GameManager.instance != null && GameManager.instance.inventoryData != null) GameManager.instance.inventoryData.money = value; }
    }

    public int slotCount => GameManager.instance != null && GameManager.instance.inventoryData != null ? GameManager.instance.inventoryData.slotCount : 0;

    private GameObject FindInventoryPanelInScene()
    {
        GameObject panel = GameObject.Find("InventoryPage");
        if (panel != null) return panel;

        foreach (var grid in FindObjectsOfType<GridLayoutGroup>(true))
        {
            if (grid != null && grid.gameObject != null && grid.gameObject.name == "InventoryPage")
            {
                return grid.gameObject;
            }
        }

        return null;
    }

    private bool EnsureInventoryUIReady()
    {
        if (GameManager.instance == null || GameManager.instance.inventoryData == null)
        {
            return false;
        }

        if (inventoryPanel == null)
        {
            inventoryPanel = FindInventoryPanelInScene();
        }

        if (inventoryPanel == null || slotPrefab == null)
        {
            return false;
        }

        bool needsRebuild = slots == null || slots.Count != slotCount;

        if (!needsRebuild)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i] == null)
                {
                    needsRebuild = true;
                    break;
                }
            }
        }

        if (!needsRebuild)
        {
            return true;
        }

        if (slots == null)
        {
            slots = new List<Slot>();
        }
        else
        {
            slots.Clear();
        }

        foreach (Transform child in inventoryPanel.transform)
        {
            Destroy(child.gameObject);
        }

        for (int i = 0; i < slotCount; i++)
        {
            GameObject slotObj = Instantiate(slotPrefab);
            slotObj.transform.SetParent(inventoryPanel.transform, false);
            Slot slot = slotObj.GetComponent<Slot>();

            if (slot == null)
            {
                Destroy(slotObj);
                continue;
            }

            slot.index = i;
            // left click does nothing in inventory, right click attempts to consume
            slot.Setup(i, null, true);
            slots.Add(slot);
        }

        return slots.Count == slotCount;
    }

    private IEnumerator RebindInventoryUIAfterSceneLoad()
    {
        // Scene-local UI is recreated on each scene; force panel rebind after load.
        inventoryPanel = null;
        yield return null;

        const int maxAttempts = 12;
        for (int i = 0; i < maxAttempts; i++)
        {
            if (EnsureInventoryUIReady())
            {
                RefreshAllSlots();
                OnInventoryUpdated?.Invoke();
                yield break;
            }

            yield return null;
        }
    }

    private void ClearSlotUIAt(int index)
    {
        if (slots == null || index < 0 || index >= slots.Count) return;

        Slot slot = slots[index];
        if (slot == null) return;

        if (slot.currentItem != null)
        {
            Destroy(slot.currentItem);
        }

        slot.currentItem = null;
    }

    public List<Item> GetItems()
    {
        return items;
    }

    public int GetTotalAmount(ItemSO itemSO, ItemQuality minQuality = ItemQuality.Common)
    {
        int total = 0;
        foreach (var it in items)
        {
            if (it != null && it.itemSO == itemSO && it.quality >= minQuality)
                total += it.amount;
        }
        return total;
    }

    // Return list of slot indices that contain the given ItemSO and meet minQuality (sorted by quality desc then index)
    public List<int> GetSlotsWithItem(ItemSO itemSO, ItemQuality minQuality = ItemQuality.Common)
    {
        List<int> result = new List<int>();
        for (int i = 0; i < items.Count; i++)
        {
            var it = items[i];
            if (it != null && it.itemSO == itemSO && it.quality >= minQuality)
            {
                result.Add(i);
            }
        }

        // Sort by quality desc so UI can present higher-quality options first
        result.Sort((a, b) => items[b].quality.CompareTo(items[a].quality));
        return result;
    }

    // Craft by specifying exact slots and amounts to consume. selections: list of (slotIndex, amount)
    // Returns true if crafted successfully.
    public bool CraftWithSelection(ItemSO recipe, List<(int slotIndex, int amount)> selections, float successRate = 1f)
    {
        if (recipe == null || selections == null) return false;
        EnsureInventoryUIReady();

        // Validate that selections cover the required items and amounts
        // Build a map of required counts per ItemSO
        Dictionary<ItemSO, int> requiredMap = new Dictionary<ItemSO, int>();
        foreach (var req in recipe.requiredItems)
        {
            if (!requiredMap.ContainsKey(req.item)) requiredMap[req.item] = 0;
            requiredMap[req.item] += req.amount;
        }

        // Aggregate selected amounts per ItemSO and check minQuality constraints
        Dictionary<ItemSO, int> selectedMap = new Dictionary<ItemSO, int>();
        List<float> qualityValues = new List<float>();

        foreach (var sel in selections)
        {
            if (sel.slotIndex < 0 || sel.slotIndex >= items.Count) return false;
            var slot = items[sel.slotIndex];
            if (slot == null) return false;
            if (!selectedMap.ContainsKey(slot.itemSO)) selectedMap[slot.itemSO] = 0;
            selectedMap[slot.itemSO] += sel.amount;

            // collect quality values per unit
            for (int t = 0; t < sel.amount; t++) qualityValues.Add(slot.GetQualityValue());
        }

        // Check selected satisfy requiredMap
        foreach (var kv in requiredMap)
        {
            int need = kv.Value;
            int got = selectedMap.ContainsKey(kv.Key) ? selectedMap[kv.Key] : 0;
            if (got < need) return false;
        }

        // Remove selected amounts from slots (use same removal as RemoveItem but per slot)
        foreach (var sel in selections)
        {
            var slot = items[sel.slotIndex];
            slot.amount -= sel.amount;
            if (slot.amount <= 0)
            {
                items[sel.slotIndex] = null;
                ClearSlotUIAt(sel.slotIndex);
            }
        }

        RefreshAllSlots();
        OnInventoryUpdated?.Invoke();

        // Compute average quality from selected units
        float avgQuality = 1f;
        if (qualityValues.Count > 0)
        {
            float sum = 0f;
            foreach (var v in qualityValues) sum += v;
            avgQuality = sum / qualityValues.Count;
        }

        float baseQualityScore = avgQuality * 20f;
        float finalScore = baseQualityScore * Mathf.Clamp01(successRate);

        float effectiveQuality = avgQuality * Mathf.Clamp01(successRate);
        int finalQualityIndex = Mathf.Clamp(Mathf.CeilToInt(effectiveQuality), 1, 5);
        ItemQuality craftedQuality = (ItemQuality)finalQualityIndex;

        Item crafted = new Item(recipe, 1, craftedQuality);
        crafted.finalQualityScore = finalScore;
        AddItem(crafted);

        return true;
    }
    /// <summary>
    /// Remove the specified amounts from inventory slots without producing a crafted item.
    /// Use when starting a cooking session so ingredients are consumed immediately.
    /// </summary>
    public bool ConsumeIngredients(List<(int slotIndex, int amount)> selections)
    {
        if (selections == null) return false;

        foreach (var sel in selections)
        {
            if (sel.slotIndex < 0 || sel.slotIndex >= items.Count) return false;
            var slot = items[sel.slotIndex];
            if (slot == null || slot.amount < sel.amount) return false;

            slot.amount -= sel.amount;
            if (slot.amount <= 0)
            {
                items[sel.slotIndex] = null;
            }
        }

        RefreshAllSlots();
        OnInventoryUpdated?.Invoke();
        return true;
    }
    private void OnEnable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        EnsureInventoryUIReady();
        RefreshAllSlots();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        StartCoroutine(RebindInventoryUIAfterSceneLoad());
    }

    public void AddMoney(int amount)
    {
        money += amount;
        OnMoneyChanged?.Invoke();
    }

    public void SpendMoney(int amount)
    {
        money -= amount;
        OnMoneyChanged?.Invoke();
    }

    public bool AddItem(Item item)
    {
        if (item == null || item.itemSO == null) return false;
        if (item.amount <= 0) return true; // Successfully added 0 items!

        EnsureInventoryUIReady();

        if (TryAddOrReplaceFixedTool(item))
        {
            OnInventoryUpdated?.Invoke();
            return true;
        }

        if (item.itemSO.stackable)
        {
            for (int i = 0; i < slotCount; i++)
            {
                Item slotItem = items[i];
                // Only stack if same itemSO AND same quality
                if (slotItem != null && slotItem.itemSO == item.itemSO && slotItem.quality == item.quality)
                {
                    // Check if stack is not full
                    int currentAmount = slotItem.amount;
                    int maxAmount = item.itemSO.maxStack > 0 ? item.itemSO.maxStack : 99;

                    if (currentAmount < maxAmount)
                    {
                        int canAdd = maxAmount - currentAmount;
                        int toAdd = Mathf.Min(canAdd, item.amount);

                        slotItem.amount += toAdd;
                        item.amount -= toAdd;

                        if (item.amount <= 0)
                        {
                            RefreshAllSlots();
                            OnInventoryUpdated?.Invoke();
                            return true;
                        }
                    }
                }
            }
        }

        // If still have remaining amount, try to find an empty slot
        if (item.amount > 0)
        {
            for (int i = 0; i < slotCount; i++)
            {
                if (items[i] == null || items[i].itemSO == null)
                {
                    int maxAmount = item.itemSO.maxStack > 0 ? item.itemSO.maxStack : 99;
                    int toAdd = Mathf.Min(maxAmount, item.amount);

                    Item stored = new Item(item.itemSO, toAdd, item.quality);
                    items[i] = stored;
                    item.amount -= toAdd;

                    if (item.amount <= 0)
                    {
                        RefreshAllSlots();
                        OnInventoryUpdated?.Invoke();
                        return true;
                    }

                    // If we have more than maxStack, we continue the loop for next empty slot
                }
            }
        }

        RefreshAllSlots();
        OnInventoryUpdated?.Invoke();

        if (item.amount > 0)
        {
            Debug.Log("Inventory FULL or Stack Limit reached for remaining " + item.amount);
            return false;
        }

        return true;
    }

    private bool TryAddOrReplaceFixedTool(Item item)
    {
        ToolType toolType = item.itemSO.toolType;
        if (toolType == ToolType.None) return false;

        Item fixedSlotItem = new Item(item.itemSO, 1, item.quality);
        GameManager.instance.inventoryData.SetFixedSlot(toolType, fixedSlotItem);
        return true;
    }

    private void SpawnUIItem(int index, Item item)
    {
        // Safety: don't spawn UI for null items
        if (item == null || item.itemSO == null) return;
        if (!EnsureInventoryUIReady()) return;
        if (index < 0 || index >= slots.Count) return;

        Slot slot = slots[index];
        if (slot == null) return;

        if (slot.currentItem != null)
            Destroy(slot.currentItem);

        GameObject uiItem = Instantiate(uiItemPrefab);
        uiItem.transform.SetParent(slot.transform, false);
        uiItem.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;

        slot.currentItem = uiItem;

        // assign slot index for drag & click handlers
        var dragHandler = uiItem.GetComponent<ItemDragHandler>();
        if (dragHandler != null)
        {
            dragHandler.slotIndex = index;
            dragHandler.inventoryController = this;
        }

        var ui = uiItem.GetComponent<ItemUI>();
        if (ui != null)
        {
            ui.Set(item);
            GameObject slotBackground = slot.transform.Find("BackgroundQualityParent/BackgroundQuality")?.gameObject;
            if (slotBackground != null)
            {
                slotBackground.GetComponent<Image>().color = item.GetQualityColor();
            }
        }

    }

    public void SwapItems(int indexA, int indexB)
    {
        if (items == null)
        {
            return;
        }

        if (indexA < 0 || indexB < 0 || indexA >= items.Count || indexB >= items.Count)
        {
            Debug.LogWarning($"[InventoryController] SwapItems ignored for invalid indices: A={indexA}, B={indexB}, Count={items.Count}");
            return;
        }

        if (indexA != indexB)
        {
            Item temp = items[indexA];
            items[indexA] = items[indexB];
            items[indexB] = temp;
        }

        RefreshAllSlots();
        OnInventoryUpdated?.Invoke();
    }

    public void RefreshAllSlots()
    {
        if (!EnsureInventoryUIReady()) return;
        if (slots == null || slots.Count == 0) return;

        for (int i = 0; i < slotCount; i++)
        {
            if (i >= slots.Count || slots[i] == null) continue;

            // Clear existing UI item first
            if (slots[i].currentItem != null)
            {
                Destroy(slots[i].currentItem);
                slots[i].currentItem = null;
            }

            // Fix 'Unknown' item bug: If item exists but has no data, make it null
            if (items[i] != null && items[i].itemSO == null)
            {
                items[i] = null;
            }

            // Update background color based on item existence
            GameObject backgroundQualityObj = slots[i].transform.Find("BackgroundQualityParent/BackgroundQuality")?.gameObject;
            bool hasItem = items[i] != null && items[i].itemSO != null;

            if (backgroundQualityObj != null)
            {
                backgroundQualityObj.GetComponent<Image>().color = hasItem ? items[i].GetQualityColor() : Color.clear;
            }

            if (hasItem)
            {
                SpawnUIItem(i, items[i]);
            }
        }
    }

    public Item GetItemAt(int index)
    {
        if (index < 0 || index >= items.Count) return null;
        return items[index];
    }

    public bool RemoveItem(Item item)
    {
        if (item.itemSO == null || item.amount <= 0) return false;
        EnsureInventoryUIReady();
        int remaining = item.amount;

        // First pass: consume from slots that match exact quality
        for (int i = 0; i < items.Count && remaining > 0; i++)
        {
            var slotItem = items[i];
            if (slotItem == null) continue;
            if (slotItem.itemSO != item.itemSO) continue;
            if (slotItem.quality != item.quality) continue;

            int take = Math.Min(slotItem.amount, remaining);
            slotItem.amount -= take;
            remaining -= take;

            if (slotItem.amount <= 0)
            {
                items[i] = null;
                ClearSlotUIAt(i);
            }
        }

        // Second pass: if still remaining, consume any quality
        for (int i = 0; i < items.Count && remaining > 0; i++)
        {
            var slotItem = items[i];
            if (slotItem == null) continue;
            if (slotItem.itemSO != item.itemSO) continue;

            int take = Math.Min(slotItem.amount, remaining);
            slotItem.amount -= take;
            remaining -= take;

            if (slotItem.amount <= 0)
            {
                items[i] = null;
                ClearSlotUIAt(i);
            }
        }

        RefreshAllSlots();
        OnInventoryUpdated?.Invoke();

        return remaining <= 0;
    }

    /// Attempt to consume a single item at the given slot index if it is a consumable.
    /// Heals the player based on item quality and removes one unit from inventory.
    /// Returns true if consumption occurred.
    public bool TryConsumeItemAt(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= items.Count) return false;
        Item slotItem = items[slotIndex];
        if (slotItem == null || slotItem.itemSO == null) return false;
        if (slotItem.itemSO.itemType != ItemType.Consumable) return false;

        int healAmount = ItemSO.GetHealthRestoreFromQuality(slotItem.itemSO, slotItem.quality);
        PlayerHealth ph = GameManager.instance.player.GetComponent<PlayerHealth>();
        if (ph != null)
        {
            ph.Heal(healAmount);
            Debug.Log($"[Inventory] Consumed {slotItem.itemSO.GetDisplayName()} (quality={slotItem.quality}) restored {healAmount} HP");
        }

        // remove one copy of this quality
        RemoveItem(new Item(slotItem.itemSO, 1, slotItem.quality));
        ARQuestManager.Instance?.NotifyItemRemoved(slotItem.itemSO.itemId, 1);
        return true;
    }

    public bool CanCraft(ItemSO item)
    {
        if (item.requiredItems == null || item.requiredItems.Count == 0)
            return false;

        foreach (var req in item.requiredItems)
        {
            int totalAvailable = 0;
            foreach (var it in items)
            {
                if (it != null && it.itemSO == req.item && it.quality >= req.minQuality)
                    totalAvailable += it.amount;
            }

            if (totalAvailable < req.amount) return false;
        }

        return true;
    }

    public void Craft(ItemSO item, float successRate = 1f)
    {
        EnsureInventoryUIReady();
        if (!CanCraft(item))
        {
            Debug.Log("Not enough ingredients to craft " + item.name);
            return;
        }

        // Gather ingredient quality values and remove actual items from inventory
        List<float> qualityValues = new List<float>();

        foreach (var req in item.requiredItems)
        {
            int remaining = req.amount;

            // iterate through slots and take from those with matching itemSO and quality >= minQuality
            for (int i = 0; i < items.Count && remaining > 0; i++)
            {
                var slot = items[i];
                if (slot == null) continue;
                if (slot.itemSO != req.item) continue;
                if (slot.quality < req.minQuality) continue;

                int take = Mathf.Min(slot.amount, remaining);
                // record quality value once per unit
                for (int t = 0; t < take; t++) qualityValues.Add(slot.GetQualityValue());

                slot.amount -= take;
                remaining -= take;

                if (slot.amount <= 0)
                {
                    items[i] = null;
                    ClearSlotUIAt(i);
                }
            }

            if (remaining > 0)
            {
                Debug.LogError("Crafting logic error: insufficient items after CanCraft check.");
                return;
            }
        }

        RefreshAllSlots();
        OnInventoryUpdated?.Invoke();

        // Compute base quality score
        float avgQuality = 1f;
        if (qualityValues.Count > 0)
        {
            float sum = 0f;
            foreach (var v in qualityValues) sum += v;
            avgQuality = sum / qualityValues.Count;
        }

        float baseQualityScore = avgQuality * 20f;
        Debug.Log($"Base Quality Score: {baseQualityScore}");

        // Final score after mini-game success rate
        float finalScore = baseQualityScore * Mathf.Clamp01(successRate);
        Debug.Log($"Final Quality Score (after successRate {successRate}): {finalScore}");

        // Determine final quality by effectiveQuality = avgQuality * successRate, then ceil to favor higher grades
        float effectiveQuality = avgQuality * Mathf.Clamp01(successRate);
        int finalQualityIndex = Mathf.Clamp(Mathf.CeilToInt(effectiveQuality), 1, 5);
        ItemQuality craftedQuality = (ItemQuality)finalQualityIndex;

        Item crafted = new Item(item, 1, craftedQuality);
        crafted.finalQualityScore = finalScore;

        AddItem(crafted);
    }
}
