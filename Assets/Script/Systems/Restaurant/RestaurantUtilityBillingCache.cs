using System;
using System.Collections.Generic;
using UnityEngine;

public static class RestaurantUtilityBillingCache
{
    public static event System.Action OnAnyBillPaid;

    private struct DeferredTransaction
    {
        public string itemName;
        public ItemCategory category;
        public int quantity;
        public int totalPrice;
        public TransactionType type;
        public StoreType store;
    }

    private static RestaurantServiceManager.UtilityBillingSaveState runtimeState = new RestaurantServiceManager.UtilityBillingSaveState();
    private static readonly List<DeferredTransaction> deferredTransactions = new List<DeferredTransaction>();
    private static bool initialized;
    private static float electricityCostPerOpenMinute = 0.45f;
    private static float waterCostPerDishSold = 3f;
    private static float electricityBaseWeeklyFee = 120f;
    private static int weeklyBillIssueHour = 6;
    private static int weeklyBillIssueMinute = 0;
    private static bool showBillIssuedPopup = true;

    public static void ConfigureBillingRules(
        float electricityCostPerMinute,
        float waterCostPerDish,
        float electricityBaseFee,
        int billIssueHour,
        int billIssueMinute,
        bool showIssuePopup)
    {
        EnsureInitialized();
        electricityCostPerOpenMinute = Mathf.Max(0f, electricityCostPerMinute);
        waterCostPerDishSold = Mathf.Max(0f, waterCostPerDish);
        electricityBaseWeeklyFee = Mathf.Max(0f, electricityBaseFee);
        weeklyBillIssueHour = Mathf.Clamp(billIssueHour, 0, 23);
        weeklyBillIssueMinute = Mathf.Clamp(billIssueMinute, 0, 59);
        showBillIssuedPopup = showIssuePopup;
        AdvanceStateToCurrentTime();
    }

    public static void RefreshTimeState()
    {
        EnsureInitialized();
        AdvanceStateToCurrentTime();
    }

    public static void NotifyShopOpened()
    {
        EnsureInitialized();
        AdvanceStateToCurrentTime();

        if (!TimeManager.TryGetCurrentDateTime(out TimeManager.DateTime now))
        {
            return;
        }

        runtimeState.hasOpenSession = true;
        runtimeState.openSessionStartAbsoluteMinutes = TimeManager.GetAbsoluteMinutes(now);
    }

    public static void NotifyShopClosed()
    {
        EnsureInitialized();
        if (!runtimeState.hasOpenSession)
        {
            return;
        }

        if (TimeManager.TryGetCurrentDateTime(out TimeManager.DateTime now))
        {
            AccumulateOpenMinutesUntil(now);
        }

        runtimeState.hasOpenSession = false;
        runtimeState.openSessionStartAbsoluteMinutes = -1;
    }

    public static void RegisterDishSold(int soldCount = 1)
    {
        EnsureInitialized();
        AdvanceStateToCurrentTime();
        runtimeState.weeklyDishesSold += Mathf.Max(0, soldCount);
    }

    public static RestaurantServiceManager.UtilityBillingSaveState CaptureState()
    {
        EnsureInitialized();
        AdvanceStateToCurrentTime();
        return CloneState(runtimeState);
    }

    public static void ApplyState(RestaurantServiceManager.UtilityBillingSaveState state)
    {
        EnsureInitialized();
        runtimeState = CloneState(state) ?? new RestaurantServiceManager.UtilityBillingSaveState();
        NormalizeState(runtimeState);
        AdvanceStateToCurrentTime();
    }

    public static IReadOnlyList<RestaurantServiceManager.UtilityBillStatement> GetUnpaidBillsSnapshot()
    {
        EnsureInitialized();
        AdvanceStateToCurrentTime();
        NormalizeState(runtimeState);

        List<RestaurantServiceManager.UtilityBillStatement> snapshot = new List<RestaurantServiceManager.UtilityBillStatement>();
        if (runtimeState.issuedBills == null)
        {
            return snapshot;
        }

        for (int i = 0; i < runtimeState.issuedBills.Count; i++)
        {
            RestaurantServiceManager.UtilityBillStatement bill = runtimeState.issuedBills[i];
            if (bill == null)
            {
                continue;
            }

            if (GetTotalOutstanding(bill) <= 0)
            {
                continue;
            }

            if (!IsBillVisibleToPlayer(bill))
            {
                continue;
            }

            snapshot.Add(CloneBill(bill));
        }

        snapshot.Sort((a, b) => a.weekIndex.CompareTo(b.weekIndex));
        return snapshot;
    }

