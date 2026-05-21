using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class APRepaymentUI : MonoBehaviour
{
    public static APRepaymentUI Instance;

    [Header("Header")]
    [SerializeField] private TMP_Text vendorNameText;

    [Header("List")]
    [SerializeField] private Transform contentParent;
    [SerializeField] private GameObject debtEntryPrefab;

    [Header("Selected Debt Info")]
    [SerializeField] private TMP_Text selectedDebtInfoText;
    [SerializeField] private Button repayButton;
    [SerializeField] private TMP_Text repayButtonText;

    [Header("Feedback")]
    [SerializeField] private TMP_Text feedbackText;

    [Header("Panel Root")]
    [SerializeField] private GameObject panelRoot;

    private string currentVendorName;
    private APQuestData selectedDebt;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        if (panelRoot != null) panelRoot.SetActive(false);
    }

    private void OnEnable()
    {
        APQuestManager.OnAPQuestRepaid += OnDebtRepaid;
    }

    private void OnDisable()
    {
        APQuestManager.OnAPQuestRepaid -= OnDebtRepaid;
    }

    public void ShowForVendor(string vendorName)
    {
        currentVendorName = vendorName;
        selectedDebt = null;

        if (vendorNameText != null) vendorNameText.text = vendorName;
        if (feedbackText != null) feedbackText.text = "";

        if (panelRoot != null) panelRoot.SetActive(true);
        Time.timeScale = 0f;
        UI_StateManager.Instance.interactWindowOpen = true;

        RefreshList();
    }

    public void Hide()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
        Time.timeScale = 1f;
        UI_StateManager.Instance.interactWindowOpen = false;
    }

    private void RefreshList()
    {
        foreach (Transform child in contentParent) Destroy(child.gameObject);
        selectedDebt = null;
        UpdateSelectedDebtUI();

        if (APQuestManager.Instance == null) return;

        var debts = APQuestManager.Instance.GetActiveDebts();
        foreach (var debt in debts)
        {
            if (debt.vendorName != currentVendorName) continue;

            GameObject entry = Instantiate(debtEntryPrefab, contentParent);
            SetupDebtEntry(entry, debt);
        }
    }

    private void SetupDebtEntry(GameObject entry, APQuestData debt)
    {
        TMP_Text[] texts = entry.GetComponentsInChildren<TMP_Text>(true);

        TMP_Text itemText    = System.Array.Find(texts, t => t.name == "Text_Item");
        TMP_Text amountText  = System.Array.Find(texts, t => t.name == "Text_Amount");
        TMP_Text statusText  = System.Array.Find(texts, t => t.name == "Text_Status");

        int total = debt.principalAmount + debt.accruedInterest;
        if (itemText != null) itemText.text = $"{debt.itemName} x{debt.quantity}";
        if (amountText != null) amountText.text = $"{total:N0} G";

        if (statusText != null)
        {
            int currentDay = DailyJournalRules.GetCurrentAccountingDay();
            int remaining = debt.dueTotalDay - currentDay;
            if (debt.isOverdue)
            {
                statusText.text = $"OVERDUE +{debt.accruedInterest}G interest";
                statusText.color = new Color(0.78f, 0.18f, 0.12f);
            }
            else
            {
                statusText.text = $"Due in {remaining} day(s)";
                statusText.color = Color.white;
            }
        }

        Button btn = entry.GetComponent<Button>();
        if (btn != null)
        {
            btn.onClick.RemoveAllListeners();
            APQuestData captured = debt;
            btn.onClick.AddListener(() => SelectDebt(captured));
        }
    }

    private void SelectDebt(APQuestData debt)
    {
        selectedDebt = debt;
        UpdateSelectedDebtUI();
    }

    private void UpdateSelectedDebtUI()
    {
        if (selectedDebt == null)
        {
            if (selectedDebtInfoText != null) selectedDebtInfoText.text = "Select a debt to pay";
            if (repayButton != null) repayButton.interactable = false;
            if (repayButtonText != null) repayButtonText.text = "Pay";
            return;
        }

        int total = selectedDebt.principalAmount + selectedDebt.accruedInterest;
        if (selectedDebtInfoText != null)
            selectedDebtInfoText.text = $"{selectedDebt.itemName}\nPrincipal: {selectedDebt.principalAmount:N0} G\nInterest: {selectedDebt.accruedInterest:N0} G\nTotal: {total:N0} G";

        int playerMoney = GameManager.Instance?.playerInventory?.money ?? 0;
        bool canAfford = playerMoney >= total;
        if (repayButton != null) repayButton.interactable = canAfford;
        if (repayButtonText != null) repayButtonText.text = canAfford ? $"Pay {total:N0} G" : "Not Enough Gold";
    }

    public void OnRepayButtonClicked()
    {
        if (selectedDebt == null) return;
        if (feedbackText != null) feedbackText.text = "";

        // Disable button immediately to prevent double-click spam
        if (repayButton != null) repayButton.interactable = false;

        APQuestData debtToRepay = selectedDebt;
        selectedDebt = null; // clear before TryRepayDebt so re-entry is safe

        bool success = APQuestManager.Instance?.TryRepayDebt(debtToRepay) ?? false;
        if (!success)
        {
            selectedDebt = debtToRepay; // restore selection so player can retry
            if (feedbackText != null) feedbackText.text = "Not enough gold!";
            UpdateSelectedDebtUI(); // re-enable button via UI refresh
        }
    }

    private void OnDebtRepaid(APQuestData _)
    {
        RefreshList();
    }
}
