using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// Manages an active cooking session.  After the player selects a recipe and ingredients
/// the session consumes the items, starts a timer and tracks the number of oven visits
/// (quick‑time events) that must be performed before the dish is finished.
/// </summary>
public class CookingSession : MonoBehaviour
{
  public static event Action<Item> OnCookingCompleted;

  [Header("References")]
  public PointerController pointer;            // quick‑time pointer UI
  public string timerText;                  // optional timer display
  public string visitsText;                 // show progress (e.g. 1/3 visits)

  // internal state
  private ItemSO recipe;
  private List<(int slotIndex, int amount)> ingredientSelections;
  private float cookTimer;
  private int requiredVisits;
  private int visitsDone;
  private float totalPrecisionScore = 0f; // score on each qte
  private float avgIngredientQuality;
  public bool isActive;
  private Coroutine timerCoroutine;
  private bool timerPaused;

  // Global lock: if any oven is currently in QTE, all visit windows should pause.
  private static int activeQuickTimeCount = 0;
  private bool ownsQuickTimeLock = false;
  private static bool IsAnyQuickTimeRunning => activeQuickTimeCount > 0;
  private Coroutine quickTimeUnlockCoroutine;

  [Header("Overall Progress")]
  public GameObject durationIndicator;
  private Image durationIndicatorRadial;

  [Header("Visit Handling")] // new fields for visit windows
  public GameObject visitIndicator;           // UI element that pops above oven
  private Image visitIndicatorRadial;
  public float visitWindowDuration = 5f;      // how long the player has to click the oven

  [Header("Result Icons")]
  public GameObject finishedIndicator;
  public GameObject failedIndicator;
  private Item pendingCookedItem;
  public bool isWaitingForPickup = false;
  private bool hasFailed = false;

  private bool indicatorActive = false;
  private float visitInterval;
  private float nextVisitThreshold;

  private void OnDisable()
  {
    if (quickTimeUnlockCoroutine != null)
    {
      StopCoroutine(quickTimeUnlockCoroutine);
      quickTimeUnlockCoroutine = null;
    }

    ReleaseQuickTimeLockIfOwned();
  }

  private void Awake()
  {
    RefreshReferences();
  }

  private void RefreshReferences()
  {
    visitIndicatorRadial = visitIndicator.transform.Find("Border/VisitIndicatorRadial").GetComponent<Image>();
    durationIndicatorRadial = durationIndicator.transform.Find("DurationIndicatorRadial").GetComponent<Image>();
  }

  public void BeginCooking(ItemSO recipe, List<(int slotIndex, int amount)> selections, float averageQuality)
  {
    if (recipe == null || selections == null) return;
    if (isActive)
    {
      Debug.LogWarning("CookingSession already active");
      return;
    }

    durationIndicator.SetActive(true);
    var myInteractable = gameObject.GetComponent<InteractableObject>();
    if (myInteractable != null)
    {
      myInteractable.HideUI();
    }

    this.recipe = recipe;
    ingredientSelections = selections;
    avgIngredientQuality = averageQuality;

    // remove ingredients from inventory immediately so they can't be reused
    var inv = GameManager.instance.player.GetComponent<Player>().GetInventoryController();
    inv.ConsumeIngredients(selections);

    cookTimer = recipe.cookTime;

    if (visitIndicator == null)
    {
      Debug.LogWarning("CookingSession has no visitIndicator assigned – the oven prompt will be invisible.");
    }

    // determine visits required; quality may bump it
    requiredVisits = recipe.baseOvenVisits;
    if (recipe.qualityAffectsVisits)
    {
      requiredVisits += Mathf.FloorToInt(avgIngredientQuality - 1f); // +1 visit per full quality point above common
    }
    requiredVisits = Mathf.Max(1, requiredVisits);

    // compute visit timing: divide cooking period into (requiredVisits+1) slices
    visitInterval = cookTimer / (requiredVisits + 1);
    nextVisitThreshold = cookTimer - visitInterval; // first time to pop an indicator

    visitsDone = 0;
    isActive = true;

    // start countdown
    timerCoroutine = StartCoroutine(CookingTimer());
    timerPaused = false;

    // kick off the visit watcher if we need any visits
    if (requiredVisits > 0)
    {
      StartCoroutine(VisitWatcher());
    }

    UpdateUI();

    Debug.Log($"Started cooking {recipe.displayName} ({cookTimer}s, {requiredVisits} visits)");
  }

  private IEnumerator CookingTimer()
  {
    while (cookTimer > 0f)
    {
      cookTimer -= Time.deltaTime;
      UpdateUI();
      yield return null;
    }

    EndSession();
  }

