using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[RequireComponent(typeof(Volume))]
public class PostProcessingController : MonoBehaviour, IEnvironmentObserver
{
    private Volume _volume;
    private EnvironmentStateController _controller;

    private Bloom _bloom;
    private Vignette _vignette;
    private ColorAdjustments _colorAdjustments;

    private void Start()
    {
        _volume = GetComponent<Volume>();
        _controller = EnvironmentStateController.Instance;

        if (_controller == null)
        {
            Debug.LogError("[PostProcessingController] EnvironmentStateController not found.");
            enabled = false;
            return;
        }

        if (_volume.profile == null)
        {
            Debug.LogError("[PostProcessingController] Volume has no Profile assigned.");
            enabled = false;
            return;
        }

        // ดึง Override references จาก Profile (ไม่บังคับต้องมีครบทุกตัว)
        _volume.profile.TryGet(out _bloom);
        _volume.profile.TryGet(out _vignette);
        _volume.profile.TryGet(out _colorAdjustments);

        _controller.Register(this);

        // Apply quality settings จาก EnvironmentSettings ทันที
        ApplyQualitySettings(_controller.Settings);

        // Sync state เริ่มต้น
        OnEnvironmentStateChanged(_controller.CurrentState);
    }

    private void OnDestroy()
    {
        _controller?.Unregister(this);
    }

    // ── IEnvironmentObserver ────────────────────────────────────────────────

    public void OnEnvironmentStateChanged(EnvironmentState newState)
    {
        if (_volume.profile == null) return;

        // ในถ้ำ: เพิ่ม Vignette และลด Bloom ลงเพื่อให้รู้สึกอึดอัดมากขึ้น
        if (_vignette != null && _vignette.active)
        {
            _vignette.intensity.Override(newState == EnvironmentState.Cave ? 0.45f : 0.25f);
        }

        if (_bloom != null && _bloom.active)
        {
            _bloom.intensity.Override(newState == EnvironmentState.Cave ? 0.3f : 0.6f);
        }
    }

    // ── Quality Toggles ─────────────────────────────────────────────────────

    public void ApplyQualitySettings(EnvironmentSettings settings)
    {
        if (settings == null) return;

        if (_bloom != null)
            _bloom.active = settings.enableBloom;

        if (_vignette != null)
            _vignette.active = settings.enableVignette;

        if (_colorAdjustments != null)
            _colorAdjustments.active = settings.enableColorAdjustments;
    }
}
