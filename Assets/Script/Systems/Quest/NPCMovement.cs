using UnityEngine;

/// <summary>
/// NPC เดินตาม NPCPath และเดินหา player อัตโนมัติเมื่อมี quest พร้อม
/// ต้องมี Collider2D บน NPC ด้วยเพื่อให้กำแพงกั้นได้
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class NPCMovement : MonoBehaviour
{
    [Header("Path (Patrol)")]
    [Tooltip("ลาก NPCPath Component จาก GameObject ในฉากมาใส่")]
    [SerializeField] private NPCPath path;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 1.5f;
    [SerializeField] private float acceleration = 10f;
    [SerializeField] private float waypointReachDistance = 0.2f;
    [SerializeField] private float waypointWaitDuration = 2f;
    [Tooltip("ถ้าติดนานเกินนี้ (วินาที) จะข้ามจุดถัดไป")]
    [SerializeField] private float stuckTimeout = 2f;

    [Header("Player Interaction")]
    [Tooltip("ระยะที่ NPC หยุดเพื่อให้ player เข้ามาคุย")]
    [SerializeField] private float playerStopDistance = 1.8f;
    [Tooltip("รัศมีที่ NPC จะเริ่มเดินหา player เมื่อมี quest พร้อม")]
    [SerializeField] private float seekRadius = 6f;

    [Header("References (optional)")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Animator animator;

    private static readonly int AnimSpeed      = Animator.StringToHash("Speed");
    private static readonly int AnimHorizontal = Animator.StringToHash("Horizontal");
    private static readonly int AnimVertical   = Animator.StringToHash("Vertical");

    private Rigidbody2D rb;
    private Transform   playerTransform;

    // Patrol state
    private int   pointIndex;
    private float waitTimer;
    private bool  isWaiting;
    private Vector2 lastPos;
    private float   stuckTimer;

    // Shared
    private float   currentSpeed;
    private Vector2 velocity;
    private bool    questAvailable;
    private bool    hasAnimSpeed;
    private bool    hasAnimHorizontal;
    private bool    hasAnimVertical;

    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        ConfigureRigidbody();

        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (animator       == null) animator       = GetComponentInChildren<Animator>();

        if (animator != null)
            foreach (var p in animator.parameters)
            {
                if (p.type != AnimatorControllerParameterType.Float) continue;
                if (p.name == "Speed")      hasAnimSpeed      = true;
                if (p.name == "Horizontal") hasAnimHorizontal = true;
                if (p.name == "Vertical")   hasAnimVertical   = true;
            }
    }

    private void Start()
    {
        var playerGO = GameManager.instance?.player;
        if (playerGO != null) playerTransform = playerGO.transform;
        lastPos = rb.position;
    }

    /// <summary>เรียกจาก ARQuestNPC เมื่อสถานะ quest เปลี่ยน</summary>
    public void SetQuestAvailable(bool available)
    {
        questAvailable = available;
    }

    // ─────────────────────────────────────────────────────────────────────────

    private void FixedUpdate()
    {
        // หยุดเมื่อ player อยู่ในระยะ interact แล้ว
        if (IsPlayerNearby())
        {
            Decelerate();
            return;
        }

        // Seek mode — เดินหา player เมื่อมี quest พร้อมและ player เข้ามาในรัศมี
        if (questAvailable && IsPlayerInSeekRange())
        {
            SeekPlayer();
            return;
        }

        // Patrol mode
        if (path == null || path.Count < 2)
        {
            Decelerate();
            return;
        }

        if (isWaiting)
        {
            waitTimer -= Time.fixedDeltaTime;
            if (waitTimer <= 0f)
            {
                isWaiting  = false;
                pointIndex = (pointIndex + 1) % path.Count;
                stuckTimer = 0f;
            }
            Decelerate();
            return;
        }

        PatrolToCurrentPoint();
    }

    private void Update()
    {
        if (animator == null) return;

        float speed = velocity.magnitude;
        if (hasAnimSpeed) animator.SetFloat(AnimSpeed, speed);

        if (speed > 0.01f)
        {
            Vector2 dir = velocity.normalized;
            if (hasAnimHorizontal) animator.SetFloat(AnimHorizontal, dir.x);
            if (hasAnimVertical)   animator.SetFloat(AnimVertical,   dir.y);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Seek
    // ─────────────────────────────────────────────────────────────────────────

    private void SeekPlayer()
    {
        Vector2 current = rb.position;
        Vector2 target  = playerTransform.position;
        Vector2 delta   = target - current;
        float   dist    = delta.magnitude;

        // หยุดก่อนถึง player (เว้นที่ให้ player เข้ามา interact)
        float stopAt = playerStopDistance + 0.1f;
        if (dist <= stopAt)
        {
            Decelerate();
            return;
        }

        Vector2 dir  = delta / dist;
        float   step = Mathf.Min(dist - stopAt, GetSpeed() * Time.fixedDeltaTime);
        Vector2 next = current + dir * step;

        rb.MovePosition(next);
        velocity = (next - current) / Mathf.Max(0.0001f, Time.fixedDeltaTime);
        FlipSprite(dir.x);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Patrol
    // ─────────────────────────────────────────────────────────────────────────

    private void PatrolToCurrentPoint()
    {
        Vector3 target3 = path.GetPoint(pointIndex);
        Vector2 current = rb.position;
        Vector2 target  = new Vector2(target3.x, target3.y);
        Vector2 delta   = target - current;
        float   dist    = delta.magnitude;

        if (dist <= waypointReachDistance)
        {
            isWaiting  = true;
            waitTimer  = waypointWaitDuration;
            stuckTimer = 0f;
            lastPos    = current;
            Decelerate();
            return;
        }

        Vector2 dir  = delta / dist;
        float   step = Mathf.Min(dist, GetSpeed() * Time.fixedDeltaTime);
        Vector2 next = current + dir * step;

        rb.MovePosition(next);
        velocity = (next - current) / Mathf.Max(0.0001f, Time.fixedDeltaTime);
        FlipSprite(dir.x);

        // Stuck detection
        if (Vector2.Distance(current, lastPos) < 0.01f)
        {
            stuckTimer += Time.fixedDeltaTime;
            if (stuckTimer >= stuckTimeout)
            {
                stuckTimer = 0f;
                pointIndex = (pointIndex + 1) % path.Count;
            }
        }
        else
        {
            stuckTimer = 0f;
        }
        lastPos = current;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private float GetSpeed()
    {
        currentSpeed = Mathf.MoveTowards(currentSpeed, moveSpeed, acceleration * Time.fixedDeltaTime);
        return currentSpeed;
    }

    private void Decelerate()
    {
        currentSpeed = 0f;
        velocity     = Vector2.zero;
    }

    private void FlipSprite(float dirX)
    {
        if (spriteRenderer != null && Mathf.Abs(dirX) > 0.05f)
            spriteRenderer.flipX = dirX > 0f;
    }

    private bool IsPlayerNearby()
    {
        if (playerTransform == null) return false;
        return Vector2.Distance(transform.position, playerTransform.position) <= playerStopDistance;
    }

    private bool IsPlayerInSeekRange()
    {
        if (playerTransform == null) return false;
        return Vector2.Distance(transform.position, playerTransform.position) <= seekRadius;
    }

    private void ConfigureRigidbody()
    {
        rb.gravityScale           = 0f;
        rb.freezeRotation         = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.interpolation          = RigidbodyInterpolation2D.Interpolate;
        if (rb.bodyType != RigidbodyType2D.Kinematic)
            rb.bodyType = RigidbodyType2D.Kinematic;
    }

    private void OnDrawGizmosSelected()
    {
        // Seek radius — วงสีเหลือง
        Gizmos.color = new Color(1f, 0.9f, 0f, 0.25f);
        Gizmos.DrawSphere(transform.position, seekRadius);
        Gizmos.color = new Color(1f, 0.9f, 0f, 0.9f);
        DrawCircle(transform.position, seekRadius);

        // Stop distance — วงสีแดง
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.15f);
        Gizmos.DrawSphere(transform.position, playerStopDistance);
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.9f);
        DrawCircle(transform.position, playerStopDistance);
    }

    private static void DrawCircle(Vector3 center, float radius, int segments = 32)
    {
        float step = 360f / segments;
        Vector3 prev = center + new Vector3(radius, 0f, 0f);
        for (int i = 1; i <= segments; i++)
        {
            float rad = i * step * Mathf.Deg2Rad;
            Vector3 next = center + new Vector3(Mathf.Cos(rad) * radius, Mathf.Sin(rad) * radius, 0f);
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }
}
