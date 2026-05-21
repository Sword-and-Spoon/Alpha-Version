using UnityEngine;

/// <summary>
/// ล็อค frame rate ไว้ที่ 120 fps สูงสุด และปิด vSync เพื่อให้ targetFrameRate ทำงานได้จริง
/// ใส่ component นี้บน GameManager GameObject (DontDestroyOnLoad)
/// </summary>
public class FrameRateManager : MonoBehaviour
{
    [Header("Frame Rate")]
    [SerializeField] private int targetFrameRate = 120;

    [Header("V-Sync")]
    [Tooltip("0 = ปิด vSync (แนะนำ), 1 = sync ทุก frame, 2 = sync ทุก 2 frames")]
    [SerializeField] [Range(0, 2)] private int vSyncCount = 0;

    private void Awake()
    {
        Apply();
    }

    private void Apply()
    {
        QualitySettings.vSyncCount = vSyncCount;
        Application.targetFrameRate = Mathf.Max(30, targetFrameRate);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            Apply();
        }
    }
#endif
}
