using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class Item
{
    public ItemSO itemSO;
    public int amount;
    public ItemQuality quality;
    // Optional: final computed score for crafted food (0-100)
    public float finalQualityScore = 0f;

    public Item(ItemSO itemSO, int amount = 1, ItemQuality quality = ItemQuality.Common)
    {
        this.itemSO = itemSO;
        this.amount = amount;
        this.quality = quality;
    }

    // Copy constructor of this Item class
    public Item(Item other)
    {
        this.itemSO = other.itemSO;
        this.amount = other.amount;
        this.quality = other.quality;
    }

    public Item Clone(int amountOverride = -1)
    {
        return new Item(this)
        {
            amount = amountOverride >= 0 ? amountOverride : this.amount
        };
    }

    public int GetSellPrice()
    {
        return ItemSO.GetSellPriceFromQuality(this.itemSO, this.quality);
    }

    public Sprite GetSprite()
    {
        return itemSO != null ? itemSO.icon : null;
    }

    public bool IsStackable()
    {
        return itemSO != null && itemSO.stackable;
    }

    public string GetName()
    {
        if (itemSO == null) return "Unknown";
        // Hide quality label for items that don't use quality (e.g., weapons)
        if (itemSO != null && itemSO.UsesQuality())
            return $"{itemSO.GetDisplayName()} ({quality})";
        return itemSO.GetDisplayName();
    }

    public float GetQualityValue()
    {
        return ItemSO.QualityToValue(quality);
    }

    public void PickUp(int displayAmount = -1)
    {
        if (ItemPickupUIController.Instance != null)
        {
            int finalAmount = displayAmount >= 0 ? displayAmount : amount;
            ItemPickupUIController.Instance.ShowItemPickup(new Item(itemSO, finalAmount, quality));
        }
    }

    public Color GetQualityColor()
    {
        return ItemSO.GetQualityColor(quality, itemSO != null && itemSO.UsesQuality());
    }
}
