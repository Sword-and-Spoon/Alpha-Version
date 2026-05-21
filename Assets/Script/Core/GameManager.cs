using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager instance;
    public static GameManager Instance => instance;

    public GameObject player; // This can stay as a reference to the active player
    public GameObject pfItemScene;

    public InventoryController playerInventory
    {
        get
        {
            if (player == null) return null;
            Player playerComponent = player.GetComponent<Player>();
            return playerComponent != null ? playerComponent.GetInventoryController() : null;
        }
    }

    [Header("Persistent Data")]
    public InventoryData inventoryData;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);

            // Force load database early
            var db = ItemDatabaseSO.Instance;

            InitializePersistentData();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void InitializePersistentData()
    {
        // 1. Ensure inventoryData and its items list exist
        if (inventoryData == null)
        {
            inventoryData = new InventoryData(18);
        }

        if (inventoryData.items == null)
        {
            inventoryData.items = new List<Item>();
        }

        // 2. Ensure exactly slotCount elements
        int targetCount = inventoryData.slotCount > 0 ? inventoryData.slotCount : 18;
        inventoryData.slotCount = targetCount;

        while (inventoryData.items.Count < targetCount)
        {
            inventoryData.items.Add(null);
        }

        if (inventoryData.items.Count > targetCount)
        {
            inventoryData.items.RemoveRange(targetCount, inventoryData.items.Count - targetCount);
        }

        inventoryData.ValidateAndRepair();
        MigrateLegacyToolSlots();
        EnsureStarterFixedTools();
    }

    private void EnsureStarterFixedTools()
    {
        EnsureStarterTool(ToolType.Sword, "Sword");
        EnsureStarterTool(ToolType.Axe, "Axe");
        EnsureStarterTool(ToolType.Pickaxe, "Pickaxe");
    }

    private void EnsureStarterTool(ToolType toolType, string itemName)
    {
        if (inventoryData.GetFixedSlot(toolType) != null) return;

        ItemSO starterItemSO = ItemSO.GetItemByName(itemName);
        if (starterItemSO == null)
        {
            Debug.LogWarning($"[GameManager] Starter item not found for fixed slot: {itemName}");
            return;
        }

        inventoryData.SetFixedSlot(toolType, new Item(starterItemSO, 1));
    }

    public void ApplySave(SaveDataV1 data)
    {
        if (data == null || data.inventory == null) return;

        int slotCount = data.inventory.slotCount > 0 ? data.inventory.slotCount : 18;
        if (inventoryData == null) inventoryData = new InventoryData(slotCount);

        inventoryData.slotCount = slotCount;
        inventoryData.money = data.inventory.money;
        inventoryData.swordSlot = ItemSerializer.FromDTO(data.inventory.sword);
        inventoryData.axeSlot = ItemSerializer.FromDTO(data.inventory.axe);
        inventoryData.pickaxeSlot = ItemSerializer.FromDTO(data.inventory.pickaxe);

        if (inventoryData.items == null)
            inventoryData.items = new List<Item>();
        inventoryData.items.Clear();

        if (data.inventory.items != null)
        {
            foreach (var dto in data.inventory.items)
                inventoryData.items.Add(ItemSerializer.FromDTO(dto));
        }

        while (inventoryData.items.Count < slotCount) inventoryData.items.Add(null);
        if (inventoryData.items.Count > slotCount)
            inventoryData.items.RemoveRange(slotCount, inventoryData.items.Count - slotCount);

        inventoryData.ValidateAndRepair();
    }

    private void MigrateLegacyToolSlots()
    {
        if (inventoryData.items == null) return;

        for (int i = 0; i < inventoryData.items.Count; i++)
        {
            Item slotItem = inventoryData.items[i];
            if (slotItem == null || slotItem.itemSO == null) continue;

            ToolType toolType = slotItem.itemSO.toolType;
            if (toolType == ToolType.None) continue;

            if (inventoryData.GetFixedSlot(toolType) == null)
            {
                inventoryData.SetFixedSlot(toolType, new Item(slotItem.itemSO, 1, slotItem.quality));
            }

            inventoryData.items[i] = null;
        }

        inventoryData.ValidateAndRepair();
    }
}
