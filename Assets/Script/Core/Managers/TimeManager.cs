using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

public class TimeManager : MonoBehaviour
{
    [Header("Date & Time Settings")]
    [Range(1, 28)]
    public int dateInMonth;
    [Range(1, 4)]
    public int season;
    [Range(1, 99)]
    public int year;
    [Range(0, 24)]
    public int hour;
    [Range(0, 59)]
    public int minutes;

    public DateTime dateTime;

    [Header("Tick Settings")]
    public int TickMinutesIncrease = 10;
    public float TimeBetweenTicks = 1;
    private float currentTimeBetweenTicks = 0;

    public float sleepTimer = 0;
    public DateTime previousDateTime;
    public int newDayHour = 6;
    public int wakeFromFaintHour = 13;

    [Header("Faint Recovery")]
    [Tooltip("Scene loaded after the player faints. Leave empty to stay in the current scene.")]
    [SerializeField] private string faintRecoverySceneName;
    [Tooltip("SpawnPoint.spawnId used after faint recovery. Place this SpawnPoint by the bed.")]
    [SerializeField] private string faintRecoverySpawnId;
    [Tooltip("Extra real-time delay while the screen is fully faded out after fainting.")]
    [SerializeField] private float faintRecoveryHoldSeconds = 0.75f;

    public int CurrentHour => dateTime.Hour;
    public int CurrentMinutes => dateTime.Minutes;

    private PlayerHealth player;

