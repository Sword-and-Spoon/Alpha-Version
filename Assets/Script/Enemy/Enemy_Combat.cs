using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Enemy_Combat : MonoBehaviour
{
    public int damage = 1;
    public Transform attackPoint;
    public float attackRange = 0.8f;
    public LayerMask playerLayer;

    [Header("Attack Settings")]
    public float attackCooldown = 1.5f;
    public float knockbackForce = 3f;
    public float stunTime = 0.2f;

    [Header("Contact Damage (Optional)")]
    public bool useContactDamage = true;
    public float contactDamageCooldown = 0.5f;
    private float nextContactDamageTime;

    private float nextAttackTime;
    private Enemy_Movement enemyMovement;
    private Animator animator;

    private void Start()
    {
        enemyMovement = GetComponent<Enemy_Movement>();
        animator = GetComponent<Animator>();

        if (attackPoint == null)
        {
            // สร้างจุดโจมตีชั่วคราวถ้าไม่ได้ใส่มา
            GameObject go = new GameObject("AttackPoint");
            go.transform.SetParent(transform);
            go.transform.localPosition = Vector3.zero;
            attackPoint = go.transform;
        }
    }

    private void Update()
    {
        if (useContactDamage) TryDealContactDamage();
    }

    public bool CanAttack()
    {
        return Time.time >= nextAttackTime;
    }

    // ฟังก์ชันนี้จะถูกเรียกโดย Animation Event ชื่อ "DoAttack"
    public void DoAttack()
    {
        // ตรวจสอบตำแหน่งเป้าหมายตามที่ Animator กำลังหันหน้าไป
        float moveX = animator.GetFloat("MoveX");
        float moveY = animator.GetFloat("MoveY");
        Vector2 attackDirection = new Vector2(moveX, moveY).normalized;

        // อัปเดตตำแหน่ง AttackPoint ให้อยู่ด้านหน้ามอนสเตอร์ตามทิศทาง
        attackPoint.localPosition = (Vector3)attackDirection * 0.5f;

        Collider2D[] hits = Physics2D.OverlapCircleAll(attackPoint.position, attackRange, playerLayer);
        foreach (var hit in hits)
        {
            ApplyDamageAndKnockback(hit, knockbackForce, stunTime);
        }
    }

    // ฟังก์ชันนี้จะถูกเรียกโดย Animation Event ชื่อ "FinishAttack"
    public void FinishAttack()
    {
        nextAttackTime = Time.time + attackCooldown;
        if (enemyMovement != null)
        {
            enemyMovement.ChangeState(EnemyState.Chasing);
        }
    }

    private void TryDealContactDamage()
    {
        if (Time.time < nextContactDamageTime) return;

        Collider2D hit = Physics2D.OverlapCircle(transform.position, 0.4f, playerLayer);
        if (hit != null)
        {
            if (ApplyDamageAndKnockback(hit, 2f, 0.1f))
            {
                nextContactDamageTime = Time.time + contactDamageCooldown;
            }
        }
    }

    private bool ApplyDamageAndKnockback(Collider2D hitCollider, float force, float stun)
    {
        PlayerHealth playerHealth = hitCollider.GetComponentInParent<PlayerHealth>();
        if (playerHealth == null) return false;

        playerHealth.ChangeHealth(-damage);

        PlayerMovement playerMovement = hitCollider.GetComponentInParent<PlayerMovement>();
        if (playerMovement != null && force > 0f)
        {
            playerMovement.Knockback(transform, force, stun);
        }
        return true;
    }

    private void OnDrawGizmosSelected()
    {
        if (attackPoint != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(attackPoint.position, attackRange);
        }
    }
}
