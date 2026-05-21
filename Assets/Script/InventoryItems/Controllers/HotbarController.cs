using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using UnityEngine.UI;

public class HotbarController : MonoBehaviour
{
    private const int FixedSlotCount = 3;

    [Header("References")]
    public GameObject slotPrefab;
    public GameObject uiItemPrefab;
    [SerializeField] private InputActionReference interactTab;
    public TMP_Text selectedItemName;
    public Sprite swordGhostIcon;
    public Sprite axeGhostIcon;
    public Sprite pickaxeGhostIcon;

    [Header("Settings")]
    public int columnCount = 9;

    private InventoryController inventoryController;
    private PlayerItemHandler playerItemHandler;
    private List<Slot> hotbarSlots = new();
    private int selectedHotbarIndex = 0;

    public int SelectedHotbarIndex => selectedHotbarIndex;

    private void OnEnable()
    {
        interactTab.action.performed += OnInteract;
    }
    private void OnDisable()
    {
        interactTab.action.performed -= OnInteract;
    }

    private void Start()
    {
        RefreshReferences();

        for (int i = 0; i < columnCount; i++)
        {
            GameObject slotObj = Instantiate(slotPrefab, transform);
            Slot slot = slotObj.GetComponent<Slot>();

            hotbarSlots.Add(slot);
            hotbarSlots[i].Setup(i, SelectHotbarSlot, true);
        }

        // Try refresh immediately
        RefreshHotbar();
        RefreshHighlight();

        // Retry after a tiny delay to ensure GameManager finished everything
        Invoke(nameof(FinalInitialRefresh), 0.1f);
    }

    private void FinalInitialRefresh()
    {
        RefreshHotbar();
        RefreshHighlight();
    }

    private void RefreshReferences()
    {
        inventoryController = FindObjectOfType<InventoryController>();

        if (Player.Instance != null)
        {
            playerItemHandler = Player.Instance.GetComponentInChildren<PlayerItemHandler>(true);
        }
        else if (GameManager.instance != null && GameManager.instance.player != null)
        {
            playerItemHandler = GameManager.instance.player.GetComponentInChildren<PlayerItemHandler>(true);
        }
        else
        {
            playerItemHandler = FindObjectOfType<PlayerItemHandler>();
        }

        if (inventoryController != null)
        {
            inventoryController.OnInventoryUpdated -= RefreshHotbar;
            inventoryController.OnInventoryUpdated += RefreshHotbar;
        }
    }

    private void OnInteract(InputAction.CallbackContext obj)
    {
        RefreshHighlight();
    }

    public void RefreshHotbar()
    {
        if (inventoryController == null) RefreshReferences();
        if (inventoryController == null) return;

        for (int i = 0; i < columnCount; i++)
        {
            if (i >= hotbarSlots.Count || hotbarSlots[i] == null) continue;

            Item itemData = GetItemByHotbarIndex(i);
            Slot slot = hotbarSlots[i];

            GameObject backgroundQualityObj = slot.transform.Find("BackgroundQualityParent/BackgroundQuality")?.gameObject;
            if (backgroundQualityObj != null)
            {
                backgroundQualityObj.GetComponent<Image>().color = (itemData != null && itemData.itemSO != null) ? itemData.GetQualityColor() : Color.clear;
            }

            slot.index = i;

            if (slot.currentItem != null)
            {
                Destroy(slot.currentItem);
                slot.currentItem = null;
            }

            if (itemData != null && itemData.itemSO != null)
            {
                ClearGhostIcon(slot);
                GameObject uiItem = Instantiate(uiItemPrefab, slot.transform);
                uiItem.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
                slot.currentItem = uiItem;

                var itemUI = uiItem.GetComponent<ItemUI>();
                if (itemUI != null)
                {
                    itemUI.Set(itemData);
                    if (backgroundQualityObj != null)
                        backgroundQualityObj.GetComponent<Image>().color = itemData.GetQualityColor();
                }

                var dragHandler = uiItem.GetComponent<ItemDragHandler>();
                if (dragHandler != null)
                {
                    if (i < FixedSlotCount)
                    {
                        dragHandler.enabled = false;
                    }
                    else
                    {
                        dragHandler.enabled = true;
                        dragHandler.slotIndex = i - FixedSlotCount;
                    }
                }
            }
            else if (i < FixedSlotCount)
            {
                ShowGhostIcon(slot, (ToolType)(i + 1));
            }
            else
            {
                ClearGhostIcon(slot);
            }
        }

        RefreshHighlight();
    }

    public void SelectHotbarSlot(int index)
    {
        // Disable Axe (1) and Pickaxe (2) for now
        if (index == 1 || index == 2) return;

        if (index >= FixedSlotCount)
        {
            Item target = GetItemByHotbarIndex(index);
            if (target == null || target.itemSO == null)
            {
                return;
            }
        }

        // Toggle selection: if already selected, deselect it (-1)
        if (selectedHotbarIndex == index)
        {
            selectedHotbarIndex = -1;
        }
        else
        {
            selectedHotbarIndex = index;
        }

        RefreshHighlight();
    }