  /// <summary>
  /// Observes the cooking timer and triggers visit windows when the remaining
  /// cook time drops below successive thresholds.  Runs in parallel with the
  /// main countdown but stops the timer while the window is active.
  /// </summary>
  private IEnumerator VisitWatcher()
  {
    while (isActive && visitsDone < requiredVisits && cookTimer > 0f)
    {
      if (cookTimer <= nextVisitThreshold)
      {
        yield return StartCoroutine(VisitWindow());
        // schedule next threshold: subtract another interval
        nextVisitThreshold -= visitInterval;
      }
      yield return null;
    }
  }

  /// <summary>
  /// Handles a single visit window: pause the cook timer, show the indicator
  /// and give the player a limited unscaled window in which to click the oven.
  /// Failing to click causes the entire session to fail.
  /// </summary>
  private IEnumerator VisitWindow()
  {
    // pause main timer
    if (timerCoroutine != null)
    {
      StopCoroutine(timerCoroutine);
      timerCoroutine = null;
      timerPaused = true;
      durationIndicator.SetActive(false);
    }

    indicatorActive = true;
    if (visitIndicator != null)
      visitIndicator.SetActive(true);

    float t = 0f;
    while (t < visitWindowDuration && indicatorActive && isActive)
    {
      // While another oven is running QTE, freeze this window timer.
      if (!IsAnyQuickTimeRunning)
      {
        t += Time.unscaledDeltaTime; // unscaled, but explicitly paused by global QTE lock
        if (visitIndicatorRadial != null)
        {
          float pct = Mathf.Clamp01(t / visitWindowDuration);
          visitIndicatorRadial.color = Color.Lerp(Color.green, Color.red, pct);
          visitIndicatorRadial.fillAmount = 1f - pct; // reverse so full at start
        }
      }

      yield return null;
    }

    if (indicatorActive && isActive)
    {
      // time ran out without a click
      FailCooking("missed oven visit window");
    }
    else
    {
      // player clicked; wait for quick time result to resume timer
      while (timerPaused && isActive)
        yield return null;
    }
  }

  private void FailCooking(string reason)
  {
    ScheduleQuickTimeLockRelease();
    hasFailed = true;
    isActive = false;
    indicatorActive = false;

    if (timerCoroutine != null) StopCoroutine(timerCoroutine);

    pendingCookedItem = new Item(recipe, 1, ItemQuality.Trash);
    isWaitingForPickup = true;

    if (failedIndicator != null) failedIndicator.SetActive(true);
    if (finishedIndicator != null) finishedIndicator.SetActive(false);
    if (visitIndicator != null) visitIndicator.SetActive(false);
    if (durationIndicator != null) durationIndicator.SetActive(false);

    Time.timeScale = 1f;
    if (UI_StateManager.Instance != null)
    {
      UI_StateManager.Instance.interactWindowOpen = false;
    }

    timerText = "";
    timerText = "";

    Debug.Log($"Cooking Failed: {reason} - Oven is now waiting for clear/trash pickup.");
  }

  private void UpdateUI()
  {
    timerText = $"Time: {cookTimer:F1}s";
    visitsText = $"Oven visits: {visitsDone}/{requiredVisits}";

    if (durationIndicatorRadial != null && recipe != null)
    {
      float progressPct = 1f - Mathf.Clamp01(cookTimer / recipe.cookTime);

      durationIndicatorRadial.fillAmount = progressPct;
    }
  }

  /// <summary>
  /// Called by the oven when the player interacts while a session is active.
  /// </summary>
  public void OnOvenClicked()
  {
    if (!isActive) return;

    // Ignore clicks until an indicator window is active
    if (!indicatorActive)
    {
      Debug.Log("Can't interact with oven yet - wait for the indicator");
      return;
    }

    // we've clicked inside a pop‑up window – satisfy it and start the QTE
    indicatorActive = false;
    if (visitIndicator != null)
      visitIndicator.SetActive(false);

    // only start quick time if there are remaining visits
    if (visitsDone < requiredVisits)
    {
      // pause timer while quick time runs
      if (timerCoroutine != null)
      {
        StopCoroutine(timerCoroutine);
        timerCoroutine = null;
        timerPaused = true;
      }

      // if there is no pointer UI we cannot do a quick time event; just count the visit
      if (pointer == null)
      {
        Debug.LogWarning("PointerController reference not set on CookingSession");
        OnQuickTimeResult(0);
        return;
      }

      Debug.Log("Quick time event Start!");
      AcquireQuickTimeLock();

      // adjust difficulty by shrinking zones based on average ingredient quality
      float difficultyFactor = Mathf.Clamp01(avgIngredientQuality - 1f) * 0.25f; // 0 = easy, higher = smaller zones
      pointer.ConfigureZonesForDifficulty(difficultyFactor);
      pointer.RandomizeZones();              // move zones to a random location
      pointer.BeginQuickTime(OnQuickTimeResult);
    }
  }

