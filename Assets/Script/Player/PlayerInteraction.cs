using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

public class PlayerInteraction : MonoBehaviour
{
    private static readonly int AttackHash = Animator.StringToHash("Attack");

    [SerializeField] private InputActionReference leftClick, interactF, pressedE, pressedJ, scroll, numberKeys;
    [SerializeField] private float interactDistance = 2f;
    [SerializeField] private LayerMask interactLayer;
    [SerializeField] private Vector2 rayOffset = new Vector2(0, 0.5f);

    private PlayerMovement playerMovement;
    private InteractableObject currentTarget;
    private MenuController menuController;
    private PlayerItemHandler playerItemHandler;
    private HotbarController hotbarController;
    private JournalController journalController;
    private bool pointerOverUI;
    private Animator playerAnimator;

    private void Awake()
    {
        RefreshReferences();
    }

    private void RefreshReferences()
    {
        menuController = FindObjectOfType<MenuController>();
        playerItemHandler = GetComponentInChildren<PlayerItemHandler>(true);
        hotbarController = FindObjectOfType<HotbarController>();
        journalController = FindObjectOfType<JournalController>();
        playerAnimator = GetComponent<Animator>();
        playerMovement = GetComponent<PlayerMovement>();
    }

    private void Update()
    {
        CheckForInteractable();
        pointerOverUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        if (hotbarController == null) RefreshReferences();

        // Q = toggle เปิด/ปิดหน้าเมนูที่แท็บ Quest
        if (Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame)
        {
            if (menuController == null) RefreshReferences();
            menuController?.ToggleQuestTab();
        }
    }

    private void OnEnable()
    {
        interactF.action.performed += OnInteractF;
        pressedE.action.performed += OnInteractE;
        // pressedJ.action.performed += OnInteractJ;
        leftClick.action.performed += onLeftClick;
        numberKeys.action.performed += onNumberKey;
        scroll.action.performed += onScroll;
    }
    private void OnDisable()
    {
        interactF.action.performed -= OnInteractF;
        pressedE.action.performed -= OnInteractE;
        // pressedJ.action.performed -= OnInteractJ;
        leftClick.action.performed -= onLeftClick;
        numberKeys.action.performed -= onNumberKey;
        scroll.action.performed -= onScroll;
    }

    private void CheckForInteractable()
    {
        if (playerMovement == null) return;
        Vector2 facingDir = playerMovement.GetPlayerFacingDirection();
        Vector2 rayStartPoint = (Vector2)transform.position + rayOffset;
        RaycastHit2D hit = Physics2D.Raycast(rayStartPoint, facingDir, interactDistance, interactLayer);

        if (hit.collider != null)
        {
            InteractableObject interactable = hit.collider.GetComponent<InteractableObject>();
            if (interactable != null && interactable.CanInteract())
            {
                if (interactable != currentTarget)
                {
                    if (currentTarget != null) currentTarget.HideUI();
                    currentTarget = interactable;
                    currentTarget.ShowUI();
                }
                return;
            }
        }
        if (currentTarget != null)
        {
            currentTarget.HideUI();
            currentTarget = null;
        }
    }

    private void OnInteractF(InputAction.CallbackContext obj)
    {
        var panel = NPCDialoguePanel.Instance;
        if (panel != null && panel.gameObject.activeSelf)
        {
            panel.OnOKClicked();
            return;
        }

        if (currentTarget == null)
        {
            return;
        }

        if (TutorialManager.Instance != null
            && !TutorialManager.Instance.CanInteractWith(currentTarget, out string blockedReason))
        {
            TutorialManager.Instance.ShowBlockedMessage(blockedReason, currentTarget.transform.position);
            return;
        }

        currentTarget.Interact();
    }

    private void OnInteractE(InputAction.CallbackContext obj)
    {
        if (menuController == null) RefreshReferences();
        menuController?.ToggleMenu();
    }

    private void OnInteractJ(InputAction.CallbackContext obj)
    {
        if (journalController == null) RefreshReferences();
        journalController?.Toggle();
    }

    private void onLeftClick(InputAction.CallbackContext obj)
    {
        if (pointerOverUI) return;
        if (playerItemHandler == null) RefreshReferences();
        playerItemHandler?.Use();
    }

    private void onNumberKey(InputAction.CallbackContext obj)
    {
        if (hotbarController == null) RefreshReferences();
        hotbarController?.OnNumberSelect(obj);
    }

    private void onScroll(InputAction.CallbackContext obj)
    {
        if (hotbarController == null) RefreshReferences();
        hotbarController?.OnScroll(obj);
    }

    public void AnimationHit() => playerItemHandler?.AnimationHit();
    public void AnimationStop() => playerItemHandler?.AnimationStop();

    public InteractableObject GetTargetInteractable() => currentTarget;
}
