using UnityEngine;

/// <summary>
/// วางบน scene object ที่ต้องการให้ลูกศร tutorial ชี้ไปหา
/// จะลงทะเบียน/ถอนตัวเองออกจาก TutorialManager อัตโนมัติเมื่อ scene load/unload
/// </summary>
public class TutorialTargetRegistrar : MonoBehaviour
{
    [SerializeField] private TutorialTargetType targetType;

    private void OnEnable()
    {
        TutorialManager.Instance?.RegisterTarget(targetType, transform);
    }

    private void OnDisable()
    {
        TutorialManager.Instance?.UnregisterTarget(targetType);
    }
}