    public void RefreshHighlight()
    {
        if (inventoryController == null) RefreshReferences();
        if (inventoryController == null) return;

        if (playerItemHandler == null && Player.Instance != null)
        {
            playerItemHandler = Player.Instance.GetComponentInChildren<PlayerItemHandler>(true);
        }

        bool hasSelection = selectedHotbarIndex >= 0;

        for (int i = 0; i < hotbarSlots.Count; i++)
        {
            if (hotbarSlots[i] == null) continue;

            bool isSelected = hasSelection && (i == selectedHotbarIndex % columnCount);
            hotbarSlots[i].SetHighlight(isSelected);

            if (isSelected)
            {
                Item item = GetItemByHotbarIndex(i);
                if (selectedItemName != null)
                {
                    selectedItemName.text = (item != null && item.itemSO != null) ? item.GetName() : "";
                }

                if (playerItemHandler != null)
                {
                    if (item != null && item.itemSO != null)
                    {
                        playerItemHandler.Equip(item);
                    }
                    else
                    {
                        playerItemHandler.Unequip();
                    }
                }
            }
        }

        // Handle unequipped state when no slot is selected
        if (!hasSelection)
        {
            if (selectedItemName != null) selectedItemName.text = "";
            if (playerItemHandler != null) playerItemHandler.Unequip();
        }
    }

    public void OnNumberSelect(InputAction.CallbackContext context)
    {
        int hotbarIndex = GetHotbarIndexFromContext(context);

        if (hotbarIndex >= 0 && hotbarIndex < columnCount)
        {
            if (hotbarIndex >= FixedSlotCount)
            {
                Item target = GetItemByHotbarIndex(hotbarIndex);
                if (target == null || target.itemSO == null)
                {
                    return;
                }
            }

            SelectHotbarSlot(hotbarIndex);
        }
    }

    private int GetHotbarIndexFromContext(InputAction.CallbackContext context)
    {
        if (context.control != null)
        {
            string path = context.control.path;
            int slashIndex = path.LastIndexOf('/');

            if (slashIndex >= 0 && slashIndex < path.Length - 1)
            {
                string token = path.Substring(slashIndex + 1).ToLowerInvariant();

                if (token.StartsWith("digit"))
                {
                    token = token.Substring(5);
                }
                else if (token.StartsWith("numpad"))
                {
                    token = token.Substring(6);
                }

                if (int.TryParse(token, out int parsedNumber))
                {
                    return parsedNumber - 1;
                }
            }
        }

        float numberPressed = context.ReadValue<float>();
        return Mathf.RoundToInt(numberPressed) - 1;
    }

    public void OnScroll(InputAction.CallbackContext context)
    {
        Vector2 scrollValue = context.ReadValue<Vector2>();
        int direction = (int)Mathf.Sign(scrollValue.y);
        if (direction == 0) return;

        int currentIndex = selectedHotbarIndex >= 0 ? selectedHotbarIndex : 0;
        int newIndex = currentIndex;

        // Find the next valid slot
        for (int i = 0; i < columnCount; i++)
        {
            newIndex += direction;

            if (newIndex >= columnCount) newIndex = 0;
            else if (newIndex < 0) newIndex = columnCount - 1;

            // Skip Axe (1) and Pickaxe (2)
            if (newIndex == 1 || newIndex == 2) continue;

            // For slots >= FixedSlotCount, check if they have items
            if (newIndex >= FixedSlotCount)
            {
                Item target = GetItemByHotbarIndex(newIndex);
                if (target == null || target.itemSO == null) continue;
            }

            break; // Found a valid slot
        }

        SelectHotbarSlot(newIndex);
    }

    private Item GetItemByHotbarIndex(int hotbarIndex)
    {
        if (GameManager.instance == null || GameManager.instance.inventoryData == null) return null;

        return GameManager.instance.inventoryData.GetItemAtHotbarIndex(hotbarIndex);
    }

    public Item GetSelectedItem()
    {
        return GetItemByHotbarIndex(selectedHotbarIndex);
    }

    public int GetSelectedInventorySlotIndex()
    {
        return selectedHotbarIndex < FixedSlotCount ? -1 : selectedHotbarIndex - FixedSlotCount;
    }

    private void ShowGhostIcon(Slot slot, ToolType toolType)
    {
        Image ghostImage = GetOrCreateGhostIcon(slot);
        Sprite ghostSprite = GetGhostSprite(toolType);

        if (ghostImage == null)
        {
            return;
        }

        ghostImage.sprite = ghostSprite;
        ghostImage.enabled = ghostSprite != null;
    }

    private void ClearGhostIcon(Slot slot)
    {
        Transform ghostTransform = slot.transform.Find("GhostIcon");
        if (ghostTransform == null) return;

        Image ghostImage = ghostTransform.GetComponent<Image>();
        if (ghostImage != null)
        {
            ghostImage.enabled = false;
        }
    }

    private Image GetOrCreateGhostIcon(Slot slot)
    {
        Transform ghostTransform = slot.transform.Find("GhostIcon");
        if (ghostTransform == null)
        {
            GameObject ghostObject = new GameObject("GhostIcon", typeof(RectTransform), typeof(Image));
            ghostTransform = ghostObject.transform;
            ghostTransform.SetParent(slot.transform, false);

            RectTransform ghostRect = ghostObject.GetComponent<RectTransform>();
            ghostRect.anchorMin = new Vector2(0.15f, 0.15f);
            ghostRect.anchorMax = new Vector2(0.85f, 0.85f);
            ghostRect.offsetMin = Vector2.zero;
            ghostRect.offsetMax = Vector2.zero;

            Image createdImage = ghostObject.GetComponent<Image>();
            createdImage.color = new Color(1f, 1f, 1f, 0.25f);
            createdImage.raycastTarget = false;
            return createdImage;
        }

        return ghostTransform.GetComponent<Image>();
    }

    private Sprite GetGhostSprite(ToolType toolType)
    {
        return toolType switch
        {
            ToolType.Sword => swordGhostIcon,
            ToolType.Axe => axeGhostIcon,
            ToolType.Pickaxe => pickaxeGhostIcon,
            _ => null,
        };
    }
}
