using UnityEngine;

/// <summary>
/// ทำให้ GameObject ลอยขึ้น-ลงแบบ sine wave
/// ใส่บน QuestAvailableIndicator / QuestBonusIndicator / QuestActiveIndicator
/// </summary>
public class IndicatorBob : MonoBehaviour
{
    [Tooltip("ความสูงที่ขึ้นลง (หน่วย Unity)")]
    [SerializeField] private float amplitude = 0.08f;
    [Tooltip("ความเร็วในการขึ้นลง")]
    [SerializeField] private float frequency = 2f;
    [Tooltip("offset เวลา เพื่อให้แต่ละ indicator ไม่ขยับพร้อมกัน")]
    [SerializeField] private float phaseOffset = 0f;

    private Vector3 originLocalPos;

    private void OnEnable()
    {
        originLocalPos = transform.localPosition;
    }

    private void Update()
    {
        float y = Mathf.Sin((Time.time + phaseOffset) * frequency) * amplitude;
        transform.localPosition = originLocalPos + new Vector3(0f, y, 0f);
    }

    private void OnDisable()
    {
        transform.localPosition = originLocalPos;
    }
}
