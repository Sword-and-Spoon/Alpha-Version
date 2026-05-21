using UnityEngine;

public class Enemy_Stats : MonoBehaviour
{
    [Header("Enemy Data")]
    [Tooltip("ลาก EnemySO ของมอนสเตอร์ตัวนี้มาใส่ — ใช้จับคู่กับ KillMonster quest")]
    public EnemySO enemyData;

    public MonsterTier tier;
    public MonsterTierSettingsSO tierSettings;

    [Header("Base Stats")]
    public int baseMaxHealth = 10;
    public int baseDamage = 1;
    public float baseMoveSpeed = 5f;

    void Awake()
    {
        ApplyTierStats();
    }

    public void ApplyTierStats()
    {
        if (tierSettings == null)
        {
            Debug.LogWarning("Tier Settings SO is missing on " + gameObject.name);
            return;
        }

        var settings = tierSettings.GetSettings(tier);

        // 1. Update Health (Health System)
        if (TryGetComponent(out Health health))
        {
            health.InitializeHealth(Mathf.RoundToInt(baseMaxHealth * settings.hpMultiplier));
        }

        // 2. Update Movement Speed
        if (TryGetComponent(out Enemy_Movement movement))
        {
            movement.speed = baseMoveSpeed * settings.speedMultiplier;
        }

        // 3. Update Combat Damage
        if (TryGetComponent(out Enemy_Combat combat))
        {
            combat.damage = Mathf.RoundToInt(baseDamage * settings.damageMultiplier);
        }
    }
}
