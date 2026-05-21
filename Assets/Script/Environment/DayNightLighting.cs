using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class DayNightLighting : MonoBehaviour, IEnvironmentObserver
{
    [SerializeField] private Light2D globalLight;

    private EnvironmentStateController _controller;
    private EnvironmentState _currentState = EnvironmentState.Outdoor;
    private Coroutine _transitionCoroutine;

    // สีและ Intensity ที่คำนวณจาก Day/Night ล่าสุด (ใช้ restore เมื่อออกจากถ้ำ)
    private Color _outdoorTargetColor = Color.white;
    private float _outdoorTargetIntensity = 1f;

    private void Start()
    {
        _controller = EnvironmentStateController.Instance;

        if (_controller == null)
        {
            Debug.LogError("[DayNightLighting] EnvironmentStateController not found in scene.");
            enabled = false;
            return;
        }

        if (globalLight == null)
        {
            Debug.LogError("[DayNightLighting] Global Light2D reference not assigned.");
            enabled = false;
            return;
        }

        _controller.Register(this);
        TimeManager.OnDateTimeChanged += OnDateTimeChanged;

        // Sync state ทันทีหลัง register
        _currentState = _controller.CurrentState;
        if (_currentState == EnvironmentState.Cave)
        {
            var s = _controller.Settings;
            globalLight.color = s.caveAmbientColor;
            globalLight.intensity = s.caveIntensity;
        }
        else if (_currentState == EnvironmentState.Indoor)
        {
            // Indoor: ใช้แสงคงที่ ไม่ขึ้นกับเวลา
        }
        else if (TimeManager.Instance != null)
            ApplyOutdoorLighting(TimeManager.Instance.dateTime);
        else
            StartCoroutine(WaitForTimeManager());
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

        if (newState == EnvironmentState.Cave)
        {
            var settings = _controller.Settings;
            StartTransition(settings.caveAmbientColor, settings.caveIntensity, settings.transitionDuration);
        }
        else if (newState == EnvironmentState.Indoor)
        {
            // Indoor: หยุด transition ไว้กับค่าปัจจุบัน ไม่อัปเดตตามเวลา
            if (_transitionCoroutine != null)
            {
                StopCoroutine(_transitionCoroutine);
                _transitionCoroutine = null;
            }
        }
        else
        {
            // กลับ Outdoor → restore ค่า outdoor ล่าสุด
            StartTransition(_outdoorTargetColor, _outdoorTargetIntensity, _controller.Settings.transitionDuration);
        }
    }

    // ── TimeManager Event ───────────────────────────────────────────────────

    private void OnDateTimeChanged(TimeManager.DateTime dateTime)
    {
        // Cave/Indoor ไม่อัปเดตแสงตามเวลา
        if (_currentState != EnvironmentState.Outdoor) return;

        ApplyOutdoorLighting(dateTime);
    }

    // ── Lighting Logic ──────────────────────────────────────────────────────

    private void ApplyOutdoorLighting(TimeManager.DateTime dateTime)
    {
        float t = (dateTime.Hour * 60f + dateTime.Minutes) / (24f * 60f);
        var settings = _controller.Settings;

        _outdoorTargetColor = settings.dayNightColorGradient != null
            ? settings.dayNightColorGradient.Evaluate(t)
            : Color.white;

        _outdoorTargetIntensity = settings.intensityCurve != null
            ? settings.intensityCurve.Evaluate(t)
            : 1f;

        globalLight.color = _outdoorTargetColor;
        globalLight.intensity = _outdoorTargetIntensity;
    }

    // ── Cross-fade (Spam-safe: stop แล้ว restart) ───────────────────────────

    private void StartTransition(Color targetColor, float targetIntensity, float duration)
    {
        if (_transitionCoroutine != null)
            StopCoroutine(_transitionCoroutine);

        _transitionCoroutine = StartCoroutine(TransitionRoutine(targetColor, targetIntensity, duration));
    }

    private IEnumerator TransitionRoutine(Color targetColor, float targetIntensity, float duration)
    {
        Color startColor = globalLight.color;
        float startIntensity = globalLight.intensity;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            globalLight.color = Color.Lerp(startColor, targetColor, t);
            globalLight.intensity = Mathf.Lerp(startIntensity, targetIntensity, t);

            yield return null;
        }

        globalLight.color = targetColor;
        globalLight.intensity = targetIntensity;
        _transitionCoroutine = null;
    }

    // ── Late Init Guard ─────────────────────────────────────────────────────

    private IEnumerator WaitForTimeManager()
    {
        while (TimeManager.Instance == null)
            yield return null;

        ApplyOutdoorLighting(TimeManager.Instance.dateTime);
    }
}
