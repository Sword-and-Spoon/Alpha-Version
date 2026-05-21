using System;
using System.Collections.Generic;
using UnityEngine;

public class RestaurantServiceManager : MonoBehaviour
{
    public enum UtilityChargeType
    {
        Electricity,
        Water,
    }

    [Header("Core References")]
    [HideInInspector][SerializeField] private RestaurantCounter counter; // legacy single-counter ref
    [HideInInspector][SerializeField] private List<RestaurantCounter> counters = new();
    [HideInInspector][SerializeField] private bool autoFindCounters = true;

    [HideInInspector][SerializeField] private RestaurantTableManager tableManager; // legacy single-manager ref
    [HideInInspector][SerializeField] private List<RestaurantTableManager> tableManagers = new();
    [HideInInspector][SerializeField] private bool autoFindTableManagers = true;
    [HideInInspector][SerializeField] private bool autoCreateSceneSeatsFromChairObjects = true;
    [HideInInspector][SerializeField] private string seatChairNamePrefix = "Chair";

    [SerializeField] private RestaurantCustomerAI customerPrefab;

    [Header("Waypoints")]
    [SerializeField] private Transform entrancePoint;
    [HideInInspector][SerializeField] private Transform counterPoint;
    [SerializeField] private Transform exitPoint;

    [Header("Spawn")]
    [SerializeField] private bool openOnStart;
    [SerializeField] private int maxCustomersInside = 8;
    [SerializeField] private Vector2 spawnIntervalRangeSeconds = new Vector2(4f, 9f);
    [SerializeField] private bool requireFoodOnCounterToSpawn = true;
    [HideInInspector][SerializeField] private float spawnJitterRadius = 0.45f;
    [HideInInspector][SerializeField] private int spawnPositionSearchAttempts = 12;
    [HideInInspector][SerializeField] private float spawnProbeRadius = 0.08f;
    [HideInInspector][SerializeField] private LayerMask spawnObstacleMask = ~0;
    [HideInInspector][SerializeField] private bool spawnIncludeTriggers;

    [Header("Operating Hours")]
    [SerializeField] private bool enforceOperatingHours = true;
    [SerializeField][Range(0, 23)] private int openHour = 17;
    [SerializeField][Range(0, 59)] private int openMinute = 0;
    [SerializeField][Range(0, 23)] private int closeHour = 22;
    [SerializeField][Range(0, 59)] private int closeMinute = 0;

    [Header("Weekly Utility Billing")]
    [SerializeField] private bool enableWeeklyUtilityBilling = true;
    [SerializeField][Min(0f)] private float electricityCostPerOpenMinute = 0.45f;
    [SerializeField][Min(0f)] private float waterCostPerDishSold = 3f;
    [SerializeField][Min(0f)] private float electricityBaseWeeklyFee = 120f;
    [SerializeField] private bool showBillIssuedPopup = true;
    [SerializeField][Range(0, 23)] private int weeklyBillIssueHour = 6;
    [SerializeField][Range(0, 59)] private int weeklyBillIssueMinute = 0;
    // [HideInInspector][SerializeField] private bool issueBillOnSundayClose = true; // legacy
    [SerializeField][Min(1)] private int overdueWeeksBeforeShopLock = 2;

    [Serializable]
    public class UtilityBillStatement
    {
        public string billId;
        public int weekIndex;
        public int openMinutes;
        public int dishesSold;
        public int electricityAmount;
        public int waterAmount;
        public int totalAmount;
        public bool isPaid;
        public bool isAccrued;
        public int outstandingAmount;
        public bool hasPaidAt;
        public TimeManager.DateTime issuedAt;
        public TimeManager.DateTime paidAt;