    public static IReadOnlyList<RestaurantServiceManager.UtilityBillStatement> GetUnreadMailboxBillsSnapshot()
    {
        EnsureInitialized();
        AdvanceStateToCurrentTime();
        NormalizeState(runtimeState);

        List<RestaurantServiceManager.UtilityBillStatement> snapshot = new List<RestaurantServiceManager.UtilityBillStatement>();
        if (runtimeState.issuedBills == null)
        {
            return snapshot;
        }

        for (int i = 0; i < runtimeState.issuedBills.Count; i++)
        {
            RestaurantServiceManager.UtilityBillStatement bill = runtimeState.issuedBills[i];
            if (bill == null || bill.mailboxLetterRead || GetTotalOutstanding(bill) <= 0)
            {
                continue;
            }

            snapshot.Add(CloneBill(bill));
        }

        snapshot.Sort((a, b) => a.weekIndex.CompareTo(b.weekIndex));
        return snapshot;
    }

    public static bool TryMarkMailboxLetterRead(string billId)
    {
        EnsureInitialized();
        AdvanceStateToCurrentTime();
        NormalizeState(runtimeState);

        if (!TryFindBill(runtimeState, billId, out RestaurantServiceManager.UtilityBillStatement bill) || bill == null)
        {
            return false;
        }

        bill.mailboxLetterRead = true;
        TryPostBillToJournal(bill);
        return true;
    }

    public static bool TryGetOverdueLockReason(int overdueWeeksBeforeShopLock, out string reason)
    {
        reason = string.Empty;
        if (overdueWeeksBeforeShopLock <= 0)
        {
            return false;
        }

        EnsureInitialized();
        AdvanceStateToCurrentTime();
        if (!TimeManager.TryGetCurrentDateTime(out TimeManager.DateTime now))
        {
            return false;
        }

        NormalizeState(runtimeState);
        if (runtimeState.issuedBills == null)
        {
            return false;
        }

        int currentWeek = TimeManager.GetWeekIndex(now);
        for (int i = 0; i < runtimeState.issuedBills.Count; i++)
        {
            RestaurantServiceManager.UtilityBillStatement bill = runtimeState.issuedBills[i];
            if (bill == null)
            {
                continue;
            }

            if (GetTotalOutstanding(bill) <= 0)
            {
                continue;
            }

            int overdueWeeks = currentWeek - bill.weekIndex;
            if (overdueWeeks >= overdueWeeksBeforeShopLock)
            {
                reason = $"Utility bills are overdue by {overdueWeeksBeforeShopLock} weeks. Please pay before opening the shop.";
                return true;
            }
        }

        return false;
    }

    public static bool TryPayCharge(
        string billId,
        RestaurantServiceManager.UtilityChargeType chargeType,
        out string failureReason,
        bool showResultNotice = true)
    {
        failureReason = string.Empty;
        EnsureInitialized();
        AdvanceStateToCurrentTime();
        NormalizeState(runtimeState);
        if (!TryFindBill(runtimeState, billId, out RestaurantServiceManager.UtilityBillStatement bill))
        {
            failureReason = "Bill not found.";
            return false;
        }

        if (!IsBillVisibleToPlayer(bill))
        {
            failureReason = "Read the utility bill from the mailbox first.";
            if (showResultNotice)
            {
                ShowUtilityNotice(failureReason);
            }

            return false;
        }

        int amountDue = GetOutstandingAmountForCharge(bill, chargeType);
        if (amountDue <= 0)
        {
            failureReason = $"No outstanding {GetChargeDisplayName(chargeType)} in this bill.";
            return false;
        }

        if (!TrySpendMoney(amountDue))
        {
            failureReason = $"Not enough cash to pay {GetChargeDisplayName(chargeType)} (${amountDue:N0}).";
            if (showResultNotice)
            {
                ShowUtilityNotice(failureReason);
            }

            return false;
        }

        bool hasNow = TimeManager.TryGetCurrentDateTime(out TimeManager.DateTime now);
        bool sameJournalEntryDate = hasNow && IsSameDate(now, GetJournalEntryPostedDate(bill));
        bool wasAccrued = IsChargeAccrued(bill, chargeType);

        if (wasAccrued)
        {
            if (sameJournalEntryDate)
            {
                ReplaceAccruedWithCashExpenseOnSameDay(bill, chargeType, amountDue);
            }
            else
            {
                PostAccruedSettlementTransaction(bill, chargeType, amountDue);
            }
        }

        MarkChargeAsPaid(bill, chargeType, hasNow, now);
        RefreshBillAggregateState(bill);
        NormalizeState(runtimeState);
        MailboxInteractable.Instance?.RebuildPendingMail();

        OnAnyBillPaid?.Invoke();

        if (showResultNotice)
        {
            ShowUtilityNotice($"Paid {GetChargeDisplayName(chargeType)}: ${amountDue:N0}.");
        }

        return true;
    }

