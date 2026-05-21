using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ItemDatabase", menuName = "Item/Database")]
public class ItemDatabaseSO : ScriptableObject
{
    public List<ItemSO> allItems = new List<ItemSO>();
    private static ItemDatabaseSO instance;
    private Dictionary<string, ItemSO> itemLookup;
    private Dictionary<string, ItemSO> itemIdLookup;

    public static ItemDatabaseSO Instance
    {
        get
        {
            if (instance == null)
            {
                // Try several common paths
                instance = Resources.Load<ItemDatabaseSO>("Database/ItemDatabase");
                if (instance == null) instance = Resources.Load<ItemDatabaseSO>("Database/Database");
                if (instance == null) instance = Resources.Load<ItemDatabaseSO>("ItemDatabase");
                if (instance == null) instance = Resources.Load<ItemDatabaseSO>("Database");

                if (instance == null)
                {
                    // Search for any object of this type in Resources if specific path fails
                    ItemDatabaseSO[] allDBs = Resources.LoadAll<ItemDatabaseSO>("");
                    if (allDBs.Length > 0) instance = allDBs[0];
                }

                if (instance == null)
                {
                    Debug.LogError("CRITICAL: ItemDatabase asset NOT FOUND in any Resources folder!");
                }
            }
            return instance;
        }
    }

    public ItemSO GetItemById(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        if (itemIdLookup == null) InitializeLookup();

        if (itemIdLookup != null && itemIdLookup.TryGetValue(id, out ItemSO item))
            return item;

        return null;
    }

    public ItemSO GetItemByName(string itemName)
    {
        if (itemLookup == null) InitializeLookup();

        // 1. Try exact match from dictionary (Fastest)
        if (itemLookup != null && itemLookup.TryGetValue(itemName, out ItemSO item))
        {
            return item;
        }

        // 2. Fallback: Search manually (Case-insensitive and DisplayName check)
        if (allItems != null)
        {
            foreach (var it in allItems)
            {
                if (it == null) continue;

                // Check asset name or display name (ignoring case)
                if (it.name.Equals(itemName, System.StringComparison.OrdinalIgnoreCase) ||
                    (it.displayName != null && it.displayName.Equals(itemName, System.StringComparison.OrdinalIgnoreCase)))
                {
                    return it;
                }
            }
        }

        // // --- DEBUG IF STILL NOT FOUND ---
        // if (allItems == null || allItems.Count == 0)
        // {
        //     Debug.LogError($"Database exists at '{this.name}' but the 'allItems' list is EMPTY! Please add items to it in the Inspector.");
        // }
        // else
        // {
        //     Debug.LogWarning($"Item '{itemName}' not found. Database has {allItems.Count} items. Available names:");
        //     foreach(var it in allItems) if(it != null) Debug.Log($"- {it.name} (Display: {it.displayName})");
        // }

        return null;
    }

    private void InitializeLookup()
    {
        itemLookup = new Dictionary<string, ItemSO>();
        itemIdLookup = new Dictionary<string, ItemSO>();
        if (allItems == null) return;

        foreach (var item in allItems)
        {
            if (item == null) continue;
            if (!itemLookup.ContainsKey(item.name))
                itemLookup.Add(item.name, item);
            if (!string.IsNullOrEmpty(item.itemId) && !itemIdLookup.ContainsKey(item.itemId))
                itemIdLookup.Add(item.itemId, item);
        }
    }
}