        public int electricityQuantity;
        public int waterQuantity;
        public int electricityOutstandingAmount;
        public int waterOutstandingAmount;
        public bool electricityIsAccrued;
        public bool waterIsAccrued;
        public bool electricityPaid;
        public bool waterPaid;
        public bool hasElectricityPaidAt;
        public bool hasWaterPaidAt;
        public TimeManager.DateTime electricityPaidAt;
        public TimeManager.DateTime waterPaidAt;
        public bool mailboxLetterRead;
        public bool journalEntryPosted;
        public bool hasJournalEntryPostedAt;
        public TimeManager.DateTime journalEntryPostedAt;
    }

    [Serializable]
    public class UtilityBillingSaveState
    {
        public int trackedWeekIndex = -1;
        public int lastBilledWeekIndex = -1;
        public int weeklyOpenMinutes;
        public int weeklyDishesSold;
        public int pendingBillWeekIndex = -1;
        public int pendingBillOpenMinutes;
        public int pendingBillDishesSold;
        public bool hasOpenSession;
        public int openSessionStartAbsoluteMinutes = -1;
        public List<UtilityBillStatement> issuedBills = new List<UtilityBillStatement>();
    }

    private readonly List<RestaurantCustomerAI> activeCustomers = new();
    private readonly Dictionary<RestaurantCounter, int> pendingCounterReservations = new();

    private RestaurantOpenCloseInteractable restaurantToggle;
    private InventoryController inventoryController;
    private float spawnTimer;
    private float nextSpawnDelay;
    private bool hasAnnouncedFirstSaleThisSession;

    public static RestaurantServiceManager ActiveInstance { get; private set; }
    public static event Action OnShopOpened;
    public static event Action OnFirstSaleMade;
    public static event Action OnShopClosed;
    public bool IsOpen { get; private set; }
    public int ActiveCustomerCount => activeCustomers.Count;
    public IReadOnlyList<UtilityBillStatement> IssuedUtilityBills
        => RestaurantUtilityBillingCache.CaptureState().issuedBills;

    public event Action<bool> OnShopStateChanged;

    private void Awake()
    {
        ActiveInstance = this;
        ResolveReferences();
        SetNextSpawnDelay();
        RestaurantUtilityBillingCache.ConfigureBillingRules(
            electricityCostPerOpenMinute,
            waterCostPerDishSold,
            electricityBaseWeeklyFee,
            weeklyBillIssueHour,
            weeklyBillIssueMinute,
            showBillIssuedPopup);
        RestaurantUtilityBillingCache.RefreshTimeState();
    }

    private void OnEnable()
    {
        TimeManager.OnDateTimeChanged -= HandleDateTimeChanged;
        TimeManager.OnDateTimeChanged += HandleDateTimeChanged;
    }

    private void OnDisable()
    {
        EndOpenSessionTracking();
        TimeManager.OnDateTimeChanged -= HandleDateTimeChanged;

        if (ActiveInstance == this)
        {
            ActiveInstance = null;
        }
    }

    private void Start()
    {
        if (openOnStart)
        {
            OpenShop(showBlockedNotice: false);
        }
        else
        {
            IsOpen = false;
        }

    }

    private void Update()
    {
        CleanupNullCustomers();
        EnforceOperatingHoursIfNeeded();

        if (!IsOpen) return;
        if (customerPrefab == null) return;
        if (entrancePoint == null) return;

        if (GetActiveInsideCount() >= Mathf.Max(1, maxCustomersInside))
        {
            return;
        }

        if (requireFoodOnCounterToSpawn && GetTotalAvailableCounterFood() <= 0)
        {
            return;
        }

        spawnTimer += Time.deltaTime;
        if (spawnTimer >= nextSpawnDelay)
        {
            SpawnCustomer();
            spawnTimer = 0f;
            SetNextSpawnDelay();
        }
    }

    public void OpenShop()
    {
        OpenShop(showBlockedNotice: true);
    }

