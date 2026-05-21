using System;
using System.Collections.Generic;
using UnityEngine;

public class APQuestManager : MonoBehaviour
{
    public static APQuestManager Instance;
    public static event Action<APQuestData> OnAPQuestCreated;
    public static event Action<APQuestData> OnAPQuestViewed;
    public static event Action<APQuestData> OnAPQuestRepaid;
    public static event Action<APQuestData> OnDebtBecameOverdue;

    [SerializeField] private float interestRatePerDay = 0.05f;

    private readonly List<APQuestData> activeDebts = new List<APQuestData>();
    private int lastCheckedDay = -1;

    private void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else Destroy(gameObject);
    }

    private void OnEnable()
    {
        TimeManager.OnDateTimeChanged += OnDateTimeChanged;
        RefreshDebtStatusForCurrentTime();
    }

    private void OnDisable()
    {
        TimeManager.OnDateTimeChanged -= OnDateTimeChanged;
    }

    public void CreateAPQuest(string vendorName, string itemName, int quantity, int principalAmount, int daysTillPayment, ItemQuality quality = ItemQuality.Common)
    {
        int currentDay = DailyJournalRules.GetCurrentAccountingDay();
        int computedInterestPerDay = Mathf.Max(1, Mathf.RoundToInt(principalAmount * interestRatePerDay));

        var debt = new APQuestData
        {
            vendorName = vendorName,
            itemName = itemName,
            quantity = quantity,
            quality = quality,
            principalAmount = principalAmount,
            interestPerDay = computedInterestPerDay,
            dueTotalDay = currentDay + daysTillPayment,
            createdTotalDay = currentDay,
            isOverdue = false,
            accruedInterest = 0,
            lastAccruedDay = -1,
        };
        activeDebts.Add(debt);
        OnAPQuestCreated?.Invoke(debt);

        if (TransactionManager.Instance == null)
        {
            Debug.LogWarning("[APQuestManager] TransactionManager ยังไม่ initialize — ข้าม transaction records สำหรับ AP quest ใหม่");
            return;
        }

        // Dr: FoodSupplies | Cr: AccountsPayable
        TransactionManager.Instance.AddRecord(new TransactionRecord(
            itemName,
            ItemCategory.FoodSupplies,
            quantity,
            principalAmount,
            TransactionType.CreditPurchase,
            StoreType.TownShop,
            vendorName
        ));

        Debug.Log($"[APQuestManager] Created AP debt: {vendorName} — {principalAmount}G due day {debt.dueTotalDay}");
    }

    public bool TryRepayDebt(APQuestData debt)
    {
        int total = debt.principalAmount + debt.accruedInterest;
        var inventory = GameManager.Instance?.playerInventory;
        if (inventory == null || inventory.money < total)
        {
            Debug.Log($"[APQuestManager] Cannot repay: not enough money ({inventory?.money} < {total})");
            return false;
        }

        inventory.SpendMoney(total);

        if (TransactionManager.Instance == null)
        {
            Debug.LogWarning("[APQuestManager] TransactionManager ยังไม่ initialize — ข้าม transaction records สำหรับการชำระหนี้");
        }
        else
        {
            // Dr: AccountsPayable (clears principal)
            TransactionManager.Instance.AddRecord(new TransactionRecord(
                debt.itemName,
                ItemCategory.AccountsPayable,
                1,
                debt.principalAmount,
                TransactionType.RepayAP,
                StoreType.TownShop,
                debt.vendorName
            ));

            // Dr: InterestPayable (clears accrued interest)
            if (debt.accruedInterest > 0)
            {
                TransactionManager.Instance.AddRecord(new TransactionRecord(
                    debt.itemName,
                    ItemCategory.InterestPayable,
                    1,
                    debt.accruedInterest,
                    TransactionType.RepayAP,
                    StoreType.TownShop,
                    debt.vendorName
                ));
            }

        }

        activeDebts.Remove(debt);
        OnAPQuestRepaid?.Invoke(debt);

        Debug.Log($"[APQuestManager] Repaid debt to {debt.vendorName}: {total}G (principal {debt.principalAmount} + interest {debt.accruedInterest})");
        return true;
    }

    public List<APQuestData> GetActiveDebts() => activeDebts;

    public void NotifyAPQuestViewed(APQuestData debt)
    {
        if (debt == null)
        {
            return;
        }

        OnAPQuestViewed?.Invoke(debt);
    }

    public bool HasAnyDebt(string vendorName)
    {
        return activeDebts.Exists(d => d.vendorName == vendorName);
    }

    private void OnDateTimeChanged(TimeManager.DateTime dt)
    {
        RefreshDebtStatus(dt);
    }

    public void RefreshDebtStatusForCurrentTime()
    {
        if (!TimeManager.TryGetCurrentDateTime(out TimeManager.DateTime currentDateTime))
        {
            return;
        }

        RefreshDebtStatus(currentDateTime);
    }

    private void RefreshDebtStatus(TimeManager.DateTime dt)
    {
        int accountingDay = DailyJournalRules.GetAccountingDay(dt);
        if (accountingDay == lastCheckedDay) return;
        lastCheckedDay = accountingDay;

        foreach (var debt in new List<APQuestData>(activeDebts))
        {
            // Due day is still allowed for repayment without extra interest.
            if (!debt.isOverdue && accountingDay > debt.dueTotalDay)
            {
                debt.isOverdue = true;
                OnDebtBecameOverdue?.Invoke(debt);
            }

            // ป้องกันคิดดอกเบี้ยซ้ำในวันเดิม (เช่น หลัง save/load หรือ scene reload)
            if (debt.isOverdue && debt.lastAccruedDay != accountingDay)
                AccrueInterestOn(debt, accountingDay);
        }
    }

    private void AccrueInterestOn(APQuestData debt, int day)
    {
        debt.lastAccruedDay = day;
        debt.accruedInterest += debt.interestPerDay;

        if (TransactionManager.Instance == null)
        {
            Debug.LogWarning("[APQuestManager] TransactionManager ยังไม่ initialize — ข้าม interest accrual records");
            return;
        }

        // Dr: InterestExpense | Cr: InterestPayable
        TransactionManager.Instance.AddRecord(new TransactionRecord(
            debt.itemName,
            ItemCategory.InterestExpense,
            1,
            debt.interestPerDay,
            TransactionType.AccrueInterest,
            StoreType.TownShop,
            debt.vendorName
        ));

        Debug.Log($"[APQuestManager] Accrued {debt.interestPerDay}G interest on debt to {debt.vendorName} (total accrued: {debt.accruedInterest}G)");
    }

    public List<APQuestDTO> CaptureState()
    {
        var dtos = new List<APQuestDTO>();
        foreach (var d in activeDebts)
        {
            dtos.Add(new APQuestDTO
            {
                vendorName = d.vendorName,
                itemName = d.itemName,
                quantity = d.quantity,
                quality = (int)d.quality,
                principalAmount = d.principalAmount,
                interestPerDay = d.interestPerDay,
                dueTotalDay = d.dueTotalDay,
                createdTotalDay = d.createdTotalDay,
                isOverdue = d.isOverdue,
                accruedInterest = d.accruedInterest,
                lastAccruedDay = d.lastAccruedDay,
            });
        }
        return dtos;
    }

    public void RestoreState(List<APQuestDTO> dtos)
    {
        activeDebts.Clear();
        lastCheckedDay = -1;
        if (dtos == null) return;
        foreach (var dto in dtos)
        {
            activeDebts.Add(new APQuestData
            {
                vendorName = dto.vendorName,
                itemName = dto.itemName,
                quantity = dto.quantity,
                quality = dto.quality > 0 ? (ItemQuality)dto.quality : ItemQuality.Common,
                principalAmount = dto.principalAmount,
                interestPerDay = dto.interestPerDay,
                dueTotalDay = dto.dueTotalDay,
                createdTotalDay = dto.createdTotalDay,
                isOverdue = dto.isOverdue,
                accruedInterest = dto.accruedInterest,
                lastAccruedDay = dto.lastAccruedDay,
            });
        }
        Debug.Log($"[APQuestManager] Restored {activeDebts.Count} active debt(s).");
        RefreshDebtStatusForCurrentTime();
    }
}
