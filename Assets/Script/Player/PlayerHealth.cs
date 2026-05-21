using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class PlayerHealth : MonoBehaviour
{
    /// <summary>fires whenever currentHealth or maxHealth changes — (currentHealth, maxHealth)</summary>
    public static event System.Action<int, int> OnHealthChanged;

    [Header("Damage Protection")]
    [SerializeField] private float damageInvulnerabilityTime = 0.4f;

    [SerializeField] private int _currentHealth;
    public int currentHealth
    {
        get => _currentHealth;
        set
        {
            _currentHealth = value;
            if (slider != null)
                slider.value = _currentHealth;
            if (healthText != null)
                healthText.text = $"{_currentHealth}/{maxHealth}";
            OnHealthChanged?.Invoke(_currentHealth, maxHealth);
        }
    }
    public int maxHealth;

    public SpriteRenderer playerSr;
    public PlayerMovement playerMovement;
    public Slider slider;
    public TMP_Text healthText;

    private bool initialized = false;
    private float nextDamageTime;
    private Coroutine pendingUiRebind;

    private void OnEnable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        QueueHealthUIRebind();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    public void Start()
    {
        // Only initialize max health if not already set (prevents reset on scene load)
        if (!initialized)
        {
            currentHealth = maxHealth;
            initialized = true;
        }

        // Always update UI when starting a scene because slider/text might be new objects
        QueueHealthUIRebind();
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        QueueHealthUIRebind();
    }

    private void QueueHealthUIRebind()
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        if (pendingUiRebind != null)
        {
            StopCoroutine(pendingUiRebind);
        }

        pendingUiRebind = StartCoroutine(RebindHealthUIRoutine());
    }

    private IEnumerator RebindHealthUIRoutine()
    {
        const int maxAttempts = 10;
        for (int i = 0; i < maxAttempts; i++)
        {
            RebindHealthUI();
            if (slider != null && healthText != null)
            {
                break;
            }

            yield return null;
        }

        pendingUiRebind = null;
    }

    public void RebindHealthUI()
    {
        slider = FindHealthSlider();
        healthText = FindHealthText();
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (slider != null)
        {
            slider.maxValue = maxHealth;
            slider.value = currentHealth;
        }
        if (healthText != null)
        {
            healthText.text = $"{currentHealth}/{maxHealth}";
        }
    }

    private static Slider FindHealthSlider()
    {
        Slider namedSlider = FindComponentByObjectName<Slider>("UI_HealthSlider");
        if (namedSlider != null)
        {
            return namedSlider;
        }

        GameObject canvas = GameObject.Find("HealthBarCanvas");
        return canvas != null ? canvas.GetComponentInChildren<Slider>(true) : null;
    }

    private static TMP_Text FindHealthText()
    {
        TMP_Text namedText = FindComponentByObjectName<TMP_Text>("Text_Health");
        if (namedText != null)
        {
            return namedText;
        }

        GameObject canvas = GameObject.Find("HealthTextCanvas");
        return canvas != null ? canvas.GetComponentInChildren<TMP_Text>(true) : null;
    }

    private static T FindComponentByObjectName<T>(string objectName) where T : Component
    {
        T[] components = FindObjectsOfType<T>(true);
        foreach (T component in components)
        {
            if (component != null && component.gameObject.name == objectName)
            {
                return component;
            }
        }

        return null;
    }

    public void ChangeHealth(int amount)
    {
        if (amount < 0)
        {
            if (Time.time < nextDamageTime) return;
            nextDamageTime = Time.time + damageInvulnerabilityTime;

            // Show Damage Feedback for Player (Red)
            if (DamagePopupManager.Instance != null)
            {
                DamagePopupManager.Instance.CreatePopup(transform.position, Mathf.Abs(amount).ToString(), DamageType.PlayerHit);
            }

            // Hit Flash for Player
            if (TryGetComponent(out HitFlash hitFlash))
            {
                hitFlash.Flash();
            }
        }
        else if (amount > 0)
        {
            // Show Heal Feedback (Green)
            if (DamagePopupManager.Instance != null)
            {
                DamagePopupManager.Instance.CreatePopup(transform.position, amount.ToString(), DamageType.Heal);
            }
        }

        currentHealth += amount;

        if (currentHealth <= 0)
        {
            gameObject.SetActive(false);
        }
    }

    public void SetHealthDirect(int current, int max)
    {
        maxHealth = Mathf.Max(1, max);
        _currentHealth = Mathf.Clamp(current, 0, maxHealth);
        initialized = true;
        UpdateUI();
        OnHealthChanged?.Invoke(_currentHealth, maxHealth);
    }

    public void FullRestore()
    {
        int healAmount = maxHealth - currentHealth;
        currentHealth = maxHealth;
        UpdateUI();

        if (healAmount > 0 && DamagePopupManager.Instance != null)
        {
            DamagePopupManager.Instance.CreatePopup(transform.position, healAmount.ToString(), DamageType.Heal);
        }
    }

    public void Heal(int amount)
    {
        currentHealth = Mathf.Clamp(currentHealth + amount, 0, maxHealth);
        UpdateUI();

        if (DamagePopupManager.Instance != null)
        {
            DamagePopupManager.Instance.CreatePopup(transform.position, amount.ToString(), DamageType.Heal);
        }
    }
}
