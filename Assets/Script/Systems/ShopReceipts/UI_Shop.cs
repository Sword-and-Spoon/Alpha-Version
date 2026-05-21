using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Linq;

public class UI_Shop : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject descriptionContentUI;
    [SerializeField] private Button buyTab;
    [SerializeField] private Button sellTab;
    [SerializeField] private Button closeButton;

    [Header("AP / Credit Purchase")]
    [SerializeField] private Button buyOnCreditButton;
    [SerializeField] private string vendorName = "Town Shop";
    [SerializeField] private int creditDueDays = 3;

    [Header("Debts Tab")]
    [SerializeField] private Button debtsTab;
    [SerializeField] private Color payColor = new Color(0.2f, 0.6f, 0.2f);

    private APQuestData selectedDebt;

    [Header("Button Colors")]
    [SerializeField] private Color buyColor = Color.green;
    [SerializeField] private Color sellColor = Color.red;

    [Header("Tab Size Settings")]
    [SerializeField] private float activeHeight = 100f;
    [SerializeField] private float inactiveHeight = 80f;

    private ItemDatabaseSO itemDatabase;
    private InventoryController playerInventory;
    [SerializeField] private Transform itemSlotContainer; // Scroll View > Viewport > Content
    [SerializeField] private Transform itemSlotTemplate;

    private Image itemDescImage;
    private Image itemBackgroundQuality;
    private TMP_Text itemNameText;
    private TMP_Text itemDescTitleText;
    private TMP_Text itemDescText;
    private TMP_InputField itemAmountText;
    private TMP_Text itemTotalPriceText;

    private Button decreaseButton;
    private Button increaseButton;
    private Button button; // Buy / Sell / Pay Button

    public Tab tabStatus;
    // private Item inventoryItem; // the real item in inventory
    private Item currentItem;   // the UI clone for display
    private Dictionary<ItemSO, TMP_Text> itemSlotAmountTexts = new();
    private bool subscribedToInventoryUpdates;

    [HideInInspector][SerializeField] private APVendorNPC ownerInteractable;

    private int defaultAmount = 1;
    private int _itemAmount;
    private int itemAmount
    {
        get => _itemAmount;
        set
        {
            int clampedValue = value;

            if (tabStatus == Tab.Sell && currentItem != null)
            {
                int owned = 0;
                if (currentItem.itemSO.UsesQuality())
                {
                    // For quality items, count only this quality
                    owned = playerInventory.GetItems()
                        .FindAll(i => i != null && i.itemSO == currentItem.itemSO && i.quality == currentItem.quality)
                        .Sum(i => i.amount);
                }
                else
                {
                    // For non-quality items, count all
                    owned = playerInventory.GetItems()
                        .FindAll(i => i != null && i.itemSO == currentItem.itemSO)
                        .Sum(i => i.amount);
                }
                clampedValue = Mathf.Clamp(value, 1, Mathf.Max(1, owned));
            }
            else
            {
                clampedValue = Mathf.Clamp(value, 1, 999);
            }

            if (_itemAmount != clampedValue)
            {
                _itemAmount = clampedValue;
                OnItemAmountChanged();
            }
        }
    }

    private void Awake()
    {
        // Load database automatically from Resources
        itemDatabase = Resources.Load<ItemDatabaseSO>("Database/ItemDatabase");
        if (itemDatabase == null)
        {
            Debug.LogError("ItemDatabaseSO not found in Data/ItemDatabase!");
            return;
        }

        TryResolvePlayerInventory();

        // Cache UI elements
        itemDescImage = descriptionContentUI.transform.Find("Image").GetComponent<Image>();
        itemBackgroundQuality = descriptionContentUI.transform.Find("BackgroundQualityParent/BackgroundQuality").GetComponent<Image>();
        itemNameText = descriptionContentUI.transform.Find("Text_Name").GetComponent<TMP_Text>();
        itemDescTitleText = descriptionContentUI.transform.Find("Text_DescriptionTitle")?.GetComponent<TMP_Text>();
        itemDescText = descriptionContentUI.transform.Find("Text_Description").GetComponent<TMP_Text>();
        itemAmountText = descriptionContentUI.transform.Find("AdjustAmountContainer/Field").GetComponent<TMP_InputField>();
        itemTotalPriceText = descriptionContentUI.transform.Find("Text_Price").GetComponent<TMP_Text>();

        decreaseButton = descriptionContentUI.transform.Find("AdjustAmountContainer/Button_Decrease").GetComponent<Button>();
        increaseButton = descriptionContentUI.transform.Find("AdjustAmountContainer/Button_Increase").GetComponent<Button>();
        button = descriptionContentUI.transform.Find("ButtonLayout/Button").GetComponent<Button>();

        decreaseButton.onClick.AddListener(() => itemAmount--);
        increaseButton.onClick.AddListener(() => itemAmount++);
        button.onClick.AddListener(OnButtonClick);

        buyTab.onClick.AddListener(ShowBuyTab);
        sellTab.onClick.AddListener(ShowSellTab);

        closeButton.onClick.AddListener(CloseWindow);

        if (buyOnCreditButton != null)
            buyOnCreditButton.onClick.AddListener(OnBuyOnCreditClicked);

        if (debtsTab != null)
            debtsTab.onClick.AddListener(ShowDebtsTab);

        itemAmountText.onValueChanged.AddListener(GrabFromInputField);

        // SET DEFAULT
        ResetAmount();
        itemAmountText.text = defaultAmount.ToString();

        RefreshTabs();
    }

    public void BindOwner(APVendorNPC owner)
    {
        ownerInteractable = owner;
    }

    public void GrabFromInputField(string input)
    {
        if (int.TryParse(input, out int value))
        {
            itemAmount = value;
        }
    }

    private void OnItemAmountChanged()
    {
        itemAmountText.text = itemAmount.ToString();

        if (currentItem != null && currentItem.itemSO != null)
        {
            currentItem.amount = itemAmount;
            int price = tabStatus == Tab.Buy ? currentItem.itemSO.buyPrice : currentItem.GetSellPrice();
            int totalPrice = itemAmount * price;

            itemTotalPriceText.text = totalPrice.ToString();

            if (tabStatus == Tab.Buy)
            {
                button.interactable = playerInventory.money >= totalPrice;
                RefreshCreditButton();
            }
            else if (tabStatus == Tab.Sell)
            {
                int owned = playerInventory.GetItems()
                    .FindAll(i => i != null && i.itemSO == currentItem.itemSO && i.quality == currentItem.quality)
                    .Sum(i => i.amount);
                button.interactable = owned > 0;
            }
        }
    }

    private void CloseWindow()
    {
        if (ownerInteractable != null)
        {
            ownerInteractable.Interact();
            return;
        }
    }

    private void OnButtonClick()
    {
        if (tabStatus == Tab.Debts)
        {
            if (selectedDebt == null) return;
            bool success = APQuestManager.Instance?.TryRepayDebt(selectedDebt) ?? false;
            if (success)
            {
                selectedDebt = null;
                CloseDescUI();
                GenerateItemList();
            }
            return;
        }

        if (currentItem == null) return;

        if (tabStatus == Tab.Buy)
        {
            Item newItem = currentItem.Clone(itemAmount);

            TransactionManager.Instance.AddRecord(new TransactionRecord(newItem, newItem.itemSO.buyPrice * itemAmount, TransactionType.Buy, StoreType.TownShop));

            playerInventory.AddItem(newItem);
            playerInventory.SpendMoney(currentItem.itemSO.buyPrice * itemAmount);
            ResetAmount();
        }
        else if (tabStatus == Tab.Sell)
        {
            // For sell, create item with the selected quality
            Item newItem = new Item(currentItem.itemSO, itemAmount, currentItem.quality);

            TransactionManager.Instance.AddRecord(new TransactionRecord(newItem, newItem.GetSellPrice() * itemAmount, TransactionType.Sell, StoreType.TownShop));

            playerInventory.RemoveItem(newItem);
            playerInventory.AddMoney(newItem.GetSellPrice() * itemAmount);

            CreditSystem.Instance?.RecordSale(vendorName, currentItem.quality, itemAmount);

            // Check if any of this quality remain
            int ownedAmount = playerInventory.GetItems()
                .FindAll(i => i != null && i.itemSO == currentItem.itemSO && i.quality == currentItem.quality)
                .Sum(i => i.amount);

            if (ownedAmount <= 0)
            {
                CloseDescUI();
            }
        }

        ResetAmount();
        GenerateItemList();
        OnItemAmountChanged();
    }

    private void ResetAmount()
    {
        itemAmount = defaultAmount;
    }

    public void ShowBuyTab()
    {
        tabStatus = Tab.Buy;
        var btnText = button.transform.Find("Text").GetComponent<TMP_Text>();
        btnText.text = "Buy";
        SetButtonColor(buyColor);
        RefreshTabs();
    }

    public void ShowSellTab()
    {
        tabStatus = Tab.Sell;
        var btnText = button.transform.Find("Text").GetComponent<TMP_Text>();
        btnText.text = "Sell";
        SetButtonColor(sellColor);
        RefreshTabs();
    }

    private void RefreshTabs()
    {
        SetTabUI(buyTab, tabStatus == Tab.Buy);
        SetTabUI(sellTab, tabStatus == Tab.Sell);
        if (debtsTab != null) SetTabUI(debtsTab, tabStatus == Tab.Debts);

        CloseDescUI();
        GenerateItemList();
    }

    private void SetTabUI(Button btn, bool isActive)
    {
        RectTransform tabRect = btn.GetComponent<RectTransform>();

        SetTabHeight(tabRect, isActive ? activeHeight : inactiveHeight);
    }

    private void SetTabHeight(RectTransform rect, float height)
    {
        if (rect == null) return;

        Vector2 size = rect.sizeDelta;
        size.y = height;
        rect.sizeDelta = size;
    }

    private void SetButtonColor(Color color)
    {
        if (button.image != null)
        {
            button.image.color = color;
        }
    }

    private void OnEnable()
    {
        SubscribeToInventoryUpdates();
        ShowBuyTab();
    }

    private void OnDisable()
    {
        UnsubscribeFromInventoryUpdates();
        CloseDescUI();
    }

    private void OnDestroy()
    {
        UnsubscribeFromInventoryUpdates();
    }

    private bool TryResolvePlayerInventory()
    {
        if (playerInventory != null)
        {
            return true;
        }

        if (GameManager.instance == null || GameManager.instance.player == null)
        {
            return false;
        }

        Player player = GameManager.instance.player.GetComponent<Player>();
        if (player == null)
        {
            return false;
        }

        playerInventory = player.GetInventoryController();
        return playerInventory != null;
    }

    private void SubscribeToInventoryUpdates()
    {
        if (subscribedToInventoryUpdates || !TryResolvePlayerInventory())
        {
            return;
        }

        playerInventory.OnInventoryUpdated += UpdateOwnedAmounts;
        subscribedToInventoryUpdates = true;
    }

    private void UnsubscribeFromInventoryUpdates()
    {
        if (!subscribedToInventoryUpdates || playerInventory == null)
        {
            return;
        }

        playerInventory.OnInventoryUpdated -= UpdateOwnedAmounts;
        subscribedToInventoryUpdates = false;
    }

    private void CloseDescUI()
    {
        if (descriptionContentUI == null)
        {
            return;
        }

        currentItem = null;
        selectedDebt = null;
        descriptionContentUI.SetActive(false);
        if (itemDescTitleText != null) itemDescTitleText.text = "DESCRIPTION";
        itemAmountText?.gameObject.SetActive(true);
        decreaseButton?.gameObject.SetActive(true);
        increaseButton?.gameObject.SetActive(true);
    }

    private void GenerateItemList()
    {
        if (itemSlotContainer == null || itemSlotTemplate == null || itemDatabase == null)
        {
            return;
        }

        itemSlotAmountTexts.Clear();

        foreach (Transform child in itemSlotContainer)
        {
            if (child == itemSlotTemplate) continue;
            Destroy(child.gameObject);
        }

        if (tabStatus == Tab.Buy)
        {
            // Buy tab: show all items from database (without quality separation)
            List<ItemSO> itemsToDisplay = itemDatabase.allItems
                .FindAll(i => i.buyPrice > 0)
                .OrderBy(i => (int)i.defaultQuality)
                .ThenBy(i => i.GetDisplayName())
                .ToList();

            foreach (ItemSO itemSO in itemsToDisplay)
            {
                RectTransform slot = Instantiate(itemSlotTemplate, itemSlotContainer).GetComponent<RectTransform>();
                slot.gameObject.SetActive(true);

                slot.Find("Image").GetComponent<Image>().sprite = itemSO.icon;
                TMP_Text nameText = slot.Find("Text_Name").GetComponent<TMP_Text>();
                nameText.text = BuildItemDisplayName(itemSO, itemSO.GetDisplayName(), itemSO.defaultQuality, 0);


                TMP_Text priceText = slot.Find("Text_Price").GetComponent<TMP_Text>();
                priceText.text = itemSO.buyPrice.ToString();

                Image qualityIndicator = slot.Find("QualityIndicator").GetComponent<Image>();
                qualityIndicator.color = ItemSO.GetQualityColor(itemSO.defaultQuality, true);

                slot.GetComponent<Button_UI>().ClickFunc = () => ShowItemDetails(itemSO, itemSO.defaultQuality);
                ApplyMenuSlotAutoHeight(slot);
            }
        }
        else if (tabStatus == Tab.Sell)
        {
            // Sell tab: group items by quality and show each quality separately
            Dictionary<ItemSO, Dictionary<ItemQuality, int>> itemsByQuality = new();

            // Group inventory items by ItemSO and quality
            foreach (Item item in playerInventory.GetItems())
            {
                if (item == null || item.itemSO == null || item.itemSO.sellPrice <= 0) continue;

                if (!itemsByQuality.ContainsKey(item.itemSO))
                    itemsByQuality[item.itemSO] = new Dictionary<ItemQuality, int>();

                if (!itemsByQuality[item.itemSO].ContainsKey(item.quality))
                    itemsByQuality[item.itemSO][item.quality] = 0;

                itemsByQuality[item.itemSO][item.quality] += item.amount;
            }

            // Create slots for each quality variant
            foreach (var kvp in itemsByQuality)
            {
                ItemSO itemSO = kvp.Key;
                Dictionary<ItemQuality, int> qualityAmounts = kvp.Value;

                // If item uses quality, create separate slots for each quality
                if (itemSO.UsesQuality())
                {
                    foreach (var qualityKvp in qualityAmounts)
                    {
                        ItemQuality quality = qualityKvp.Key;
                        int amount = qualityKvp.Value;

                        RectTransform slot = Instantiate(itemSlotTemplate, itemSlotContainer).GetComponent<RectTransform>();
                        slot.gameObject.SetActive(true);

                        slot.Find("Image").GetComponent<Image>().sprite = itemSO.icon;
                        TMP_Text nameText = slot.Find("Text_Name").GetComponent<TMP_Text>();
                        nameText.text = BuildItemDisplayName(itemSO, itemSO.GetDisplayName(), quality, amount);

                        TMP_Text priceText = slot.Find("Text_Price").GetComponent<TMP_Text>();
                        priceText.text = ItemSO.GetSellPriceFromQuality(itemSO, quality).ToString();

                        Image qualityIndicator = slot.Find("QualityIndicator").GetComponent<Image>();
                        qualityIndicator.color = ItemSO.GetQualityColor(quality, true);

                        itemSlotAmountTexts[itemSO] = nameText;

                        slot.GetComponent<Button_UI>().ClickFunc = () => ShowItemDetails(itemSO, quality);
                        ApplyMenuSlotAutoHeight(slot);
                    }
                }
                else
                {
                    // Non-quality items: show once
                    int totalAmount = 0;
                    foreach (var qualityAmount in qualityAmounts.Values)
                        totalAmount += qualityAmount;

                    RectTransform slot = Instantiate(itemSlotTemplate, itemSlotContainer).GetComponent<RectTransform>();
                    slot.gameObject.SetActive(true);

                    slot.Find("Image").GetComponent<Image>().sprite = itemSO.icon;
                    TMP_Text nameText = slot.Find("Text_Name").GetComponent<TMP_Text>();
                    nameText.text = BuildItemDisplayName(itemSO, itemSO.GetDisplayName(), ItemQuality.Common, totalAmount);

                    TMP_Text priceText = slot.Find("Text_Price").GetComponent<TMP_Text>();
                    priceText.text = itemSO.sellPrice.ToString();

                    Image qualityIndicator = slot.Find("QualityIndicator").GetComponent<Image>();
                    qualityIndicator.color = ItemSO.GetQualityColor(ItemQuality.Common, false); // no quality color

                    itemSlotAmountTexts[itemSO] = nameText;

                    slot.GetComponent<Button_UI>().ClickFunc = () => ShowItemDetails(itemSO, ItemQuality.Common);
                    ApplyMenuSlotAutoHeight(slot);
                }
            }
        }
        else if (tabStatus == Tab.Debts)
        {
            var debts = APQuestManager.Instance?.GetActiveDebts();
            if (debts == null) return;

            foreach (var debt in debts)
            {
                if (debt.vendorName != vendorName) continue;

                RectTransform slot = Instantiate(itemSlotTemplate, itemSlotContainer).GetComponent<RectTransform>();
                slot.gameObject.SetActive(true);

                ItemSO itemSO = ResolveDebtItemSO(debt);
                if (itemSO != null)
                    slot.Find("Image").GetComponent<Image>().sprite = itemSO.icon;

                int total = debt.principalAmount + debt.accruedInterest;

                TMP_Text nameText = slot.Find("Text_Name").GetComponent<TMP_Text>();
                nameText.text = BuildItemDisplayName(itemSO, debt.itemName, debt.quality, debt.quantity);

                TMP_Text priceText = slot.Find("Text_Price").GetComponent<TMP_Text>();
                priceText.text = total.ToString("N0");

                Image qualityIndicator = slot.Find("QualityIndicator").GetComponent<Image>();
                qualityIndicator.color = debt.isOverdue
                    ? new Color(0.78f, 0.18f, 0.12f)
                    : new Color(0.95f, 0.75f, 0.2f);

                APQuestData captured = debt;
                slot.GetComponent<Button_UI>().ClickFunc = () => ShowDebtDetails(captured);
                ApplyMenuSlotAutoHeight(slot);
            }
        }

        RectTransform content = itemSlotContainer as RectTransform;
        if (content != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        }
    }

    private void ApplyMenuSlotAutoHeight(RectTransform slot)
    {
        if (slot == null)
        {
            return;
        }

        TMP_Text nameText = slot.Find("Text_Name")?.GetComponent<TMP_Text>();
        if (nameText == null)
        {
            return;
        }

        nameText.enableWordWrapping = true;
        nameText.overflowMode = TextOverflowModes.Overflow;
        nameText.ForceMeshUpdate();

        RectTransform textRect = nameText.rectTransform;
        float textWidth = textRect.rect.width > 0f ? textRect.rect.width : Mathf.Abs(textRect.sizeDelta.x);
        float preferredTextHeight = nameText.GetPreferredValues(nameText.text, textWidth, 0f).y;
        float textHeight = Mathf.Max(65.3f, Mathf.Ceil(preferredTextHeight));
        float slotHeight = Mathf.Max(100f, 100f + Mathf.Max(0f, textHeight - 65.3f) + 8f);

        textRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, textHeight);
        slot.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, slotHeight);

        LayoutElement layoutElement = slot.GetComponent<LayoutElement>();
        if (layoutElement == null)
        {
            layoutElement = slot.gameObject.AddComponent<LayoutElement>();
        }

        layoutElement.minHeight = slotHeight;
        layoutElement.preferredHeight = slotHeight;
        layoutElement.flexibleHeight = 0f;

        SetChildHeight(slot, "Menu", slotHeight);
        SetChildHeight(slot, "Background", slotHeight);
        SetChildHeight(slot, "Parent", slotHeight);
        SetChildHeight(slot, "MenuBorder", slotHeight + 2f);
        SetChildHeight(slot, "QualityIndicator", Mathf.Max(55.1f, slotHeight - 44.9f));
    }

    private void SetChildHeight(RectTransform root, string childName, float height)
    {
        Transform child = root.Find(childName);
        RectTransform rect = child as RectTransform;
        if (rect == null)
        {
            return;
        }

        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
    }

    private string BuildItemDisplayName(ItemSO itemSO, string fallbackName, ItemQuality quality, int amount)
    {
        string displayName = itemSO != null ? itemSO.GetDisplayName() : fallbackName;
        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = "Unknown";
        }

        if (itemSO != null && itemSO.UsesQuality())
        {
            displayName = $"{displayName} ({quality})";
        }

        if (amount > 0)
        {
            displayName = $"{displayName} ({amount})";
        }

        return displayName;
    }

    private ItemSO ResolveDebtItemSO(APQuestData debt)
    {
        return debt != null ? ItemSO.GetItemByName(debt.itemName) : null;
    }

    private void ShowItemDetails(ItemSO itemSO, ItemQuality quality = ItemQuality.Common)
    {
        currentItem = new Item(itemSO, 1, quality);

        descriptionContentUI.SetActive(true);
        if (itemDescTitleText != null) itemDescTitleText.text = "DESCRIPTION";
        itemDescImage.sprite = itemSO.icon;
        itemBackgroundQuality.color = ItemSO.GetQualityColor(quality, itemSO.UsesQuality());
        itemDescText.text = itemSO.description;

        // Show quality in name if applicable
        string displayName = itemSO.GetDisplayName();
        if (itemSO.UsesQuality())
            displayName = $"{displayName} ({quality})";

        int ownedAmount = 0;
        if (tabStatus == Tab.Sell && itemSO.UsesQuality())
        {
            // For sell tab with quality items, show only amount of this quality
            ownedAmount = playerInventory.GetItems()
                .FindAll(i => i != null && i.itemSO == itemSO && i.quality == quality)
                .Sum(i => i.amount);
        }
        else
        {
            // For buy tab or non-quality items, show total
            ownedAmount = playerInventory.GetItems()
                .FindAll(i => i != null && i.itemSO == itemSO)
                .Sum(i => i.amount);
        }

        itemNameText.text = $"{displayName} ({ownedAmount})";

        ResetAmount();
        OnItemAmountChanged();
        RefreshCreditButton();
    }

    private void ShowDebtDetails(APQuestData debt)
    {
        selectedDebt = debt;
        currentItem = null;

        descriptionContentUI.SetActive(true);

        // Title
        if (itemDescTitleText != null) itemDescTitleText.text = "DEBT SUMMARY";

        // Icon & quality background
        ItemSO itemSO = ResolveDebtItemSO(debt);
        if (itemSO != null) itemDescImage.sprite = itemSO.icon;
        itemBackgroundQuality.color = debt.isOverdue
            ? new Color(0.78f, 0.18f, 0.12f, 0.4f)
            : new Color(0.95f, 0.75f, 0.2f, 0.4f);

        // Name row
        itemNameText.text = BuildItemDisplayName(itemSO, debt.itemName, debt.quality, debt.quantity);

        // Description block
        int total = debt.principalAmount + debt.accruedInterest;
        int remaining = debt.dueTotalDay - DailyJournalRules.GetCurrentAccountingDay();

        string statusLine = debt.isOverdue
            ? $"<color=#C72E1F>OVERDUE  (+{debt.accruedInterest:N0} G interest)</color>"
            : $"<color=#147B14>Due in {remaining} day(s)</color>";

        itemDescText.text =
            $"Principal: {debt.principalAmount:N0} G\n" +
            $"Interest: {debt.accruedInterest:N0} G\n" +
            $"─────────────────\n" +
            $"{statusLine}";

        // Total price
        itemTotalPriceText.text = $"{total:N0}";

        // Hide amount controls
        itemAmountText.gameObject.SetActive(false);
        decreaseButton.gameObject.SetActive(false);
        increaseButton.gameObject.SetActive(false);
        if (buyOnCreditButton != null) buyOnCreditButton.gameObject.SetActive(false);

        // PAY button
        bool canAfford = (playerInventory?.money ?? 0) >= total;
        button.interactable = canAfford;
        button.image.color = canAfford ? payColor : Color.gray;
        var btnText = button.transform.Find("Text").GetComponent<TMP_Text>();
        if (btnText != null) btnText.text = canAfford ? $"PAY" : "Not Enough Gold";
    }

    private void RefreshCreditButton()
    {
        if (buyOnCreditButton == null) return;

        bool visible = tabStatus == Tab.Buy && currentItem != null;
        buyOnCreditButton.gameObject.SetActive(visible);

        if (!visible) return;

        bool hasCredit = CreditSystem.Instance?.HasCreditFor(vendorName, currentItem.quality) ?? false;
        buyOnCreditButton.interactable = hasCredit;
    }

    private void OnBuyOnCreditClicked()
    {
        if (currentItem == null) return;
        if (APQuestManager.Instance == null || CreditSystem.Instance == null) return;
        if (!CreditSystem.Instance.HasCreditFor(vendorName, currentItem.quality)) return;

        Item newItem = currentItem.Clone(itemAmount);
        int totalCost = currentItem.itemSO.buyPrice * itemAmount;

        // Add item without deducting money
        playerInventory.AddItem(newItem);

        // Create AP debt
        APQuestManager.Instance.CreateAPQuest(vendorName, currentItem.itemSO.GetDisplayName(), itemAmount, totalCost, creditDueDays, currentItem.quality);

        ResetAmount();
        GenerateItemList();
        OnItemAmountChanged();
    }

    public void ShowDebtsTab()
    {
        tabStatus = Tab.Debts;
        RefreshTabs();

        if (APQuestManager.Instance != null)
        {
            List<APQuestData> debts = APQuestManager.Instance.GetActiveDebts();
            if (debts != null && debts.Count > 0)
            {
                APQuestManager.Instance.NotifyAPQuestViewed(debts[0]);
            }
        }
    }

    private void UpdateOwnedAmounts()
    {
        if (!isActiveAndEnabled || !TryResolvePlayerInventory() || itemSlotContainer == null)
        {
            return;
        }

        // Regenerate the item list when inventory changes
        // This ensures quality-separated slots show accurate amounts
        GenerateItemList();

        // Update detail UI if showing current item
        if (currentItem != null)
        {
            int ownedAmount = 0;
            if (tabStatus == Tab.Sell && currentItem.itemSO.UsesQuality())
            {
                // For sell tab with quality items, show only amount of this quality
                ownedAmount = playerInventory.GetItems()
                    .FindAll(i => i != null && i.itemSO == currentItem.itemSO && i.quality == currentItem.quality)
                    .Sum(i => i.amount);
            }
            else
            {
                // For buy tab or non-quality items, show total
                ownedAmount = playerInventory.GetItems()
                    .FindAll(i => i != null && i.itemSO == currentItem.itemSO)
                    .Sum(i => i.amount);
            }

            string displayName = currentItem.itemSO.GetDisplayName();
            if (currentItem.itemSO.UsesQuality())
                displayName = $"{displayName} ({currentItem.quality})";

            itemNameText.text = $"{displayName} ({ownedAmount})";
        }
    }
}

public enum Tab
{
    Buy,
    Sell,
    Debts,
}
