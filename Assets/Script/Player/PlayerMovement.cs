using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    private const string ToolModeParam = "ToolMode";
    private const string MoveXParam = "MoveX";
    private const string MoveYParam = "MoveY";

    [SerializeField]
    private InputActionReference movement, pointerPosition;

    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float lungeForce = 0f;

    public Rigidbody2D rb;
    public Animator animator;
    [SerializeField] private SpriteRenderer spriteRenderer;
    private Vector2 pointerInput, movementInput;
    private float lastMoveX = 0f;
    private float lastMoveY = -1f;

    private bool isKnockedBack;
    private bool isAttacking;

    private PlayerItemHandler itemHandler;
    private bool hasMoveXParam;
    private bool hasMoveYParam;
    private bool hasToolModeParam;
    private bool warnedMissingParams;
    private bool hasNotifiedFirstMove;
    private Camera mainCamera;

    public static event Action OnPlayerFirstMove;

    private void Awake()
    {
        mainCamera = Camera.main;
        itemHandler = FindObjectOfType<PlayerItemHandler>().GetComponent<PlayerItemHandler>();
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }
    }

    void Update()
    {
        if (Mathf.Approximately(Time.timeScale, 0f))
        {
            movementInput = Vector2.zero;
            return;
        }

        if (isAttacking)
        {
            movementInput = Vector2.zero;
            UpdateAnimator();
            return;
        }

        pointerInput = GetPointerInput();
        Vector2 rawInput = movement.action.ReadValue<Vector2>();

        // Restrict to 4 directions: Prioritize the axis with the higher absolute value
        if (rawInput.sqrMagnitude > 0.001f)
        {
            if (Mathf.Abs(rawInput.x) > Mathf.Abs(rawInput.y))
            {
                movementInput = new Vector2(rawInput.x > 0 ? 1 : -1, 0);
            }
            else
            {
                movementInput = new Vector2(0, rawInput.y > 0 ? 1 : -1);
            }

            lastMoveX = movementInput.x;
            lastMoveY = movementInput.y;

            if (!hasNotifiedFirstMove)
            {
                hasNotifiedFirstMove = true;
                OnPlayerFirstMove?.Invoke();
            }
        }
        else
        {
            movementInput = Vector2.zero;
        }

        UpdateAnimator();

        if (itemHandler != null)
        {
            itemHandler.HandleItemVisuals(movementInput);
        }
    }

    private void UpdateAnimator()
    {
        // Logic for Flipping: If moving right, flip the sprite and tell animator we are moving "left" (-1)
        float animatorHorizontal = lastMoveX;
        if (spriteRenderer != null)
        {
            bool isMovingRight = lastMoveX > 0.001f;
            spriteRenderer.flipX = isMovingRight;

            if (isMovingRight)
            {
                animatorHorizontal = -1f; // Force use of Left animation when moving Right
            }
        }

        // Send values to Animator
        animator.SetFloat("Horizontal", animatorHorizontal);
        animator.SetFloat("Vertical", lastMoveY);
        animator.SetFloat("Speed", movementInput.sqrMagnitude);

        // ToolMode is set by PlayerItemHandler.SetToolMode() on equip/unequip.
    }

    void FixedUpdate()
    {
        if (isKnockedBack || isAttacking) return;
        rb.MovePosition(rb.position + movementInput * moveSpeed * Time.fixedDeltaTime);
    }

    public void SetAttacking(bool attacking)
    {
        isAttacking = attacking;
        if (attacking)
        {
            movementInput = Vector2.zero;

            // Perform Lunge: apply a burst of force in the last facing direction
            Vector2 lungeDirection = new Vector2(lastMoveX, lastMoveY).normalized;
            rb.velocity = Vector2.zero; // Clear existing velocity for consistent lunge
            rb.AddForce(lungeDirection * lungeForce, ForceMode2D.Impulse);
        }
        else
        {
            rb.velocity = Vector2.zero; // Stop the lunge momentum when attack finishes
        }
    }

    // New method for Animation Events
    public void StopAttack()
    {
        SetAttacking(false);
    }

    private Vector2 GetPointerInput()
    {
        if (mainCamera == null) mainCamera = Camera.main;
        Vector3 mousePos = pointerPosition.action.ReadValue<Vector2>();
        mousePos.z = mainCamera.nearClipPlane;
        return mainCamera.ScreenToWorldPoint(mousePos);
    }

    public void Knockback(Transform enemy, float force, float stunTime)
    {
        isKnockedBack = true;
        Vector2 direction = (transform.position - enemy.position).normalized;
        rb.velocity = direction * force;
        StartCoroutine(KnockbackCounter(stunTime));
    }

    IEnumerator KnockbackCounter(float stunTime)
    {
        yield return new WaitForSeconds(stunTime);
        rb.velocity = Vector2.zero;
        isKnockedBack = false;
    }

    private bool HasAnimatorParameter(Animator targetAnimator, string parameterName, AnimatorControllerParameterType parameterType)
    {
        if (targetAnimator == null) return false;

        foreach (var parameter in targetAnimator.parameters)
        {
            if (parameter.name == parameterName && parameter.type == parameterType)
            {
                return true;
            }
        }

        return false;
    }

    public Vector2 GetPlayerFacingDirection()
    {
        float x = animator.GetFloat("Horizontal");
        float y = animator.GetFloat("Vertical");

        if (spriteRenderer != null && spriteRenderer.flipX && Mathf.Approximately(x, -1f))
        {
            x = 1f;
        }

        Vector2 dir = new Vector2(x, y);

        if (dir.sqrMagnitude < 0.001f)
        {
            dir = new Vector2(lastMoveX, lastMoveY);
        }

        return dir.normalized;
    }
}
