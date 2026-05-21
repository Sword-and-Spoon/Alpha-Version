using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }

    public const int MAX_MANUAL_SLOTS = 3;
    public const int AUTOSAVE_SLOT = 0;

    public static event Action OnBeforeSave;
    public static event Action OnAfterLoad;

    private const string SAVE_DIR = "Saves";
    private const string FILE_PREFIX = "slot_";
    private const string FILE_EXT = ".json";

    private bool isLoading;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);
        EnsureSaveDirectory();
    }

    // -------------------- Public API --------------------

    public bool Save(int slot)
    {
        if (slot < 0 || slot > MAX_MANUAL_SLOTS)
        {
            Debug.LogError($"[SaveManager] Invalid slot {slot}");
            return false;
        }

        try
        {
            OnBeforeSave?.Invoke();
            SaveDataV1 data = CaptureCurrentState();
            string json = JsonUtility.ToJson(data, prettyPrint: true);
            WriteAtomic(GetSlotPath(slot), json);
            Debug.Log($"[SaveManager] Saved slot {slot}");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveManager] Save failed: {e}");
            return false;
        }
    }

    public void AutoSave()
    {
        Save(AUTOSAVE_SLOT);
    }

    public bool HasSave(int slot) => File.Exists(GetSlotPath(slot));

    public bool DeleteSlot(int slot)
    {
        string path = GetSlotPath(slot);
        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            File.Delete(path);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveManager] Delete failed: {e}");
            return false;
        }
    }

    public SlotInfo GetSlotInfo(int slot)
    {
        var info = new SlotInfo { slot = slot, exists = false };
        string path = GetSlotPath(slot);
        if (!File.Exists(path))
        {
            return info;
        }

        try
        {
            string json = File.ReadAllText(path);
            SaveDataV1 data = JsonUtility.FromJson<SaveDataV1>(json);
            if (data == null)
            {
                return info;
            }

            info.exists = true;
            info.timestamp = data.timestamp;
            info.sceneName = data.sceneName;
            info.money = data.inventory != null ? data.inventory.money : 0;

            if (data.time != null && data.time.dateTime != null)
            {
                info.date = data.time.dateTime.date;
                info.season = data.time.dateTime.season;
                info.year = data.time.dateTime.year;
                info.hour = data.time.dateTime.hour;
                info.minutes = data.time.dateTime.minutes;
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[SaveManager] Could not parse slot {slot}: {e.Message}");
        }

        return info;
    }

    public void LoadAndApply(int slot, Action<bool> onComplete = null)
    {
        if (isLoading)
        {
            Debug.LogWarning("[SaveManager] Load already in progress.");
            onComplete?.Invoke(false);
            return;
        }

        SaveDataV1 data = ReadSlot(slot);
        if (data == null)
        {
            onComplete?.Invoke(false);
            return;
        }

        if (data.version != SaveDataV1.CURRENT_VERSION)
        {
            Debug.LogError(
                $"[SaveManager] Incompatible save version {data.version} (expected {SaveDataV1.CURRENT_VERSION})");
            onComplete?.Invoke(false);
            return;
        }

        StartCoroutine(ApplySaveRoutine(data, onComplete));
    }

    // -------------------- Capture --------------------

    private SaveDataV1 CaptureCurrentState()
    {
        SaveDataV1 data = new SaveDataV1
        {
            version = SaveDataV1.CURRENT_VERSION,
            timestamp = DateTime.UtcNow.ToString("o"),
            sceneName = SceneManager.GetActiveScene().name,
        };

        if (GameManager.instance != null && GameManager.instance.inventoryData != null)
        {
            var src = GameManager.instance.inventoryData;
            data.inventory = new InventoryDTO
            {
                sword = ItemSerializer.ToDTO(src.swordSlot),
                axe = ItemSerializer.ToDTO(src.axeSlot),
                pickaxe = ItemSerializer.ToDTO(src.pickaxeSlot),
                money = src.money,
                slotCount = src.slotCount,
                items = new List<ItemDTO>(),
            };

            if (src.items != null)
            {
                foreach (Item item in src.items)
                {
                    data.inventory.items.Add(ItemSerializer.ToDTO(item));
                }
            }
        }

        if (TimeManager.Instance != null)
        {
            data.time = TimeManager.Instance.ToDTO();
        }

        data.restaurantCounters = RestaurantCounterStateCache.CaptureState();

        GameObject playerGo = GameManager.instance != null ? GameManager.instance.player : null;
        if (playerGo == null)
        {
            playerGo = GameObject.FindGameObjectWithTag("Player");
        }

        if (playerGo != null)
        {
            Vector3 pos = playerGo.transform.position;
            Quaternion rot = playerGo.transform.rotation;
            data.player = new PlayerDTO
            {
                px = pos.x,
                py = pos.y,
                pz = pos.z,
                rx = rot.x,
                ry = rot.y,
                rz = rot.z,
                rw = rot.w,
            };

            PlayerHealth health = playerGo.GetComponent<PlayerHealth>();
            if (health != null)
            {
                data.player.currentHealth = health.currentHealth;
                data.player.maxHealth = health.maxHealth;
            }
        }

        if (TransactionManager.Instance != null && TransactionManager.Instance.todayTransactions != null)
        {
            foreach (TransactionRecord r in TransactionManager.Instance.todayTransactions)
            {
                data.todayTransactions.Add(new TransactionDTO
                {
                    itemName = r.itemName,
                    npcName = r.npcName,
                    category = (int)r.category,
                    quantity = r.quantity,
                    totalPrice = r.totalPrice,
                    type = (int)r.type,
                    store = (int)r.store,
                    gameTime = TimeManager.DateTimeToDTO(r.gameTime),
                });
            }

            data.activeJournalDay = TransactionManager.Instance.ActiveJournalDay;
            data.journalCompletedForActiveDay = TransactionManager.Instance.IsJournalCompletedForActiveDay;
            data.hasDailyJournalReport = TransactionManager.Instance.TryGetCompletedJournalReport(out AuditReport dailyReport);
            if (data.hasDailyJournalReport)
            {
                data.dailyJournalReport = AuditReportToDTO(dailyReport);
            }
        }

        if (JournalManager.Instance != null)
        {
            foreach (EntryData e in JournalManager.Instance.GetConfirmedEntries())
            {
                data.confirmedJournalEntries.Add(new JournalEntryDTO
                {
                    entryId = e.entryId,
                    side = e.side,
                    itemName = e.itemName,
                    category = (int)e.category,
                    type = (int)e.type,
                    store = (int)e.store,
                    quantity = e.quantity,
                    amount = e.amount,
                    time = TimeManager.DateTimeToDTO(e.time),
                    isBalancingEntry = e.isBalancingEntry,
                    separatorAfter = e.separatorAfter,
                });
            }
        }

        if (ARQuestManager.Instance != null)
        {
            data.pendingQuests = ARQuestManager.Instance.CaptureState();
            data.npcCooldowns = ARQuestManager.Instance.CaptureCooldowns();
        }

        RestaurantServiceManager.UtilityBillingSaveState utilityState = RestaurantUtilityBillingCache.CaptureState();
        if (utilityState != null)
        {
            data.restaurantUtility = new RestaurantUtilityStateDTO
            {
                trackedWeekIndex = utilityState.trackedWeekIndex,
                lastBilledWeekIndex = utilityState.lastBilledWeekIndex,
                weeklyOpenMinutes = utilityState.weeklyOpenMinutes,
                weeklyDishesSold = utilityState.weeklyDishesSold,
                pendingBillWeekIndex = utilityState.pendingBillWeekIndex,
                pendingBillOpenMinutes = utilityState.pendingBillOpenMinutes,
                pendingBillDishesSold = utilityState.pendingBillDishesSold,
                hasOpenSession = utilityState.hasOpenSession,
                openSessionStartAbsoluteMinutes = utilityState.openSessionStartAbsoluteMinutes,
                bills = new List<UtilityBillDTO>(),
            };

            if (utilityState.issuedBills != null)
            {
                foreach (RestaurantServiceManager.UtilityBillStatement bill in utilityState.issuedBills)
                {
                    if (bill == null)
                    {
                        continue;
                    }

                    data.restaurantUtility.bills.Add(new UtilityBillDTO
                    {
                        billId = bill.billId,
                        weekIndex = bill.weekIndex,
                        openMinutes = bill.openMinutes,
                        dishesSold = bill.dishesSold,
                        electricityAmount = bill.electricityAmount,
                        waterAmount = bill.waterAmount,
                        totalAmount = bill.totalAmount,
                        isPaid = bill.isPaid,
                        isAccrued = bill.isAccrued,
                        outstandingAmount = bill.outstandingAmount,
                        hasPaidAt = bill.hasPaidAt,
                        issuedAt = TimeManager.DateTimeToDTO(bill.issuedAt),
                        paidAt = bill.hasPaidAt ? TimeManager.DateTimeToDTO(bill.paidAt) : new GameDateTimeDTO(),
                        electricityQuantity = bill.electricityQuantity,
                        waterQuantity = bill.waterQuantity,
                        electricityOutstandingAmount = bill.electricityOutstandingAmount,
                        waterOutstandingAmount = bill.waterOutstandingAmount,
                        electricityIsAccrued = bill.electricityIsAccrued,
                        waterIsAccrued = bill.waterIsAccrued,
                        electricityPaid = bill.electricityPaid,
                        waterPaid = bill.waterPaid,
                        hasElectricityPaidAt = bill.hasElectricityPaidAt,
                        hasWaterPaidAt = bill.hasWaterPaidAt,
                        electricityPaidAt = bill.hasElectricityPaidAt ? TimeManager.DateTimeToDTO(bill.electricityPaidAt) : new GameDateTimeDTO(),
                        waterPaidAt = bill.hasWaterPaidAt ? TimeManager.DateTimeToDTO(bill.waterPaidAt) : new GameDateTimeDTO(),
                        mailboxLetterRead = bill.mailboxLetterRead,
                        journalEntryPosted = bill.journalEntryPosted,
                        hasJournalEntryPostedAt = bill.hasJournalEntryPostedAt,
                        journalEntryPostedAt = bill.hasJournalEntryPostedAt ? TimeManager.DateTimeToDTO(bill.journalEntryPostedAt) : new GameDateTimeDTO(),
                    });
                }
            }
        }

        if (APQuestManager.Instance != null)
        {
            data.pendingAPQuests = APQuestManager.Instance.CaptureState();
        }

        if (CreditSystem.Instance != null)
        {
            data.vendorSalesRecords = CreditSystem.Instance.CaptureState();
        }

        if (TutorialManager.Instance != null)
        {
            TutorialStateSnapshot tutorialState = TutorialManager.Instance.CaptureState();
            if (tutorialState != null)
            {
                data.hasTutorialState = tutorialState.hasState;
                data.tutorialStateVersion = tutorialState.version;
                data.tutorialStepIndex = tutorialState.stepIndex;
                data.tutorialCompleted = tutorialState.completed;
                data.tutorialBonusQuestActive = tutorialState.bonusQuestActive;
                data.tutorialBonusRecallTriggered = tutorialState.bonusRecallTriggered;
                data.tutorialCreditOverrideApplied = tutorialState.creditOverrideApplied;
                data.tutorialFlags = tutorialState.flags != null
                    ? new List<string>(tutorialState.flags)
                    : new List<string>();
            }
        }

        return data;
    }

    // -------------------- Apply --------------------

    private IEnumerator ApplySaveRoutine(SaveDataV1 data, Action<bool> onComplete)
    {
        isLoading = true;

        if (GameManager.instance != null)
        {
            GameManager.instance.ApplySave(data);
        }

        if (TimeManager.Instance != null && data.time != null)
        {
            TimeManager.Instance.ApplyDTO(data.time);
        }

        bool finalized = false;
        UnityAction<Scene, LoadSceneMode> handler = null;
        handler = (scene, mode) =>
        {
            if (scene.name != data.sceneName)
            {
                return;
            }

            SceneManager.sceneLoaded -= handler;
            ApplyPerSceneState(data);
            finalized = true;
        };
        SceneManager.sceneLoaded += handler;

        string targetScene = string.IsNullOrEmpty(data.sceneName)
            ? SceneManager.GetActiveScene().name
            : data.sceneName;

        if (SceneController.Instance != null)
        {
            SceneController.Instance.LoadScene(targetScene, null);
        }
        else
        {
            AsyncOperation op = SceneManager.LoadSceneAsync(targetScene);
            while (op != null && !op.isDone)
            {
                yield return null;
            }
        }

        float timeout = 10f;
        float elapsed = 0f;
        while (!finalized && elapsed < timeout)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!finalized)
        {
            SceneManager.sceneLoaded -= handler;
            Debug.LogError("[SaveManager] Scene load timed out; state may be inconsistent.");
        }

        isLoading = false;
        OnAfterLoad?.Invoke();
        onComplete?.Invoke(finalized);
    }

    private void ApplyPerSceneState(SaveDataV1 data)
    {
        GameObject playerGo = GameManager.instance != null ? GameManager.instance.player : null;
        if (playerGo == null)
        {
            playerGo = GameObject.FindGameObjectWithTag("Player");
        }

        if (playerGo != null && data.player != null)
        {
            playerGo.transform.position = new Vector3(data.player.px, data.player.py, data.player.pz);
            playerGo.transform.rotation = new Quaternion(data.player.rx, data.player.ry, data.player.rz, data.player.rw);

            PlayerHealth health = playerGo.GetComponent<PlayerHealth>();
            if (health != null && data.player.maxHealth > 0)
            {
                health.SetHealthDirect(data.player.currentHealth, data.player.maxHealth);
            }
        }

        if (TransactionManager.Instance != null)
        {
            var list = new List<TransactionRecord>();
            if (data.todayTransactions != null)
            {
                foreach (TransactionDTO dto in data.todayTransactions)
                {
                    TransactionRecord record = new TransactionRecord(
                        dto.itemName,
                        (ItemCategory)dto.category,
                        dto.quantity,
                        dto.totalPrice,
                        (TransactionType)dto.type,
                        (StoreType)dto.store,
                        dto.npcName);
                    record.gameTime = TimeManager.DateTimeFromDTO(dto.gameTime);
                    list.Add(record);
                }
            }

            TransactionManager.Instance.LoadRecords(list);
            TransactionManager.Instance.ApplyDailyJournalState(
                data.activeJournalDay,
                data.journalCompletedForActiveDay,
                data.hasDailyJournalReport,
                AuditReportFromDTO(data.dailyJournalReport));
        }

        if (JournalManager.Instance != null)
        {
            var entries = new List<EntryData>();
            if (data.confirmedJournalEntries != null)
            {
                foreach (JournalEntryDTO dto in data.confirmedJournalEntries)
                {
                    EntryData entry = new EntryData(
                        dto.side,
                        dto.itemName,
                        (ItemCategory)dto.category,
                        (TransactionType)dto.type,
                        (StoreType)dto.store,
                        dto.quantity,
                        dto.amount,
                        TimeManager.DateTimeFromDTO(dto.time),
                        dto.isBalancingEntry,
                        dto.separatorAfter);

                    entry.entryId = dto.entryId;
                    entry.EnsureEntryId();
                    entries.Add(entry);
                }
            }

            JournalManager.Instance.LoadConfirmedEntries(entries);
            TransactionManager.Instance?.SyncJournalManagerState();
        }

        if (ARQuestManager.Instance != null)
        {
            ARQuestManager.Instance.RestoreState(data.pendingQuests);
            ARQuestManager.Instance.RestoreCooldowns(data.npcCooldowns);
        }

        RestaurantServiceManager.UtilityBillingSaveState utilityState = new RestaurantServiceManager.UtilityBillingSaveState();
        if (data.restaurantUtility != null)
        {
            utilityState.trackedWeekIndex = data.restaurantUtility.trackedWeekIndex;
            utilityState.lastBilledWeekIndex = data.restaurantUtility.lastBilledWeekIndex;
            utilityState.weeklyOpenMinutes = data.restaurantUtility.weeklyOpenMinutes;
            utilityState.weeklyDishesSold = data.restaurantUtility.weeklyDishesSold;
            utilityState.pendingBillWeekIndex = data.restaurantUtility.pendingBillWeekIndex;
            utilityState.pendingBillOpenMinutes = data.restaurantUtility.pendingBillOpenMinutes;
            utilityState.pendingBillDishesSold = data.restaurantUtility.pendingBillDishesSold;
            utilityState.hasOpenSession = data.restaurantUtility.hasOpenSession;
            utilityState.openSessionStartAbsoluteMinutes = data.restaurantUtility.openSessionStartAbsoluteMinutes;

            if (data.restaurantUtility.bills != null)
            {
                foreach (UtilityBillDTO dto in data.restaurantUtility.bills)
                {
                    if (dto == null)
                    {
                        continue;
                    }

                    utilityState.issuedBills.Add(new RestaurantServiceManager.UtilityBillStatement
                    {
                        billId = dto.billId,
                        weekIndex = dto.weekIndex,
                        openMinutes = dto.openMinutes,
                        dishesSold = dto.dishesSold,
                        electricityAmount = dto.electricityAmount,
                        waterAmount = dto.waterAmount,
                        totalAmount = dto.totalAmount,
                        isPaid = dto.isPaid,
                        isAccrued = dto.isAccrued,
                        outstandingAmount = dto.outstandingAmount,
                        hasPaidAt = dto.hasPaidAt,
                        issuedAt = TimeManager.DateTimeFromDTO(dto.issuedAt),
                        paidAt = dto.hasPaidAt ? TimeManager.DateTimeFromDTO(dto.paidAt) : default(TimeManager.DateTime),
                        electricityQuantity = dto.electricityQuantity,
                        waterQuantity = dto.waterQuantity,
                        electricityOutstandingAmount = dto.electricityOutstandingAmount,
                        waterOutstandingAmount = dto.waterOutstandingAmount,
                        electricityIsAccrued = dto.electricityIsAccrued,
                        waterIsAccrued = dto.waterIsAccrued,
                        electricityPaid = dto.electricityPaid,
                        waterPaid = dto.waterPaid,
                        hasElectricityPaidAt = dto.hasElectricityPaidAt,
                        hasWaterPaidAt = dto.hasWaterPaidAt,
                        electricityPaidAt = dto.hasElectricityPaidAt ? TimeManager.DateTimeFromDTO(dto.electricityPaidAt) : default(TimeManager.DateTime),
                        waterPaidAt = dto.hasWaterPaidAt ? TimeManager.DateTimeFromDTO(dto.waterPaidAt) : default(TimeManager.DateTime),
                        mailboxLetterRead = dto.mailboxLetterRead,
                        journalEntryPosted = dto.journalEntryPosted,
                        hasJournalEntryPostedAt = dto.hasJournalEntryPostedAt,
                        journalEntryPostedAt = dto.hasJournalEntryPostedAt ? TimeManager.DateTimeFromDTO(dto.journalEntryPostedAt) : default(TimeManager.DateTime),
                    });
                }
            }
        }

        RestaurantUtilityBillingCache.ApplyState(utilityState);
        MailboxInteractable.Instance?.RebuildPendingMail();
        RestaurantCounterStateCache.RestoreState(data.restaurantCounters);

        if (APQuestManager.Instance != null)
        {
            APQuestManager.Instance.RestoreState(data.pendingAPQuests);
        }

        if (CreditSystem.Instance != null)
        {
            CreditSystem.Instance.RestoreState(data.vendorSalesRecords);
        }

        TutorialManager tutorialManager = TutorialManager.Instance != null
            ? TutorialManager.Instance
            : FindObjectOfType<TutorialManager>(true);

        if (tutorialManager != null)
        {
            var tutorialState = new TutorialStateSnapshot
            {
                version = data.tutorialStateVersion,
                hasState = data.hasTutorialState,
                stepIndex = data.tutorialStepIndex,
                completed = data.tutorialCompleted,
                bonusQuestActive = data.tutorialBonusQuestActive,
                bonusRecallTriggered = data.tutorialBonusRecallTriggered,
                creditOverrideApplied = data.tutorialCreditOverrideApplied,
                flags = data.tutorialFlags != null ? new List<string>(data.tutorialFlags) : new List<string>(),
            };

            tutorialManager.RestoreState(tutorialState);
        }
    }

    private static AuditReportDTO AuditReportToDTO(AuditReport report)
    {
        return new AuditReportDTO
        {
            isPassed = report.isPassed,
            score = report.score,
            maxScore = report.maxScore,
            structureScore = report.structureScore,
            structureMaxScore = report.structureMaxScore,
            orderScore = report.orderScore,
            orderMaxScore = report.orderMaxScore,
            sideScore = report.sideScore,
            sideMaxScore = report.sideMaxScore,
            transactionScore = report.transactionScore,
            transactionMaxScore = report.transactionMaxScore,
            attemptNumber = report.attemptNumber,
            retryCount = report.retryCount,
            mistakeCount = report.mistakeCount,
            earnedStructurePoint = report.earnedStructurePoint,
            earnedTransactionPoint = report.earnedTransactionPoint,
            mistakeDetails = report.mistakeDetails != null ? new List<string>(report.mistakeDetails) : new List<string>(),
            lineMistakeDetails = report.lineMistakeDetails != null ? new List<string>(report.lineMistakeDetails) : new List<string>(),
            lineHints = AuditLineHintsToDTO(report.lineHints),
        };
    }

    private static AuditReport AuditReportFromDTO(AuditReportDTO dto)
    {
        if (dto == null)
        {
            return default;
        }

        return new AuditReport
        {
            isPassed = dto.isPassed,
            score = dto.score,
            maxScore = dto.maxScore,
            structureScore = dto.structureScore,
            structureMaxScore = dto.structureMaxScore,
            orderScore = dto.orderScore,
            orderMaxScore = dto.orderMaxScore,
            sideScore = dto.sideScore,
            sideMaxScore = dto.sideMaxScore,
            transactionScore = dto.transactionScore,
            transactionMaxScore = dto.transactionMaxScore,
            attemptNumber = dto.attemptNumber,
            retryCount = dto.retryCount,
            mistakeCount = dto.mistakeCount,
            earnedStructurePoint = dto.earnedStructurePoint,
            earnedTransactionPoint = dto.earnedTransactionPoint,
            mistakeDetails = dto.mistakeDetails != null ? new List<string>(dto.mistakeDetails) : new List<string>(),
            lineMistakeDetails = dto.lineMistakeDetails != null ? new List<string>(dto.lineMistakeDetails) : new List<string>(),
            lineHints = AuditLineHintsFromDTO(dto.lineHints),
        };
    }

    private static List<AuditLineHintDTO> AuditLineHintsToDTO(List<AuditLineHint> hints)
    {
        List<AuditLineHintDTO> dtoHints = new List<AuditLineHintDTO>();
        if (hints == null)
        {
            return dtoHints;
        }

        foreach (AuditLineHint hint in hints)
        {
            dtoHints.Add(new AuditLineHintDTO
            {
                entryId = hint.entryId,
                rowNumber = hint.rowNumber,
                journalLineNumber = hint.journalLineNumber,
                message = hint.message,
            });
        }

        return dtoHints;
    }

    private static List<AuditLineHint> AuditLineHintsFromDTO(List<AuditLineHintDTO> dtoHints)
    {
        List<AuditLineHint> hints = new List<AuditLineHint>();
        if (dtoHints == null)
        {
            return hints;
        }

        foreach (AuditLineHintDTO dto in dtoHints)
        {
            if (dto == null)
            {
                continue;
            }

            hints.Add(new AuditLineHint
            {
                entryId = dto.entryId,
                rowNumber = dto.rowNumber,
                journalLineNumber = dto.journalLineNumber,
                message = dto.message,
            });
        }

        return hints;
    }

    // -------------------- IO Helpers --------------------

    public static string GetSlotPath(int slot)
    {
        return Path.Combine(Application.persistentDataPath, SAVE_DIR, $"{FILE_PREFIX}{slot}{FILE_EXT}");
    }

    private SaveDataV1 ReadSlot(int slot)
    {
        string path = GetSlotPath(slot);
        if (!File.Exists(path))
        {
            Debug.LogWarning($"[SaveManager] Slot {slot} is empty.");
            return null;
        }

        try
        {
            string json = File.ReadAllText(path);
            SaveDataV1 data = JsonUtility.FromJson<SaveDataV1>(json);
            if (data == null)
            {
                throw new InvalidDataException("Parsed save is null.");
            }

            return data;
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveManager] Cannot read slot {slot}: {e.Message}");
            return null;
        }
    }

    private void EnsureSaveDirectory()
    {
        string dir = Path.Combine(Application.persistentDataPath, SAVE_DIR);
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
    }

    private void WriteAtomic(string path, string contents)
    {
        string dir = Path.GetDirectoryName(path);
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        string tmp = path + ".tmp";
        File.WriteAllText(tmp, contents);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        File.Move(tmp, path);
    }

    // -------------------- Lifecycle Hooks --------------------

    private void OnApplicationQuit()
    {
        if (!ShouldAutoSaveOnQuit())
        {
            return;
        }

        AutoSave();
    }

    private void OnApplicationPause(bool pause)
    {
        if (!pause || !ShouldAutoSaveOnQuit())
        {
            return;
        }

        AutoSave();
    }

    private bool ShouldAutoSaveOnQuit()
    {
        return GameManager.instance != null && GameManager.instance.player != null;
    }
}
