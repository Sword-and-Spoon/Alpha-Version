using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum TransactionType
{
  None,
  Buy,
  Sell,
  Bill,
  BillAccrued,
  BillSettlement,
  CreditSale,       // ขาย/ให้บริการแบบเชื่อ (AR เพิ่ม) → Dr: AR | Cr: Revenue
  ReceivePayment,   // รับชำระจากลูกหนี้ (AR ลด)  → Dr: Cash | Cr: AR
  CreditPurchase,   // Buy on credit        → Dr: FoodSupplies | Cr: AccountsPayable
  RepayAP,          // Repay vendor debt    → Dr: AccountsPayable + InterestPayable | Cr: Cash
  AccrueInterest,   // Accrue daily interest → Dr: InterestExpense | Cr: InterestPayable
}

public enum StoreType
{
  None,
  TownShop,
  Restaurant,
}

[System.Serializable]
public class TransactionRecord
{
  public string itemName;
  public string npcName; // เพิ่มฟิลด์ชื่อ NPC (สำหรับลูกหนี้/เจ้าหนี้รายตัว)
  public ItemCategory category;
  public int quantity;
  public int totalPrice;
  public TransactionType type;
  public StoreType store;
  public TimeManager.DateTime gameTime;

  public TransactionRecord(Item item, int totalPrice, TransactionType type, StoreType store, string npcName = null)
  {
    this.itemName = item.GetName();
    this.npcName = npcName;
    this.category = item.itemSO.category;
    this.quantity = item.amount;
    this.totalPrice = totalPrice;
    this.type = type;
    this.store = store;
    this.gameTime = TimeManager.Instance.dateTime;
  }

  public TransactionRecord(string itemName, ItemCategory category, int quantity, int totalPrice, TransactionType type, StoreType store, string npcName = null)
  {
    this.itemName = itemName;
    this.npcName = npcName;
    this.category = category;
    this.quantity = quantity;
    this.totalPrice = totalPrice;
    this.type = type;
    this.store = store;
    this.gameTime = TimeManager.Instance.dateTime;
  }
}

public class TransactionManager : MonoBehaviour
{
  public static TransactionManager Instance;
  [HideInInspector]
  public List<TransactionRecord> todayTransactions = new();
  public List<TransactionRecord> GetRecordsToday()
  {
    EnsureActiveJournalDay();
    return todayTransactions;
  }

  private const int UnsetJournalDay = -1;

  private int activeJournalDay = UnsetJournalDay;
  private bool journalCompletedForActiveDay;
  private bool hasCompletedJournalReport;
  private AuditReport completedJournalReport;

  public int ActiveJournalDay
  {
    get
    {
      EnsureActiveJournalDay();
      return activeJournalDay;
    }
  }

  public bool IsJournalCompletedForActiveDay
  {
    get
    {
      EnsureActiveJournalDay();
      return journalCompletedForActiveDay;
    }
  }

  private void Awake()
  {
    if (Instance == null) Instance = this;
    else Destroy(gameObject);
  }

  public void AddRecord(TransactionRecord newRecord)
  {
    EnsureActiveJournalDay();

    // ปรับให้เช็ค npcName ด้วย เพื่อแยกยอดลูกหนี้รายตัว
    TransactionRecord existing = todayTransactions.Find(t =>
        t.itemName == newRecord.itemName &&
        t.npcName == newRecord.npcName &&
        t.category == newRecord.category &&
        t.store == newRecord.store &&
        t.type == newRecord.type
    );

    if (existing != null)
    {
      existing.quantity += newRecord.quantity;
      existing.totalPrice += newRecord.totalPrice;
    }
    else
    {
      todayTransactions.Add(newRecord);
    }
  }

  public bool TryRemoveRecordPortion(
      string itemName,
      ItemCategory category,
      TransactionType type,
      StoreType store,
      int quantityToRemove,
      int totalPriceToRemove)
  {
    TransactionRecord existing = todayTransactions.Find(t =>
      t.itemName == itemName &&
      t.category == category &&
      t.type == type &&
      t.store == store);

    if (existing == null)
    {
      return false;
    }

    existing.quantity = Mathf.Max(0, existing.quantity - Mathf.Max(0, quantityToRemove));
    existing.totalPrice = Mathf.Max(0, existing.totalPrice - Mathf.Max(0, totalPriceToRemove));

    if (existing.quantity <= 0 || existing.totalPrice <= 0)
    {
      todayTransactions.Remove(existing);
    }

    return true;
  }

  public void LoadRecords(List<TransactionRecord> records)
  {
    todayTransactions = records ?? new List<TransactionRecord>();

    if (activeJournalDay <= 0)
    {
      activeJournalDay = ResolveJournalDayFromRecords(todayTransactions);
    }
  }

  public bool HasTransactionsToday()
  {
    EnsureActiveJournalDay();
    return todayTransactions != null && todayTransactions.Count > 0;
  }

  public bool HasTransactionsNeedingJournal()
  {
    EnsureActiveJournalDay();
    return HasTransactionRecords() && !journalCompletedForActiveDay;
  }

