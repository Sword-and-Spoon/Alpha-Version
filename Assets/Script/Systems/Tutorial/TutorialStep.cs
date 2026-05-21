using System;

public enum TutorialStep
{
    WaitForFirstMove = 0,
    VisitUtilityPaymentZone = 1,
    VisitShopZone = 2,
    VisitHouseZone = 3,
    VisitRestaurantZone = 4,
    TalkToGuideNpc = 5,
    CompleteARCollectObjective = 6,
    ReturnToGuideNpc = 7,
    SellForCreditUnlock = 8,
    CreateAPDebt = 9,
    ViewAPQuest = 10,
    GoToRestaurant = 11,
    CookFood = 12,
    PlaceFoodOnCounter = 13,
    OpenRestaurant = 14,
    MakeFirstSale = 15,
    CloseRestaurant = 16,
    GoHomeAtNight = 17,
    OpenJournal = 18,
    ConfirmJournalEntry = 19,
    FinishJournal = 20,
    WaitFirstLetter = 21,
    ReadFirstLetter = 22,
    ReadUtilityBillLetter = 23,
    PayUtilityBill = 24,
    Completed = 25,
}

public enum TutorialTriggerType
{
    UtilityPaymentZone,
    ShopZone,
    HouseZone,
    RestaurantZone,
    GuideNpcZone,
    OvenZone,
    HomeEntranceZone,
    Zone1Entrance,
    Zone2Entrance,
}

public enum TutorialTargetType
{
    UtilityPayment,
    Shop,
    House,
    Restaurant,
    GuideNpc,
    CollectZoneEntrance,
    Zone1Entrance,
    Zone2Entrance,
    Oven,
    Counter,
    RestaurantSign,
    Home,
    JournalTable,
    Mailbox,
    VillageSlime,
}

[Serializable]
public class TutorialStateSnapshot
{
    public const int CURRENT_VERSION = 1;

    public int version = CURRENT_VERSION;
    public bool hasState;
    public int stepIndex = -1;
    public bool completed;
    public bool bonusQuestActive;
    public bool bonusRecallTriggered;
    public bool creditOverrideApplied;
    public System.Collections.Generic.List<string> flags = new System.Collections.Generic.List<string>();
}