    private static void EnsureInitialized()
    {
        if (!initialized)
        {
            TimeManager.OnDateTimeChanged -= HandleDateTimeChanged;
            TimeManager.OnDateTimeChanged += HandleDateTimeChanged;
            initialized = true;
        }

        FlushDeferredTransactionsIfPossible();
    }

    private static void HandleDateTimeChanged(TimeManager.DateTime currentDateTime)
    {
        AdvanceStateTo(currentDateTime);
    }

    private static void AdvanceStateToCurrentTime()
    {
        if (!TimeManager.TryGetCurrentDateTime(out TimeManager.DateTime currentDateTime))
        {
            return;
        }

        AdvanceStateTo(currentDateTime);
    }

    private static void AdvanceStateTo(TimeManager.DateTime currentDateTime)
    {
        int currentWeek = TimeManager.GetWeekIndex(currentDateTime);

        if (runtimeState.trackedWeekIndex < 0)
        {
            runtimeState.trackedWeekIndex = currentWeek;
            if (runtimeState.lastBilledWeekIndex < 0)
            {
                runtimeState.lastBilledWeekIndex = currentWeek - 1;
            }

            TryIssuePendingWeeklyBillIfDue(currentDateTime);
            return;
        }

        if (currentWeek != runtimeState.trackedWeekIndex)
        {
            if (runtimeState.hasOpenSession)
            {
                AccumulateOpenMinutesUntil(currentDateTime);
                runtimeState.openSessionStartAbsoluteMinutes = TimeManager.GetAbsoluteMinutes(currentDateTime);
                runtimeState.hasOpenSession = true;
            }

            QueueCompletedWeekForBilling(
                runtimeState.trackedWeekIndex,
                runtimeState.weeklyOpenMinutes,
                runtimeState.weeklyDishesSold);

            runtimeState.trackedWeekIndex = currentWeek;
            runtimeState.weeklyOpenMinutes = 0;
            runtimeState.weeklyDishesSold = 0;
        }

        TryIssuePendingWeeklyBillIfDue(currentDateTime);
    }

    private static void QueueCompletedWeekForBilling(int weekToBill, int openMinutesForWeek, int dishesSoldForWeek)
    {
        if (weekToBill <= runtimeState.lastBilledWeekIndex)
        {
            return;
        }

        if (runtimeState.pendingBillWeekIndex > runtimeState.lastBilledWeekIndex)
        {
            if (runtimeState.pendingBillWeekIndex == weekToBill)
            {
                runtimeState.pendingBillOpenMinutes = Mathf.Max(0, openMinutesForWeek);
                runtimeState.pendingBillDishesSold = Mathf.Max(0, dishesSoldForWeek);
            }

            return;
        }

        runtimeState.pendingBillWeekIndex = weekToBill;
        runtimeState.pendingBillOpenMinutes = Mathf.Max(0, openMinutesForWeek);
        runtimeState.pendingBillDishesSold = Mathf.Max(0, dishesSoldForWeek);
    }

    private static void TryIssuePendingWeeklyBillIfDue(TimeManager.DateTime currentDateTime)
    {
        if (runtimeState.pendingBillWeekIndex <= runtimeState.lastBilledWeekIndex)
        {
            ClearPendingBillQueue();
            return;
        }

        if (!HasReachedWeeklyBillIssueTime(currentDateTime))
        {
            return;
        }

        TryIssueWeeklyUtilityBill(
            runtimeState.pendingBillWeekIndex,
            currentDateTime,
            runtimeState.pendingBillOpenMinutes,
            runtimeState.pendingBillDishesSold);

        ClearPendingBillQueue();
    }

    private static bool HasReachedWeeklyBillIssueTime(TimeManager.DateTime currentDateTime)
    {
        if (currentDateTime.Day == TimeManager.Days.Mon)
        {
            int currentMinuteOfDay = TimeManager.GetMinuteOfDay(currentDateTime);
            int configuredMinuteOfDay = (Mathf.Clamp(weeklyBillIssueHour, 0, 23) * 60) + Mathf.Clamp(weeklyBillIssueMinute, 0, 59);
            return currentMinuteOfDay >= configuredMinuteOfDay;
        }

        return (int)currentDateTime.Day > (int)TimeManager.Days.Mon;
    }

