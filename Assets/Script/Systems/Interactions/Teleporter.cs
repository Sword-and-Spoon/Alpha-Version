using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class Teleporter : MonoBehaviour
{
    [Header("Target Settings")]
    [Tooltip("ชื่อซีนที่ต้องการไป (เว้นว่างไว้ถ้าจะวาร์ปในซีนเดียวกัน)")]
    public string targetScene;
    [Tooltip("ID ของ SpawnPoint ปลายทาง")]
    public string targetSpawnId;

    [Header("Boundary Settings (Custom)")]
    [Tooltip("ปรับขนาดขอบเขตแกน X และ Y")]
    public Vector2 areaSize = new Vector2(1f, 1f);
    [Tooltip("ปรับตำแหน่งเยื้องของขอบเขต")]
    public Vector2 areaOffset = Vector2.zero;

    [Header("Mode Settings")]
    [Tooltip("หากติ๊กถูก จะต้องกดปุ่ม (เช่น F) เพื่อวาร์ป ไม่วาร์ปอัตโนมัติ")]
    public bool isManual = false;

    [Header("Gizmo Settings")]
    public Color areaColor = new Color(0, 1, 1, 0.2f);

    private BoxCollider2D col;

    private void OnValidate()
    {
        // อัปเดต Collider ทันทีเมื่อมีการเปลี่ยนค่าใน Inspector
        UpdateCollider();
    }

    private void Awake()
    {
        UpdateCollider();
    }

    private void UpdateCollider()
    {
        if (col == null) col = GetComponent<BoxCollider2D>();
        if (col != null)
        {
            col.isTrigger = true;
            col.size = areaSize;
            col.offset = areaOffset;
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!isManual && collision.CompareTag("Player"))
        {
            HandleTeleport(collision.gameObject);
        }
    }

    public void HandleTeleport(GameObject player)
    {
        // 1. วาร์ปข้ามซีน
        if (!string.IsNullOrEmpty(targetScene))
        {
            if (DailyJournalRules.ShouldBlockLeavingHome(targetScene, out string message))
            {
                DailyJournalRules.ShowMessage(message);
                return;
            }

            if (SceneController.Instance != null)
                SceneController.Instance.LoadScene(targetScene, targetSpawnId);
            else
                Debug.LogError("ไม่พบ SceneController ในซีน!");
            return;
        }

        // 2. วาร์ปในซีนเดียวกัน
        var target = SpawnPoint.Find(targetSpawnId);
        if (target != null)
        {
            player.transform.position = target.GetSpawnPosition();
            return;
        }
        Debug.LogWarning("ไม่พบ SpawnPoint ที่มี ID: " + targetSpawnId);
    }

    private void OnDrawGizmos()
    {
        // วาดพื้นที่ React ตามขนาดที่ตั้งค่าไว้ (X, Y)
        Gizmos.color = areaColor;
        Gizmos.DrawCube(transform.position + (Vector3)areaOffset, new Vector3(areaSize.x, areaSize.y, 0.1f));

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(transform.position + (Vector3)areaOffset, new Vector3(areaSize.x, areaSize.y, 0.1f));

        // เส้นไกด์ไลน์ (เฉพาะในซีนเดียวกัน)
        if (string.IsNullOrEmpty(targetScene) && !string.IsNullOrEmpty(targetSpawnId))
        {
            var sp = SpawnPoint.Find(targetSpawnId);
            if (sp != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(transform.position + (Vector3)areaOffset, sp.GetSpawnPosition());
            }
        }
    }}
