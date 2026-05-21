using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UI_Menu : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject recipeUI;
    [SerializeField] private CanBeCraftIn currentStation;

    private ItemDatabaseSO itemDatabase;
    private InventoryController playerInventory;
    private Transform menuSlotContainer;
    private Transform menuSlotTemplate;

    private Image recipeImage;
    private TMP_Text recipeNameText;
    private Transform ingredientContainer;
    private Transform ingredientTemplate;
    private Button craftButton;
    private Item currentItem; // currently selected craftable item

    private void Awake()
    {
        // Load database automatically
        itemDatabase = Resources.Load<ItemDatabaseSO>("Database/ItemDatabase");
        if (itemDatabase == null)
        {
            Debug.LogError("ItemDatabaseSO not found in Resources/Database!");
            return;
        }

        menuSlotContainer = transform.Find("MenuSlotContainer");
        menuSlotTemplate = menuSlotContainer.Find("MenuSlotTemplate");
        menuSlotTemplate.gameObject.SetActive(false);

        playerInventory = GameManager.instance.player.GetComponent<Player>().GetInventoryController();

        // Cache recipeUI elements
        recipeImage = recipeUI.transform.Find("Image").GetComponent<Image>();
        recipeNameText = recipeUI.transform.Find("MenuName").GetComponent<TMP_Text>();
        ingredientContainer = recipeUI.transform.Find("IngredientContainer");
        ingredientTemplate = ingredientContainer.Find("Text_Template");
        craftButton = recipeUI.transform.Find("Button_Cook").GetComponent<Button>();

        ingredientTemplate.gameObject.SetActive(false);
        recipeUI.SetActive(false);

        craftButton.onClick.AddListener(OnCraftClicked);
    }

    private void OnDisable()
    {
        CloseRecipeUI();
    }

    private void CloseRecipeUI()
    {
        recipeUI.SetActive(false);
        currentItem = null;

        foreach (Transform child in ingredientContainer)
        {
            if (child == ingredientTemplate) continue;
            Destroy(child.gameObject);
        }
    }

    private void Start()
    {
        GenerateMenuSlots();
    }

    private void GenerateMenuSlots()
    {
        foreach (Transform child in menuSlotContainer)
        {
            if (child == menuSlotTemplate) continue;
            Destroy(child.gameObject);
        }

        Vector2 startPos = menuSlotTemplate.GetComponent<RectTransform>().anchoredPosition;
        int x = 0;
        int y = 0;
        float menuSlotCellSize = 116f;

        foreach (ItemSO itemSO in itemDatabase.allItems)
        {
            if (itemSO.requiredItems == null || itemSO.requiredItems.Count == 0) continue;
            if (itemSO.stationType != currentStation) continue;

            Item item = new Item(itemSO);

            RectTransform slot = Instantiate(menuSlotTemplate, menuSlotContainer).GetComponent<RectTransform>();
            slot.gameObject.SetActive(true);

            slot.Find("Image").GetComponent<Image>().sprite = item.GetSprite();
            slot.anchoredPosition = startPos + new Vector2(x * menuSlotCellSize, -y * menuSlotCellSize);

            slot.GetComponent<Button_UI>().ClickFunc = () => ShowRecipe(item);

            x++;
            if (x > 5)
            {
                x = 0;
                y++;
            }
        }
    }

    private void ShowRecipe(Item item)
    {
        currentItem = item;
        recipeUI.SetActive(true);

        recipeImage.sprite = item.GetSprite();
        recipeNameText.text = item.itemSO.GetDisplayName();

        // Clear old ingredient texts
        foreach (Transform child in ingredientContainer)
        {
            if (child == ingredientTemplate) continue;
            Destroy(child.gameObject);
        }

        int index = 0;
        // Populate ingredient list
        foreach (RequiredItemSO req in item.itemSO.requiredItems)
        {
            int owned = playerInventory.GetTotalAmount(req.item, req.minQuality);

            RectTransform entry = Instantiate(ingredientTemplate, ingredientContainer).GetComponent<RectTransform>();
            entry.gameObject.SetActive(true);
            entry.anchoredPosition = new Vector2(0, -30f * index);
            entry.GetComponent<TMP_Text>().text = $"{req.item.GetDisplayName()} x{req.amount} ({owned})";

            index++;
        }

        craftButton.interactable = playerInventory.CanCraft(item.itemSO);
    }

    private void OnCraftClicked()
    {
        if (currentItem == null) return;

        if (playerInventory.CanCraft(currentItem.itemSO))
        {
            // For now, use full success (1.0). Later pass actual minigame success rate here.
            playerInventory.Craft(currentItem.itemSO, 1f);
            ShowRecipe(currentItem); // refresh UI
        }
        else
        {
            Debug.Log("Not enough ingredients to craft.");
        }
    }
}
