using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UtilityPaymentWeekBoxUI : MonoBehaviour
{
    [Header("Texts")]
    [SerializeField] private TMP_Text weekLabel;
    [SerializeField] private TMP_Text electricityAmountLabel;
    [SerializeField] private TMP_Text waterAmountLabel;

    [Header("Buttons")]
    [SerializeField] private Button payElectricityButton;
    [SerializeField] private Button payWaterButton;

    [Header("Visual")]
    [SerializeField] private Color dueAmountColor = Color.white;
    [SerializeField] private Color paidAmountColor = new Color32(143, 210, 132, 255);
    [SerializeField] private Color dueButtonColor = Color.white;
    [SerializeField] private Color paidButtonColor = new Color32(150, 150, 150, 255);
    [SerializeField] private string payButtonLabel = "Pay";
    [SerializeField] private string paidButtonLabel = "Paid";

    private Action onPayElectricity;
    private Action onPayWater;
    private TMP_Text payElectricityButtonLabel;
    private TMP_Text payWaterButtonLabel;
    private Image payElectricityButtonImage;
    private Image payWaterButtonImage;

    private void Awake()
    {
        CacheButtonVisuals();

        if (payElectricityButton != null)
        {
            payElectricityButton.onClick.RemoveListener(HandlePayElectricity);
            payElectricityButton.onClick.AddListener(HandlePayElectricity);
        }

        if (payWaterButton != null)
        {
            payWaterButton.onClick.RemoveListener(HandlePayWater);
            payWaterButton.onClick.AddListener(HandlePayWater);
        }
    }

    private void CacheButtonVisuals()
    {
        payElectricityButtonLabel = payElectricityButton != null
            ? payElectricityButton.GetComponentInChildren<TMP_Text>(true)
            : null;
        payWaterButtonLabel = payWaterButton != null
            ? payWaterButton.GetComponentInChildren<TMP_Text>(true)
            : null;

        payElectricityButtonImage = payElectricityButton != null
            ? payElectricityButton.GetComponent<Image>()
            : null;
        payWaterButtonImage = payWaterButton != null
            ? payWaterButton.GetComponent<Image>()
            : null;
    }

    public void Bind(
        RestaurantServiceManager.UtilityBillStatement bill,
        Action payElectricityAction,
        Action payWaterAction)
    {
        if (bill == null)
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);
        onPayElectricity = payElectricityAction;
        onPayWater = payWaterAction;

        if (weekLabel != null)
        {
            weekLabel.text = $"Week {bill.weekIndex}";
        }

        int electricityDue = Mathf.Max(0, bill.electricityOutstandingAmount);
        int waterDue = Mathf.Max(0, bill.waterOutstandingAmount);
        bool electricityPaid = electricityDue <= 0;
        bool waterPaid = waterDue <= 0;

        UpdateAmountLabel(electricityAmountLabel, "Electricity", electricityDue, electricityPaid);
        UpdateAmountLabel(waterAmountLabel, "Water", waterDue, waterPaid);
        ConfigurePayButton(payElectricityButton, payElectricityButtonLabel, payElectricityButtonImage, electricityDue);
        ConfigurePayButton(payWaterButton, payWaterButtonLabel, payWaterButtonImage, waterDue);
    }

    private void UpdateAmountLabel(TMP_Text label, string chargeName, int dueAmount, bool isPaid)
    {
        if (label == null)
        {
            return;
        }

        if (isPaid)
        {
            label.text = $"{chargeName}: Paid";
            label.color = paidAmountColor;
            return;
        }

        label.text = $"{chargeName} due: ${dueAmount:N0}";
        label.color = dueAmountColor;
    }

    private void ConfigurePayButton(Button button, TMP_Text buttonLabel, Image buttonImage, int dueAmount)
    {
        bool canPay = dueAmount > 0;
        if (button != null)
        {
            Color targetColor = canPay ? dueButtonColor : paidButtonColor;
            ColorBlock colors = button.colors;
            colors.normalColor = targetColor;
            colors.highlightedColor = targetColor;
            colors.selectedColor = targetColor;
            colors.pressedColor = targetColor;
            colors.disabledColor = paidButtonColor;
            button.colors = colors;
            button.interactable = canPay;
        }

        if (buttonLabel != null)
        {
            buttonLabel.text = canPay ? payButtonLabel : paidButtonLabel;
        }

        if (buttonImage != null)
        {
            buttonImage.color = canPay ? dueButtonColor : paidButtonColor;
        }
    }

    private void HandlePayElectricity()
    {
        onPayElectricity?.Invoke();
    }

    private void HandlePayWater()
    {
        onPayWater?.Invoke();
    }
}
