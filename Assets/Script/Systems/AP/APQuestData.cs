using System;

[Serializable]
public class APQuestData
{
    public string vendorName;
    public string itemName;
    public int quantity;
    public ItemQuality quality = ItemQuality.Common;
    public int principalAmount;     // Original debt amount at time of purchase
    public int interestPerDay;      // Precomputed: Mathf.Max(1, Round(principal * 5%))
    public int dueTotalDay;         // TotalNumDays when payment is due
    public int createdTotalDay;     // TotalNumDays when debt was created
    public bool isOverdue;
    public int accruedInterest;     // Running total of all interest accrued so far
    public int lastAccruedDay = -1; // วันสุดท้ายที่คิดดอกเบี้ย (-1 = ยังไม่เคยคิด)
}
