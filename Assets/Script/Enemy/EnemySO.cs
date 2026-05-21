using UnityEngine;

/// <summary>
/// ข้อมูลระบุตัวตนของมอนสเตอร์ — ลาก SO นี้ใส่ ARQuestNPC เพื่อกำหนดเป้าหมาย KillMonster quest
/// สร้างผ่าน Assets → Create → Game/Enemy/Enemy Data
/// </summary>
[CreateAssetMenu(menuName = "Game/Enemy/Enemy Data", fileName = "EnemyData_")]
public class EnemySO : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("รหัสเฉพาะ ใช้จับคู่กับ quest — ต้องไม่ซ้ำกัน")]
    public string enemyId;

    [Tooltip("ชื่อที่แสดงใน quest log และ dialogue")]
    public string displayName;

    [Header("Visuals (Optional)")]
    public Sprite portrait;

    private void OnValidate()
    {
        if (string.IsNullOrEmpty(enemyId))
            enemyId = name; // ใช้ชื่อ asset เป็น ID ตั้งต้น
    }
}