    public bool OpenShop(bool showBlockedNotice)
    {
        ResolveReferences();

        if (IsOpen)
        {
            return true;
        }

        if (!CanOpenShopNow(out string blockedReason))
        {
            if (showBlockedNotice)
            {
                ShowShopNotice(blockedReason);
            }

            return false;
        }

        IsOpen = true;
        spawnTimer = 0f;
        SetNextSpawnDelay();
        hasAnnouncedFirstSaleThisSession = false;
        BeginOpenSessionTracking();
        OnShopStateChanged?.Invoke(true);
        OnShopOpened?.Invoke();
        return true;
    }

    public void CloseShop(bool forceCustomersToExit = true)
    {
        if (!IsOpen)
        {
            return;
        }

        EndOpenSessionTracking();

        IsOpen = false;
        OnShopStateChanged?.Invoke(false);
        OnShopClosed?.Invoke();

        if (!forceCustomersToExit) return;

        for (int i = 0; i < activeCustomers.Count; i++)
        {
            RestaurantCustomerAI customerAI = activeCustomers[i];
            if (customerAI != null)
            {
                customerAI.ForceExitNow();
            }
        }
    }

    public void RegisterSale(RestaurantCounter.ServedFood soldFood)
    {
        Vector3 fallbackPopupPosition = counter != null ? counter.transform.position : transform.position;
        RegisterSale(soldFood, fallbackPopupPosition);
    }

    public void RegisterSale(RestaurantCounter.ServedFood soldFood, Vector3 popupWorldPosition)
    {
        ResolveReferences();

        if (soldFood.totalPrice > 0 && inventoryController != null)
        {
            inventoryController.AddMoney(soldFood.totalPrice);
        }

        if (soldFood.totalPrice > 0 && !hasAnnouncedFirstSaleThisSession)
        {
            hasAnnouncedFirstSaleThisSession = true;
            OnFirstSaleMade?.Invoke();
        }

        if (soldFood.totalPrice > 0)
        {
            CashPopupManager.ShowSalePopup(popupWorldPosition, soldFood.totalPrice);
        }

        if (TransactionManager.Instance != null)
        {
            TransactionRecord record = new TransactionRecord(
                soldFood.itemName,
                soldFood.category,
                1,
                soldFood.totalPrice,
                TransactionType.Sell,
                StoreType.Restaurant
            );

            TransactionManager.Instance.AddRecord(record);
        }

        if (enableWeeklyUtilityBilling)
        {
            RestaurantUtilityBillingCache.RegisterDishSold(1);
        }
    }

    public bool TryReserveSeat(RestaurantCustomerAI customer, out RestaurantSeat reservedSeat)
    {
        reservedSeat = null;
        if (customer == null)
        {
            return false;
        }

        ResolveReferences();

        List<RestaurantSeat> availableSeats = new List<RestaurantSeat>();
        CollectAvailableSeatsAcrossRestaurant(availableSeats);

        if (availableSeats.Count == 0)
        {
            return false;
        }

        Vector3 customerPos = customer.transform.position;
        availableSeats.Sort((a, b) =>
        {
            float distanceA = Vector2.SqrMagnitude((Vector2)a.GetApproachPosition() - (Vector2)customerPos);
            float distanceB = Vector2.SqrMagnitude((Vector2)b.GetApproachPosition() - (Vector2)customerPos);
            return distanceA.CompareTo(distanceB);
        });

        for (int i = 0; i < availableSeats.Count; i++)
        {
            RestaurantSeat seat = availableSeats[i];
            if (seat != null && seat.TryReserve(customer))
            {
                reservedSeat = seat;
                return true;
            }
        }

        return false;
    }

    public void NotifyCustomerExited(RestaurantCustomerAI customerAI)
    {
        if (customerAI != null)
        {
            activeCustomers.Remove(customerAI);
            Destroy(customerAI.gameObject);
        }
    }

    public void ReleasePendingCounterReservation(RestaurantCounter targetCounter)
    {
        if (targetCounter == null) return;

        if (!pendingCounterReservations.TryGetValue(targetCounter, out int current))
        {
            return;
        }

        current -= 1;
        if (current <= 0)
        {
            pendingCounterReservations.Remove(targetCounter);
        }
        else
        {
            pendingCounterReservations[targetCounter] = current;
        }
    }

