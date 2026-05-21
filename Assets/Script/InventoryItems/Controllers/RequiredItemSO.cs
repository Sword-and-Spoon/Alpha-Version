using UnityEngine;

[System.Serializable]
public class RequiredItemSO
{
    public ItemSO item;
    public int amount = 1;
    public ItemQuality minQuality = ItemQuality.Common;
}