  private void OnQuickTimeResult(int zone)
  {
    ScheduleQuickTimeLockRelease();

    if (indicatorActive)
    {
      indicatorActive = false;
      if (visitIndicator != null) visitIndicator.SetActive(false);
    }

    // แปลง Zone ที่กดได้เป็นคะแนนความแม่นยำ (0.0 - 1.0)
    var precision = zone switch
    {
      1 => 1.0f,
      2 => 0.8f,
      3 => 0.5f,
      _ => 0.0f,
    };
    totalPrecisionScore += precision;
    visitsDone++;
    UpdateUI();
    Debug.Log($"Quick time result: Zone {zone}, Precision: {precision:F1}, Visits {visitsDone}/{requiredVisits}");

    if (visitsDone >= requiredVisits && cookTimer <= 0f)
    {
      EndSession();
    }

    // resume timer if it was paused and still time remains
    if (timerPaused && cookTimer > 0f)
    {
      timerCoroutine = StartCoroutine(CookingTimer());
      timerPaused = false;
    }
  }

  private void EndSession()
  {
    ScheduleQuickTimeLockRelease();
    if (hasFailed) return;
    if (timerCoroutine != null) StopCoroutine(timerCoroutine);
    isActive = false;

    float weightedSuccessRate = (requiredVisits > 0) ? totalPrecisionScore / requiredVisits : 0;
    float baseQuality = avgIngredientQuality * 20f;
    float finalScore = baseQuality * weightedSuccessRate;

    ItemQuality finalQuality = MapScoreToQuality(finalScore);

    Debug.Log($"Final Score: {finalScore} (Base: {baseQuality} * Precision: {weightedSuccessRate:F1}) FinalQuality: {finalQuality}");

    pendingCookedItem = new Item(recipe, 1, finalQuality);
    isWaitingForPickup = true;
    OnCookingCompleted?.Invoke(pendingCookedItem);

    if (finishedIndicator != null) finishedIndicator.SetActive(true);

    // Reset state and UI
    timerText = "";
    visitsText = "";
    totalPrecisionScore = 0f;
    indicatorActive = false;
    if (visitIndicator != null) visitIndicator.SetActive(false);
    if (durationIndicator != null) durationIndicator.SetActive(false);
    Time.timeScale = 1f;

    if (UI_StateManager.Instance != null)
    {
      UI_StateManager.Instance.interactWindowOpen = false;
    }
  }

  public void CollectItem()
  {
    if (pendingCookedItem != null)
    {
      if (pendingCookedItem.quality != ItemQuality.Trash)
      {
        var inventory = GameManager.instance.player.GetComponent<Player>().GetInventoryController();
        int pickupAmount = pendingCookedItem.amount;
        bool wasAdded = inventory.AddItem(pendingCookedItem);

        if (!wasAdded)
        {
          Debug.LogWarning("Could not collect cooked item because the inventory is full.");
          return;
        }

        pendingCookedItem.PickUp(pickupAmount);
      }
      else
      {
        Debug.Log("Clear failed dish. No item added to inventory.");
      }
    }

    isWaitingForPickup = false;
    pendingCookedItem = null;
    hasFailed = false;

    if (finishedIndicator != null) finishedIndicator.SetActive(false);
    if (failedIndicator != null) failedIndicator.SetActive(false);
  }

  private void AcquireQuickTimeLock()
  {
    if (ownsQuickTimeLock)
    {
      return;
    }

    ownsQuickTimeLock = true;
    activeQuickTimeCount += 1;
  }

  private void ScheduleQuickTimeLockRelease()
  {
    if (!ownsQuickTimeLock)
    {
      return;
    }

    if (quickTimeUnlockCoroutine != null)
    {
      return;
    }

    float delay = 1f;
    if (pointer != null)
    {
      delay = Mathf.Max(0f, pointer.QuickTimeCloseDelay);
    }

    quickTimeUnlockCoroutine = StartCoroutine(ReleaseQuickTimeLockAfterDelay(delay));
  }

  private IEnumerator ReleaseQuickTimeLockAfterDelay(float delay)
  {
    if (delay > 0f)
    {
      yield return new WaitForSecondsRealtime(delay);
    }

    quickTimeUnlockCoroutine = null;
    ReleaseQuickTimeLockIfOwned();
  }

  private void ReleaseQuickTimeLockIfOwned()
  {
    if (!ownsQuickTimeLock)
    {
      return;
    }

    ownsQuickTimeLock = false;
    activeQuickTimeCount = Mathf.Max(0, activeQuickTimeCount - 1);
  }

  // ยังไม่ได้ใส่ quality Trash ในอนาคตจะเพิ่ม Trash ก็ได้ถ้าอยากให้คะแนนต่ำเกิน ทำพลาดก็จะเป็น Trash แทน Common
  private ItemQuality MapScoreToQuality(float score)
  {
    if (score >= 85) return ItemQuality.Mythical;  // 85-100
    if (score >= 61) return ItemQuality.Legendary; // 61-84
    if (score >= 41) return ItemQuality.Epic;      // 41-60
    if (score >= 21) return ItemQuality.Rare;      // 21-40
    return ItemQuality.Common;                     // 0-20
  }
}

