using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class Health : MonoBehaviour
{
    [SerializeField]
    private int currentHealth, maxHealth;

    public UnityEvent<GameObject> OnHitWithReference, OnDeathWithReference;

    [SerializeField]
    private bool isDead = false;

    Enemy_Drop enemyDrop;

    private void Start()
    {
        enemyDrop = GetComponent<Enemy_Drop>();
    }

    public void InitializeHealth(int healthValue)
    {
        currentHealth = healthValue;
        maxHealth = healthValue;
        isDead = false;
    }

    public void GetHit(int damage, GameObject sender, DamageType damageType = DamageType.Normal)
    {
        if (isDead) return;
        if (sender.layer == gameObject.layer) return;

        currentHealth -= damage;

        // Show Damage Feedback
        if (DamagePopupManager.Instance != null)
        {
            DamagePopupManager.Instance.CreatePopup(transform.position, damage.ToString(), damageType);
        }

        // Hit Flash Effect
        if (TryGetComponent(out HitFlash hitFlash))
        {
            hitFlash.Flash();
        }

        if (currentHealth > 0)
        {
            OnHitWithReference?.Invoke(sender);
        }
        else
        {
            OnDeathWithReference?.Invoke(sender);
            isDead = true;
            Destroy(gameObject);

            // Trigger item drop on death
            if (enemyDrop)
            {
                enemyDrop.DropItem();
            }
        }
    }

}
