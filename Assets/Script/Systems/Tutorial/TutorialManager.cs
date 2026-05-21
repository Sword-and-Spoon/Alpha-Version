using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TutorialManager : MonoBehaviour
{
    private const string Zone1SceneName = "Zone1";
    private const string Zone2SceneName = "Zone2 Cave";
    private const string VillageSceneName = "Zone Village";

    [Serializable]
    private struct TutorialStepMessageEntry
    {
        public TutorialStep step;
        [TextArea(1, 4)] public string message;
    }

    public static TutorialManager Instance { get; private set; }

    [Header("Runtime")]
    [SerializeField] private bool enableTutorial = true;
    [SerializeField] private bool lockInteractionsDuringTutorial = true;
    [SerializeField] private bool treatLegacySaveAsCompleted = true;

    [Header("References")]
    [SerializeField] private TutorialOverlayUI overlayUI;
    [SerializeField] private ARQuestNPC guideNpc;
    [SerializeField] private string guideNpcName = "Guide NPC";
    [SerializeField] private string creditVendorName = "Town Shop";
    [SerializeField] private ItemQuality tutorialCreditQuality = ItemQuality.Common;
    [SerializeField] private int tutorialCreditRequiredSales = 5;
    [SerializeField] private TutorialStepMessageEntry[] stepMessages;

    [Header("Restaurant Entry Detection")]
    [Tooltip("ระยะห่างจาก Oven (world units) ที่ถือว่า 'อยู่ในร้านแล้ว' — ปรับให้ครอบคลุมพื้นที่ภายในร้าน")]
    [SerializeField] private float restaurantInteriorRadius = 7f;

    [Header("Arrow Targets (Optional)")]
    [SerializeField] private Transform utilityPaymentTarget;
    [SerializeField] private Transform shopTarget;
    [SerializeField] private Transform houseTarget;
    [SerializeField] private Transform restaurantTarget;
    [SerializeField] private Transform guideNpcTarget;
    [SerializeField] private Transform collectZoneEntranceTarget;
    [SerializeField] private Transform villageSlimeTarget;
    [SerializeField] private Transform zone1EntranceTarget;
    [SerializeField] private Transform zone2EntranceTarget;
    [SerializeField] private Transform ovenTarget;
    [SerializeField] private Transform counterTarget;
    [SerializeField] private Transform restaurantSignTarget;
    [SerializeField] private Transform homeTarget;
    [SerializeField] private Transform journalTableTarget;
    [SerializeField] private Transform mailboxTarget;

    private readonly HashSet<string> flags = new(StringComparer.Ordinal);
    private readonly Dictionary<TutorialStep, string> stepMessageLookup = new Dictionary<TutorialStep, string>();
    private readonly Dictionary<TutorialTargetType, Transform> registeredTargets = new Dictionary<TutorialTargetType, Transform>();
    private TutorialStep currentStep = TutorialStep.WaitForFirstMove;
    private bool tutorialCompleted;
    private bool initialized;
    private bool bonusQuestActive;
    private bool bonusRecallTriggered;
    private bool creditOverrideApplied;

    private bool goToRestaurantArrowShown;
    private bool bonusZone1Visited;
    private bool bonusZone2Visited;

    // Cached scene-object references (populated on first use per scene)
    private Transform cachedOvenTarget;
    private Transform cachedCounterTarget;
    private Transform cachedRestaurantSignTarget;
    private Transform cachedJournalTableTarget;
    private Transform cachedMailboxTarget;
    private Transform cachedZone1EntranceTarget;
    private Transform cachedZone2EntranceTarget;
    private Transform cachedGoToRestaurantRouteTarget;

    public bool IsTutorialActive => enableTutorial && !tutorialCompleted;
    public TutorialStep CurrentStep => currentStep;

    private bool IsAtLeastFourThirty()
    {
        if (!TimeManager.TryGetCurrentDateTime(out TimeManager.DateTime dateTime))
        {
            return false;
        }
        return dateTime.Hour > 16 || (dateTime.Hour == 16 && dateTime.Minutes >= 30);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // ทำให้ overlay canvas อยู่ข้ามทุก scene
        if (overlayUI != null)
            DontDestroyOnLoad(overlayUI.transform.root.gameObject);

        RebuildStepMessageLookup();
    }

    private void OnValidate()
    {
        RebuildStepMessageLookup();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        PlayerMovement.OnPlayerFirstMove += HandlePlayerFirstMove;
        ARQuestManager.OnQuestCreated += HandleQuestCreated;
        ARQuestManager.OnQuestProgressChanged += HandleQuestProgressChanged;
        ARQuestManager.OnQuestHandedIn += HandleQuestHandedIn;
        CreditSystem.OnCreditTierUnlocked += HandleCreditTierUnlocked;
        APQuestManager.OnAPQuestCreated += HandleAPQuestCreated;
        APQuestManager.OnAPQuestViewed += HandleAPQuestViewed;
        APQuestManager.OnAPQuestRepaid += HandleAPQuestRepaid;
        APQuestManager.OnDebtBecameOverdue += HandleDebtOverdue;
        RestaurantServiceManager.OnShopOpened += HandleShopOpened;
        RestaurantServiceManager.OnFirstSaleMade += HandleFirstSaleMade;
        RestaurantServiceManager.OnShopClosed += HandleShopClosed;
        CookingSession.OnCookingCompleted += HandleCookingCompleted;
        RestaurantCounterDropZone.OnFoodPlacedOnCounter += HandleFoodPlacedOnCounter;
        JournalManager.OnJournalEntryConfirmed += HandleJournalEntryConfirmed;
        JournalManager.OnJournalFinished += HandleJournalFinished;
        JournalTable.OnJournalTableOpened += HandleJournalOpened;
        Bed.OnBedInteractBlocked += HandleBedBlocked;
        MailboxInteractable.OnFirstLetterReceived += HandleFirstLetterReceived;
        MailboxInteractable.OnLetterRead += HandleLetterRead;
        MailboxInteractable.OnUtilityBillLetterRead += HandleUtilityBillLetterRead;
        RestaurantUtilityBillingCache.OnAnyBillPaid += HandleUtilityBillPaid;
        TimeManager.OnDateTimeChanged += CheckBonusQuestRecall;
        SaveManager.OnAfterLoad += HandleAfterLoad;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        PlayerMovement.OnPlayerFirstMove -= HandlePlayerFirstMove;
        ARQuestManager.OnQuestCreated -= HandleQuestCreated;
        ARQuestManager.OnQuestProgressChanged -= HandleQuestProgressChanged;
        ARQuestManager.OnQuestHandedIn -= HandleQuestHandedIn;
        CreditSystem.OnCreditTierUnlocked -= HandleCreditTierUnlocked;
        APQuestManager.OnAPQuestCreated -= HandleAPQuestCreated;
        APQuestManager.OnAPQuestViewed -= HandleAPQuestViewed;
        APQuestManager.OnAPQuestRepaid -= HandleAPQuestRepaid;
        APQuestManager.OnDebtBecameOverdue -= HandleDebtOverdue;
        RestaurantServiceManager.OnShopOpened -= HandleShopOpened;
        RestaurantServiceManager.OnFirstSaleMade -= HandleFirstSaleMade;
        RestaurantServiceManager.OnShopClosed -= HandleShopClosed;
        CookingSession.OnCookingCompleted -= HandleCookingCompleted;
        RestaurantCounterDropZone.OnFoodPlacedOnCounter -= HandleFoodPlacedOnCounter;
        JournalManager.OnJournalEntryConfirmed -= HandleJournalEntryConfirmed;
        JournalManager.OnJournalFinished -= HandleJournalFinished;
        JournalTable.OnJournalTableOpened -= HandleJournalOpened;
        Bed.OnBedInteractBlocked -= HandleBedBlocked;
        MailboxInteractable.OnFirstLetterReceived -= HandleFirstLetterReceived;
        MailboxInteractable.OnLetterRead -= HandleLetterRead;
        MailboxInteractable.OnUtilityBillLetterRead -= HandleUtilityBillLetterRead;
        RestaurantUtilityBillingCache.OnAnyBillPaid -= HandleUtilityBillPaid;
        TimeManager.OnDateTimeChanged -= CheckBonusQuestRecall;
        SaveManager.OnAfterLoad -= HandleAfterLoad;
    }

    private void Start()
    {
        if (!enableTutorial || tutorialCompleted)
        {
            overlayUI?.Hide();
            return;
        }

        initialized = true;
        ReconcileWithCurrentWorldState();
        ApplyStepState(currentStep);
    }

    public void RegisterTarget(TutorialTargetType type, Transform t)
    {
        registeredTargets[type] = t;
        if (IsTutorialActive && initialized)
            ApplyStepState(currentStep);
    }

    public void UnregisterTarget(TutorialTargetType type)
    {
        registeredTargets.Remove(type);
    }

    public bool HandleWorldTrigger(TutorialTriggerType triggerType)
    {
        if (!IsTutorialActive)
        {
            return false;
        }

        switch (currentStep)
        {
            case TutorialStep.VisitUtilityPaymentZone:
                if (triggerType == TutorialTriggerType.UtilityPaymentZone)
                {
                    return AdvanceToStep(TutorialStep.VisitShopZone);
                }
                break;

            case TutorialStep.VisitShopZone:
                if (triggerType == TutorialTriggerType.ShopZone)
                {
                    return AdvanceToStep(TutorialStep.VisitHouseZone);
                }
                break;

            case TutorialStep.VisitHouseZone:
                if (triggerType == TutorialTriggerType.HouseZone)
                {
                    return AdvanceToStep(TutorialStep.VisitRestaurantZone);
                }
                break;

            case TutorialStep.VisitRestaurantZone:
                if (triggerType == TutorialTriggerType.RestaurantZone)
                {
                    return AdvanceToStep(TutorialStep.TalkToGuideNpc);
                }
                break;

            case TutorialStep.GoToRestaurant:
                if ((triggerType == TutorialTriggerType.OvenZone || triggerType == TutorialTriggerType.RestaurantZone)
                    && IsAtLeastFourThirty())
                {
                    return AdvanceToStep(TutorialStep.CookFood);
                }
                break;

            case TutorialStep.GoHomeAtNight:
                if (triggerType == TutorialTriggerType.HomeEntranceZone && IsNightTime())
                {
                    return AdvanceToStep(TutorialStep.OpenJournal);
                }
                break;
        }

        // Bonus zone tour — แสดง overlay นำทาง Zone 1 → Zone 2 ระหว่าง free time
        if (bonusQuestActive && !IsAtLeastFourThirty())
        {
            if (triggerType == TutorialTriggerType.Zone1Entrance && !bonusZone1Visited)
            {
                bonusZone1Visited = true;
                flags.Add("bonus-zone1-visited");
                DailyJournalRules.ShowMessage("Zone 1 — Slime drops Sugar Dust used in basic recipes. Head to Zone 2 Cave next!");
                ShowBonusZoneTourOverlay();
                return true;
            }
            if (triggerType == TutorialTriggerType.Zone2Entrance && !bonusZone2Visited)
            {
                bonusZone2Visited = true;
                flags.Add("bonus-zone2-visited");
                DailyJournalRules.ShowMessage("Zone 2 Cave — Chocolate Slime drops Chocolate Dust, Golem drops Stone. Used in advanced recipes!");
                HideOverlay();
                return true;
            }
        }

        return false;
    }

    public bool CanInteractWith(InteractableObject interactable, out string blockedReason)
    {
        blockedReason = null;

        if (!IsTutorialActive || !lockInteractionsDuringTutorial || interactable == null)
        {
            return true;
        }

        if (interactable is ARQuestNPC arNpc)
        {
            bool isGuide = IsGuideNpc(arNpc);
            if (!isGuide)
            {
                blockedReason = "Talk to the guide NPC to follow the tutorial.";
                return false;
            }

            bool allowed = currentStep >= TutorialStep.TalkToGuideNpc;
            if (!allowed)
            {
                blockedReason = "Follow the tutorial path first before talking to the guide NPC.";
            }

            return allowed;
        }

        if (interactable is APVendorNPC)
        {
            bool allowed = currentStep >= TutorialStep.SellForCreditUnlock;
            if (!allowed)
            {
                blockedReason = "Shop is not unlocked yet. Follow the tutorial to unlock credit.";
            }

            return allowed;
        }

        if (interactable is Oven)
        {
            bool allowed = currentStep >= TutorialStep.GoToRestaurant;
            if (!allowed)
            {
                blockedReason = "You can't use the oven yet. Follow the tutorial steps.";
            }

            return allowed;
        }

        if (interactable is RestaurantOpenCloseInteractable)
        {
            bool allowed = currentStep >= TutorialStep.OpenRestaurant;
            if (!allowed)
            {
                blockedReason = "You can't open the restaurant yet. Place food on the counter first.";
            }

            return allowed;
        }

        if (interactable is JournalTable)
        {
            bool allowed = currentStep >= TutorialStep.OpenJournal;
            if (!allowed)
            {
                blockedReason = "You can't open the journal yet. Return home at night first.";
            }

            return allowed;
        }

        if (interactable is Bed)
        {
            bool allowed = currentStep >= TutorialStep.FinishJournal;
            if (!allowed)
            {
                blockedReason = "You can't sleep yet. Finish your journal entries first.";
            }

            return allowed;
        }

        if (interactable is MailboxInteractable)
        {
            bool allowed = currentStep >= TutorialStep.ReadFirstLetter;
            if (!allowed)
            {
                blockedReason = "You can't open the mailbox yet. Wait for a letter the next day.";
            }

            return allowed;
        }

        if (interactable is UtilityPaymentInteractable)
        {
            bool allowed = currentStep >= TutorialStep.VisitUtilityPaymentZone;
            if (!allowed)
            {
                blockedReason = "Start following the tutorial path first.";
            }

            return allowed;
        }

        return true;
    }

    public bool CanPlaceFoodOnCounter(out string blockedReason)
    {
        blockedReason = null;

        if (!IsTutorialActive || !lockInteractionsDuringTutorial)
        {
            return true;
        }

        bool allowed = currentStep >= TutorialStep.PlaceFoodOnCounter || currentStep == TutorialStep.CookFood;
        if (!allowed)
        {
            blockedReason = "You can't place food on the counter yet. Reach the cooking step in the tutorial first.";
        }

        return allowed;
    }

    public void ShowBlockedMessage(string message, Vector3 anchor)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        DailyJournalRules.ShowMessage(message);
        CashPopupManager.ShowInfoPopup(anchor + new Vector3(0f, 1.2f, 0f), message);
    }

    public TutorialStateSnapshot CaptureState()
    {
        return new TutorialStateSnapshot
        {
            version = TutorialStateSnapshot.CURRENT_VERSION,
            hasState = true,
            stepIndex = (int)currentStep,
            completed = tutorialCompleted,
            bonusQuestActive = bonusQuestActive,
            bonusRecallTriggered = bonusRecallTriggered,
            creditOverrideApplied = creditOverrideApplied,
            flags = new List<string>(flags),
        };
    }

    public void RestoreState(TutorialStateSnapshot snapshot)
    {
        if (!enableTutorial)
        {
            return;
        }

        if (snapshot == null || !snapshot.hasState)
        {
            if (treatLegacySaveAsCompleted)
            {
                tutorialCompleted = true;
                currentStep = TutorialStep.Completed;
                overlayUI?.Hide();
            }

            return;
        }

        currentStep = ClampStep(snapshot.stepIndex);
        tutorialCompleted = snapshot.completed;
        bonusQuestActive = snapshot.bonusQuestActive;
        bonusRecallTriggered = snapshot.bonusRecallTriggered;
        creditOverrideApplied = snapshot.creditOverrideApplied;

        flags.Clear();
        if (snapshot.flags != null)
        {
            foreach (string flag in snapshot.flags)
            {
                if (!string.IsNullOrWhiteSpace(flag))
                {
                    flags.Add(flag);
                }
            }
        }

        if (tutorialCompleted)
        {
            currentStep = TutorialStep.Completed;
            overlayUI?.Hide();
            return;
        }

        bonusZone1Visited = flags.Contains("bonus-zone1-visited");
        bonusZone2Visited = flags.Contains("bonus-zone2-visited");

        if (creditOverrideApplied)
        {
            ApplyCreditThresholdOverride();
        }

        initialized = true;
        ReconcileWithCurrentWorldState();
        ApplyStepState(currentStep);
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!IsTutorialActive)
        {
            return;
        }

        cachedOvenTarget = null;
        cachedCounterTarget = null;
        cachedRestaurantSignTarget = null;
        cachedJournalTableTarget = null;
        cachedMailboxTarget = null;
        cachedZone1EntranceTarget = null;
        cachedZone2EntranceTarget = null;
        cachedGoToRestaurantRouteTarget = null;

        ApplyStepState(currentStep);
    }

    private void HandleAfterLoad()
    {
        if (!IsTutorialActive)
        {
            return;
        }

        ReconcileWithCurrentWorldState();
        ApplyStepState(currentStep);
    }

    private void HandlePlayerFirstMove()
    {
        if (currentStep == TutorialStep.WaitForFirstMove)
        {
            AdvanceToStep(TutorialStep.VisitUtilityPaymentZone);
        }
    }

    private void HandleQuestCreated(ARQuestData quest)
    {
        if (!IsGuideQuest(quest))
        {
            return;
        }

        if (currentStep <= TutorialStep.TalkToGuideNpc)
        {
            AdvanceToStep(TutorialStep.CompleteARCollectObjective);
        }

        if (quest.currentProgress >= quest.requiredCount)
        {
            AdvanceToStep(TutorialStep.ReturnToGuideNpc);
        }
    }

    private void HandleQuestProgressChanged(ARQuestData quest)
    {
        if (!IsGuideQuest(quest))
        {
            return;
        }

        if (quest.currentProgress >= quest.requiredCount && currentStep <= TutorialStep.CompleteARCollectObjective)
        {
            AdvanceToStep(TutorialStep.ReturnToGuideNpc);
        }
    }

    private void HandleQuestHandedIn(ARQuestData quest)
    {
        if (!IsGuideQuest(quest))
        {
            return;
        }

        // บังคับ tutorial payment term ให้ชัดเจน: นอน 1 คืนแล้วต้องได้จดหมาย
        int today = DailyJournalRules.GetCurrentAccountingDay();
        quest.paymentTermDays = 1;
        quest.dueTotalDay = today + 1;

        if (currentStep <= TutorialStep.ReturnToGuideNpc)
        {
            AdvanceToStep(TutorialStep.SellForCreditUnlock);
        }
    }

    private void HandleCreditTierUnlocked(string vendorName, ItemQuality quality, int requiredCount)
    {
        if (!IsTutorialActive)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(creditVendorName)
            && !string.Equals(vendorName, creditVendorName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (quality != tutorialCreditQuality)
        {
            return;
        }

        if (currentStep <= TutorialStep.SellForCreditUnlock)
        {
            flags.Add($"credit-unlocked-{quality}");
            AdvanceToStep(TutorialStep.CreateAPDebt);
            DailyJournalRules.ShowMessage(
                $"Credit {quality} unlocked (minimum {requiredCount} items). You can now purchase using credit.");
        }
    }

    private void HandleAPQuestCreated(APQuestData debt)
    {
        if (debt == null)
        {
            return;
        }

        if (currentStep <= TutorialStep.CreateAPDebt)
        {
            AdvanceToStep(TutorialStep.ViewAPQuest);
        }
    }

    private void HandleAPQuestViewed(APQuestData debt)
    {
        if (debt == null)
        {
            return;
        }

        if (currentStep <= TutorialStep.ViewAPQuest)
        {
            AdvanceToStep(TutorialStep.GoToRestaurant);
            TryActivateBonusQuest();
        }
    }

    private void HandleAPQuestRepaid(APQuestData _)
    {
        if (!IsTutorialActive)
        {
            return;
        }

        CashPopupManager.ShowInfoPopup(transform.position + Vector3.up, "AP Debt repaid, credit improved.");
    }

    private void HandleDebtOverdue(APQuestData _)
    {
        if (!IsTutorialActive)
        {
            return;
        }

        CashPopupManager.ShowInfoPopup(transform.position + Vector3.up, "AP Debt is overdue, interest will be added.");
    }

    private void HandleCookingCompleted(Item _)
    {
        if (currentStep <= TutorialStep.CookFood)
        {
            AdvanceToStep(TutorialStep.PlaceFoodOnCounter);
        }
    }

    private void HandleFoodPlacedOnCounter(RestaurantCounter _, Item __)
    {
        if (currentStep <= TutorialStep.PlaceFoodOnCounter)
        {
            AdvanceToStep(TutorialStep.OpenRestaurant);
        }
    }

    private void HandleShopOpened()
    {
        if (currentStep <= TutorialStep.OpenRestaurant)
        {
            AdvanceToStep(TutorialStep.MakeFirstSale);
        }
    }

    private void HandleFirstSaleMade()
    {
        if (currentStep <= TutorialStep.MakeFirstSale)
        {
            AdvanceToStep(TutorialStep.CloseRestaurant);
        }
    }

    private void HandleShopClosed()
    {
        if (currentStep <= TutorialStep.CloseRestaurant)
        {
            AdvanceToStep(TutorialStep.GoHomeAtNight);
        }
    }

    private void HandleJournalOpened()
    {
        if (currentStep <= TutorialStep.OpenJournal)
        {
            AdvanceToStep(TutorialStep.ConfirmJournalEntry);
        }
    }

    private void HandleJournalEntryConfirmed()
    {
        if (currentStep <= TutorialStep.ConfirmJournalEntry)
        {
            AdvanceToStep(TutorialStep.FinishJournal);
        }
    }

    private void HandleJournalFinished(bool isPassed, AuditReport _)
    {
        if (!isPassed)
        {
            return;
        }

        if (currentStep <= TutorialStep.FinishJournal)
        {
            AdvanceToStep(TutorialStep.WaitFirstLetter);
        }
    }

    private void HandleBedBlocked(string message)
    {
        if (!IsTutorialActive)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(message))
        {
            DailyJournalRules.ShowMessage(message);
        }
    }

    private void HandleFirstLetterReceived(ARQuestData quest)
    {
        if (!IsGuideQuest(quest))
        {
            return;
        }

        if (currentStep <= TutorialStep.WaitFirstLetter)
        {
            AdvanceToStep(TutorialStep.ReadFirstLetter);
        }
    }

    private void HandleLetterRead(ARQuestData quest)
    {
        if (!IsGuideQuest(quest))
        {
            return;
        }

        if (currentStep <= TutorialStep.ReadFirstLetter)
        {
            flags.Add("ar-letter-read");

            // เลือก step ถัดไปตามสถานะ bill จริง ณ ตอนนั้น
            TutorialStep next = ResolvePostLetterStep();
            AdvanceToStep(next);
        }
    }

    /// <summary>
    /// หลังอ่าน AR letter เสร็จ ให้เลือก step ถัดไปตามว่ามี utility bill อยู่ใน mailbox หรือเปล่า
    /// </summary>
    private TutorialStep ResolvePostLetterStep()
    {
        bool hasUnreadBill = RestaurantUtilityBillingCache.GetUnreadMailboxBillsSnapshot().Count > 0;
        if (hasUnreadBill)
        {
            return TutorialStep.ReadUtilityBillLetter;
        }

        bool hasUnpaidBill = RestaurantUtilityBillingCache.GetUnpaidBillsSnapshot().Count > 0;
        if (hasUnpaidBill)
        {
            return TutorialStep.PayUtilityBill;
        }

        // ถ้ายังไม่มีบิลในตอนนี้ ให้คง flow ไว้ที่ขั้นรออ่านบิล utility
        // (อย่าเพิ่งจบ tutorial หลังอ่าน AR letter)
        return TutorialStep.ReadUtilityBillLetter;
    }

    private void HandleUtilityBillLetterRead()
    {
        if (!IsTutorialActive)
        {
            return;
        }

        if (currentStep <= TutorialStep.ReadUtilityBillLetter)
        {
            flags.Add("utility-bill-letter-read");
            AdvanceToStep(TutorialStep.PayUtilityBill);
        }
    }

    private void HandleUtilityBillPaid()
    {
        if (!IsTutorialActive)
        {
            return;
        }

        if (currentStep == TutorialStep.PayUtilityBill)
        {
            flags.Add("utility-bill-paid");
            AdvanceToStep(TutorialStep.Completed);
        }
    }

    private void ShowBonusZoneTourOverlay()
    {
        if (!bonusZone1Visited)
        {
            Transform zone1Target = ResolveBonusZone1Target();
            ShowOverlay(
                "Free time! Head to Zone 1 (Slime Zone) to practice combat.\nSlime drops Sugar Dust used in basic recipes.\nZone 2 Cave has Chocolate Slime & Golem with rarer drops!",
                zone1Target, -1f);
        }
        else if (!bonusZone2Visited)
        {
            Transform zone2Target = ResolveBonusZone2Target();
            ShowOverlay(
                "Now explore Zone 2 Cave!\nChocolate Slime drops Chocolate Dust.\nGolem drops Stone — both used in advanced recipes.",
                zone2Target, -1f);
        }
    }

    private Transform ResolveBonusZone1Target()
    {
        // Try new Zone 1 Entrance field first
        Transform target = Resolve(TutorialTargetType.Zone1Entrance, zone1EntranceTarget);
        if (target != null)
        {
            return target;
        }

        // Fallback to legacy Collect Zone Entrance
        target = Resolve(TutorialTargetType.CollectZoneEntrance, collectZoneEntranceTarget);
        if (target != null)
        {
            return target;
        }

        return GetTriggerTarget(TutorialTriggerType.Zone1Entrance, ref cachedZone1EntranceTarget);
    }

    private Transform ResolveBonusZone2Target()
    {
        Transform target = Resolve(TutorialTargetType.Zone2Entrance, zone2EntranceTarget);
        if (target != null)
        {
            return target;
        }

        return GetTriggerTarget(TutorialTriggerType.Zone2Entrance, ref cachedZone2EntranceTarget);
    }

    private Transform ResolveGoToRestaurantTarget()
    {
        Transform directRestaurant = Resolve(TutorialTargetType.Restaurant, restaurantTarget);
        if (directRestaurant != null)
        {
            return directRestaurant;
        }

        Scene activeScene = SceneManager.GetActiveScene();
        string sceneName = activeScene.name;

        if (string.Equals(sceneName, Zone2SceneName, StringComparison.Ordinal))
        {
            Transform toZone1 = GetTeleporterTargetToScene(Zone1SceneName, ref cachedGoToRestaurantRouteTarget);
            if (toZone1 != null)
            {
                return toZone1;
            }
        }

        if (string.Equals(sceneName, Zone1SceneName, StringComparison.Ordinal))
        {
            Transform zone1Exit = GetTriggerTarget(TutorialTriggerType.Zone1Entrance, ref cachedZone1EntranceTarget);
            if (zone1Exit != null)
            {
                return zone1Exit;
            }

            Transform toVillage = GetTeleporterTargetToScene(VillageSceneName, ref cachedGoToRestaurantRouteTarget);
            if (toVillage != null)
            {
                return toVillage;
            }
        }

        return GetTeleporterTargetToScene(VillageSceneName, ref cachedGoToRestaurantRouteTarget);
    }

    private Transform GetTriggerTarget(TutorialTriggerType triggerType, ref Transform cache)
    {
        if (cache != null)
        {
            return cache;
        }

        TutorialTrigger[] triggers = FindObjectsOfType<TutorialTrigger>();
        for (int i = 0; i < triggers.Length; i++)
        {
            TutorialTrigger trigger = triggers[i];
            if (trigger == null || trigger.TriggerType != triggerType)
            {
                continue;
            }

            cache = trigger.transform;
            return cache;
        }

        return null;
    }

    private Transform GetTeleporterTargetToScene(string targetSceneName, ref Transform cache)
    {
        if (cache != null)
        {
            return cache;
        }

        Teleporter[] teleporters = FindObjectsOfType<Teleporter>();
        for (int i = 0; i < teleporters.Length; i++)
        {
            Teleporter teleporter = teleporters[i];
            if (teleporter == null)
            {
                continue;
            }

            if (!string.Equals(teleporter.targetScene, targetSceneName, StringComparison.Ordinal))
            {
                continue;
            }

            cache = teleporter.transform;
            return cache;
        }

        return null;
    }

    private void TryActivateBonusQuest()
    {
        if (!TimeManager.TryGetCurrentDateTime(out TimeManager.DateTime dateTime))
        {
            return;
        }

        bool beforeFifteen = dateTime.Hour < 15;
        if (!beforeFifteen || bonusQuestActive)
        {
            return;
        }

        bonusQuestActive = true;
        DailyJournalRules.ShowMessage("Free time before the restaurant opens! Explore the monster zones and return before 5:00 PM.");
        ShowBonusZoneTourOverlay();
    }

    private void CheckBonusQuestRecall(TimeManager.DateTime dateTime)
    {
        if (ReconcileWithCurrentWorldState())
        {
            ApplyStepState(currentStep);
        }

        bool isFourThirty = dateTime.Hour > 16 || (dateTime.Hour == 16 && dateTime.Minutes >= 30);

        if (currentStep == TutorialStep.GoToRestaurant && isFourThirty && !goToRestaurantArrowShown)
        {
            goToRestaurantArrowShown = true;
            ApplyStepState(currentStep);
        }

        if (!bonusQuestActive || bonusRecallTriggered)
        {
            return;
        }

        if (isFourThirty)
        {
            bonusRecallTriggered = true;
            DailyJournalRules.ShowMessage("It's almost 5:00 PM. Hurry back to the restaurant to open shop.");
        }
    }

    private bool AdvanceToStep(TutorialStep nextStep)
    {
        if (!IsTutorialActive)
        {
            return false;
        }

        if ((int)nextStep <= (int)currentStep)
        {
            return false;
        }

        currentStep = nextStep;
        flags.Add($"step-{(int)currentStep}");

        if (currentStep == TutorialStep.Completed)
        {
            tutorialCompleted = true;
            HideOverlay();
            DailyJournalRules.ShowMessage("Tutorial completed! You are now free to play.");
            return true;
        }

        ApplyStepState(currentStep);
        return true;
    }

    private void ApplyStepState(TutorialStep step)
    {
        if (!initialized || tutorialCompleted)
        {
            return;
        }

        if (step == TutorialStep.TalkToGuideNpc)
        {
            ResetGuideNpcCooldown();
        }

        if (step == TutorialStep.SellForCreditUnlock)
        {
            ApplyCreditThresholdOverride();
        }

        if (step == TutorialStep.ReadUtilityBillLetter)
        {
            EnsureTutorialUtilityBillForDayTwo();
        }

        // พิเศษสำหรับ GoToRestaurant: ก่อน 4:30 PM → แสดง bonus zone tour; หลัง 4:30 PM → นำทางไปร้าน
        if (step == TutorialStep.GoToRestaurant)
        {
            if (!IsAtLeastFourThirty())
            {
                if (bonusQuestActive && (!bonusZone1Visited || !bonusZone2Visited))
                    ShowBonusZoneTourOverlay();
                else
                    HideOverlay();
                return;
            }
        }

        ShowOverlay(GetStepMessage(step), GetStepTarget(step), -1f);
    }

    private void ResetGuideNpcCooldown()
    {
        // เรียก ClearCooldown บน NPC ตัวจริงถ้ามี (จัดการทั้ง runtime state และ save state)
        if (guideNpc != null)
        {
            guideNpc.ClearCooldown();
            Debug.Log($"[TutorialManager] Reset cooldown ของ guide NPC '{guideNpc.NpcName}'");
            return;
        }

        // fallback: reset ผ่าน ARQuestManager เฉยๆ กรณีไม่มี reference
        if (ARQuestManager.Instance == null || string.IsNullOrWhiteSpace(guideNpcName))
        {
            return;
        }

        int currentCooldown = ARQuestManager.Instance.GetNPCCooldown(guideNpcName);
        if (currentCooldown > 0)
        {
            ARQuestManager.Instance.SetNPCCooldown(guideNpcName, 0);
            Debug.Log($"[TutorialManager] Reset cooldown ของ guide NPC '{guideNpcName}' (fallback)");
        }
    }

    private void ApplyCreditThresholdOverride()
    {
        if (creditOverrideApplied)
        {
            return;
        }

        CreditSystem.Instance?.SetThresholdOverride(tutorialCreditQuality, tutorialCreditRequiredSales);
        creditOverrideApplied = true;
    }

    private void HideOverlay()
    {
        if (overlayUI != null)
        {
            overlayUI.Hide();
        }
    }

    private void ShowOverlay(string message, Transform target, float duration = -1f)
    {
        if (overlayUI != null)
        {
            overlayUI.ShowMessage(message, target, duration);
        }
    }

    private bool ReconcileWithCurrentWorldState()
    {
        if (tutorialCompleted)
        {
            return false;
        }

        bool changedTotal = false;
        bool changed;
        do
        {
            changed = false;
            ARQuestData guideQuest = FindGuideQuest();

            if (currentStep == TutorialStep.TalkToGuideNpc && guideQuest != null)
            {
                currentStep = TutorialStep.CompleteARCollectObjective;
                changed = true;
            }

            if (currentStep == TutorialStep.CompleteARCollectObjective
                && guideQuest != null
                && guideQuest.currentProgress >= guideQuest.requiredCount)
            {
                currentStep = TutorialStep.ReturnToGuideNpc;
                changed = true;
            }

            if (currentStep == TutorialStep.ReturnToGuideNpc && guideQuest != null && guideQuest.isARRecorded)
            {
                currentStep = TutorialStep.SellForCreditUnlock;
                changed = true;
            }

            if (currentStep == TutorialStep.SellForCreditUnlock
                && CreditSystem.Instance != null
                && CreditSystem.Instance.HasCreditFor(creditVendorName, tutorialCreditQuality))
            {
                currentStep = TutorialStep.CreateAPDebt;
                changed = true;
            }

            if (currentStep == TutorialStep.CreateAPDebt
                && APQuestManager.Instance != null
                && APQuestManager.Instance.GetActiveDebts().Count > 0)
            {
                currentStep = TutorialStep.ViewAPQuest;
                changed = true;
            }

            if (currentStep == TutorialStep.OpenRestaurant
                && RestaurantServiceManager.ActiveInstance != null
                && RestaurantServiceManager.ActiveInstance.IsOpen)
            {
                currentStep = TutorialStep.MakeFirstSale;
                changed = true;
            }

            if (currentStep == TutorialStep.CloseRestaurant
                && RestaurantServiceManager.ActiveInstance != null
                && !RestaurantServiceManager.ActiveInstance.IsOpen)
            {
                currentStep = TutorialStep.GoHomeAtNight;
                changed = true;
            }

            if (currentStep == TutorialStep.WaitFirstLetter)
            {
                int accountingDay = DailyJournalRules.GetCurrentAccountingDay();
                bool dayReachedForTutorial = accountingDay >= 2;

                // Tutorial-specific fallback:
                // ถึงวันถัดไปแล้วให้ไปขั้นอ่านจดหมายทันที แม้ระบบ mailbox จะยังไม่ rebuild ทัน
                if (guideQuest != null && guideQuest.isARRecorded)
                {
                    dayReachedForTutorial = accountingDay > guideQuest.createdTotalDay;
                }

                bool hasPendingLetter = HasPendingLetter();
                if (!hasPendingLetter && dayReachedForTutorial)
                {
                    hasPendingLetter = TryForceGuideLetterForTutorial(guideQuest, accountingDay);
                }

                if (hasPendingLetter || dayReachedForTutorial)
                {
                    currentStep = TutorialStep.ReadFirstLetter;
                    changed = true;
                }
            }
            else if (guideQuest != null
                     && guideQuest.isARRecorded
                     && !guideQuest.isLetterSent
                     && guideQuest.paymentTermDays > 1)
            {
                // กันกรณี quest term มากกว่า 1 วันจากค่าที่มากับ NPC/save เดิม
                // ซึ่งทำให้ tutorial ค้างที่ WaitFirstLetter ทั้งที่ flow ต้องอ่านจดหมายเช้าวันถัดไป
                guideQuest.paymentTermDays = 1;
                guideQuest.dueTotalDay = guideQuest.createdTotalDay + 1;
            }

            if (currentStep == TutorialStep.ReadFirstLetter && flags.Contains("ar-letter-read"))
            {
                // Re-evaluate step ที่ถูกต้องตาม bill state ปัจจุบัน (bill อาจมาช้าหลังจาก save/load)
                currentStep = ResolvePostLetterStep();
                changed = true;
            }
            else if (currentStep == TutorialStep.ReadFirstLetter)
            {
                int accountingDay = DailyJournalRules.GetCurrentAccountingDay();
                bool dayReachedForTutorial = accountingDay >= 2;
                if (guideQuest != null && guideQuest.isARRecorded)
                {
                    dayReachedForTutorial = accountingDay > guideQuest.createdTotalDay;
                }

                if (dayReachedForTutorial && !HasPendingLetter())
                {
                    // ถ้า tutorial เข้าขั้นอ่านจดหมายแล้ว แต่ mailbox ยังว่าง
                    // ให้พยายามสร้าง AR letter ซ้ำจนกว่าจะขึ้นจริง
                    TryForceGuideLetterForTutorial(guideQuest, accountingDay);
                }
            }

            if (currentStep == TutorialStep.ReadUtilityBillLetter)
            {
                if (flags.Contains("utility-bill-letter-read"))
                {
                    currentStep = TutorialStep.PayUtilityBill;
                    changed = true;
                }
                else
                {
                    bool hasUnread = RestaurantUtilityBillingCache.GetUnreadMailboxBillsSnapshot().Count > 0;
                    if (!hasUnread)
                    {
                        bool hasUnpaid = RestaurantUtilityBillingCache.GetUnpaidBillsSnapshot().Count > 0;
                        if (hasUnpaid)
                        {
                            // bill มีอยู่แต่ถูก journal แล้ว → ไปจ่ายได้เลย
                            flags.Add("utility-bill-letter-read");
                            currentStep = TutorialStep.PayUtilityBill;
                            changed = true;
                        }
                        else
                        {
                            // Tutorial-only fallback: บังคับให้มี utility bill ในวันที่ 2
                            EnsureTutorialUtilityBillForDayTwo();
                        }
                    }
                }
            }

            if (currentStep == TutorialStep.PayUtilityBill && flags.Contains("utility-bill-paid"))
            {
                currentStep = TutorialStep.Completed;
                changed = true;
            }

            if (changed) changedTotal = true;
        }
        while (changed);

        return changedTotal;
    }

    private bool HasPendingLetter()
    {
        if (ARQuestManager.Instance == null) return false;

        var quests = ARQuestManager.Instance.GetPendingQuests();
        for (int i = 0; i < quests.Count; i++)
        {
            ARQuestData q = quests[i];
            if (q != null && q.isLetterSent && q.status == QuestStatus.LetterSent)
            {
                return true;
            }
        }
        return false;
    }

    private bool EnsureTutorialUtilityBillForDayTwo()
    {
        int accountingDay = DailyJournalRules.GetCurrentAccountingDay();
        if (accountingDay < 2)
        {
            return false;
        }

        if (RestaurantUtilityBillingCache.GetUnreadMailboxBillsSnapshot().Count > 0
            || RestaurantUtilityBillingCache.GetUnpaidBillsSnapshot().Count > 0)
        {
            return false;
        }

        if (!TimeManager.TryGetCurrentDateTime(out TimeManager.DateTime now))
        {
            return false;
        }

        RestaurantServiceManager.UtilityBillingSaveState state = RestaurantUtilityBillingCache.CaptureState();
        if (state == null)
        {
            return false;
        }

        if (state.issuedBills == null)
        {
            state.issuedBills = new List<RestaurantServiceManager.UtilityBillStatement>();
        }

        for (int i = 0; i < state.issuedBills.Count; i++)
        {
            RestaurantServiceManager.UtilityBillStatement bill = state.issuedBills[i];
            if (bill == null)
            {
                continue;
            }

            int outstanding = Mathf.Max(0, bill.electricityOutstandingAmount) + Mathf.Max(0, bill.waterOutstandingAmount);
            if (outstanding > 0)
            {
                return false;
            }
        }

        int weekIndex = TimeManager.GetWeekIndex(now);
        RestaurantServiceManager.UtilityBillStatement tutorialBill = new RestaurantServiceManager.UtilityBillStatement
        {
            billId = $"UTIL-TUTORIAL-Y{now.Year:D2}-S{((int)now.Season + 1):D1}-W{weekIndex:D3}-D{now.Date:D2}",
            weekIndex = weekIndex,
            openMinutes = 1,
            dishesSold = 1,
            electricityAmount = 120,
            waterAmount = 30,
            totalAmount = 150,
            isPaid = false,
            isAccrued = false,
            outstandingAmount = 150,
            hasPaidAt = false,
            issuedAt = now,
            electricityQuantity = 1,
            waterQuantity = 1,
            electricityOutstandingAmount = 120,
            waterOutstandingAmount = 30,
            electricityIsAccrued = false,
            waterIsAccrued = false,
            electricityPaid = false,
            waterPaid = false,
            hasElectricityPaidAt = false,
            hasWaterPaidAt = false,
            mailboxLetterRead = false,
            journalEntryPosted = false,
            hasJournalEntryPostedAt = false,
        };

        state.issuedBills.Add(tutorialBill);
        RestaurantUtilityBillingCache.ApplyState(state);
        // อย่า enqueue เข้ากล่องจดหมายทันที เพราะอาจซ้อนกับหน้าจดหมาย AR ที่กำลังอ่านอยู่
        // ให้โผล่ตอนผู้เล่น "เปิด mailbox ครั้งถัดไป" ผ่าน RebuildPendingMail ตาม flow tutorial
        return true;
    }

    private bool TryForceGuideLetterForTutorial(ARQuestData guideQuest, int accountingDay)
    {
        ARQuestData candidate = guideQuest != null && guideQuest.isARRecorded
            ? guideQuest
            : FindRecordedQuestAwaitingLetter();

        if (candidate == null)
        {
            return false;
        }

        if (candidate.isLetterSent && candidate.status == QuestStatus.LetterSent)
        {
            return true;
        }

        if (accountingDay <= candidate.createdTotalDay)
        {
            return false;
        }

        MailboxInteractable mailbox = MailboxInteractable.Instance;
        if (mailbox == null)
        {
            return false;
        }

        candidate.paymentTermDays = 1;
        candidate.dueTotalDay = candidate.createdTotalDay + 1;
        candidate.isLetterSent = true;
        candidate.status = QuestStatus.LetterSent;
        mailbox.AddLetter(candidate);
        return true;
    }

    private ARQuestData FindRecordedQuestAwaitingLetter()
    {
        if (ARQuestManager.Instance == null)
        {
            return null;
        }

        List<ARQuestData> quests = ARQuestManager.Instance.GetPendingQuests();
        ARQuestData fallback = null;
        for (int i = 0; i < quests.Count; i++)
        {
            ARQuestData quest = quests[i];
            if (quest == null || !quest.isARRecorded)
            {
                continue;
            }

            if (quest.isLetterSent && quest.status == QuestStatus.LetterSent)
            {
                return quest;
            }

            if (!quest.isLetterSent && fallback == null)
            {
                fallback = quest;
            }
        }

        return fallback;
    }

    private ARQuestData FindGuideQuest()
    {
        if (ARQuestManager.Instance == null)
        {
            return null;
        }

        List<ARQuestData> quests = ARQuestManager.Instance.GetPendingQuests();
        for (int i = 0; i < quests.Count; i++)
        {
            ARQuestData quest = quests[i];
            if (IsGuideQuest(quest))
            {
                return quest;
            }
        }

        return null;
    }

    private bool IsGuideQuest(ARQuestData quest)
    {
        if (quest == null)
        {
            return false;
        }

        string expectedName = guideNpc != null
            ? guideNpc.NpcName
            : guideNpcName;

        string normalizedQuestNpcName = NormalizeNpcName(quest.npcName);
        string normalizedExpectedName = NormalizeNpcName(expectedName);

        if (!string.IsNullOrWhiteSpace(normalizedExpectedName)
            && string.Equals(normalizedQuestNpcName, normalizedExpectedName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Fallback สำหรับ tutorial collect flow:
        // ถ้าช่วงเก็บของมีเควสที่ยังไม่บันทึก AR อยู่แค่ 1 เควส ให้ถือว่าเป็นเควสไกด์
        if (IsLikelyTutorialGuideQuest(quest))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(normalizedExpectedName))
        {
            Debug.LogWarning("[TutorialManager] guideNpcName is empty — assign Guide NPC in the Inspector. IsGuideQuest returning false.");
        }

        return false;
    }

    private bool IsLikelyTutorialGuideQuest(ARQuestData quest)
    {
        if (ARQuestManager.Instance == null)
        {
            return false;
        }

        if (currentStep < TutorialStep.CompleteARCollectObjective || currentStep > TutorialStep.ReturnToGuideNpc)
        {
            return false;
        }

        List<ARQuestData> quests = ARQuestManager.Instance.GetPendingQuests();
        ARQuestData unresolved = null;
        int unresolvedCount = 0;

        for (int i = 0; i < quests.Count; i++)
        {
            ARQuestData q = quests[i];
            if (q == null || q.isARRecorded)
            {
                continue;
            }

            unresolved = q;
            unresolvedCount++;
            if (unresolvedCount > 1)
            {
                return false;
            }
        }

        return unresolvedCount == 1 && ReferenceEquals(unresolved, quest);
    }

    private static string NormalizeNpcName(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private bool IsGuideNpc(ARQuestNPC npc)
    {
        if (npc == null)
        {
            return false;
        }

        if (guideNpc != null)
        {
            return npc == guideNpc;
        }

        if (string.IsNullOrWhiteSpace(guideNpcName))
        {
            Debug.LogWarning("[TutorialManager] guideNpcName is empty — assign Guide NPC in the Inspector. IsGuideNpc returning false.");
            return false;
        }

        return string.Equals(npc.NpcName, guideNpcName, StringComparison.OrdinalIgnoreCase);
    }

    private bool IsNightTime()
    {
        if (!TimeManager.TryGetCurrentDateTime(out TimeManager.DateTime dateTime))
        {
            return false;
        }

        return dateTime.Hour >= 18 || dateTime.Hour < 6;
    }

    private static TutorialStep ClampStep(int index)
    {
        int min = (int)TutorialStep.WaitForFirstMove;
        int max = (int)TutorialStep.Completed;
        int clamped = Mathf.Clamp(index, min, max);
        return (TutorialStep)clamped;
    }

    private string GetStepMessage(TutorialStep step)
    {
        if (stepMessageLookup.TryGetValue(step, out string customMessage) && !string.IsNullOrWhiteSpace(customMessage))
        {
            return customMessage;
        }

        return step switch
        {
            TutorialStep.WaitForFirstMove => "Use WASD to start moving and follow the tutorial path.",
            TutorialStep.VisitUtilityPaymentZone => "Go to the utility payment zone.",
            TutorialStep.VisitShopZone => "Go to the material shop zone.",
            TutorialStep.VisitHouseZone => "Go to the house zone to locate the journal table.",
            TutorialStep.VisitRestaurantZone => "Go to your restaurant zone.",
            TutorialStep.TalkToGuideNpc => "Talk to the Guide NPC to receive an AR Quest (Collect Item).",
            TutorialStep.CompleteARCollectObjective => "Defeat the Slimes in the village to collect Sugar Dust!\nPress Q to check your quest progress.\nReturn to the Guide NPC once you have enough.",
            TutorialStep.ReturnToGuideNpc => "Items collected, return and turn in the quest to the Guide NPC.",
            TutorialStep.SellForCreditUnlock => "Sell 5 sugar dust at the shop to unlock credit.",
            TutorialStep.CreateAPDebt => "Try buying items using credit to create an AP Quest.",
            TutorialStep.ViewAPQuest => "Open the quest log and check the AP Quest details and due date.",
            TutorialStep.GoToRestaurant => "Return to the restaurant and start cooking.\nThe restaurant is open from 5:00 PM to 10:00 PM.",
            TutorialStep.CookFood => "Use the oven to cook food.\nPress F to pick up the spatula, then press Space during the cooking mini-game.\nDon't let the food burn!",
            TutorialStep.PlaceFoodOnCounter => "Place the cooked food on the counter.",
            TutorialStep.OpenRestaurant => "With food ready, flip the sign to open the restaurant.",
            TutorialStep.MakeFirstSale => "Wait for your first sale.",
            TutorialStep.CloseRestaurant => "Close the restaurant when the sales round ends.",
            TutorialStep.GoHomeAtNight => "Return home during the night.",
            TutorialStep.OpenJournal => "Open the journal table to do your accounting.",
            TutorialStep.ConfirmJournalEntry => "Confirm at least 1 journal line entry.",
            TutorialStep.FinishJournal => "Submit the journal before going to sleep.",
            TutorialStep.WaitFirstLetter => "Finish the journal, then go to sleep.\nYou will receive an AR payment letter the next day.",
            TutorialStep.ReadFirstLetter => "Open the mailbox and read your AR payment letter.",
            TutorialStep.ReadUtilityBillLetter => "A utility bill has arrived in your mailbox!\nOpen the mailbox to read it.\nBills arrive every Monday — if unpaid for 2 weeks, you cannot open the shop.",
            TutorialStep.PayUtilityBill => "Go to the utility payment counter to pay your bill.\nElectricity and water can be paid separately.",
            _ => string.Empty,
        };
    }

    private void RebuildStepMessageLookup()
    {
        stepMessageLookup.Clear();
        if (stepMessages == null)
        {
            return;
        }

        for (int i = 0; i < stepMessages.Length; i++)
        {
            TutorialStepMessageEntry entry = stepMessages[i];
            if (string.IsNullOrWhiteSpace(entry.message))
            {
                continue;
            }

            stepMessageLookup[entry.step] = entry.message;
        }
    }

    private Transform GetStepTarget(TutorialStep step)
    {
        return step switch
        {
            TutorialStep.VisitUtilityPaymentZone => Resolve(TutorialTargetType.UtilityPayment, utilityPaymentTarget),
            TutorialStep.VisitShopZone => Resolve(TutorialTargetType.Shop, shopTarget),
            TutorialStep.VisitHouseZone => Resolve(TutorialTargetType.House, houseTarget),
            TutorialStep.VisitRestaurantZone => Resolve(TutorialTargetType.Restaurant, restaurantTarget),
            TutorialStep.TalkToGuideNpc => Resolve(TutorialTargetType.GuideNpc, guideNpcTarget ?? guideNpc?.transform),
            TutorialStep.CompleteARCollectObjective => Resolve(TutorialTargetType.VillageSlime, villageSlimeTarget),
            TutorialStep.ReturnToGuideNpc => Resolve(TutorialTargetType.GuideNpc, guideNpcTarget ?? guideNpc?.transform),
            TutorialStep.SellForCreditUnlock => Resolve(TutorialTargetType.Shop, shopTarget),
            TutorialStep.CreateAPDebt => Resolve(TutorialTargetType.Shop, shopTarget),
            TutorialStep.GoToRestaurant => ResolveGoToRestaurantTarget(),
            TutorialStep.CookFood => Resolve(TutorialTargetType.Oven,
                ovenTarget ?? GetCachedTarget(ref cachedOvenTarget, FindObjectOfType<Oven>()?.transform)),
            TutorialStep.PlaceFoodOnCounter => Resolve(TutorialTargetType.Counter,
                counterTarget ?? GetCachedTarget(ref cachedCounterTarget, FindObjectOfType<RestaurantCounter>()?.transform)),
            TutorialStep.OpenRestaurant => Resolve(TutorialTargetType.RestaurantSign,
                restaurantSignTarget ?? GetCachedTarget(ref cachedRestaurantSignTarget, FindObjectOfType<RestaurantOpenCloseInteractable>()?.transform)),
            TutorialStep.GoHomeAtNight => Resolve(TutorialTargetType.Home, homeTarget),
            TutorialStep.OpenJournal => Resolve(TutorialTargetType.JournalTable,
                journalTableTarget ?? GetCachedTarget(ref cachedJournalTableTarget, FindObjectOfType<JournalTable>()?.transform)),
            TutorialStep.ReadFirstLetter => Resolve(TutorialTargetType.Mailbox,
                mailboxTarget ?? GetCachedTarget(ref cachedMailboxTarget, FindObjectOfType<MailboxInteractable>()?.transform)),
            TutorialStep.ReadUtilityBillLetter => Resolve(TutorialTargetType.Mailbox,
                mailboxTarget ?? GetCachedTarget(ref cachedMailboxTarget, FindObjectOfType<MailboxInteractable>()?.transform)),
            TutorialStep.PayUtilityBill => Resolve(TutorialTargetType.UtilityPayment, utilityPaymentTarget),
            _ => null,
        };
    }

    private static Transform GetCachedTarget(ref Transform cache, Transform found)
    {
        if (cache == null)
        {
            cache = found;
        }

        return cache;
    }

    private Transform Resolve(TutorialTargetType type, Transform fallback)
    {
        if (registeredTargets.TryGetValue(type, out Transform t) && t != null)
            return t;
        return fallback != null ? fallback : null;
    }
}
