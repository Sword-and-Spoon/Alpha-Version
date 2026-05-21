using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ItemUI : MonoBehaviour
{
    public Image icon;
    public TMP_Text amountText;
    public Image background;

    public Color backgroundColor;

    private Item item;

    public void Set(Item item)
    {
        this.item = item;
        icon.sprite = item.GetSprite();
        Refresh();
        ApplyQualityVisuals();
    }

    public void Refresh()
    {
        amountText.text = item.amount > 1 ? item.amount.ToString() : "";
    }

    private void ApplyQualityVisuals()
    {
        backgroundColor = item.GetQualityColor();
    }
}
