using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class ReceiptManager : MonoBehaviour
{
    public Transform smallReceiptContainer;
    public SmallReceiptDeckLayout smallReceiptDeckLayout;
    public Transform receiptDetailsContainer;
    public GameObject smallReceiptPrefab;
    public GameObject receipt;
    public TMP_Text receiptTitleText;
    public TMP_Text receiptStoreTypeText;
    public TMP_Text receiptTransactionTypeText;
    public GameObject itemNamePrefab;
    public GameObject draggablePrefab;
    public GameObject nonDraggablePrefab;

    public (StoreType, TransactionType, string) currentReceipt;

    private bool receiptScrollPrepared;
    private ScrollRect receiptScrollRect;
    private readonly List<SmallReceipt> smallReceiptCards = new List<SmallReceipt>();

    private void OnEnable()
    {
        currentReceipt = (StoreType.None, TransactionType.None, string.Empty);
        GenerateSmallReceipt();
        SetReceiptDetailsVisible(false);
    }

    private void OnDisable()
    {
        ClearSmallReceiptCards();
    }

    public void GenerateSmallReceipt()
    {
        ResolveSmallReceiptDeckLayout();
        ClearSmallReceiptCards();

        var grouped = TransactionManager.Instance.GetGroupedBills();

        foreach (var bill in grouped)
        {
            var store = bill.Key.Item1;
            var type = bill.Key.Item2;
            var counterpartyName = bill.Key.Item3;

            GameObject receiptObject = Instantiate(smallReceiptPrefab, smallReceiptContainer);
            SmallReceipt smallReceipt = receiptObject.GetComponent<SmallReceipt>();
            if (smallReceipt != null)
            {
                smallReceipt.SetValue(
                    store,
                    type,
                    counterpartyName,
                    GetReceiptPartyDisplayName(store, type, counterpartyName),
                    GetTransactionTypeDisplayName(type));
                smallReceiptCards.Add(smallReceipt);
            }
        }

        ApplySmallReceiptDeckLayout();
        RefreshSmallReceiptSelection();
    }

    public void ViewReceipt(StoreType storeType, TransactionType transactionType, string counterpartyName = "")
    {
        counterpartyName = NormalizeCounterpartyName(counterpartyName);
        var receiptKey = (storeType, transactionType, counterpartyName);
        if (currentReceipt == receiptKey)
        {
            SetReceiptDetailsVisible(false);
            currentReceipt = (StoreType.None, TransactionType.None, string.Empty);
            RefreshSmallReceiptSelection();
            return;
        }

        SetReceiptDetailsVisible(true);
        currentReceipt = receiptKey;
        string partyDisplayName = GetReceiptPartyDisplayName(storeType, transactionType, counterpartyName);
        string transactionDisplayName = GetTransactionTypeDisplayName(transactionType);
        if (receiptTitleText != null)
        {
            receiptTitleText.text = $"{partyDisplayName} - {transactionDisplayName}";
        }

        if (receiptStoreTypeText != null)
        {
            receiptStoreTypeText.text = partyDisplayName;
        }

        if (receiptTransactionTypeText != null)
        {
            receiptTransactionTypeText.text = transactionDisplayName;
        }

        RefreshSmallReceiptSelection();

        var grouped = TransactionManager.Instance.GetGroupedBills();

        if (grouped.TryGetValue(currentReceipt, out var records))
            GenerateReceiptDetails(records);
    }

    public void GenerateReceiptDetails(List<TransactionRecord> records)
    {
        EnsureReceiptDetailsScroll();

        foreach (Transform child in receiptDetailsContainer)
            Destroy(child.gameObject);

        if (records == null || records.Count == 0) return;

        var detailRecords = new List<TransactionRecord>();
        foreach (var record in records)
        {
            if (record == null || IsGeneratedBalancingRecord(record))
            {
                continue;
            }

            detailRecords.Add(record);
        }

        if (detailRecords.Count == 0) return;

        var grouped = new Dictionary<ItemCategory, List<TransactionRecord>>();
        foreach (var record in detailRecords)
        {
            if (!grouped.ContainsKey(record.category))
                grouped[record.category] = new List<TransactionRecord>();
            grouped[record.category].Add(record);
        }

        int grandTotal = 0;
        foreach (var group in grouped)
        {
            int categoryTotal = 0;
            foreach (var record in group.Value)
            {
                categoryTotal += record.totalPrice;
            }

            if (ShouldDisplayAccountName(group.Key, currentReceipt.Item2))
            {
                GameObject accountRow = Instantiate(draggablePrefab, receiptDetailsContainer);
                string accountName = GetCategoryDisplayName(group.Key, currentReceipt.Item2);
                accountRow.transform.Find("Draggable/TextHolder/Text_Name").GetComponent<TMP_Text>().text = accountName;
                accountRow.transform.Find("Draggable/TextHolder/Text_Price").GetComponent<TMP_Text>().text = $"{categoryTotal} $";
                accountRow.transform.Find("Draggable").GetComponent<DragGroupUI>().SetValue(
                    accountName,
                    group.Key,
                    currentReceipt.Item2,
                    currentReceipt.Item1,
                    1,
                    categoryTotal,
                    group.Value[0].gameTime,
                    false);
                RefreshReceiptRowLayout(accountRow);

                if (nonDraggablePrefab != null)
                {
                    GameObject accountTotalRow = Instantiate(nonDraggablePrefab, receiptDetailsContainer);
                    accountTotalRow.transform.Find("NonDraggable/TextHolder/Text_Name").GetComponent<TMP_Text>().text = $"{accountName} Total";
                    accountTotalRow.transform.Find("NonDraggable/TextHolder/Text_Price").GetComponent<TMP_Text>().text = $"{categoryTotal} $";
                    RefreshReceiptRowLayout(accountTotalRow);
                }

                grandTotal += categoryTotal;
                continue;
            }

            foreach (var record in group.Value)
            {
                GameObject newList = Instantiate(draggablePrefab, receiptDetailsContainer);
                newList.transform.Find("Draggable/TextHolder/Text_Name").GetComponent<TMP_Text>().text = BuildItemLineLabel(record);
                newList.transform.Find("Draggable/TextHolder/Text_Price").GetComponent<TMP_Text>().text = $"{record.totalPrice} $";
                newList.transform.Find("Draggable").GetComponent<DragGroupUI>().SetValue(
                    record.itemName,
                    record.category,
                    currentReceipt.Item2,
                    record.store,
                    record.quantity,
                    record.totalPrice,
                    record.gameTime,
                    false);
                RefreshReceiptRowLayout(newList);
            }

            if (nonDraggablePrefab != null)
            {
                GameObject newListCategory = Instantiate(nonDraggablePrefab, receiptDetailsContainer);
                newListCategory.transform.Find("NonDraggable/TextHolder/Text_Name").GetComponent<TMP_Text>().text = $"{GetCategoryDisplayName(group.Key, currentReceipt.Item2)} Total";
                newListCategory.transform.Find("NonDraggable/TextHolder/Text_Price").GetComponent<TMP_Text>().text = $"{categoryTotal} $";
                RefreshReceiptRowLayout(newListCategory);
            }

            grandTotal += categoryTotal;
        }

        ItemCategory balancingCategory = GetBalancingCategoryForTransaction(currentReceipt.Item2);
        GameObject newTotal = Instantiate(draggablePrefab, receiptDetailsContainer);
        string grandTotalLabel = GetGrandTotalLabel(currentReceipt.Item2, balancingCategory);
        newTotal.transform.Find("Draggable/TextHolder/Text_Name").GetComponent<TMP_Text>().text = grandTotalLabel;
        newTotal.transform.Find("Draggable/TextHolder/Text_Price").GetComponent<TMP_Text>().text = $"{grandTotal} $";
        newTotal.transform.Find("Draggable").GetComponent<DragGroupUI>().SetValue(
            GetCategoryDisplayName(balancingCategory, currentReceipt.Item2),
            balancingCategory,
            currentReceipt.Item2,
            currentReceipt.Item1,
            1,
            grandTotal,
            detailRecords[0].gameTime,
            true);
        RefreshReceiptRowLayout(newTotal);

        RectTransform content = receiptDetailsContainer as RectTransform;
        if (content != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        }

        if (receiptScrollRect != null)
        {
            Canvas.ForceUpdateCanvases();
            receiptScrollRect.verticalNormalizedPosition = 1f;
        }
    }

    private string BuildItemLineLabel(TransactionRecord record)
    {
        if (record == null)
        {
            return "Unknown";
        }

        if (ShouldDisplayAccountName(record.category, record.type))
        {
            return GetCategoryDisplayName(record.category, record.type);
        }

        return record.quantity > 1
            ? $"{record.itemName} x{record.quantity}"
            : record.itemName;
    }

    private void EnsureReceiptDetailsScroll()
    {
        if (receiptScrollPrepared || receiptDetailsContainer == null)
        {
            return;
        }

        RectTransform content = receiptDetailsContainer as RectTransform;
        if (content == null)
        {
            return;
        }

        receiptScrollRect = receiptDetailsContainer.GetComponentInParent<ScrollRect>(true);
        if (receiptScrollRect != null && receiptScrollRect.content == null)
        {
            receiptScrollRect.content = content;
        }

        receiptScrollPrepared = true;
    }

    private void RefreshReceiptRowLayout(GameObject receiptRow)
    {
        if (receiptRow == null)
        {
            return;
        }

        ReceiptRowAutoHeight autoHeight = receiptRow.GetComponent<ReceiptRowAutoHeight>();
        if (autoHeight != null)
        {
            autoHeight.Refresh();
        }
    }

    private void SetReceiptDetailsVisible(bool visible)
    {
        if (receipt != null)
        {
            receipt.SetActive(visible);
        }

        if (!visible)
        {
            if (receiptTitleText != null)
            {
                receiptTitleText.text = "None";
            }

            if (receiptStoreTypeText != null)
            {
                receiptStoreTypeText.text = "None";
            }

            if (receiptTransactionTypeText != null)
            {
                receiptTransactionTypeText.text = "None";
            }
        }
    }

    private void ClearSmallReceiptCards()
    {
        smallReceiptCards.Clear();

        if (smallReceiptContainer == null)
        {
            return;
        }

        for (int i = smallReceiptContainer.childCount - 1; i >= 0; i--)
        {
            Destroy(smallReceiptContainer.GetChild(i).gameObject);
        }
    }

    private void ResolveSmallReceiptDeckLayout()
    {
        if (smallReceiptDeckLayout != null || smallReceiptContainer == null)
        {
            return;
        }

        smallReceiptDeckLayout = smallReceiptContainer.GetComponent<SmallReceiptDeckLayout>();
    }

    private void ApplySmallReceiptDeckLayout()
    {
        ResolveSmallReceiptDeckLayout();

        if (smallReceiptDeckLayout == null)
        {
            return;
        }

        smallReceiptDeckLayout.ApplyLayout(smallReceiptCards);
    }

    public void RestoreSmallReceiptDeckOrder()
    {
        for (int i = 0; i < smallReceiptCards.Count; i++)
        {
            if (smallReceiptCards[i] != null)
            {
                smallReceiptCards[i].transform.SetSiblingIndex(i);
            }
        }

        for (int i = 0; i < smallReceiptCards.Count; i++)
        {
            SmallReceipt card = smallReceiptCards[i];
            if (card != null && currentReceipt == (card.storeType, card.transactionType, card.counterpartyName))
            {
                card.transform.SetAsLastSibling();
                break;
            }
        }
    }

    private void RefreshSmallReceiptSelection()
    {
        foreach (SmallReceipt card in smallReceiptCards)
        {
            if (card == null)
            {
                continue;
            }

            bool selected = currentReceipt == (card.storeType, card.transactionType, card.counterpartyName);
            card.SetSelected(selected);
        }

        RestoreSmallReceiptDeckOrder();
    }

    private static ItemCategory GetBalancingCategoryForTransaction(TransactionType type)
    {
        return type switch
        {
            TransactionType.BillAccrued => ItemCategory.AccruedExpenses,
            TransactionType.BillSettlement => ItemCategory.Cash,
            TransactionType.CreditSale => ItemCategory.AccountsReceivable,
            TransactionType.ReceivePayment => ItemCategory.Cash, // รับชำระหนี้ = ยอดรวมคือเงินสด (Cash)
            TransactionType.CreditPurchase => ItemCategory.AccountsPayable,
            TransactionType.RepayAP => ItemCategory.Cash,
            TransactionType.AccrueInterest => ItemCategory.InterestPayable,
            _ => ItemCategory.Cash,
        };
    }

    private string GetGrandTotalLabel(TransactionType type, ItemCategory balancingCategory)
    {
        string accountName = GetCategoryDisplayName(balancingCategory, type);
        return $"Grand Total ({accountName})";
    }

    private string GetCategoryDisplayName(ItemCategory cat, TransactionType type)
    {
        string normal = cat switch
        {
            ItemCategory.AccruedExpenses => "Accrued Expenses",
            ItemCategory.FoodSupplies => "Food Supplies",
            ItemCategory.KitchenEquipment => "Kitchen Equipment",
            ItemCategory.FoodSales => "Food Sales",
            ItemCategory.SalesRevenue => "Sales Revenue",
            ItemCategory.ServiceRevenue => "Service Revenue",
            ItemCategory.UtilitiesElectricity => "Electricity Expense",
            ItemCategory.UtilitiesWater => "Water Expense",
            ItemCategory.Cash => "Cash",
            ItemCategory.AccountsReceivable => "Accounts Receivable",
            ItemCategory.AccountsPayable => "Accounts Payable",
            ItemCategory.InterestExpense => "Interest Expense",
            ItemCategory.InterestPayable => "Interest Payable",
            _ => "Misc"
        };
        if (type == TransactionType.Sell || type == TransactionType.CreditSale)
        {
            return cat switch
            {
                ItemCategory.FoodSales => "Food Sales Revenue",
                ItemCategory.SalesRevenue => "Sales Revenue",
                ItemCategory.ServiceRevenue => "Service Revenue",
                _ => normal,
            };
        }
        return normal;
    }

    private static bool IsGeneratedBalancingRecord(TransactionRecord record)
    {
        return record.category == GetBalancingCategoryForTransaction(record.type);
    }

    private static bool ShouldDisplayAccountName(ItemCategory category, TransactionType type)
    {
        return type == TransactionType.ReceivePayment
            || type == TransactionType.RepayAP
            || type == TransactionType.AccrueInterest
            || type == TransactionType.BillSettlement;
    }

    private static string NormalizeCounterpartyName(string counterpartyName)
    {
        return string.IsNullOrWhiteSpace(counterpartyName) ? string.Empty : counterpartyName.Trim();
    }

    private static string GetReceiptPartyDisplayName(StoreType storeType, TransactionType transactionType, string counterpartyName)
    {
        counterpartyName = NormalizeCounterpartyName(counterpartyName);
        if (TransactionManager.UsesCounterpartyName(transactionType) && !string.IsNullOrEmpty(counterpartyName))
        {
            return counterpartyName;
        }

        return storeType switch
        {
            StoreType.TownShop => "Town Shop",
            StoreType.Restaurant => "Restaurant",
            _ => storeType.ToString(),
        };
    }

    private static string GetTransactionTypeDisplayName(TransactionType type)
    {
        return type switch
        {
            TransactionType.BillAccrued => "Bill Accrual",
            TransactionType.BillSettlement => "Bill Payment",
            TransactionType.CreditSale => "Credit Sale",
            TransactionType.ReceivePayment => "Receive Payment",
            TransactionType.CreditPurchase => "Credit Purchase",
            TransactionType.RepayAP => "AP Payment",
            TransactionType.AccrueInterest => "Interest Accrual",
            _ => type.ToString(),
        };
    }
}
