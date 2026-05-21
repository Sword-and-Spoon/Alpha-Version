using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Light2D))]
public class PlayerAuraLight : MonoBehaviour, IEnvironmentObserver
{
    private Light2D _light;
    private EnvironmentStateController _controller;
    private EnvironmentState _currentState = EnvironmentState.Outdoor;

    private void Awake()
    {
        _light = GetComponent<Light2D>();
        _light.enabled = false;
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start()
    {
        // Initial registration สำหรับการโหลดครั้งแรก
        RegisterWithCurrentController();
    }

    private void OnDestroy()
    {
        TimeManager.OnDateTimeChanged -= OnDateTimeChanged;
        _controller?.Unregister(this);
    }

    // ── Scene Load Handler ──────────────────────────────────────────────────
    // ทุกครั้งที่ Scene โหลดใหม่ (รวมถึงกลับมา Cave) จะ re-wire กับ Controller ใหม่

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // รอ 1 frame ให้ EnvironmentStateController.Awake() และ Start() ใน Scene ใหม่รันก่อน
        StartCoroutine(ReRegisterNextFrame());
    }

    private IEnumerator ReRegisterNextFrame()
    {
        yield return null;
        RegisterWithCurrentController();
    }

    // ── Registration ────────────────────────────────────────────────────────

    private void RegisterWithCurrentController()
    {
        // ถอนตัวจาก controller เดิมก่อน (ถ้ามี)
        TimeManager.OnDateTimeChanged -= OnDateTimeChanged;
        _controller?.Unregister(this);

        _controller = EnvironmentStateController.Instance;
        if (_controller == null)
        {
            Debug.LogError("[PlayerAuraLight] EnvironmentStateController not found in scene.");
            return;
        }

        _controller.Register(this);
        TimeManager.OnDateTimeChanged += OnDateTimeChanged;

        // Sync สถานะและเวลาปัจจุบันทันที
        _currentState = _controller.CurrentState;
        int hour = TimeManager.Instance != null ? TimeManager.Instance.dateTime.Hour : 0;
        EvaluateLight(_currentState, hour);
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

    // ── Light Evaluation ────────────────────────────────────────────────────

    private void EvaluateLight(EnvironmentState state, int hour)
    {
        if (_controller == null) return;

        int onHour = _controller.Settings.playerLightOnHour;
        bool isNightTime = hour >= onHour || hour < 6;

        _light.enabled = state == EnvironmentState.Cave || (state == EnvironmentState.Outdoor && isNightTime);
    }
}
