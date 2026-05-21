using UnityEngine;

[CreateAssetMenu(fileName = "MonsterTierSettings", menuName = "Enemy/Monster Tier Settings")]
public class MonsterTierSettingsSO : ScriptableObject
{
    [System.Serializable]
    public struct TierMultiplier
    {
        public MonsterTier tier;
        public float hpMultiplier;
        public float damageMultiplier;
        public float speedMultiplier;
        public ItemQuality defaultQuality; // คุณภาพไอเทมพื้นฐานของ Tier นี้
    }

    public TierMultiplier[] tierSettings;

    public TierMultiplier GetSettings(MonsterTier tier)
    {
        if (tierSettings == null || tierSettings.Length == 0) return default;

        foreach (var setting in tierSettings)
        {
            if (setting.tier == tier) return setting;
        }
        return tierSettings[0]; // Fallback to Common
    }
}
