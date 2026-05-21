using UnityEngine;
using TMPro;

public class QuestLogEntry : MonoBehaviour
{
    [SerializeField] private TMP_Text npcNameText;
    [SerializeField] private TMP_Text questInfoText;
    [SerializeField] private TMP_Text daysRemainingText;

    public void Setup(ARQuestData data)
    {
        if (data == null) { Debug.LogWarning("[QuestLogEntry] Setup called with null ARQuestData"); return; }
        if (npcNameText != null) npcNameText.text = data.npcName;
        if (questInfoText != null) questInfoText.text = $"{data.foodName} ({data.amount} Gold)";

        if (daysRemainingText != null)
        {
            int currentDay = DailyJournalRules.GetCurrentAccountingDay();
            int remaining = data.dueTotalDay - currentDay;

            if (data.status == QuestStatus.PendingTurnIn
                || (!data.isARRecorded
                    && (data.questType == QuestType.KillMonster || data.questType == QuestType.CollectItem)
                    && data.currentProgress >= data.requiredCount))
                daysRemainingText.text = "Return to NPC";
            else if (data.isARRecorded)
                daysRemainingText.text = remaining > 0 ? $"Payment in {remaining} day(s)" : "Payment Arriving Today!";
            else if (remaining <= 0)
                daysRemainingText.text = "Payment Arriving Today!";
            else
                daysRemainingText.text = $"Payment in {remaining} day(s)";
        }
    }

    public void SetupAP(APQuestData data)
    {
        if (data == null) { Debug.LogWarning("[QuestLogEntry] SetupAP called with null APQuestData"); return; }
        int currentDay = DailyJournalRules.GetCurrentAccountingDay();
        int remaining = data.dueTotalDay - currentDay;
        int totalOwed = data.principalAmount + data.accruedInterest;

        if (npcNameText != null) npcNameText.text = $"[OWE] {data.vendorName}";
        if (questInfoText != null) questInfoText.text = $"{data.itemName} x{data.quantity}  —  Owe: {totalOwed:N0} G";

        if (daysRemainingText != null)
        {
            if (remaining > 0)
            {
                daysRemainingText.text = $"Due in {remaining} day(s)";
                daysRemainingText.color = Color.white;
            }
            else if (remaining == 0)
            {
                daysRemainingText.text = "DUE TODAY";
                daysRemainingText.color = new Color(0.95f, 0.45f, 0.18f);
            }
            else
            {
                daysRemainingText.text = $"OVERDUE by {Mathf.Abs(remaining)} day(s)";
                daysRemainingText.color = new Color(0.78f, 0.18f, 0.12f);
            }
        }
    }
}
