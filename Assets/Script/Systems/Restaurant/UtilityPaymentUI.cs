using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UtilityPaymentUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform weekBoxContainer;
    [SerializeField] private UtilityPaymentWeekBoxUI weekBoxPrefab;
    [SerializeField] private TMP_Text emptyStateText;
    [SerializeField] private Button closeButton;

    [Header("Text")]
    [SerializeField] private string emptyMessage = "No unpaid utility bills";

    [HideInInspector][SerializeField] private UtilityPaymentInteractable ownerInteractable;

    private readonly List<UtilityPaymentWeekBoxUI> spawnedBoxes = new List<UtilityPaymentWeekBoxUI>();

    private void Awake()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(CloseWindow);
            closeButton.onClick.AddListener(CloseWindow);
        }
    }

    private void OnEnable()
    {
        RefreshNow();
    }

    public void RefreshNow()
    {
        ClearBoxes();

        if (weekBoxContainer == null || weekBoxPrefab == null)
        {
            SetEmptyState("Utility Payment UI is not configured.");
            return;
        }

        IReadOnlyList<RestaurantServiceManager.UtilityBillStatement> unpaidBills = RestaurantUtilityBillingCache.GetUnpaidBillsSnapshot();
        if (unpaidBills == null || unpaidBills.Count == 0)
        {
            SetEmptyState(emptyMessage);
            return;
        }

        bool hasAnyShown = false;
        for (int i = 0; i < unpaidBills.Count; i++)
        {
            RestaurantServiceManager.UtilityBillStatement bill = unpaidBills[i];
            if (bill == null)
            {
                continue;
            }

            if (Mathf.Max(0, bill.electricityOutstandingAmount) <= 0 && Mathf.Max(0, bill.waterOutstandingAmount) <= 0)
            {
                continue;
            }

            UtilityPaymentWeekBoxUI box = Instantiate(weekBoxPrefab, weekBoxContainer);
            box.Bind(
                bill,
                () => HandlePayCharge(bill.billId, RestaurantServiceManager.UtilityChargeType.Electricity),
                () => HandlePayCharge(bill.billId, RestaurantServiceManager.UtilityChargeType.Water));
            spawnedBoxes.Add(box);
            hasAnyShown = true;
        }

        if (!hasAnyShown)
        {
            SetEmptyState(emptyMessage);
            return;
        }

        SetEmptyState(string.Empty, forceHide: true);
    }

    public void CloseWindow()
    {
        if (ownerInteractable != null)
        {
            ownerInteractable.Interact();
            return;
        }

        Canvas canvas = GetComponentInParent<Canvas>(true);
        if (canvas != null)
        {
            canvas.gameObject.SetActive(false);
        }

        if (UI_StateManager.Instance != null)
        {
            UI_StateManager.Instance.interactWindowOpen = false;
        }

        Time.timeScale = 1f;
    }

    public void BindOwner(UtilityPaymentInteractable owner)
    {
        ownerInteractable = owner;
    }

    private void HandlePayCharge(string billId, RestaurantServiceManager.UtilityChargeType chargeType)
    {
        RestaurantUtilityBillingCache.TryPayCharge(billId, chargeType, out _, showResultNotice: true);
        RefreshNow();
    }

    private void SetEmptyState(string message, bool forceHide = false)
    {
        if (emptyStateText == null)
        {
            return;
        }

        if (forceHide)
        {
            emptyStateText.gameObject.SetActive(false);
            return;
        }

        emptyStateText.gameObject.SetActive(true);
        emptyStateText.text = string.IsNullOrWhiteSpace(message) ? emptyMessage : message;
    }

    private void ClearBoxes()
    {
        for (int i = 0; i < spawnedBoxes.Count; i++)
        {
            UtilityPaymentWeekBoxUI box = spawnedBoxes[i];
            if (box != null)
            {
                Destroy(box.gameObject);
            }
        }

        spawnedBoxes.Clear();
    }

}
