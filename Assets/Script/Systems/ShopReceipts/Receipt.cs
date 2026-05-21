using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ReceiptItem
{
    public string itemName;
    public float amount;
    public string category;  // เช่น "ingredient", "equipment"
}

[System.Serializable]
public class Receipt
{
    public string receiptID;
    public string type;          // "purchase" หรือ "sale"
    public string payment;       // "cash" หรือ "credit"
    public List<ReceiptItem> items;
}

public class ReceiptGroup
{
    public string groupName;     // เช่น "Ingredients"
    public float totalAmount;    // รวมราคาในหมวดนี้
}
