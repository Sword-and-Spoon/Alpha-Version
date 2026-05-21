using UnityEngine;

public static class ItemSerializer
{
    public static ItemDTO ToDTO(Item item)
    {
        if (item == null || item.itemSO == null)
            return new ItemDTO { present = false };

        return new ItemDTO
        {
            present = true,
            itemId = item.itemSO.itemId,
            itemName = item.itemSO.name,
            amount = item.amount,
            quality = (int)item.quality,
            finalQualityScore = item.finalQualityScore,
        };
    }

    public static Item FromDTO(ItemDTO dto, ItemDatabaseSO database = null)
    {
        if (dto == null || !dto.present) return null;

        ItemDatabaseSO db = database != null ? database : ItemDatabaseSO.Instance;
        if (db == null)
        {
            Debug.LogError("[ItemSerializer] ItemDatabaseSO not available; cannot restore item.");
            return null;
        }

        ItemSO so = null;
        if (!string.IsNullOrEmpty(dto.itemId))
            so = db.GetItemById(dto.itemId);
        if (so == null && !string.IsNullOrEmpty(dto.itemName))
            so = db.GetItemByName(dto.itemName);

        if (so == null)
        {
            Debug.LogWarning($"[ItemSerializer] Item not found in database: id='{dto.itemId}' name='{dto.itemName}'. Skipping.");
            return null;
        }

        if (!System.Enum.IsDefined(typeof(ItemQuality), dto.quality))
        {
            Debug.LogWarning($"[ItemSerializer] Invalid quality value {dto.quality} for item '{dto.itemName}'; defaulting to Normal.");
            dto.quality = (int)ItemQuality.Common;
        }

        var item = new Item(so, dto.amount, (ItemQuality)dto.quality);
        item.finalQualityScore = dto.finalQualityScore;
        return item;
    }
}
