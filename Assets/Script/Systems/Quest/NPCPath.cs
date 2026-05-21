using UnityEngine;

/// <summary>
/// กำหนดเส้นทางของ NPC โดยการลากจุดในหน้าต่าง Scene
/// ลาก Component นี้เข้าช่อง "Path" ของ NPCMovement
/// </summary>
public class NPCPath : MonoBehaviour
{
    [HideInInspector]
    public Vector3[] points = { Vector3.zero, new Vector3(2, 0, 0) };

    [Tooltip("วนกลับมาจุดแรกหลังถึงจุดสุดท้าย")]
    public bool loop = true;

    public Color pathColor = new Color(0.2f, 1f, 0.5f, 0.9f);

    public int Count => points?.Length ?? 0;

    public Vector3 GetPoint(int index)
    {
        if (points == null || points.Length == 0) return transform.position;
        return points[((index % points.Length) + points.Length) % points.Length];
    }

    private void OnDrawGizmos()
    {
        if (points == null || points.Length < 2) return;

        Gizmos.color = pathColor;
        int end = loop ? points.Length : points.Length - 1;
        for (int i = 0; i < end; i++)
        {
            Vector3 a = points[i];
            Vector3 b = points[(i + 1) % points.Length];
            Gizmos.DrawLine(a, b);

            // ลูกศรแสดงทิศทาง
            Vector3 mid = (a + b) * 0.5f;
            Vector3 dir = (b - a).normalized;
            if (dir != Vector3.zero)
                Gizmos.DrawRay(mid, dir * 0.25f);
        }

        Gizmos.color = Color.white;
        for (int i = 0; i < points.Length; i++)
            Gizmos.DrawSphere(points[i], 0.12f);
    }
}
