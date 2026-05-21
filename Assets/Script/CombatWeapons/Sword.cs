using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Sword : MonoBehaviour, IWeapon
{
    public Animator animator;
    public Transform circleOrigin;
    public float radius = 0.8f;
    public LayerMask enemyLayer;
    public float cooldown = 0.6f;
    private bool blocked;
    public bool IsAttacking { get; private set; }

    [Header("Weapon Stats")]
    public int baseDamage = 1;
    [Range(0f, 100f)] public float criticalChance = 10f;
    public float criticalMultiplier = 2f;

    public float knockbackForce = 100f;
    public float knockbackTime = 0.5f;
    public float stunTime = 1f;

    private void Awake()
    {
        circleOrigin = transform.parent;
    }

    public bool CanAttack()
    {
        return !blocked && !IsAttacking;
    }

    public void Attack()
    {
        if (!CanAttack()) return;
        blocked = true;
        IsAttacking = true;

        // Only trigger if an animator and controller are assigned to the sword itself
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            animator.SetTrigger("Attack");
        }

        Invoke(nameof(Unblock), cooldown);
    }

    public void ResetIsAttacking()
    {
        IsAttacking = false;
    }

    private void Unblock() => blocked = false;

    private readonly Collider2D[] _hitBuffer = new Collider2D[16];

    public void HitFrame()
    {
        if (!IsAttacking)
        {
            Debug.LogWarning("HitFrame called when sword was not in attacking state!");
            return;
        }

        int hitCount = Physics2D.OverlapCircleNonAlloc(circleOrigin.position, radius, _hitBuffer, enemyLayer);
        for (int i = 0; i < hitCount; i++)
        {
            var collider = _hitBuffer[i];
            if (collider == null || collider.isTrigger) continue;

            if (collider.TryGetComponent(out Health health))
            {
                // Calculate Damage and Critical
                bool isCritical = Random.Range(0f, 100f) <= criticalChance;
                int finalDamage = baseDamage;
                DamageType type = DamageType.Normal;

                if (isCritical)
                {
                    finalDamage = Mathf.RoundToInt(baseDamage * criticalMultiplier);
                    type = DamageType.Critical;
                }

                health.GetHit(finalDamage, gameObject, type);
            }
            _hitBuffer[i] = null; // Clear the buffer reference
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(circleOrigin.position, radius);
    }
}