    public bool TryGetLatestUnpaidUtilityBill(out UtilityBillStatement bill)
    {
        IReadOnlyList<UtilityBillStatement> unpaidBills = RestaurantUtilityBillingCache.GetUnpaidBillsSnapshot();
        if (unpaidBills == null || unpaidBills.Count == 0)
        {
            bill = null;
            return false;
        }

        bill = unpaidBills[unpaidBills.Count - 1];
        return bill != null;
    }

    public IReadOnlyList<UtilityBillStatement> GetUnpaidUtilityBillsSnapshot()
    {
        return RestaurantUtilityBillingCache.GetUnpaidBillsSnapshot();
    }

    public bool TryPayLatestUnpaidUtilityBill(out UtilityBillStatement paidBill, out string failureReason, bool showResultNotice = false)
    {
        paidBill = null;
        failureReason = string.Empty;

        if (!TryGetLatestUnpaidUtilityBill(out UtilityBillStatement billToPay) || billToPay == null)
        {
            failureReason = "No unpaid utility bills.";
            return false;
        }

        if (!TryPayUtilityBill(billToPay.billId, out failureReason, showResultNotice))
        {
            return false;
        }

        paidBill = billToPay;
        return true;
    }

    public bool MarkUtilityBillPaid(string billId)
    {
        return TryPayUtilityBill(billId, out _, showResultNotice: true);
    }

    public bool TryPayUtilityCharge(string billId, UtilityChargeType chargeType, out string failureReason, bool showResultNotice = true)
    {
        return RestaurantUtilityBillingCache.TryPayCharge(billId, chargeType, out failureReason, showResultNotice);
    }

    public bool TryPayUtilityBill(string billId, out string failureReason, bool showResultNotice = false)
    {
        failureReason = string.Empty;

        IReadOnlyList<UtilityBillStatement> unpaidBills = RestaurantUtilityBillingCache.GetUnpaidBillsSnapshot();
        UtilityBillStatement bill = null;
        if (unpaidBills != null)
        {
            for (int i = 0; i < unpaidBills.Count; i++)
            {
                UtilityBillStatement candidate = unpaidBills[i];
                if (candidate != null && string.Equals(candidate.billId, billId, StringComparison.Ordinal))
                {
                    bill = candidate;
                    break;
                }
            }
        }

        if (bill == null)
        {
            failureReason = "Bill not found.";
            return false;
        }

        int electricityDue = GetOutstandingAmountForCharge(bill, UtilityChargeType.Electricity);
        int waterDue = GetOutstandingAmountForCharge(bill, UtilityChargeType.Water);
        int totalDue = electricityDue + waterDue;
        if (totalDue <= 0)
        {
            failureReason = "This bill is already paid.";
            return false;
        }

        ResolveReferences();
        if (inventoryController == null || inventoryController.money < totalDue)
        {
            failureReason = $"Not enough cash to pay this bill in full (${totalDue:N0}).";
            if (showResultNotice)
            {
                ShowShopNotice(failureReason);
            }

            return false;
        }

        if (electricityDue > 0 && !RestaurantUtilityBillingCache.TryPayCharge(billId, UtilityChargeType.Electricity, out failureReason, showResultNotice: false))
        {
            return false;
        }

        if (waterDue > 0 && !RestaurantUtilityBillingCache.TryPayCharge(billId, UtilityChargeType.Water, out failureReason, showResultNotice: false))
        {
            return false;
        }

        if (showResultNotice)
        {
            ShowShopNotice($"Paid utility bill: ${totalDue:N0}.");
        }

        return true;
    }

    public UtilityBillingSaveState CaptureUtilityBillingSaveState()
    {
        return RestaurantUtilityBillingCache.CaptureState();
    }

    public void ApplyUtilityBillingSaveState(UtilityBillingSaveState state)
    {
        RestaurantUtilityBillingCache.ApplyState(state);
    }

