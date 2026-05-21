using UnityEngine;
using UnityEngine.Rendering.Universal;

[RequireComponent(typeof(Light2D))]
public class LocalLightController : MonoBehaviour, IEnvironmentObserver
{
    [Header("Override Hours (ปล่อยว่างเพื่อใช้ค่าจาก EnvironmentSettings)")]
    [SerializeField] private bool useCustomHours = false;
    [SerializeField] private int customOnHour = 18;
    [SerializeField] private int customOffHour = 6;

    [Header("Cave Behaviour")]
    [Tooltip("เปิดไฟเสมอเมื่ออยู่ในถ้ำ (เช่น คบเพลิงในถ้ำ) ปิดถ้าเป็นไฟกลางแจ้งที่ไม่เกี่ยวกับถ้ำ")]
    [SerializeField] private bool alwaysOnInCave = false;

    private Light2D _light;
    private EnvironmentStateController _controller;
    private EnvironmentState _currentState = EnvironmentState.Outdoor;

    private void Start()
    {
        _light = GetComponent<Light2D>();
        _controller = EnvironmentStateController.Instance;

        if (_controller == null)
        {
            Debug.LogError($"[LocalLightController] EnvironmentStateController not found. ({gameObject.name})");
            enabled = false;
            return;
        }

        _controller.Register(this);
        TimeManager.OnDateTimeChanged += OnDateTimeChanged;

        // Sync ทันที
        _currentState = _controller.CurrentState;
        int hour = TimeManager.Instance != null ? TimeManager.Instance.dateTime.Hour : 0;
        EvaluateLight(_currentState, hour);
    }

    private void OnDestroy()
    {
        TimeManager.OnDateTimeChanged -= OnDateTimeChanged;
        _controller?.Unregister(this);
    }

    // ── IEnvironmentObserver ────────────────────────────────────────────────

    public void OnEnvironmentStateChanged(EnvironmentState newState)
    {
        _currentState = newState;
        int hour = TimeManager.Instance != null ? TimeManager.Instance.dateTime.Hour : 0;
        EvaluateLight(newState, hour);
    }

    // ── TimeManager Event ───────────────────────────────────────────────────

    private void OnDateTimeChanged(TimeManager.DateTime dateTime)
    {
        EvaluateLight(_currentState, dateTime.Hour);
    }

    // ── Logic ───────────────────────────────────────────────────────────────

    private void EvaluateLight(EnvironmentState state, int hour)
    {
        if (state == EnvironmentState.Cave)
        {
            _light.enabled = alwaysOnInCave;
            return;
        }

        var settings = _controller.Settings;
        int onHour = useCustomHours ? customOnHour : settings.lightsOnHour;
        int offHour = useCustomHours ? customOffHour : settings.lightsOffHour;

        // เปิดช่วง onHour ถึงเที่ยงคืน หรือตั้งแต่เที่ยงคืนถึง offHour
        bool isLightTime = hour >= onHour || hour < offHour;
        _light.enabled = isLightTime;
    }
}
