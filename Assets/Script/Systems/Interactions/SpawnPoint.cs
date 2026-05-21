using System.Collections.Generic;
using UnityEngine;

public class SpawnPoint : MonoBehaviour
{
    [Tooltip("Unique id used to identify this spawn point across scene loads")]
    public string spawnId = "default";

    [Header("Spawn Settings")]
    [Tooltip("ระยะห่างจากจุดศูนย์กลางที่ตัวละครจะไปปรากฏตัวจริง")]
    public Vector2 spawnOffset = new Vector2(0, -1f);

    private static readonly Dictionary<string, SpawnPoint> _registry = new();

    public static SpawnPoint Find(string id) =>
        _registry.TryGetValue(id, out var sp) ? sp : null;

    private void OnEnable()
    {
        if (!string.IsNullOrEmpty(spawnId))
            _registry[spawnId] = this;
    }

    private void OnDisable()
    {
        if (!string.IsNullOrEmpty(spawnId) && _registry.TryGetValue(spawnId, out var sp) && sp == this)
            _registry.Remove(spawnId);
    }

    public Vector3 GetSpawnPosition()
    {
        return transform.position + (Vector3)spawnOffset;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, 0.2f);

        Gizmos.color = new Color(0, 1, 0, 0.3f);
        Vector3 finalPos = GetSpawnPosition();
        Gizmos.DrawCube(finalPos, new Vector3(0.6f, 1f, 0.1f));

        Gizmos.color = Color.green;
        Gizmos.DrawLine(transform.position, finalPos);

#if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up * 0.5f, "Spawn ID: " + spawnId);
#endif
    }
}
