using System.Collections.Generic;
using UnityEngine;

public class EnvironmentStateController : MonoBehaviour
{
    public static EnvironmentStateController Instance { get; private set; }

    [SerializeField] private EnvironmentSettings settings;
    [SerializeField] private EnvironmentState initialState = EnvironmentState.Outdoor;

    private EnvironmentState _currentState = EnvironmentState.Outdoor;
    private readonly List<IEnvironmentObserver> _observers = new List<IEnvironmentObserver>();

    private static EnvironmentSettings _fallbackSettings;

    public EnvironmentState CurrentState => _currentState;
    public EnvironmentSettings Settings => settings != null ? settings : GetFallback();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        _currentState = initialState;

        if (settings == null)
            Debug.LogError("[EnvironmentStateController] EnvironmentSettings asset not assigned — using hardcoded defaults.");
    }

    public void Register(IEnvironmentObserver observer)
    {
        if (!_observers.Contains(observer))
            _observers.Add(observer);
    }

    public void Unregister(IEnvironmentObserver observer)
    {
        _observers.Remove(observer);
    }

    public void SetState(EnvironmentState newState)
    {
        if (_currentState == newState) return;
        _currentState = newState;
        NotifyObservers();
    }

    private void NotifyObservers()
    {
        for (int i = _observers.Count - 1; i >= 0; i--)
            _observers[i].OnEnvironmentStateChanged(_currentState);
    }

    private static EnvironmentSettings GetFallback()
    {
        if (_fallbackSettings != null) return _fallbackSettings;

        _fallbackSettings = ScriptableObject.CreateInstance<EnvironmentSettings>();
        
        // Setup Default Gradient
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] { 
                new GradientColorKey(new Color32(10, 10, 30, 255), 0.0f),   // #0A0A1E
                new GradientColorKey(new Color32(255, 179, 71, 255), 0.25f), // #FFB347
                new GradientColorKey(new Color32(255, 245, 204, 255), 0.5f), // #FFF5CC
                new GradientColorKey(new Color32(255, 107, 53, 255), 0.75f), // #FF6B35
                new GradientColorKey(new Color32(10, 10, 30, 255), 1.0f)    // #0A0A1E
            },
            new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
        );
        _fallbackSettings.dayNightColorGradient = gradient;

        _fallbackSettings.intensityCurve = new AnimationCurve(
            new Keyframe(0f, 0.1f),
            new Keyframe(0.25f, 0.8f),
            new Keyframe(0.5f, 1.0f),
            new Keyframe(0.75f, 0.7f),
            new Keyframe(1f, 0.1f)
        );

        _fallbackSettings.caveAmbientColor = new Color32(13, 13, 38, 255);
        _fallbackSettings.caveIntensity = 0.1f;
        _fallbackSettings.transitionDuration = 1.5f;
        _fallbackSettings.playerLightOnHour = 18;
        return _fallbackSettings;
    }
}