    private static void TryIssueWeeklyUtilityBill(
        int weekToBill,
        TimeManager.DateTime issueTime,
        int openMinutesForWeek,
        int dishesSoldForWeek)
    {
        if (weekToBill <= runtimeState.lastBilledWeekIndex)
        {
            return;
        }

        int billOpenMinutes = Mathf.Max(0, openMinutesForWeek);
        int billDishesSold = Mathf.Max(0, dishesSoldForWeek);
        if (billOpenMinutes <= 0 && billDishesSold <= 0)
        {
            runtimeState.lastBilledWeekIndex = weekToBill;
            return;
        }

        int electricityAmount = Mathf.RoundToInt(
            Mathf.Max(0f, electricityBaseWeeklyFee + (billOpenMinutes * electricityCostPerOpenMinute)));
        int waterAmount = Mathf.RoundToInt(
            Mathf.Max(0f, billDishesSold * waterCostPerDishSold));
        int totalAmount = electricityAmount + waterAmount;
        int electricityQuantity = Mathf.Max(1, billOpenMinutes);
        int waterQuantity = Mathf.Max(1, billDishesSold);

        if (runtimeState.issuedBills == null)
        {
            runtimeState.issuedBills = new List<RestaurantServiceManager.UtilityBillStatement>();
        }

        RestaurantServiceManager.UtilityBillStatement statement = new RestaurantServiceManager.UtilityBillStatement
        {
            billId = BuildUtilityBillId(issueTime, weekToBill),
            weekIndex = weekToBill,
            openMinutes = billOpenMinutes,
            dishesSold = billDishesSold,
            electricityAmount = electricityAmount,
            waterAmount = waterAmount,
            totalAmount = totalAmount,
            isPaid = false,
            isAccrued = false,
            outstandingAmount = totalAmount,
            hasPaidAt = false,
            issuedAt = issueTime,
            electricityQuantity = electricityQuantity,
            waterQuantity = waterQuantity,
            electricityOutstandingAmount = electricityAmount,
            waterOutstandingAmount = waterAmount,
            electricityIsAccrued = false,
            waterIsAccrued = false,
            electricityPaid = electricityAmount <= 0,
            waterPaid = waterAmount <= 0,
            hasElectricityPaidAt = false,
            hasWaterPaidAt = false,
            mailboxLetterRead = false,
            journalEntryPosted = false,
            hasJournalEntryPostedAt = false,
        };

        RefreshBillAggregateState(statement);
        runtimeState.issuedBills.Add(statement);

        runtimeState.lastBilledWeekIndex = weekToBill;

        MailboxInteractable.Instance?.AddUtilityBillLetter(statement);

        if (showBillIssuedPopup)
        {
            ShowUtilityNotice($"Weekly utility bill delivered to mailbox: ${totalAmount:N0}.");
        }
    }

    private static void ClearPendingBillQueue()
    {
        runtimeState.pendingBillWeekIndex = -1;
        runtimeState.pendingBillOpenMinutes = 0;
        runtimeState.pendingBillDishesSold = 0;
    }

    private static void AccumulateOpenMinutesUntil(TimeManager.DateTime endTime)
    {
        if (runtimeState.openSessionStartAbsoluteMinutes < 0)
        {
            return;
        }

        int endAbsoluteMinutes = TimeManager.GetAbsoluteMinutes(endTime);
        int elapsed = Mathf.Max(0, endAbsoluteMinutes - runtimeState.openSessionStartAbsoluteMinutes);
        runtimeState.weeklyOpenMinutes += elapsed;
    }

    private static string BuildUtilityBillId(TimeManager.DateTime issueTime, int weekIndex)
    {
        return $"UTIL-Y{issueTime.Year:D2}-S{((int)issueTime.Season + 1):D1}-W{Mathf.Max(1, weekIndex):D3}";
    }

    private static void PostUtilityExpenseRecordForCharge(
        RestaurantServiceManager.UtilityBillStatement bill,
        RestaurantServiceManager.UtilityChargeType chargeType,
        TransactionType transactionType,
        int amount)
    {
        if (bill == null || amount <= 0)
        {
            return;
        }

        ItemCategory category = chargeType == RestaurantServiceManager.UtilityChargeType.Electricity
            ? ItemCategory.UtilitiesElectricity
            : ItemCategory.UtilitiesWater;
        int quantity = GetChargeQuantity(bill, chargeType);
        string itemName = BuildUtilityLineItemName(chargeType, bill.weekIndex);

        if (TransactionManager.Instance == null)
        {
            deferredTransactions.Add(new DeferredTransaction
            {
                itemName = itemName,
                category = category,
                quantity = quantity,
                totalPrice = amount,
                type = transactionType,
                store = StoreType.Restaurant,
            });
            return;
        }

        TransactionManager.Instance.AddRecord(new TransactionRecord(
            itemName,
            category,
            quantity,
            amount,
            transactionType,
            StoreType.Restaurant));
    }

