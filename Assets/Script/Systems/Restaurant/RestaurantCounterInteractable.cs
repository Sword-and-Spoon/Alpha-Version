using UnityEngine;

public class RestaurantCounterInteractable : InteractableObject
{
    [Header("Counter UI Root")]
    [SerializeField] private GameObject counterUIRoot;

    [HideInInspector][SerializeField] private RestaurantCounter counter;
    [HideInInspector][SerializeField] private RestaurantCounterSlotUI counterSlotUI;
    [HideInInspector][SerializeField] private RestaurantCounterInventoryView counterInventoryView;

    // Legacy fields are kept to avoid breaking old scene data, but hidden to reduce inspector noise.
    [HideInInspector][SerializeField] private GameObject counterWindow;
    [HideInInspector][SerializeField] private GameObject inventoryWindow;

    private bool isOpen;

    protected override void Awake()
    {
        base.Awake();
        AutoWire();
    }

    private void Start()
    {
        // Re-wire after all Awake() calls complete — ensures CounterUICanvas exists in scene
        if (counterUIRoot == null)
        {
            ResolveUIRoot();
        }
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        AutoWire();
    }

    private void OnDisable()
    {
        if (isOpen)
        {
            SetWindowState(false);
        }
    }

    public override bool CanInteract()
    {
        if (counterUIRoot == null) ResolveUIRoot();
        bool can = counter != null && (counterUIRoot != null || counterWindow != null);
        if (!can)
        {
            // Debug.Log($"[CounterInteract] CanInteract=false: counter={counter!=null}, root={counterUIRoot!=null}, win={counterWindow!=null}");
            // Try one more time to resolve
            ResolveUIRoot();
            AutoWire();
            can = counter != null && (counterUIRoot != null || counterWindow != null);
        }
        return can;
    }

    public override void Interact()
    {
        if (counterUIRoot == null) ResolveUIRoot();
        bool willOpen = !isOpen;

        if (willOpen && UI_StateManager.Instance != null && !UI_StateManager.Instance.CanOpenInteractWindow())
        {
            return;
        }

        SetWindowState(willOpen);
    }

    public void CloseCounterWindow()
    {
        if (isOpen)
        {
            SetWindowState(false);
        }
    }

    private void ResolveUIRoot()
    {
        if (counterUIRoot == null)
        {
            // GameObject.Find misses inactive objects — use Resources.FindObjectsOfTypeAll instead
            foreach (Canvas canvas in Resources.FindObjectsOfTypeAll<Canvas>())
            {
                if (canvas.name == "CounterUICanvas" && canvas.gameObject.scene.IsValid() && canvas.gameObject.scene.isLoaded)
                {
                    counterUIRoot = canvas.gameObject;
                    break;
                }
            }
        }

        if (counterUIRoot == null)
        {
            var slotUI = FindObjectOfType<RestaurantCounterSlotUI>(true);
            if (slotUI != null)
            {
                Canvas canvas = slotUI.GetComponentInParent<Canvas>(true);
                counterUIRoot = canvas != null ? canvas.gameObject : slotUI.gameObject;
            }
        }

        if (counterUIRoot != null)
        {
            if (counterSlotUI == null)
                counterSlotUI = counterUIRoot.GetComponentInChildren<RestaurantCounterSlotUI>(true);
            if (counterInventoryView == null)
                counterInventoryView = counterUIRoot.GetComponentInChildren<RestaurantCounterInventoryView>(true);
        }
    }

    private void SetWindowState(bool open)
    {
        isOpen = open;

        if (counterUIRoot != null)
        {
            if (open)
            {
                CounterUIToggle toggle = counterUIRoot.GetComponent<CounterUIToggle>();
                if (toggle != null)
                {
                    toggle.BindOwner(this);
                }
            }

            counterUIRoot.SetActive(open);
        }

        if (counterUIRoot == null)
        {
            // Legacy fallback
            if (counterWindow != null)
            {
                counterWindow.SetActive(open);
            }

            if (inventoryWindow != null)
            {
                inventoryWindow.SetActive(open);
            }
        }

        if (counterSlotUI != null)
        {
            if (open)
            {
                counterSlotUI.Bind(counter);
            }
            else
            {
                counterSlotUI.Unbind();
            }
        }

        if (counterInventoryView != null)
        {
            if (open)
            {
                counterInventoryView.Bind();
            }
            else
            {
                counterInventoryView.Unbind();
            }
        }

        Time.timeScale = open ? 0f : 1f;

        if (UI_StateManager.Instance != null)
        {
            UI_StateManager.Instance.interactWindowOpen = open;
        }
    }

    private void AutoWire()
    {
        if (counter == null)
        {
            counter = GetComponent<RestaurantCounter>();
        }

        // Clear stale cross-scene reference (destroyed scene object)
        if (counterUIRoot != null && (!counterUIRoot.scene.IsValid() || !counterUIRoot.scene.isLoaded))
        {
            counterUIRoot = null;
        }

        if (counterUIRoot == null)
        {
            ResolveUIRoot();
        }

        if (counterSlotUI == null && counterUIRoot != null)
        {
            counterSlotUI = counterUIRoot.GetComponentInChildren<RestaurantCounterSlotUI>(true);
        }

        if (counterInventoryView == null && counterUIRoot != null)
        {
            counterInventoryView = counterUIRoot.GetComponentInChildren<RestaurantCounterInventoryView>(true);
        }
    }
}