    private void SpawnCustomer()
    {
        ResolveReferences();

        RestaurantCounter targetCounterRef = SelectCounterForNextCustomer();
        if (requireFoodOnCounterToSpawn && targetCounterRef == null)
        {
            return;
        }

        if (targetCounterRef != null)
        {
            ReserveCounterForIncomingCustomer(targetCounterRef);
        }

        if (!TryResolveSpawnPosition(targetCounterRef, out Vector3 spawnPosition))
        {
            ReleasePendingCounterReservation(targetCounterRef);
            return;
        }

        RestaurantCustomerAI customerAI = Instantiate(customerPrefab, spawnPosition, Quaternion.identity);
        if (customerAI == null)
        {
            ReleasePendingCounterReservation(targetCounterRef);
            return;
        }

        Transform targetExit = exitPoint != null ? exitPoint : entrancePoint;

        customerAI.Initialize(this, targetCounterRef, targetExit);
        activeCustomers.Add(customerAI);
    }

    private bool TryResolveSpawnPosition(RestaurantCounter targetCounter, out Vector3 spawnPosition)
    {
        Vector3 fallback = entrancePoint != null ? entrancePoint.position : transform.position;
        float jitter = Mathf.Max(0f, spawnJitterRadius);
        int attempts = Mathf.Max(1, spawnPositionSearchAttempts);

        Vector2 center = fallback;
        Vector2 preferredDirection = Vector2.zero;
        if (targetCounter != null)
        {
            Vector2 toCounter = (Vector2)targetCounter.transform.position - center;
            if (toCounter.sqrMagnitude > 0.0001f)
            {
                preferredDirection = toCounter.normalized;
            }
        }

        Vector2[] seedCandidates =
        {
            center + preferredDirection * Mathf.Max(0.08f, jitter * 0.45f),
            center + preferredDirection * Mathf.Max(0.04f, jitter * 0.22f),
            center,
        };

        for (int i = 0; i < seedCandidates.Length; i++)
        {
            Vector2 seed = seedCandidates[i];
            if (!IsSpawnPointBlocked(seed))
            {
                spawnPosition = new Vector3(seed.x, seed.y, fallback.z);
                return true;
            }
        }

        for (int i = 0; i < attempts; i++)
        {
            float t = attempts <= 1 ? 1f : i / (float)(attempts - 1);
            float radius = Mathf.Lerp(0.08f, Mathf.Max(0.08f, jitter), t);
            Vector2 candidate = center
                + UnityEngine.Random.insideUnitCircle * radius
                + preferredDirection * (radius * 0.28f);

            if (!IsSpawnPointBlocked(candidate))
            {
                spawnPosition = new Vector3(candidate.x, candidate.y, fallback.z);
                return true;
            }
        }

        spawnPosition = fallback;
        return false;
    }

    private bool IsSpawnPointBlocked(Vector2 worldPoint)
    {
        float radius = Mathf.Max(0.03f, spawnProbeRadius);
        Collider2D[] hits = Physics2D.OverlapCircleAll(worldPoint, radius, spawnObstacleMask);

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null)
            {
                continue;
            }

            if (!spawnIncludeTriggers && hit.isTrigger)
            {
                continue;
            }

            if (hit.GetComponentInParent<RestaurantCustomerAI>() != null)
            {
                continue;
            }

            if (hit.GetComponentInParent<PlayerMovement>() != null)
            {
                continue;
            }

