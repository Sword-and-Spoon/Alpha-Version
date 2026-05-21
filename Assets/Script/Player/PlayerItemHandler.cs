using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerItemHandler : MonoBehaviour
{
    private const string ToolModeParam = "ToolMode";

    [Header("Item Visual Settings")]
    public Vector3 sideOffsetRight = new Vector3(0.5f, -0.1f, 0);
    public Vector3 sideOffsetLeft = new Vector3(-0.1f, -0.1f, 0);
    public Vector3 middle = new Vector3(0.2f, -0.1f, 0f);
    public Vector3 upOffset = new Vector3(0.2f, -0.1f, 0);
    public int frontSortingOrder = 11;
    public int backSortingOrder = 9;

    public GameObject handItemTemplate;
    public Transform handItemHolder; // for item that just sits in hand
    public Transform pivot; // for item that need mouse position aiming


    [Header("Melee Fallback (Animation Override)")]
    [SerializeField] private Transform fallbackAttackOrigin;
    [SerializeField] private float fallbackAttackRadius = 0.8f;
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private int fallbackDamage = 1;
    [SerializeField, Range(0, 100)] private float fallbackCriticalChance = 10f;
    [SerializeField] private float fallbackCriticalMultiplier = 2f;

    private SpriteRenderer handSprite;
    private Item equippedItem; // keep full Item so we know quality when consuming

    private IWeapon equippedWeapon;
    private GameObject spawnedWeapon;
    private Animator playerAnimator;
    [SerializeField] private float fallbackAttackUnlockDelay = 0.6f;
    [SerializeField] private float maxAttackUnlockDelay = 1.25f;
    [SerializeField] private float fallbackHitDelay = 0.12f;
    [SerializeField] private float maxHitDelay = 0.5f;
    private bool hasToolModeParam;
    private bool warnedMissingToolMode;
    private Coroutine stopAttackCoroutine;
    private Coroutine attackHitFallbackCoroutine;
    private bool hitAppliedThisAttack;

    private bool canFallbackAttack = true; // Added for fallback cooldown tracking

    public ToolType CurrentToolMode { get; private set; } = ToolType.None;

    private PlayerHealth playerHealth;
    private InventoryController inventoryController;
    private PlayerMovement playerMovement;

    private void Awake()
    {
        RefreshReferences();
        playerAnimator = GetComponentInParent<Animator>();
        playerMovement = GetComponentInParent<PlayerMovement>();

        if (fallbackAttackOrigin == null)
        {
            fallbackAttackOrigin = pivot != null ? pivot : transform;
        }

        if (handItemHolder == null)
        {
            Transform root = transform.root;
            Transform[] allChildren = root.GetComponentsInChildren<Transform>(true);
            foreach (Transform t in allChildren)
            {
                string n = t.name.ToLower().Replace(" ", "").Replace("_", "");
                if (n == "handitemholder" || n == "handpoint")
                {
                    handItemHolder = t;
                    break;
                }
            }
        }

        if (pivot == null)
        {
            Transform root = transform.root;
            Transform[] allChildren = root.GetComponentsInChildren<Transform>(true);
            foreach (Transform t in allChildren)
            {
                string n = t.name.ToLower().Replace(" ", "").Replace("_", "");
                if (n == "pivot" || n == "weaponpivot")
                {
                    pivot = t;
                    break;
                }
            }
        }

        hasToolModeParam = HasAnimatorParameter(playerAnimator, ToolModeParam, AnimatorControllerParameterType.Int);

        if (handItemTemplate != null && handItemHolder != null)
        {
            if (!Application.isPlaying || !gameObject.scene.IsValid()) return;
            if (!handItemHolder.gameObject.scene.IsValid())
            {
                Debug.LogWarning("[PlayerItemHandler] handItemHolder resides in a Prefab Asset, cannot be a parent at runtime.");
                return;
            }

            GameObject obj = Instantiate(handItemTemplate);
            obj.transform.SetParent(handItemHolder, false);
            handSprite = obj.GetComponent<SpriteRenderer>();
            handSprite.enabled = false;
        }
        else
        {
            if (handItemTemplate == null) Debug.LogWarning("[PlayerItemHandler] handItemTemplate is missing!");
            if (handItemHolder == null) Debug.LogWarning("[PlayerItemHandler] handItemHolder is missing! Make sure 'Hand Item Holder' is assigned or exists as a child.");
        }
    }

    private void RefreshReferences()
    {
        playerHealth = GetComponent<PlayerHealth>();
        inventoryController = FindObjectOfType<InventoryController>();
    }

    public void Equip(Item item)
    {
        // store a copy of the item (with quality) so we can reference later
        equippedItem = item != null ? new Item(item) : null;
        ItemSO itemSO = equippedItem != null ? equippedItem.itemSO : null;
        bool isWeapon = itemSO != null && itemSO.itemType == ItemType.Weapon;

        if (spawnedWeapon != null) Destroy(spawnedWeapon);

        if (itemSO == null)
        {
            if (handSprite != null) handSprite.enabled = false;
            equippedWeapon = null;
            SetToolMode(ToolType.None);
            return;
        }

        SetToolMode(itemSO.toolType);

        // Debug.Log($"[PlayerItemHandler] Equipping item: {itemSO.name} (quality={equippedItem.quality}) on {gameObject.name}");

        if (handSprite != null)
        {
            handSprite.sprite = itemSO.icon;
            handSprite.enabled = !isWeapon;
        }

        if (isWeapon && itemSO.weaponPrefab != null)
        {
            if (handSprite != null) handSprite.enabled = false;

            if (pivot != null && pivot.gameObject.scene.IsValid())
            {
                spawnedWeapon = Instantiate(itemSO.weaponPrefab);
                spawnedWeapon.transform.SetParent(pivot, false);
                equippedWeapon = spawnedWeapon.GetComponent<IWeapon>();
                Debug.Log($"[PlayerItemHandler] Spawned weapon prefab: {itemSO.weaponPrefab.name}");
            }
            else
            {
                Debug.LogWarning("[PlayerItemHandler] Cannot spawn weapon because pivot is missing or resides in a Prefab Asset.");
            }
        }
        else
        {
            equippedWeapon = null;
        }
    }

    public void Use()
    {
        if (Mathf.Approximately(Time.timeScale, 0f)) return;
        if (equippedItem == null || equippedItem.itemSO == null) return;

        switch (equippedItem.itemSO.itemType)
        {
            case ItemType.Weapon:
                Attack();
                break;
            case ItemType.Tool:
                ToolAction();
                break;
            case ItemType.Consumable:
                // Consume();
                break;
        }
    }

    // Called by Animation Event on the Player Animator
    public void AnimationHit()
    {
        if (hitAppliedThisAttack) return;

        hitAppliedThisAttack = true;
        if (equippedWeapon != null)
        {
            equippedWeapon.HitFrame();
            return;
        }

        PerformFallbackMeleeHit();
    }

    // Called by Animation Event on the Player Animator (at the end of attack clip)
    public void AnimationStop()
    {
        StopAttack();
    }

    private void Attack()
    {
        // Check if weapon can attack before playing animation
        if (equippedWeapon != null && !equippedWeapon.CanAttack()) return;
        if (equippedWeapon == null && !canFallbackAttack) return;

        hitAppliedThisAttack = false;

        if (playerAnimator != null)
        {
            if (playerMovement != null) playerMovement.SetAttacking(true);
            playerAnimator.SetTrigger("Attack");
            ScheduleStopAttackFallback();
            ScheduleHitFallback();

            if (equippedWeapon == null) canFallbackAttack = false;
        }
        else
        {
            Debug.LogError("[PlayerItemHandler] Cannot trigger Attack: playerAnimator is null!");
            TryApplyAttackHit();
        }

        if (equippedWeapon != null)
        {
            equippedWeapon.Attack();
        }
    }

    public void StopAttack()
    {
        // Debug.Log("[PlayerItemHandler] StopAttack called. Re-enabling movement.");
        if (playerMovement != null) playerMovement.SetAttacking(false);
        if (stopAttackCoroutine != null)
        {
            StopCoroutine(stopAttackCoroutine);
            stopAttackCoroutine = null;
        }

        if (attackHitFallbackCoroutine != null)
        {
            StopCoroutine(attackHitFallbackCoroutine);
            attackHitFallbackCoroutine = null;
        }

        hitAppliedThisAttack = false;
        canFallbackAttack = true;
        if (equippedWeapon != null) equippedWeapon.ResetIsAttacking();
    }

    private void PerformFallbackMeleeHit()
    {
        Transform attackerTransform = playerMovement != null ? playerMovement.transform : transform;
        Vector3 attackCenter = fallbackAttackOrigin != null ? fallbackAttackOrigin.position : attackerTransform.position;

        foreach (var collider in Physics2D.OverlapCircleAll(attackCenter, fallbackAttackRadius, enemyLayer))
        {
            if (collider == null || collider.isTrigger) continue;

            if (collider.TryGetComponent(out Health health))
            {
                // Calculate Fallback Damage and Critical
                bool isCritical = Random.Range(0f, 100f) <= fallbackCriticalChance;
                int finalDamage = fallbackDamage;
                DamageType type = DamageType.Normal;

                if (isCritical)
                {
                    finalDamage = Mathf.RoundToInt(fallbackDamage * fallbackCriticalMultiplier);
                    type = DamageType.Critical;
                }

                health.GetHit(finalDamage, gameObject, type);
            }
        }
    }

    private void ToolAction()
    {
        // Debug.Log("[PlayerItemHandler] Tool action triggered");
        hitAppliedThisAttack = false;

        if (playerAnimator != null)
        {
            if (playerMovement != null) playerMovement.SetAttacking(true);
            playerAnimator.SetTrigger("Attack");
            ScheduleStopAttackFallback();
            ScheduleHitFallback();
        }
        else
        {
            TryApplyAttackHit();
        }
    }

    private void ScheduleHitFallback()
    {
        if (attackHitFallbackCoroutine != null)
        {
            StopCoroutine(attackHitFallbackCoroutine);
        }

        float delay = Mathf.Clamp(fallbackHitDelay, 0f, maxHitDelay);
        attackHitFallbackCoroutine = StartCoroutine(ApplyHitAfterDelay(delay));
    }

    private IEnumerator ApplyHitAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        TryApplyAttackHit();
        attackHitFallbackCoroutine = null;
    }

    private void TryApplyAttackHit()
    {
        if (hitAppliedThisAttack) return;
        hitAppliedThisAttack = true;

        if (equippedWeapon != null)
        {
            equippedWeapon.HitFrame();
            return;
        }

        PerformFallbackMeleeHit();
    }

    private void ScheduleStopAttackFallback()
    {
        if (stopAttackCoroutine != null)
        {
            StopCoroutine(stopAttackCoroutine);
        }

        float delay = GetAttackUnlockDelay();
        stopAttackCoroutine = StartCoroutine(StopAttackAfterDelay(delay));
    }

    private IEnumerator StopAttackAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        StopAttack();
    }

    private float GetAttackUnlockDelay()
    {
        float maxAttackClipLength = 0f;

        if (playerAnimator != null && playerAnimator.runtimeAnimatorController != null)
        {
            foreach (AnimationClip clip in playerAnimator.runtimeAnimatorController.animationClips)
            {
                if (clip == null) continue;
                if (!clip.name.Contains("Attack")) continue;

                if (clip.length > maxAttackClipLength)
                {
                    maxAttackClipLength = clip.length;
                }
            }
        }

        float selectedDelay = maxAttackClipLength > 0f ? maxAttackClipLength : fallbackAttackUnlockDelay;
        return Mathf.Clamp(selectedDelay, 0.05f, maxAttackUnlockDelay);
    }

    private void Consume()
    {
        if (playerHealth == null) RefreshReferences();
        if (inventoryController == null) RefreshReferences();
        if (equippedItem == null || equippedItem.itemSO == null) return;

        int healthToRestore = ItemSO.GetHealthRestoreFromQuality(equippedItem.itemSO, equippedItem.quality);
        Debug.Log($"Consume item: {equippedItem.itemSO.GetDisplayName()} (quality={equippedItem.quality}) restore {healthToRestore} health");
        if (playerHealth != null) playerHealth.Heal(healthToRestore);

        // remove one of this quality from inventory
        if (inventoryController != null)
        {
            inventoryController.RemoveItem(new Item(equippedItem.itemSO, 1, equippedItem.quality));
            ARQuestManager.Instance?.NotifyItemRemoved(equippedItem.itemSO.itemId, 1);
        }
    }

    public void Unequip()
    {
        equippedItem = null;
        if (handSprite != null) handSprite.enabled = false;
        if (spawnedWeapon != null) Destroy(spawnedWeapon);
        equippedWeapon = null;
        SetToolMode(ToolType.None);
    }

    private void SetToolMode(ToolType toolType)
    {
        CurrentToolMode = toolType;
        if (playerAnimator != null && hasToolModeParam)
        {
            // Player.controller has dedicated states per weapon (Attack_Sword, etc.)
            // driven by this parameter — no runtime controller swap needed.
            playerAnimator.SetInteger(ToolModeParam, (int)toolType);
        }
    }

    private bool HasAnimatorParameter(Animator animator, string parameterName, AnimatorControllerParameterType parameterType)
    {
        if (animator == null) return false;

        foreach (var parameter in animator.parameters)
        {
            if (parameter.name == parameterName && parameter.type == parameterType)
            {
                return true;
            }
        }

        return false;
    }

    public void HandleItemVisuals(Vector2 movement)
    {
        if (handSprite == null) return;

        Transform itemTransform = handSprite.transform.parent;

        if (itemTransform == null)
        {
            Debug.LogWarning("[PlayerItemHandler] itemTransform is null! Make sure 'Hand Item Holder' is assigned in the Inspector.");
            return;
        }

        if (spawnedWeapon != null)
        {
            itemTransform.localPosition = Vector3.zero;
            return;
        }

        if (movement.x > 0.1f) // Moving Right
        {
            itemTransform.localPosition = sideOffsetRight;
        }
        else if (movement.x < -0.1f) // Moving Left
        {
            itemTransform.localPosition = sideOffsetLeft;
        }
        else if (movement.y > 0.1f) // Moving Up
        {
            itemTransform.localPosition = upOffset;
        }
        else // Idle or Moving Down
        {
            itemTransform.localPosition = middle;
        }

        if (movement.y > 0.1f) // Moving Up (Item -> BEHIND)
        {
            handSprite.sortingOrder = frontSortingOrder;
        }
        else // Moving Down or Sideways (Item -> FRONT)
        {
            handSprite.sortingOrder = frontSortingOrder;
        }
    }
}