    private static void FlushDeferredTransactionsIfPossible()
    {
        if (TransactionManager.Instance == null || deferredTransactions.Count == 0)
        {
            return;
        }

        for (int i = 0; i < deferredTransactions.Count; i++)
        {
            DeferredTransaction pending = deferredTransactions[i];
            TransactionManager.Instance.AddRecord(new TransactionRecord(
                pending.itemName,
                pending.category,
                Mathf.Max(0, pending.quantity),
                Mathf.Max(0, pending.totalPrice),
                pending.type,
                pending.store));
        }

        deferredTransactions.Clear();
    }

    private static bool TryFindBill(
        RestaurantServiceManager.UtilityBillingSaveState state,
        string billId,
        out RestaurantServiceManager.UtilityBillStatement bill)
    {
        bill = null;
        if (state == null || state.issuedBills == null || string.IsNullOrWhiteSpace(billId))
        {
            return false;
        }

        for (int i = state.issuedBills.Count - 1; i >= 0; i--)
        {
            RestaurantServiceManager.UtilityBillStatement candidate = state.issuedBills[i];
            if (candidate == null || !string.Equals(candidate.billId, billId, StringComparison.Ordinal))
            {
                continue;
            }

            bill = candidate;
            return true;
        }

        return false;
    }

    private static void NormalizeState(RestaurantServiceManager.UtilityBillingSaveState state)
    {
        if (state == null)
        {
            return;
        }

        state.weeklyOpenMinutes = Mathf.Max(0, state.weeklyOpenMinutes);
        state.weeklyDishesSold = Mathf.Max(0, state.weeklyDishesSold);
        state.pendingBillOpenMinutes = Mathf.Max(0, state.pendingBillOpenMinutes);
        state.pendingBillDishesSold = Mathf.Max(0, state.pendingBillDishesSold);
        if (state.pendingBillWeekIndex <= state.lastBilledWeekIndex)
        {
            state.pendingBillWeekIndex = -1;
            state.pendingBillOpenMinutes = 0;
            state.pendingBillDishesSold = 0;
        }

        if (state.issuedBills == null)
        {
            return;
        }

        for (int i = 0; i < state.issuedBills.Count; i++)
        {
            NormalizeBill(state.issuedBills[i]);
        }
    }

    private static void NormalizeBill(RestaurantServiceManager.UtilityBillStatement bill)
    {
        if (bill == null)
        {
            return;
        }

        bool hasPerChargeData = bill.electricityQuantity > 0
            || bill.waterQuantity > 0
            || bill.electricityOutstandingAmount > 0
            || bill.waterOutstandingAmount > 0
            || bill.electricityPaid
            || bill.waterPaid;

        if (!hasPerChargeData)
        {
            bill.electricityQuantity = Mathf.Max(1, bill.openMinutes);
            bill.waterQuantity = Mathf.Max(1, bill.dishesSold);

            bool paid = bill.isPaid || bill.outstandingAmount <= 0;
            bill.electricityOutstandingAmount = paid ? 0 : Mathf.Max(0, bill.electricityAmount);
            bill.waterOutstandingAmount = paid ? 0 : Mathf.Max(0, bill.waterAmount);
            bill.electricityPaid = bill.electricityOutstandingAmount <= 0;
            bill.waterPaid = bill.waterOutstandingAmount <= 0;
            bool defaultAccrued = !paid && (bill.isAccrued || bill.outstandingAmount > 0);
            bill.electricityIsAccrued = bill.electricityOutstandingAmount > 0 && defaultAccrued;
            bill.waterIsAccrued = bill.waterOutstandingAmount > 0 && defaultAccrued;
        }

        if (!bill.journalEntryPosted && (bill.mailboxLetterRead || bill.electricityIsAccrued || bill.waterIsAccrued))
        {
            bill.journalEntryPosted = true;
        }

        if (bill.journalEntryPosted && !bill.hasJournalEntryPostedAt)
        {
            bill.hasJournalEntryPostedAt = true;
            bill.journalEntryPostedAt = bill.issuedAt;
        }

        if (!bill.journalEntryPosted)
        {
            bill.electricityIsAccrued = false;
            bill.waterIsAccrued = false;
        }

        bill.electricityQuantity = Mathf.Max(1, bill.electricityQuantity > 0 ? bill.electricityQuantity : bill.openMinutes);
        bill.waterQuantity = Mathf.Max(1, bill.waterQuantity > 0 ? bill.waterQuantity : bill.dishesSold);
        bill.electricityOutstandingAmount = Mathf.Max(0, bill.electricityOutstandingAmount);
        bill.waterOutstandingAmount = Mathf.Max(0, bill.waterOutstandingAmount);
        bill.electricityPaid = bill.electricityOutstandingAmount <= 0;
        bill.waterPaid = bill.waterOutstandingAmount <= 0;

        RefreshBillAggregateState(bill);
    }

