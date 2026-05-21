using UnityEngine;
using UnityEngine.InputSystem;

public abstract class InteractableObject : MonoBehaviour
{
    protected Transform InteractParent;

    protected virtual void Awake()
    {
        if (InteractParent == null)
        {
            InteractParent = transform.Find("InteractParent");
            if (InteractParent != null)
                InteractParent.gameObject.SetActive(false);
        }
    }

    public abstract void Interact();
    public abstract bool CanInteract();

    public void ShowUI()
    {
        var session = GetComponent<CookingSession>();
        if (session != null && (session.isActive || session.isWaitingForPickup))
        {
            HideUI();
            return;
        }

        InteractParent?.gameObject.SetActive(true);
    }
    public void HideUI()
    {
        if (this == null || InteractParent == null) return;
        InteractParent.gameObject.SetActive(false);
    }

    public void openUI(GameObject ui)
    {
        if (ui == null)
        {
            Debug.LogError($"[InteractableObject] openUI called with null/destroyed UI on {name}");
            return;
        }

        if (UI_StateManager.Instance == null)
        {
            Debug.LogError($"[InteractableObject] UI_StateManager.Instance is null on {name}");
            return;
        }

        if (!UI_StateManager.Instance.CanOpenInteractWindow())
        {
            Debug.Log($"[InteractableObject] CanOpenInteractWindow = false on {name} (menu={UI_StateManager.Instance.menuOpen} journal={UI_StateManager.Instance.journalOpen} quest={UI_StateManager.Instance.questLogOpen})");
            return;
        }

        bool open = !UI_StateManager.Instance.interactWindowOpen;
        ui.SetActive(open);
        Time.timeScale = open ? 0f : 1f;
        UI_StateManager.Instance.interactWindowOpen = open;
    }
}
