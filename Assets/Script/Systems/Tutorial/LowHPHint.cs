using UnityEngine;

public class LowHPHint : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("HP ที่ถือว่า 'ต่ำ' (inclusive)")]
    [SerializeField] private int hpThreshold = 10;
    [Tooltip("แสดง hint ซ้ำทุกกี่วินาที")]
    [SerializeField] private float blinkInterval = 5f;
    [TextArea(2, 5)]
    [SerializeField] private string message =
        "HP ต่ำ!  กด E → เปิด Inventory\nคลิกขวาที่ Sugar Dust หรืออาหารเพื่อกินและเพิ่ม HP";

    private float timer;
    private PlayerHealth playerHealth;

    private void Update()
    {
        if (TutorialManager.Instance != null && !TutorialManager.Instance.IsTutorialActive)
        {
            timer = 0f;
            return;
        }

        if (playerHealth == null)
        {
            GameObject p = GameObject.FindWithTag("Player");
            if (p != null) playerHealth = p.GetComponent<PlayerHealth>();
        }

        if (playerHealth == null || playerHealth.currentHealth > hpThreshold)
        {
            timer = 0f;
            return;
        }

        timer += Time.deltaTime;
        if (timer >= blinkInterval)
        {
            timer = 0f;
            DailyJournalRules.ShowMessage(message);
        }
    }
}
