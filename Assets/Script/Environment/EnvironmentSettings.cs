using UnityEngine;

[CreateAssetMenu(menuName = "Environment/EnvironmentSettings")]
public class EnvironmentSettings : ScriptableObject
{
    [Header("Day/Night Colors")]
    public Gradient dayNightColorGradient;
    public AnimationCurve intensityCurve = new AnimationCurve(
        new Keyframe(0f, 0.1f),
        new Keyframe(0.25f, 0.8f),
        new Keyframe(0.5f, 1.0f),
        new Keyframe(0.75f, 0.7f),
        new Keyframe(1f, 0.1f)
    );

    [Header("Cave Override")]
    public Color caveAmbientColor = new Color32(13, 13, 38, 255); // #0D0D26
    [Range(0f, 1f)] public float caveIntensity = 0.1f;

    [Header("Transitions")]
    public float transitionDuration = 1.5f;

    [Header("Player Aura Light")]
    public int playerLightOnHour = 18;

    [Header("Local Lights (Lanterns / House Lights)")]
    public int lightsOnHour = 18;
    public int lightsOffHour = 6;

    [Header("Post-Processing Quality Toggles")]
    public bool enableBloom = true;
    public bool enableVignette = true;
    public bool enableColorAdjustments = true;

    [Header("Cloud Shadows")]
    public Sprite cloudShadowSprite;
    public int maxCloudCount = 5;
    public Vector2 windDirection = Vector2.right;
    [Range(0.1f, 3f)] public float windSpeedMin = 0.3f;
    [Range(0.1f, 3f)] public float windSpeedMax = 1.2f;
    [Range(0f, 1f)] public float cloudAlphaMin = 0.1f;
    [Range(0f, 1f)] public float cloudAlphaMax = 0.35f;
    [Range(0.5f, 5f)] public float cloudScaleMin = 1.5f;
    [Range(0.5f, 5f)] public float cloudScaleMax = 5f;
}
