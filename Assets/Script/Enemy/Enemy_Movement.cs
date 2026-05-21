using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class Enemy_Movement : MonoBehaviour
{
    public float speed;
    public float stopDistance = 0.1f;
    public float attackRange = 1.3f; // ระยะที่จะเริ่มหยุดโจมตี

    [Header("Intelligence")]
    public LayerMask obstacleLayer;
    [SerializeField] private float obstacleCheckDist = 0.6f;
    [SerializeField] private float axisSwitchThreshold = 0.2f;

    private EnemyState enemyState;
    private Rigidbody2D rb;
    private Transform player;
    private Animator animator;
    private Enemy_Combat enemyCombat;
    private PlayerSlotManager playerSlotManager;
    private PlayerSlotManager.Slot currentSlot;

    private Vector2 lastMoveDirection;

    // Throttle obstacle raycasts to 20 Hz instead of running every frame
    private Vector2 cachedMoveDir;
    private float nextPathCalcTime;
    private const float PathCalcInterval = 0.05f;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        enemyCombat = GetComponent<Enemy_Combat>();

        ChangeState(EnemyState.Idle);
    }

    void Update()
    {
        if (enemyState != EnemyState.Knockback && enemyState != EnemyState.Attacking)
        {
            if (enemyState == EnemyState.Chasing)
            {
                Chase();
            }
        }
    }

    void OnDisable() { ReleaseSlot(); }

    void Chase()
    {
        if (playerSlotManager == null)
        {
            if (player != null) playerSlotManager = player.GetComponent<PlayerSlotManager>();
        }

        if (currentSlot == null && playerSlotManager != null)
            currentSlot = playerSlotManager.RequestSlot(gameObject);

        if (player == null) return;

        float distanceToPlayer = Vector2.Distance(transform.position, player.position);

        if (distanceToPlayer <= attackRange)
        {
            // หยุดการเคลื่อนที่ทันที
            rb.velocity = Vector2.zero;
            UpdateAnimation(Vector2.zero);
            FaceTarget(player.position);

            // ถ้าคูลดาวน์โจมตีพร้อม ให้โจมตี
            if (enemyCombat != null && enemyCombat.CanAttack())
            {
                ChangeState(EnemyState.Attacking);
            }
            return; // ออกจากฟังก์ชันทันทีเพื่อไม่ให้ไปรันลอจิกเดินด้านล่าง
        }
        // ----------------------------------------

        Vector3 targetPos;
        float actualStopDist = stopDistance;

        if (currentSlot != null)
        {
            targetPos = currentSlot.GetWorldPosition(player);
        }
        else
        {
            targetPos = player.position;
            actualStopDist = 2.5f;
        }

        float distanceToTarget = Vector2.Distance(transform.position, targetPos);

        if (distanceToTarget <= actualStopDist)
        {
            rb.velocity = Vector2.zero;
            UpdateAnimation(Vector2.zero);
            FaceTarget(player.position);
            return;
        }

        // Throttle raycast pathfinding to 20 Hz — runs at most once every 50 ms
        if (Time.time >= nextPathCalcTime)
        {
            cachedMoveDir = CalculateSmarterMovement(targetPos);
            nextPathCalcTime = Time.time + PathCalcInterval;
        }
        Vector2 direction = cachedMoveDir;

        if (rb.bodyType == RigidbodyType2D.Kinematic)
        {
            rb.MovePosition(rb.position + direction * speed * Time.deltaTime);
        }
        else
        {
            rb.velocity = direction * speed;
        }

        UpdateAnimation(direction);
        lastMoveDirection = direction;
    }

    Vector2 CalculateSmarterMovement(Vector3 targetPos)
    {
        Vector2 diff = targetPos - transform.position;
        float absX = Mathf.Abs(diff.x);
        float absY = Mathf.Abs(diff.y);
        Vector2 primaryDir, secondaryDir;

        bool preferX = (lastMoveDirection.x != 0) ? (absX > absY - axisSwitchThreshold) : (absX > absY + axisSwitchThreshold);
        if (preferX) { primaryDir = new Vector2(Mathf.Sign(diff.x), 0); secondaryDir = new Vector2(0, Mathf.Sign(diff.y)); }
        else { primaryDir = new Vector2(0, Mathf.Sign(diff.y)); secondaryDir = new Vector2(Mathf.Sign(diff.x), 0); }

        if (IsPathBlocked(primaryDir))
        {
            if (!IsPathBlocked(secondaryDir)) return secondaryDir;
            return Vector2.zero;
        }
        return primaryDir;
    }

    bool IsPathBlocked(Vector2 direction)
    {
        if (direction == Vector2.zero) return false;
        RaycastHit2D hit = Physics2D.Raycast(transform.position, direction, obstacleCheckDist, obstacleLayer);
        Vector2 sideOffset = new Vector2(-direction.y, direction.x) * 0.3f;
        RaycastHit2D hitL = Physics2D.Raycast((Vector2)transform.position + sideOffset, direction, obstacleCheckDist, obstacleLayer);
        RaycastHit2D hitR = Physics2D.Raycast((Vector2)transform.position - sideOffset, direction, obstacleCheckDist, obstacleLayer);
        return hit.collider != null || hitL.collider != null || hitR.collider != null;
    }

    void UpdateAnimation(Vector2 moveDir)
    {
        if (moveDir != Vector2.zero)
        {
            animator.SetFloat("MoveX", moveDir.x);
            animator.SetFloat("MoveY", moveDir.y);
            animator.SetBool("isChasing", true);
            animator.SetBool("isIdle", false);
        }
        else
        {
            animator.SetBool("isChasing", false);
            animator.SetBool("isIdle", true);
        }
    }

    void FaceTarget(Vector3 targetPos)
    {
        Vector2 diff = targetPos - transform.position;
        if (Mathf.Abs(diff.x) > Mathf.Abs(diff.y))
        {
            animator.SetFloat("MoveX", Mathf.Sign(diff.x));
            animator.SetFloat("MoveY", 0);
        }
        else
        {
            animator.SetFloat("MoveX", 0);
            animator.SetFloat("MoveY", Mathf.Sign(diff.y));
        }
    }

    private void ReleaseSlot()
    {
        if (currentSlot != null && playerSlotManager != null)
        {
            playerSlotManager.ReleaseSlot(gameObject);
            currentSlot = null;
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            if (player == null) player = collision.transform;
            ChangeState(EnemyState.Chasing);
        }
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            if (enemyState != EnemyState.Chasing && enemyState != EnemyState.Knockback && enemyState != EnemyState.Attacking)
            {
                ChangeState(EnemyState.Chasing);
            }
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            rb.velocity = Vector2.zero;
            ChangeState(EnemyState.Idle);
        }
    }

    public void ChangeState(EnemyState newState)
    {
        if (enemyState == newState) return;
        if (enemyState == EnemyState.Chasing && newState != EnemyState.Chasing && newState != EnemyState.Attacking) ReleaseSlot();

        enemyState = newState;

        if (rb != null)
        {
            if (enemyState == EnemyState.Knockback)
            {
                rb.bodyType = RigidbodyType2D.Dynamic;
            }
            else
            {
                rb.bodyType = RigidbodyType2D.Kinematic;
                rb.velocity = Vector2.zero; // Clear velocity when moving back to Kinematic
            }
        }

        if (enemyState == EnemyState.Idle)
        {
            rb.velocity = Vector2.zero;
            animator.SetBool("isIdle", true);
            animator.SetBool("isChasing", false);
        }
        else if (enemyState == EnemyState.Attacking)
        {
            rb.velocity = Vector2.zero;
            FaceTarget(player.position);
            animator.SetTrigger("attack");
            animator.SetBool("isChasing", false);
            animator.SetBool("isIdle", false);
        }
    }

    public EnemyState GetCurrentState() => enemyState;

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawRay(transform.position, lastMoveDirection * obstacleCheckDist);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}

public enum EnemyState
{
    Idle,
    Chasing,
    Knockback,
    Attacking
}