    public static UnityAction<DateTime> OnDateTimeChanged;
    public static Action OnTimeTicked;
    private static TimeManager _instance;
    public static TimeManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<TimeManager>();
            }
            return _instance;
        }
    }

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
            return;
        }

        dateTime = new DateTime(dateInMonth, season - 1, year, hour, minutes);
    }

    private void Start()
    {
        OnDateTimeChanged?.Invoke(dateTime);
        previousDateTime = dateTime;

        RefreshPlayerReference();
    }

    private void RefreshPlayerReference()
    {
        if (GameManager.instance != null && GameManager.instance.player != null)
        {
            player = GameManager.instance.player.GetComponent<PlayerHealth>();
        }
    }

    private void Update()
    {
        // Safety: try to refresh player if lost (scene change artifact)
        if (player == null) RefreshPlayerReference();

        currentTimeBetweenTicks += Time.deltaTime;

        if (currentTimeBetweenTicks >= TimeBetweenTicks)
        {
            currentTimeBetweenTicks = 0;
            Tick();
        }
    }

    void Tick()
    {
        if (faint) return;

        dateTime.AdvanceMinutes(TickMinutesIncrease);
        AddSleepTimeFromDifference(dateTime);

        if (sleepTimer >= (24 * 60))
        {
            Faint();
            return;
        }

        NotifyTimeChanged();
    }

    void AdvanceTime()
    {
        dateTime.AdvanceMinutes(TickMinutesIncrease);

        NotifyTimeChanged();
    }

    public static void AdvanceToNextDay(int targetHour = 6, int targetMinute = 0)
    {
        if (_instance == null)
        {
            Debug.LogError("TimeManager instance not found. Cannot advance time.");
            return;
        }

        _instance.currentTimeBetweenTicks = 0;

        int date = _instance.dateTime.Date;
        int season = (int)_instance.dateTime.Season;
        int year = _instance.dateTime.Year;

        int nextDate = date + 1;
        int nextSeason = season;
        int nextYear = year;

        if (nextDate > 28)
        {
            nextDate = 1;
            nextSeason += 1;

            if (nextSeason > (int)Season.Winter)
            {
                nextSeason = (int)Season.Spring;
                nextYear += 1;
            }
        }

        DateTime newTime = new DateTime(
            nextDate,
            nextSeason,
            nextYear,
            targetHour,
            targetMinute
        );

        _instance.AddSleepTimeFromDifference(newTime);
        _instance.sleepTimer = 0;

        _instance.dateTime = newTime;

        OnDateTimeChanged?.Invoke(_instance.dateTime);
    }

    public bool faint = false;

    public static void SetTime(int hour, int minutes)
    {
        if (_instance == null)
        {
            Debug.LogError("TimeManager instance not found. Cannot set time.");
            return;
        }

        DateTime newTime = new DateTime(
            _instance.dateTime.Date,
            (int)_instance.dateTime.Season,
            _instance.dateTime.Year,
            hour,
            minutes
        );

        if (_instance.faint == true)
        {
            _instance.sleepTimer = 0;
            _instance.faint = false;
        }

        _instance.AddSleepTimeFromDifference(newTime);
        _instance.dateTime = newTime;
        OnDateTimeChanged?.Invoke(_instance.dateTime);
    }

    public static bool TryGetCurrentDateTime(out DateTime currentDateTime)
    {
        TimeManager manager = Instance;
        if (manager == null)
        {
            currentDateTime = default(DateTime);
            return false;
        }

        currentDateTime = manager.dateTime;
        return true;
    }

    public static int GetWeekIndex(DateTime currentDateTime)
    {
        return Mathf.Max(1, currentDateTime.TotalNumWeeks);
    }

    public static int GetMinuteOfDay(DateTime currentDateTime)
    {
        return (Mathf.Clamp(currentDateTime.Hour, 0, 23) * 60) + Mathf.Clamp(currentDateTime.Minutes, 0, 59);
    }

    public static int GetAbsoluteMinutes(DateTime currentDateTime)
    {
        return (currentDateTime.TotalNumDays * 24 * 60) + (currentDateTime.Hour * 60) + currentDateTime.Minutes;
    }

    private void AddSleepTimeFromDifference(DateTime newTime)
    {
        // convert to total minutes
        int oldTotal = previousDateTime.TotalNumDays * 24 * 60
                    + previousDateTime.Hour * 60
                    + previousDateTime.Minutes;

        int newTotal = newTime.TotalNumDays * 24 * 60
                    + newTime.Hour * 60
                    + newTime.Minutes;

        int diff = newTotal - oldTotal;

        if (diff > 0)
            sleepTimer += diff;

        previousDateTime = newTime;
    }

    // -------------------- Save/Load Support --------------------
    public TimeDTO ToDTO()
    {
        return new TimeDTO
        {
            dateTime = DateTimeToDTO(dateTime),
            sleepTimer = sleepTimer,
            faint = faint,
        };
    }

    public void ApplyDTO(TimeDTO dto)
    {
        if (dto == null || dto.dateTime == null) return;
        dateTime = DateTimeFromDTO(dto.dateTime);
        previousDateTime = dateTime;
        sleepTimer = dto.sleepTimer;
        faint = dto.faint;
        OnDateTimeChanged?.Invoke(dateTime);
    }

    public static GameDateTimeDTO DateTimeToDTO(DateTime dt)
    {
        return new GameDateTimeDTO
        {
            date = dt.Date,
            season = (int)dt.Season,
            year = dt.Year,
            hour = dt.Hour,
            minutes = dt.Minutes,
        };
    }

    public static DateTime DateTimeFromDTO(GameDateTimeDTO dto)
    {
        if (dto == null) return new DateTime(1, 0, 1, 6, 0);
        return new DateTime(dto.date, dto.season, dto.year, dto.hour, dto.minutes);
    }

    public IEnumerator SleepRoutine(int targetHour)
    {
        yield return ScreenFader.Instance.FadeOutIn(() =>
        {
            previousDateTime = dateTime;

            SetTime(targetHour, 0);
            player?.FullRestore();
        });
    }

    public void Faint()
    {
        if (faint) return;
        faint = true;

        dateTime = new DateTime(dateTime.Date, (int)dateTime.Season, dateTime.Year, newDayHour, 0);
        previousDateTime = dateTime;
        sleepTimer = 0;
        NotifyTimeChanged();

        StartCoroutine(FaintRecoveryRoutine());
    }

    private IEnumerator FaintRecoveryRoutine()
    {
        float originalTimeScale = Time.timeScale;
        Time.timeScale = 0f;

        if (ScreenFader.Instance != null)
        {
            yield return ScreenFader.Instance.Fade(0f, 1f);
        }

        float holdSeconds = Mathf.Max(0f, faintRecoveryHoldSeconds);
        if (holdSeconds > 0f)
        {
            yield return new WaitForSecondsRealtime(holdSeconds);
        }

        yield return LoadFaintRecoverySceneIfNeeded();
        yield return null;

        SetFaintWakeTime();
        MovePlayerToFaintRecoverySpawn();
        player?.FullRestore();
        DailyJournalRules.RefreshNewDaySystemsAfterWake();

        if (ScreenFader.Instance != null)
        {
            yield return ScreenFader.Instance.Fade(1f, 0f);
        }

        faint = false;
        Time.timeScale = originalTimeScale;
    }

    private IEnumerator LoadFaintRecoverySceneIfNeeded()
    {
        if (string.IsNullOrWhiteSpace(faintRecoverySceneName))
        {
            yield break;
        }

        Scene currentScene = SceneManager.GetActiveScene();
        if (currentScene.name == faintRecoverySceneName)
        {
            yield break;
        }

        AsyncOperation operation = SceneManager.LoadSceneAsync(faintRecoverySceneName);
        if (operation == null)
        {
            Debug.LogWarning($"[TimeManager] Could not load faint recovery scene '{faintRecoverySceneName}'.");
            yield break;
        }

        while (!operation.isDone)
        {
            yield return null;
        }
    }

    private void SetFaintWakeTime()
    {
        int targetHour = Mathf.Clamp(wakeFromFaintHour, 0, 23);

        DateTime wakeTime = new DateTime(dateTime.Date, (int)dateTime.Season, dateTime.Year, targetHour, 0);
        sleepTimer = 0;
        AddSleepTimeFromDifference(wakeTime);
        dateTime = wakeTime;

        NotifyTimeChanged();
    }

    private void NotifyTimeChanged()
    {
        OnDateTimeChanged?.Invoke(dateTime);
        OnTimeTicked?.Invoke();
    }

    private void MovePlayerToFaintRecoverySpawn()
    {
        RefreshPlayerReference();

        GameObject playerObject = GameManager.instance != null ? GameManager.instance.player : null;
        if (playerObject == null && Player.Instance != null)
        {
            playerObject = Player.Instance.gameObject;
        }

        if (playerObject == null)
        {
            Debug.LogWarning("[TimeManager] Cannot move player after faint because the player was not found.");
            return;
        }

        if (!string.IsNullOrWhiteSpace(faintRecoverySpawnId))
        {
            SpawnPoint spawnPoint = SpawnPoint.Find(faintRecoverySpawnId);
            if (spawnPoint == null)
            {
                Debug.LogWarning($"[TimeManager] Faint recovery SpawnPoint '{faintRecoverySpawnId}' was not found in scene '{SceneManager.GetActiveScene().name}'.");
            }
            else
            {
                playerObject.transform.position = spawnPoint.GetSpawnPosition();
                playerObject.transform.rotation = spawnPoint.transform.rotation;
            }
        }

        BindCameraToPlayer(playerObject.transform);
        player = playerObject.GetComponent<PlayerHealth>();
    }

    private static void BindCameraToPlayer(Transform playerTransform)
    {
        if (playerTransform == null)
        {
            return;
        }

        var vcam = FindObjectOfType<Cinemachine.CinemachineVirtualCamera>();
        if (vcam != null)
        {
            vcam.Follow = playerTransform;
            vcam.LookAt = playerTransform;
        }
    }

    [System.Serializable]
    public struct DateTime
    {
        #region Fields

        private Days day;
        private int date;
        private int year;

        private int hour;
        private int minutes;

        private Season season;

        private int totalNumDays;
        private int totalNumWeeks;
        #endregion

        #region Properties

        public Days Day => day;
        public int Date => date;
        public int Hour => hour;
        public int Minutes => minutes;
        public Season Season => season;
        public int Year => year;
        public int TotalNumDays => totalNumDays;
        public int TotalNumWeeks => totalNumWeeks;
        public int CurrentWeek => totalNumWeeks % 16 == 0 ? 16 : totalNumWeeks % 16;

        #endregion

        #region Constructors

        public DateTime(int date, int season, int year, int hour, int minutes)
        {
            this.day = (Days)(date % 7);
            if (day == 0) day = (Days)7;
            this.date = date;
            this.season = (Season)season;
            this.year = year;

            this.hour = hour;
            this.minutes = minutes;

            totalNumDays = date + (28 * (int)this.season) + (112 * (year - 1));

            // Keep constructor math consistent with AdvanceDay():
            // week changes only after finishing day 7, 14, 21, ...
            int zeroBasedDayIndex = Mathf.Max(0, totalNumDays - 1);
            totalNumWeeks = 1 + (zeroBasedDayIndex / 7);
        }

        #endregion

        #region Time Advancement

        public void AdvanceMinutes(int MinutesToAdvanceBy)
        {
            if (minutes + MinutesToAdvanceBy >= 60)
            {
                minutes = (minutes + MinutesToAdvanceBy) % 60;
                AdvanceHour();
            }
            else
            {
                minutes += MinutesToAdvanceBy;
            }
        }

        private void AdvanceHour()
        {
            if ((hour + 1) == 24)
            {
                hour = 0;
                AdvanceDay();
            }
            else
            {
                hour++;
            }
        }

        private void AdvanceDay()
        {
            day++;

            if (day > (Days)7)
            {
                day = (Days)1;
                totalNumWeeks++;
            }

            date++;

            if (date > 28)
            {
                AdvanceSeason();
                date = 1;
            }

            totalNumDays++;
        }

        private void AdvanceSeason()
        {
            if (Season == Season.Winter)
            {
                season = Season.Spring;
                AdvanceYear();
            }
            else season++;
        }

        private void AdvanceYear()
        {
            date = 1;
            year++;
        }

        #endregion

        #region Bool Checks

        public bool IsNight()
        {
            return hour > 18 || hour < 6;
        }

        public bool IsMorning()
        {
            return hour >= 6 && hour <= 12;
        }

        public bool IsAfternoon()
        {
            return hour > 12 && hour < 18;
        }

        public bool IsWeekend()
        {
            return day > Days.Fri ? true : false;
        }

        public bool IsParticularDay(Days _day)
        {
            return day == _day;
        }

        #endregion

        #region Key Dates

        public DateTime NewYearDay(int year)
        {
            if (year == 0) year = 1;
            return new DateTime(1, 0, year, 6, 0);
        }

        #endregion

        #region Start Of Season

        public DateTime StartOfSeason(int season, int year)
        {
            return new DateTime(1, season, year, 6, 0);
        }

        public DateTime StartOfSpring(int year)
        {
            return StartOfSeason(0, year);
        }

        public DateTime StartOfSummer(int year)
        {
            return StartOfSeason(1, year);
        }

        public DateTime StartOfAutumn(int year)
        {
            return StartOfSeason(2, year);
        }

        public DateTime StartOfWinter(int year)
        {
            return StartOfSeason(3, year);
        }

        #endregion

        #region To Strings

        public override string ToString()
        {
            return $"Date: {DateToString()} Season: {season} Time: {TimeToString()} " + $"\nTotal Days: {totalNumDays} | Total Weeks: {totalNumWeeks}";
        }

        public string DateToString()
        {
            return $"{Day} {Date} Y:{Year.ToString("D2")}";
        }

        public string TimeToString()
        {
            int adjustedHour = 0;

            if (hour == 0)
            {
                adjustedHour = 12;
            }
            else if (hour >= 13)
            {
                adjustedHour = hour - 12;
            }
            else
            {
                adjustedHour = hour;
            }

            string AmPm = hour < 12 ? "AM" : "PM";

            return $"{adjustedHour}:{minutes.ToString("D2")} {AmPm}";
        }

        #endregion
    }

    [System.Serializable]
    public enum Days
    {
        NULL = 0,
        Mon = 1,
        Tue = 2,
        Wed = 3,
        Thu = 4,
        Fri = 5,
        Sat = 6,
        Sun = 7,
    }

    [System.Serializable]
    public enum Season
    {
        Spring = 0,
        Summer = 1,
        Autumn = 2,
        Winter = 3,
    }
}