  public void MarkJournalCompleted(AuditReport report)
  {
    EnsureActiveJournalDay();
    completedJournalReport = report;
    hasCompletedJournalReport = true;
    journalCompletedForActiveDay = true;
  }

  public bool TryGetCompletedJournalReport(out AuditReport report)
  {
    EnsureActiveJournalDay();
    report = completedJournalReport;
    return hasCompletedJournalReport;
  }

  public void CompleteDailyRolloverAfterSleep()
  {
    BeginJournalDay(GetCurrentJournalDayIndex(), true);
  }

  public void ApplyDailyJournalState(
    int journalDay,
    bool completed,
    bool hasReport,
    AuditReport report)
  {
    activeJournalDay = journalDay > 0 ? journalDay : ResolveJournalDayFromRecords(todayTransactions);
    journalCompletedForActiveDay = completed;
    hasCompletedJournalReport = hasReport;
    completedJournalReport = hasReport ? report : default;
    EnsureActiveJournalDay();
    SyncJournalManagerState();
  }

  public void SyncJournalManagerState()
  {
    if (JournalManager.Instance == null)
    {
      return;
    }

    JournalManager.Instance.ApplyDailyJournalCompletionState(
      journalCompletedForActiveDay,
      completedJournalReport,
      hasCompletedJournalReport);
  }

  public string BuildSleepJournalSummary()
  {
    EnsureActiveJournalDay();

    if (hasCompletedJournalReport)
    {
      int transactions = todayTransactions != null ? todayTransactions.Count : 0;
      return "Daily Journal Summary\n"
        + $"Attempts: {Mathf.Max(1, completedJournalReport.attemptNumber)}\n"
        + $"Transactions recorded: {transactions}";
    }

    if (HasTransactionRecords())
    {
      return "Daily Journal Summary\nJournal has not been completed yet.";
    }

    return "Daily Journal Summary\nNo transactions recorded today.";
  }

  public Dictionary<(StoreType, TransactionType, string), List<TransactionRecord>> GetGroupedBills()
  {
    EnsureActiveJournalDay();

    Dictionary<(StoreType, TransactionType, string), List<TransactionRecord>> grouped =
        new Dictionary<(StoreType, TransactionType, string), List<TransactionRecord>>();

    foreach (var record in todayTransactions)
    {
      var key = (record.store, record.type, GetCounterpartyGroupName(record));

      if (!grouped.ContainsKey(key))
        grouped[key] = new List<TransactionRecord>();

      grouped[key].Add(record);
    }

    return grouped;
  }

  public static bool UsesCounterpartyName(TransactionType type)
  {
    return type == TransactionType.CreditSale
      || type == TransactionType.ReceivePayment
      || type == TransactionType.CreditPurchase
      || type == TransactionType.RepayAP
      || type == TransactionType.AccrueInterest;
  }

  public static string GetCounterpartyGroupName(TransactionRecord record)
  {
    if (record == null || !UsesCounterpartyName(record.type))
    {
      return string.Empty;
    }

    return string.IsNullOrWhiteSpace(record.npcName) ? string.Empty : record.npcName.Trim();
  }

  private void EnsureActiveJournalDay()
  {
    int currentJournalDay = GetCurrentJournalDayIndex();

    if (activeJournalDay <= 0)
    {
      activeJournalDay = currentJournalDay;
      return;
    }

    if (activeJournalDay == currentJournalDay)
    {
      return;
    }

    if (!HasTransactionRecords())
    {
      BeginJournalDay(currentJournalDay, true);
    }
  }

  private void BeginJournalDay(int journalDay, bool clearJournalUI)
  {
    activeJournalDay = journalDay > 0 ? journalDay : GetCurrentJournalDayIndex();
    if (todayTransactions == null)
    {
      todayTransactions = new List<TransactionRecord>();
    }
    else
    {
      todayTransactions.Clear();
    }

    journalCompletedForActiveDay = false;
    hasCompletedJournalReport = false;
    completedJournalReport = default;

    if (clearJournalUI && JournalManager.Instance != null)
    {
      JournalManager.Instance.ResetForNewJournalDay();
    }
  }

  private bool HasTransactionRecords()
  {
    return todayTransactions != null && todayTransactions.Count > 0;
  }

  private int ResolveJournalDayFromRecords(List<TransactionRecord> records)
  {
    if (records != null && records.Count > 0)
    {
      return GetJournalDayIndex(records[0].gameTime);
    }

    return GetCurrentJournalDayIndex();
  }

  private static int GetCurrentJournalDayIndex()
  {
    if (TimeManager.Instance == null)
    {
      return 1;
    }

    return GetJournalDayIndex(TimeManager.Instance.dateTime);
  }

  private static int GetJournalDayIndex(TimeManager.DateTime dateTime)
  {
    int newDayHour = TimeManager.Instance != null ? TimeManager.Instance.newDayHour : 6;
    int journalDay = dateTime.TotalNumDays;

    if (dateTime.Hour < newDayHour)
    {
      journalDay--;
    }

    return Mathf.Max(1, journalDay);
  }
}