    private static int GetTotalOutstanding(RestaurantServiceManager.UtilityBillStatement bill)
    {
        if (bill == null)
        {
            return 0;
        }

        return Mathf.Max(0, bill.electricityOutstandingAmount) + Mathf.Max(0, bill.waterOutstandingAmount);
    }

    private static int GetOutstandingAmountForCharge(
        RestaurantServiceManager.UtilityBillStatement bill,
        RestaurantServiceManager.UtilityChargeType chargeType)
    {
        if (bill == null)
        {
            return 0;
        }

        return chargeType == RestaurantServiceManager.UtilityChargeType.Electricity
            ? Mathf.Max(0, bill.electricityOutstandingAmount)
            : Mathf.Max(0, bill.waterOutstandingAmount);
    }

    private static bool IsBillVisibleToPlayer(RestaurantServiceManager.UtilityBillStatement bill)
    {
        return bill != null && (bill.mailboxLetterRead || bill.journalEntryPosted);
    }

    private static bool IsChargeAccrued(
        RestaurantServiceManager.UtilityBillStatement bill,
        RestaurantServiceManager.UtilityChargeType chargeType)
    {
        if (bill == null)
        {
            return false;
        }

        return chargeType == RestaurantServiceManager.UtilityChargeType.Electricity
            ? bill.electricityIsAccrued
            : bill.waterIsAccrued;
    }

    private static void SetChargeAccrued(
        RestaurantServiceManager.UtilityBillStatement bill,
        RestaurantServiceManager.UtilityChargeType chargeType,
        bool accrued)
    {
        if (bill == null)
        {
            return;
        }

        if (chargeType == RestaurantServiceManager.UtilityChargeType.Electricity)
        {
            bill.electricityIsAccrued = accrued;
        }
        else
        {
            bill.waterIsAccrued = accrued;
        }
    }

    private static int GetChargeQuantity(
        RestaurantServiceManager.UtilityBillStatement bill,
        RestaurantServiceManager.UtilityChargeType chargeType)
    {
        if (bill == null)
        {
            return 1;
        }

        return chargeType == RestaurantServiceManager.UtilityChargeType.Electricity
            ? Mathf.Max(1, bill.electricityQuantity)
            : Mathf.Max(1, bill.waterQuantity);
    }

    private static string BuildUtilityLineItemName(
        RestaurantServiceManager.UtilityChargeType chargeType,
        int weekIndex)
    {
        return chargeType == RestaurantServiceManager.UtilityChargeType.Electricity
            ? $"Electricity Bill (Week {weekIndex})"
            : $"Water Bill (Week {weekIndex})";
    }

    private static string GetChargeDisplayName(RestaurantServiceManager.UtilityChargeType chargeType)
    {
        return chargeType == RestaurantServiceManager.UtilityChargeType.Electricity ? "electricity" : "water";
    }

    private static bool TryPostBillToJournal(RestaurantServiceManager.UtilityBillStatement bill)
    {
        if (bill == null)
        {
            return false;
        }

        bool postedAnyCharge = false;
        postedAnyCharge |= TryPostBillChargeToJournal(bill, RestaurantServiceManager.UtilityChargeType.Electricity);
        postedAnyCharge |= TryPostBillChargeToJournal(bill, RestaurantServiceManager.UtilityChargeType.Water);

        if (!bill.journalEntryPosted)
        {
            bill.journalEntryPosted = true;
        }

        if (!bill.hasJournalEntryPostedAt && TimeManager.TryGetCurrentDateTime(out TimeManager.DateTime now))
        {
            bill.hasJournalEntryPostedAt = true;
            bill.journalEntryPostedAt = now;
        }

        RefreshBillAggregateState(bill);
        return postedAnyCharge;
    }

