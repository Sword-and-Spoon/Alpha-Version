using UnityEngine;
using UnityEngine.UI;

public class HealthDisplay : MonoBehaviour
{
    public Sprite emptyHeart;
    public Sprite fullHeart;
    public Image[] hearts;

    public PlayerHealth playerHealth;

    private int lastHealth = -1;
    private int lastMaxHealth = -1;

    private void Start()
    {
        RefreshPlayerReference();
        ForceRefreshDisplay();
    }

    private void OnEnable()
    {
        PlayerHealth.OnHealthChanged += HandleHealthChanged;
    }

    private void OnDisable()
    {
        PlayerHealth.OnHealthChanged -= HandleHealthChanged;
    }

    private void HandleHealthChanged(int currentHealth, int maxHealth)
    {
        if (lastHealth == currentHealth && lastMaxHealth == maxHealth) return;
        lastHealth = currentHealth;
        lastMaxHealth = maxHealth;
        UpdateHearts(currentHealth, maxHealth);
    }

    private void RefreshPlayerReference()
    {
        if (playerHealth == null && GameManager.instance != null && GameManager.instance.player != null)
        {
            playerHealth = GameManager.instance.player.GetComponent<PlayerHealth>();
        }
    }

    private void ForceRefreshDisplay()
    {
        RefreshPlayerReference();
        if (playerHealth == null) return;
        HandleHealthChanged(playerHealth.currentHealth, playerHealth.maxHealth);
    }

    private void UpdateHearts(int health, int maxHealth)
    {
        for (int i = 0; i < hearts.Length; i++)
        {
            if (hearts[i] == null) continue;
            hearts[i].sprite = i < health ? fullHeart : emptyHeart;
            hearts[i].enabled = i < maxHealth;
        }
    }
}
