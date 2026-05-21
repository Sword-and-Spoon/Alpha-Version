using System;
using System.Collections.Generic;

[Serializable]
public class SaveDataV1
{
    public const int CURRENT_VERSION = 1;

    public int version = CURRENT_VERSION;
    public string timestamp;
    public string sceneName;
    public PlayerDTO player = new PlayerDTO();
    public InventoryDTO inventory = new InventoryDTO();
    public TimeDTO time = new TimeDTO();
    public List<TransactionDTO> todayTransactions = new List<TransactionDTO>();
    public List<JournalEntryDTO> confirmedJournalEntries = new List<JournalEntryDTO>();
    public int activeJournalDay = -1;
    public bool journalCompletedForActiveDay;
    public bool hasDailyJournalReport;
    public AuditReportDTO dailyJournalReport = new AuditReportDTO();
    public RestaurantUtilityStateDTO restaurantUtility = new RestaurantUtilityStateDTO();
    
    public List<QuestDTO> pendingQuests = new List<QuestDTO>();
    public List<APQuestDTO> pendingAPQuests = new List<APQuestDTO>();
    public List<VendorSalesRecordDTO> vendorSalesRecords = new List<VendorSalesRecordDTO>();
    public List<NPCCooldownDTO> npcCooldowns = new List<NPCCooldownDTO>();
    public List<RestaurantCounterDTO> restaurantCounters = new List<RestaurantCounterDTO>();

    public bool hasTutorialState;
    public int tutorialStateVersion = TutorialStateSnapshot.CURRENT_VERSION;
    public int tutorialStepIndex = -1;
    public bool tutorialCompleted;
    public bool tutorialBonusQuestActive;
    public bool tutorialBonusRecallTriggered;
    public bool tutorialCreditOverrideApplied;
    public List<string> tutorialFlags = new List<string>();
}

[Serializable]
public class NPCCooldownDTO
{
    public string npcName;
    public int nextAvailableDay; // int.MaxValue = one-time (never repeats)
}

[Serializable]
public class APQuestDTO
{
    public string vendorName;
    public string itemName;
    public int quantity;
    public int quality = (int)ItemQuality.Common;
    public int principalAmount;
    public int interestPerDay;
    public int dueTotalDay;
    public int createdTotalDay;
    public bool isOverdue;
    public int accruedInterest;
    public int lastAccruedDay;
}

[Serializable]
public class VendorSalesRecordDTO
{
    public string vendorName;
    public int commonSold;
    public int rareSold;
    public int epicSold;
    public int legendarySold;
    public int mythicalSold;
}

[Serializable]
public class QuestDTO
{
    public string npcName;
    public string foodName;
    public int amount;
    public int dueTotalDay;
    public int createdTotalDay;
    public int paymentTermDays = 1;
    public bool isLetterSent;
    public bool isARRecorded;
    public int language;      // NPCLanguage as int
    public string objectiveText;
    public string letterSenderTemplate;
    public string letterContentTemplate;
    // Quest Type System
    public int questType;      // QuestType as int
    public int questStatus;    // QuestStatus as int
    public string targetTag;
    public int requiredCount;
    public int currentProgress;
    public int minimumTier;           // MonsterTier as int (KillMonster only)
    public int minimumItemQuality;    // ItemQuality as int (DeliverItem/CookAndDeliver/CollectItem)
    public int consumeRequiredItemsOnTurnIn = 1;
}

[Serializable]
public class ItemDTO
{
    public bool present;
    public string itemId;
    public string itemName;
    public int amount;
    public int quality;
    public float finalQualityScore;
}

[Serializable]
public class RestaurantCounterDTO
{
    public string counterId;
    public ItemDTO storedFood = new ItemDTO();
    public int storedUnitPrice;
}

[Serializable]
public class InventoryDTO
{
    public ItemDTO sword = new ItemDTO();
    public ItemDTO axe = new ItemDTO();
    public ItemDTO pickaxe = new ItemDTO();
    public List<ItemDTO> items = new List<ItemDTO>();
    public int money;
    public int slotCount = 18;
}

[Serializable]
public class PlayerDTO
{
    public float px, py, pz;
    public float rx, ry, rz, rw;
    public int currentHealth;
    public int maxHealth;
}

[Serializable]
public class GameDateTimeDTO
{
    public int date = 1;
    public int season = 1;
    public int year = 1;
    public int hour = 6;
    public int minutes = 0;
}

[Serializable]
public class TimeDTO
{
    public GameDateTimeDTO dateTime = new GameDateTimeDTO();
    public float sleepTimer; // Restored
    public bool faint;       // Restored
}

[Serializable]
public class TransactionDTO
{
    public string itemName;
    public string npcName;
    public int category;
    public int quantity;
    public int totalPrice;
    public int type;
    public int store;
    public GameDateTimeDTO gameTime = new GameDateTimeDTO();
}

[Serializable]
public class JournalEntryDTO
{
    public string entryId;
    public string side;
    public string itemName;
    public int category;
    public int type;
    public int store;
    public int quantity;
    public float amount;
    public GameDateTimeDTO time = new GameDateTimeDTO();
    public bool isBalancingEntry;
    public bool separatorAfter;
}

[Serializable]
public class AuditReportDTO
{
    public bool isPassed;
    public int score;
    public int maxScore;
    public int structureScore;
    public int structureMaxScore;
    public int orderScore;
    public int orderMaxScore;
    public int sideScore;
    public int sideMaxScore;
    public int transactionScore;
    public int transactionMaxScore;
    public int attemptNumber;
    public int retryCount;
    public int mistakeCount;
    public bool earnedStructurePoint;
    public bool earnedTransactionPoint;
    public List<string> mistakeDetails = new List<string>();
    public List<string> lineMistakeDetails = new List<string>();
    public List<AuditLineHintDTO> lineHints = new List<AuditLineHintDTO>();
}

[Serializable]
public class AuditLineHintDTO
{
    public string entryId;
    public int rowNumber;
    public int journalLineNumber;
    public string message;
}

[Serializable]
public class RestaurantUtilityStateDTO
{
    public int trackedWeekIndex;
    public int lastBilledWeekIndex;
    public int weeklyOpenMinutes;
    public int weeklyDishesSold;
    public int pendingBillWeekIndex;
    public int pendingBillOpenMinutes;
    public int pendingBillDishesSold;
    public bool hasOpenSession;
    public int openSessionStartAbsoluteMinutes;
    public List<UtilityBillDTO> bills = new List<UtilityBillDTO>();
}

[Serializable]
public class UtilityBillDTO
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
    public GameDateTimeDTO issuedAt = new GameDateTimeDTO();
    public GameDateTimeDTO paidAt = new GameDateTimeDTO();
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
    public GameDateTimeDTO electricityPaidAt = new GameDateTimeDTO();
    public GameDateTimeDTO waterPaidAt = new GameDateTimeDTO();
    public bool mailboxLetterRead;
    public bool journalEntryPosted;
    public bool hasJournalEntryPostedAt;
    public GameDateTimeDTO journalEntryPostedAt = new GameDateTimeDTO();
}

[Serializable]
public class SlotInfo
{
    public int slot;
    public bool exists;
    public string timestamp;
    public string sceneName;
    public int money;
    public int date;
    public int season;
    public int year;
    public int hour;
    public int minutes;
}
