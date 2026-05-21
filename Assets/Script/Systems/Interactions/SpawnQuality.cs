using UnityEngine;

public class SpawnQuality : MonoBehaviour
{
    public enum QualityBias { None, High, Low }

    [Header("Spawn quality bias for items spawned at this transform")]
    public QualityBias bias = QualityBias.None;

    [Header("Custom weights (overrides bias when >0)")]
    [Tooltip("Relative weight for Common quality when spawning here")]
    public float commonWeight = 40f;
    [Tooltip("Relative weight for Rare quality when spawning here")]
    public float rareWeight = 30f;
    [Tooltip("Relative weight for Epic quality when spawning here")]
    public float epicWeight = 15f;
    [Tooltip("Relative weight for Legendary quality when spawning here")]
    public float legendaryWeight = 10f;
    [Tooltip("Relative weight for Mythical quality when spawning here")]
    public float mythicalWeight = 5f;
}