            return true;
        }

        return false;
    }
    private RestaurantCounter SelectCounterForNextCustomer()
    {
        CleanupCounters();

        List<RestaurantCounter> available = new List<RestaurantCounter>();
        for (int i = 0; i < counters.Count; i++)
        {
            RestaurantCounter c = counters[i];
            if (c == null) continue;

            if (!requireFoodOnCounterToSpawn)
            {
                available.Add(c);
                continue;
            }

            int availableFood = Mathf.Max(0, c.TotalFoodCount - GetPendingReservationCount(c));
            if (availableFood > 0)
            {
                available.Add(c);
            }
        }

        if (available.Count == 0)
        {
            return null;
        }

        return available[UnityEngine.Random.Range(0, available.Count)];
    }

    private int GetTotalAvailableCounterFood()
    {
        CleanupCounters();

        int total = 0;
        for (int i = 0; i < counters.Count; i++)
        {
            RestaurantCounter c = counters[i];
            if (c == null) continue;

            int availableFood = Mathf.Max(0, c.TotalFoodCount - GetPendingReservationCount(c));
            total += availableFood;
        }

        return total;
    }

    private int GetActiveInsideCount()
    {
        int count = 0;
        for (int i = 0; i < activeCustomers.Count; i++)
        {
            RestaurantCustomerAI customer = activeCustomers[i];
            if (customer == null) continue;
            if (customer.IsExiting) continue;

            count++;
        }

        return count;
    }

    private void ReserveCounterForIncomingCustomer(RestaurantCounter targetCounter)
    {
        if (targetCounter == null) return;

        int current = 0;
        pendingCounterReservations.TryGetValue(targetCounter, out current);
        pendingCounterReservations[targetCounter] = current + 1;
    }

    private int GetPendingReservationCount(RestaurantCounter targetCounter)
    {
        if (targetCounter == null) return 0;
        if (!pendingCounterReservations.TryGetValue(targetCounter, out int current)) return 0;
        return Mathf.Max(0, current);
    }

    private void SetNextSpawnDelay()
    {
        float min = Mathf.Max(0.5f, spawnIntervalRangeSeconds.x);
        float max = Mathf.Max(min, spawnIntervalRangeSeconds.y);
        nextSpawnDelay = UnityEngine.Random.Range(min, max);
    }

    private void BeginOpenSessionTracking()
    {
        if (!enableWeeklyUtilityBilling)
        {
            return;
        }

        RestaurantUtilityBillingCache.NotifyShopOpened();
    }

    private void EndOpenSessionTracking()
    {
        if (!enableWeeklyUtilityBilling)
        {
            return;
        }

        RestaurantUtilityBillingCache.NotifyShopClosed();
    }

    private bool TryGetOverdueUtilityLockReason(out string reason)
    {
        reason = string.Empty;

        if (!enableWeeklyUtilityBilling || overdueWeeksBeforeShopLock <= 0)
        {
            return false;
        }

        RestaurantUtilityBillingCache.RefreshTimeState();
        return RestaurantUtilityBillingCache.TryGetOverdueLockReason(overdueWeeksBeforeShopLock, out reason);
    }

    private static int GetOutstandingAmountForCharge(UtilityBillStatement bill, UtilityChargeType chargeType)
    {
        if (bill == null)
        {
            return 0;
        }

        return chargeType == UtilityChargeType.Electricity
            ? Mathf.Max(0, bill.electricityOutstandingAmount)
            : Mathf.Max(0, bill.waterOutstandingAmount);
    }


    private void HandleDateTimeChanged(TimeManager.DateTime currentDateTime)
    {
        EnforceOperatingHoursIfNeeded(currentDateTime.Hour, currentDateTime.Minutes);
    }

    private void EnforceOperatingHoursIfNeeded()
    {
        if (!TryGetCurrentClock(out int hour, out int minute))
        {
            return;
        }

        EnforceOperatingHoursIfNeeded(hour, minute);
    }

    private void EnforceOperatingHoursIfNeeded(int hour, int minute)
    {
        if (!enforceOperatingHours || !IsOpen)
        {
            return;
        }

        if (IsWithinOperatingHours(hour, minute))
        {
            return;
        }

        CloseShop(true);
        ShowShopNotice($"Shop closed ({FormatClock(closeHour, closeMinute)})");
    }

    private bool CanOpenShopNow(out string blockedReason)
    {
        blockedReason = string.Empty;

        if (TryGetOverdueUtilityLockReason(out string utilityReason))
        {
            blockedReason = utilityReason;
            return false;
        }

        if (!enforceOperatingHours)
        {
            return true;
        }

        if (!TryGetCurrentClock(out int hour, out int minute))
        {
            return true;
        }

        if (IsWithinOperatingHours(hour, minute))
        {
            return true;
        }

        int currentTotal = hour * 60 + minute;
        int openTotal = GetOpenTotalMinutes();
        int closeTotal = GetCloseTotalMinutes();
        string rangeText = $"{FormatClock(openHour, openMinute)} - {FormatClock(closeHour, closeMinute)}";

        if (openTotal < closeTotal && currentTotal < openTotal)
        {
            blockedReason = $"Shop can only be open from ({rangeText})";
            return false;
        }

        blockedReason = $"Shop can only be open from {rangeText}";
        return false;
    }

    private bool IsWithinOperatingHours(int hour, int minute)
    {
        if (!enforceOperatingHours)
        {
            return true;
        }

        int current = Mathf.Clamp(hour, 0, 23) * 60 + Mathf.Clamp(minute, 0, 59);
        int openTotal = GetOpenTotalMinutes();
        int closeTotal = GetCloseTotalMinutes();

        if (openTotal == closeTotal)
        {
            return true;
        }

        if (openTotal < closeTotal)
        {
            return current >= openTotal && current < closeTotal;
        }

        return current >= openTotal || current < closeTotal;
    }

    private bool TryGetCurrentClock(out int hour, out int minute)
    {
        TimeManager timeManager = TimeManager.Instance;
        if (timeManager == null)
        {
            hour = 0;
            minute = 0;
            return false;
        }

        hour = Mathf.Clamp(timeManager.CurrentHour, 0, 23);
        minute = Mathf.Clamp(timeManager.CurrentMinutes, 0, 59);
        return true;
    }

    private int GetOpenTotalMinutes()
    {
        return Mathf.Clamp(openHour, 0, 23) * 60 + Mathf.Clamp(openMinute, 0, 59);
    }

    private int GetCloseTotalMinutes()
    {
        return Mathf.Clamp(closeHour, 0, 23) * 60 + Mathf.Clamp(closeMinute, 0, 59);
    }

    private static string FormatClock(int hour, int minute)
    {
        int h = Mathf.Clamp(hour, 0, 23);
        int m = Mathf.Clamp(minute, 0, 59);
        string ampm = h >= 12 ? "PM" : "AM";
        int displayHour = h % 12;
        if (displayHour == 0) displayHour = 12;
        return $"{displayHour}:{m:D2} {ampm}";
    }

    private void ShowShopNotice(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        Debug.Log($"[RestaurantServiceManager] {message}");

        Vector3 anchor = transform.position;
        if (restaurantToggle != null)
        {
            anchor = restaurantToggle.transform.position;
        }
        else if (entrancePoint != null)
        {
            anchor = entrancePoint.position;
        }

        CashPopupManager.ShowInfoPopup(anchor + new Vector3(0f, 1.2f, 0f), message);
    }

    private void CleanupNullCustomers()
    {
        for (int i = activeCustomers.Count - 1; i >= 0; i--)
        {
            if (activeCustomers[i] == null)
            {
                activeCustomers.RemoveAt(i);
            }
        }
    }

    private void ResolveReferences()
    {
        CleanupCounters();
        CleanupTableManagers();

        if (inventoryController == null)
        {
            inventoryController = FindObjectOfType<InventoryController>();
        }

        if (restaurantToggle == null)
        {
            restaurantToggle = FindObjectOfType<RestaurantOpenCloseInteractable>();
        }
    }

    private void CollectAvailableSeatsAcrossRestaurant(List<RestaurantSeat> output)
    {
        if (output == null)
        {
            return;
        }

        for (int i = 0; i < tableManagers.Count; i++)
        {
            RestaurantTableManager manager = tableManagers[i];
            if (manager == null)
            {
                continue;
            }

            manager.RefreshSeats();
            manager.CollectAvailableSeats(output);
        }

        if (output.Count > 0)
        {
            return;
        }

        RestaurantSeat[] sceneSeats = GetComponentsInChildren<RestaurantSeat>(true);
        for (int i = 0; i < sceneSeats.Length; i++)
        {
            RestaurantSeat seat = sceneSeats[i];
            if (seat != null && seat.IsAvailable && !output.Contains(seat))
            {
                output.Add(seat);
            }
        }

        if (output.Count > 0 || !autoCreateSceneSeatsFromChairObjects || string.IsNullOrWhiteSpace(seatChairNamePrefix))
        {
            return;
        }

        Transform[] allTransforms = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < allTransforms.Length; i++)
        {
            Transform t = allTransforms[i];
            if (t == null)
            {
                continue;
            }

            if (!t.name.StartsWith(seatChairNamePrefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (t.GetComponent<RestaurantSeat>() == null)
            {
                t.gameObject.AddComponent<RestaurantSeat>();
            }
        }

        sceneSeats = GetComponentsInChildren<RestaurantSeat>(true);
        for (int i = 0; i < sceneSeats.Length; i++)
        {
            RestaurantSeat seat = sceneSeats[i];
            if (seat != null && seat.IsAvailable && !output.Contains(seat))
            {
                output.Add(seat);
            }
        }
    }

    private void CleanupCounters()
    {
        if (counters == null)
        {
            counters = new List<RestaurantCounter>();
        }

        for (int i = counters.Count - 1; i >= 0; i--)
        {
            if (counters[i] == null)
            {
                counters.RemoveAt(i);
            }
        }

        if (counter != null && !counters.Contains(counter))
        {
            counters.Add(counter);
        }

        if (autoFindCounters)
        {
            RestaurantCounter[] found = FindObjectsOfType<RestaurantCounter>();
            for (int i = 0; i < found.Length; i++)
            {
                RestaurantCounter c = found[i];
                if (c != null && !counters.Contains(c))
                {
                    counters.Add(c);
                }
            }
        }

        CleanupCounterReservations();

        if (counter == null && counters.Count > 0)
        {
            counter = counters[0];
        }
    }

    private void CleanupTableManagers()
    {
        if (tableManagers == null)
        {
            tableManagers = new List<RestaurantTableManager>();
        }

        for (int i = tableManagers.Count - 1; i >= 0; i--)
        {
            if (tableManagers[i] == null)
            {
                tableManagers.RemoveAt(i);
            }
        }

        if (tableManager != null && !tableManagers.Contains(tableManager))
        {
            tableManagers.Add(tableManager);
        }

        if (autoFindTableManagers)
        {
            RestaurantTableManager[] found = FindObjectsOfType<RestaurantTableManager>(true);
            for (int i = 0; i < found.Length; i++)
            {
                RestaurantTableManager manager = found[i];
                if (manager != null && !tableManagers.Contains(manager))
                {
                    tableManagers.Add(manager);
                }
            }
        }

        if (tableManager == null && tableManagers.Count > 0)
        {
            tableManager = tableManagers[0];
        }
    }

    private void CleanupCounterReservations()
    {
        if (pendingCounterReservations.Count == 0)
        {
            return;
        }

        List<RestaurantCounter> removeKeys = null;

        foreach (KeyValuePair<RestaurantCounter, int> kv in pendingCounterReservations)
        {
            RestaurantCounter targetCounter = kv.Key;
            if (targetCounter == null || !counters.Contains(targetCounter))
            {
                if (removeKeys == null)
                {
                    removeKeys = new List<RestaurantCounter>();
                }

                removeKeys.Add(targetCounter);
            }
        }

        if (removeKeys == null) return;

        for (int i = 0; i < removeKeys.Count; i++)
        {
            pendingCounterReservations.Remove(removeKeys[i]);
        }
    }
}
