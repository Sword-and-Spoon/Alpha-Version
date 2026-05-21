using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class ItemScene : MonoBehaviour
{
    private Item item;
    private SpriteRenderer spriteRenderer;
    private TextMeshPro textMeshPro;
    public bool collected = false;

    public int itemAmount;

    public static ItemScene SpawnItemScene(Vector3 position, Item item)
    {
        if (GameManager.instance.pfItemScene == null)
        {
            Debug.Log("ItemSO has no world prefab assigned");
            return null;
        }

        Transform transform = Instantiate(GameManager.instance.pfItemScene, position, Quaternion.identity).transform;
        ItemScene itemScene = transform.GetComponent<ItemScene>();
        itemScene.SetItem(item);
        itemScene.itemAmount = item.amount;
        return itemScene;
    }

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        textMeshPro = transform.Find("Text").GetComponent<TextMeshPro>();
    }

    public void SetItem(Item item)
    {
        this.item = item;
        spriteRenderer.sprite = item.GetSprite();
        if (textMeshPro != null) textMeshPro.text = item.amount > 1 ? item.amount.ToString() : "";
    }

    public Item GetItem() => item;

    public void DestroySelf() => Destroy(gameObject);
}