    private static bool TryPostBillChargeToJournal(
        RestaurantServiceManager.UtilityBillStatement bill,
        RestaurantServiceManager.UtilityChargeType chargeType)
    {
        if (bill == null || IsChargeAccrued(bill, chargeType))
        {
            return false;
        }

        int amountToPost = GetOutstandingAmountForCharge(bill, chargeType);
        if (amountToPost <= 0)
        {
            return false;
        }

        PostUtilityExpenseRecordForCharge(
            bill,
            chargeType,
            TransactionType.BillAccrued,
            amountToPost);
        SetChargeAccrued(bill, chargeType, true);
        return true;
    }

    private static TimeManager.DateTime GetJournalEntryPostedDate(RestaurantServiceManager.UtilityBillStatement bill)
    {
        if (bill != null && bill.hasJournalEntryPostedAt)
        {
            return bill.journalEntryPostedAt;
        }

        return bill != null ? bill.issuedAt : default(TimeManager.DateTime);
    }

    private static void ReplaceAccruedWithCashExpenseOnSameDay(
        RestaurantServiceManager.UtilityBillStatement bill,
        RestaurantServiceManager.UtilityChargeType chargeType,
        int amount)
    {
        if (bill == null || TransactionManager.Instance == null || amount <= 0)
        {
            return;
        }

        string itemName = BuildUtilityLineItemName(chargeType, bill.weekIndex);
        ItemCategory category = chargeType == RestaurantServiceManager.UtilityChargeType.Electricity
            ? ItemCategory.UtilitiesElectricity
            : ItemCategory.UtilitiesWater;
        int quantity = GetChargeQuantity(bill, chargeType);

        TransactionManager.Instance.TryRemoveRecordPortion(
            itemName,
            category,
            TransactionType.BillAccrued,
            StoreType.Restaurant,
            quantity,
            amount);

        TransactionManager.Instance.AddRecord(new TransactionRecord(
            itemName,
            category,
            quantity,
            amount,
            TransactionType.Bill,
            StoreType.Restaurant));

        SetChargeAccrued(bill, chargeType, false);
    }

    private static void PostAccruedSettlementTransaction(
        RestaurantServiceManager.UtilityBillStatement bill,
        RestaurantServiceManager.UtilityChargeType chargeType,
        int amount)
    {
        if (bill == null || TransactionManager.Instance == null || amount <= 0)
        {
            return;
        }

        string itemName = $"Utility Payment ({GetChargeDisplayName(chargeType)}) Week {bill.weekIndex}";
        TransactionManager.Instance.AddRecord(new TransactionRecord(
            itemName,
            ItemCategory.AccruedExpenses,
            1,
            amount,
            TransactionType.BillSettlement,
            StoreType.Restaurant));
    }

    private static void MarkChargeAsPaid(
        RestaurantServiceManager.UtilityBillStatement bill,
        RestaurantServiceManager.UtilityChargeType chargeType,
        bool hasNow,
        TimeManager.DateTime now)
    {
        if (bill == null)
        {
            return;
        }

        if (chargeType == RestaurantServiceManager.UtilityChargeType.Electricity)
        {
            bill.electricityOutstandingAmount = 0;
            bill.electricityPaid = true;
            bill.electricityIsAccrued = false;
            if (hasNow)
            {
                bill.hasElectricityPaidAt = true;
                bill.electricityPaidAt = now;
            }
        }
        else
        {
            bill.waterOutstandingAmount = 0;
            bill.waterPaid = true;
            bill.waterIsAccrued = false;
            if (hasNow)
            {
                bill.hasWaterPaidAt = true;
                bill.waterPaidAt = now;
            }
        }

        if (bill.electricityOutstandingAmount <= 0 && bill.waterOutstandingAmount <= 0 && hasNow)
        {
            bill.hasPaidAt = true;
            bill.paidAt = now;
        }
    }

    private static void RefreshBillAggregateState(RestaurantServiceManager.UtilityBillStatement bill)
    {
        if (bill == null)
        {
            return;
        }

        bill.totalAmount = Mathf.Max(0, bill.electricityAmount) + Mathf.Max(0, bill.waterAmount);
        bill.outstandingAmount = GetTotalOutstanding(bill);
        bill.electricityPaid = bill.electricityOutstandingAmount <= 0;
        bill.waterPaid = bill.waterOutstandingAmount <= 0;
        bill.isPaid = bill.outstandingAmount <= 0;
        bill.isAccrued = (bill.electricityOutstandingAmount > 0 && bill.electricityIsAccrued)
            || (bill.waterOutstandingAmount > 0 && bill.waterIsAccrued);

        if (bill.isPaid)
        {
            bill.outstandingAmount = 0;
        }
    }

