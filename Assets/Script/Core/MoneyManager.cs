using UnityEngine;
using TMPro;

public class MoneyManager : MonoBehaviour
{
    [Header("Single Text (Optional)")]
    public TextMeshProUGUI Money;

    [Header("Digit Slots (Left to Right)")]
    [SerializeField] private TextMeshProUGUI[] moneySlots;
    [SerializeField] private string emptySlotText = "";
    [SerializeField] private bool rightAlign = true;

    private InventoryController inventory;

    private void Start()
    {
        var playerGo = GameManager.instance?.player;
        if (playerGo == null) { Debug.LogError("[MoneyManager] GameManager.instance.player is null"); return; }
        var playerComp = playerGo.GetComponent<Player>();
        if (playerComp == null) { Debug.LogError("[MoneyManager] Player component not found"); return; }
        inventory = playerComp.GetInventoryController();
        if (inventory == null) { Debug.LogError("[MoneyManager] InventoryController not found"); return; }

        UpdateMoneyUI();

        inventory.OnMoneyChanged += UpdateMoneyUI;
    }

    private void OnDestroy()
    {
        if (inventory != null)
            inventory.OnMoneyChanged -= UpdateMoneyUI;
    }

    private void UpdateMoneyUI()
    {
        if (inventory == null) return;

        string moneyValue = Mathf.Max(0, inventory.money).ToString();

        if (Money != null)
        {
            Money.text = moneyValue;
        }

        UpdateMoneySlotUI(moneyValue);
    }

    private void UpdateMoneySlotUI(string moneyValue)
    {
        if (moneySlots == null || moneySlots.Length == 0) return;

        for (int i = 0; i < moneySlots.Length; i++)
        {
            if (moneySlots[i] != null)
            {
                moneySlots[i].text = emptySlotText;
            }
        }

        int charCount = Mathf.Min(moneyValue.Length, moneySlots.Length);
        int moneyStartIndex = moneyValue.Length - charCount;
        int slotStartIndex = rightAlign ? moneySlots.Length - charCount : 0;

        for (int i = 0; i < charCount; i++)
        {
            int slotIndex = slotStartIndex + i;
            if (moneySlots[slotIndex] == null) continue;

            moneySlots[slotIndex].text = moneyValue[moneyStartIndex + i].ToString();
        }

        if (moneyValue.Length > moneySlots.Length)
        {
            Debug.LogWarning($"[MoneyManager] Money overflow: {moneyValue} exceeds {moneySlots.Length} slot(s).");
        }
    }
}
