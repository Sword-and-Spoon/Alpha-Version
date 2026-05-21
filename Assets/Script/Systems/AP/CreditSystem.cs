using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct CreditTierThreshold
{
    public ItemQuality quality;
    public int requiredSoldCount;
}

[Serializable]
public class VendorSalesRecord
{
    public string vendorName;
    public int commonSold;
    public int rareSold;
    public int epicSold;
    public int legendarySold;
    public int mythicalSold;
}

public class CreditSystem : MonoBehaviour
{
    public static CreditSystem Instance;
    public static event Action<string, ItemQuality, int> OnCreditTierUnlocked;

    [SerializeField] private CreditTierThreshold[] thresholds = new CreditTierThreshold[]
    {
        new CreditTierThreshold { quality = ItemQuality.Common,    requiredSoldCount = 50 },
        new CreditTierThreshold { quality = ItemQuality.Rare,      requiredSoldCount = 25 },
        new CreditTierThreshold { quality = ItemQuality.Epic,      requiredSoldCount = 10 },
        new CreditTierThreshold { quality = ItemQuality.Legendary, requiredSoldCount = 5  },
        new CreditTierThreshold { quality = ItemQuality.Mythical,  requiredSoldCount = 3  },
    };

    private Dictionary<string, VendorSalesRecord> records = new Dictionary<string, VendorSalesRecord>();
    private readonly Dictionary<ItemQuality, int> thresholdOverrides = new Dictionary<ItemQuality, int>();
    private readonly HashSet<string> unlockedTierKeys = new HashSet<string>();

    private void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else Destroy(gameObject);
    }

    public void RecordSale(string vendorName, ItemQuality quality, int quantity)
    {
        if (!records.TryGetValue(vendorName, out var rec))
        {
            rec = new VendorSalesRecord { vendorName = vendorName };
            records[vendorName] = rec;
        }

        int previousCount = GetSoldCount(rec, quality);

        switch (quality)
        {
            case ItemQuality.Common:    rec.commonSold    += quantity; break;
            case ItemQuality.Rare:      rec.rareSold      += quantity; break;
            case ItemQuality.Epic:      rec.epicSold      += quantity; break;
            case ItemQuality.Legendary: rec.legendarySold += quantity; break;
            case ItemQuality.Mythical:  rec.mythicalSold  += quantity; break;
        }

        int updatedCount = GetSoldCount(rec, quality);
        TryUnlockTier(vendorName, quality, previousCount, updatedCount);
    }

    public bool HasCreditFor(string vendorName, ItemQuality quality)
    {
        string tierKey = BuildTierKey(vendorName, quality);
        if (unlockedTierKeys.Contains(tierKey))
        {
            return true;
        }

        int threshold = GetThreshold(quality);
        if (threshold <= 0) return false;
        if (!records.TryGetValue(vendorName, out var rec)) return false;

        int soldCount = GetSoldCount(rec, quality);
        if (soldCount >= threshold)
        {
            unlockedTierKeys.Add(tierKey);
            return true;
        }

        return false;
    }

    public int GetThreshold(ItemQuality quality)
    {
        if (thresholdOverrides.TryGetValue(quality, out int overrideValue))
        {
            return Mathf.Max(0, overrideValue);
        }

        foreach (var t in thresholds)
        {
            if (t.quality == quality) return t.requiredSoldCount;
        }
        return 0;
    }

    public void SetThresholdOverride(ItemQuality quality, int requiredSoldCount)
    {
        thresholdOverrides[quality] = Mathf.Max(0, requiredSoldCount);
        RebuildUnlockedTierCache();
    }

    public void ClearThresholdOverride(ItemQuality quality)
    {
        if (!thresholdOverrides.Remove(quality))
        {
            return;
        }

        RebuildUnlockedTierCache();
    }

    public void ClearAllThresholdOverrides()
    {
        if (thresholdOverrides.Count == 0)
        {
            return;
        }

        thresholdOverrides.Clear();
        RebuildUnlockedTierCache();
    }

    public VendorSalesRecord GetRecord(string vendorName)
    {
        if (!records.TryGetValue(vendorName, out var rec))
        {
            Debug.LogWarning($"[CreditSystem] ไม่พบ record สำหรับ vendor: {vendorName}");
            return null;
        }
        return rec;
    }

    private int GetSoldCount(VendorSalesRecord rec, ItemQuality quality)
    {
        return quality switch
        {
            ItemQuality.Common    => rec.commonSold,
            ItemQuality.Rare      => rec.rareSold,
            ItemQuality.Epic      => rec.epicSold,
            ItemQuality.Legendary => rec.legendarySold,
            ItemQuality.Mythical  => rec.mythicalSold,
            _                     => 0,
        };
    }

    public List<VendorSalesRecordDTO> CaptureState()
    {
        var dtos = new List<VendorSalesRecordDTO>();
        foreach (var rec in records.Values)
        {
            dtos.Add(new VendorSalesRecordDTO
            {
                vendorName    = rec.vendorName,
                commonSold    = rec.commonSold,
                rareSold      = rec.rareSold,
                epicSold      = rec.epicSold,
                legendarySold = rec.legendarySold,
                mythicalSold  = rec.mythicalSold,
            });
        }
        return dtos;
    }

    public void RestoreState(List<VendorSalesRecordDTO> dtos)
    {
        records.Clear();
        unlockedTierKeys.Clear();
        if (dtos == null) return;
        foreach (var dto in dtos)
        {
            records[dto.vendorName] = new VendorSalesRecord
            {
                vendorName    = dto.vendorName,
                commonSold    = dto.commonSold,
                rareSold      = dto.rareSold,
                epicSold      = dto.epicSold,
                legendarySold = dto.legendarySold,
                mythicalSold  = dto.mythicalSold,
            };
        }

        RebuildUnlockedTierCache();
        Debug.Log($"[CreditSystem] Restored records for {records.Count} vendor(s).");
    }

    private void TryUnlockTier(string vendorName, ItemQuality quality, int previousCount, int updatedCount)
    {
        int requiredCount = GetThreshold(quality);
        if (requiredCount <= 0)
        {
            return;
        }

        if (previousCount >= requiredCount || updatedCount < requiredCount)
        {
            return;
        }

        string tierKey = BuildTierKey(vendorName, quality);
        if (!unlockedTierKeys.Add(tierKey))
        {
            return;
        }

        OnCreditTierUnlocked?.Invoke(vendorName, quality, requiredCount);
    }

    private void RebuildUnlockedTierCache()
    {
        unlockedTierKeys.Clear();

        foreach (var pair in records)
        {
            string vendorName = pair.Key;
            VendorSalesRecord rec = pair.Value;

            foreach (var threshold in thresholds)
            {
                int requiredCount = GetThreshold(threshold.quality);
                if (requiredCount <= 0)
                {
                    continue;
                }

                int soldCount = GetSoldCount(rec, threshold.quality);
                if (soldCount >= requiredCount)
                {
                    unlockedTierKeys.Add(BuildTierKey(vendorName, threshold.quality));
                }
            }
        }
    }

    private static string BuildTierKey(string vendorName, ItemQuality quality)
    {
        return $"{vendorName?.Trim().ToLowerInvariant()}::{quality}";
    }
}