    private static bool TrySpendMoney(int amount)
    {
        if (amount <= 0)
        {
            return true;
        }

        InventoryController inventoryController = null;
        if (GameManager.instance != null && GameManager.instance.player != null)
        {
            inventoryController = GameManager.instance.player.GetComponent<Player>()?.GetInventoryController();
        }

        if (inventoryController == null)
        {
            inventoryController = UnityEngine.Object.FindObjectOfType<InventoryController>();
        }

        if (inventoryController == null || inventoryController.money < amount)
        {
            return false;
        }

        inventoryController.SpendMoney(amount);
        return true;
    }

    private static bool IsSameDate(TimeManager.DateTime a, TimeManager.DateTime b)
    {
        return a.Year == b.Year && a.Season == b.Season && a.Date == b.Date;
    }

    private static void ShowUtilityNotice(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        Vector3 anchor = Vector3.zero;
        if (GameManager.instance != null && GameManager.instance.player != null)
        {
            anchor = GameManager.instance.player.transform.position + new Vector3(0f, 1.2f, 0f);
        }

        CashPopupManager.ShowInfoPopup(anchor, message);
    }

    private static RestaurantServiceManager.UtilityBillingSaveState CloneState(RestaurantServiceManager.UtilityBillingSaveState source)
    {
        if (source == null)
        {
            return new RestaurantServiceManager.UtilityBillingSaveState();
        }

        RestaurantServiceManager.UtilityBillingSaveState clone = new RestaurantServiceManager.UtilityBillingSaveState
        {
            trackedWeekIndex = source.trackedWeekIndex,
            lastBilledWeekIndex = source.lastBilledWeekIndex,
            weeklyOpenMinutes = source.weeklyOpenMinutes,
            weeklyDishesSold = source.weeklyDishesSold,
            pendingBillWeekIndex = source.pendingBillWeekIndex,
            pendingBillOpenMinutes = source.pendingBillOpenMinutes,
            pendingBillDishesSold = source.pendingBillDishesSold,
            hasOpenSession = source.hasOpenSession,
            openSessionStartAbsoluteMinutes = source.openSessionStartAbsoluteMinutes,
            issuedBills = new List<RestaurantServiceManager.UtilityBillStatement>(),
        };

        if (source.issuedBills != null)
        {
            for (int i = 0; i < source.issuedBills.Count; i++)
            {
                RestaurantServiceManager.UtilityBillStatement bill = source.issuedBills[i];
                if (bill != null)
                {
                    clone.issuedBills.Add(CloneBill(bill));
                }
            }
        }

        return clone;
    }

    private static RestaurantServiceManager.UtilityBillStatement CloneBill(RestaurantServiceManager.UtilityBillStatement source)
    {
        if (source == null)
        {
            return null;
        }

        return new RestaurantServiceManager.UtilityBillStatement
        {
            billId = source.billId,
            weekIndex = source.weekIndex,
            openMinutes = source.openMinutes,
            dishesSold = source.dishesSold,
            electricityAmount = source.electricityAmount,
            waterAmount = source.waterAmount,
            totalAmount = source.totalAmount,
            isPaid = source.isPaid,
            isAccrued = source.isAccrued,
            outstandingAmount = source.outstandingAmount,
            hasPaidAt = source.hasPaidAt,
            issuedAt = source.issuedAt,
            paidAt = source.paidAt,
            electricityQuantity = source.electricityQuantity,
            waterQuantity = source.waterQuantity,
            electricityOutstandingAmount = source.electricityOutstandingAmount,
            waterOutstandingAmount = source.waterOutstandingAmount,
            electricityIsAccrued = source.electricityIsAccrued,
            waterIsAccrued = source.waterIsAccrued,
            electricityPaid = source.electricityPaid,
            waterPaid = source.waterPaid,
            hasElectricityPaidAt = source.hasElectricityPaidAt,
            hasWaterPaidAt = source.hasWaterPaidAt,
            electricityPaidAt = source.electricityPaidAt,
            waterPaidAt = source.waterPaidAt,
            mailboxLetterRead = source.mailboxLetterRead,
            journalEntryPosted = source.journalEntryPosted,
            hasJournalEntryPostedAt = source.hasJournalEntryPostedAt,
            journalEntryPostedAt = source.journalEntryPostedAt,
        };
    }
}

