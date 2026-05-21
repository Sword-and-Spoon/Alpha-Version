using TMPro;
using UnityEngine;

public class RestaurantOpenCloseInteractable : InteractableObject
{
    [Header("References")]
    [SerializeField] private RestaurantServiceManager serviceManager;
    [SerializeField] private TMP_Text actionText;
    [SerializeField] private TMP_Text signText;

    [Header("Prompt")]
    [SerializeField] private string openPrompt = "Open";
    [SerializeField] private string closePrompt = "Close";
    [SerializeField] private bool includeKeyHint;
    [SerializeField] private string keyHint = "F";

    protected override void Awake()
    {
        base.Awake();
        ResolveReferences();
        SubscribeToShopState();
        RefreshPrompt();
    }

    private void OnEnable()
    {
        ResolveReferences();
        SubscribeToShopState();
        RefreshPrompt();
    }

    private void OnDisable()
    {
        if (serviceManager != null)
        {
            serviceManager.OnShopStateChanged -= HandleShopStateChanged;
        }
    }

    public override bool CanInteract()
    {
        return serviceManager != null;
    }

    public override void Interact()
    {
        if (serviceManager == null)
        {
            Debug.LogWarning("[RestaurantOpenCloseInteractable] Missing RestaurantServiceManager reference.");
            return;
        }

        if (serviceManager.IsOpen)
        {
            serviceManager.CloseShop();
        }
        else
        {
            serviceManager.OpenShop();
        }

        RefreshPrompt();
    }

    private void ResolveReferences()
    {
        if (serviceManager == null)
        {
            serviceManager = FindObjectOfType<RestaurantServiceManager>();
        }

        if (actionText == null)
        {
            if (InteractParent != null)
            {
                actionText = InteractParent.GetComponentInChildren<TMP_Text>(true);
            }

            // if (actionText == null)
            // {
            //     actionText = GetComponentInChildren<TMP_Text>(true);
            // }
        }

        if (signText == null)
        {
            var signTextTransform = transform.Find("SignTextCanvas/SignText");
            if (signTextTransform != null)
            {
                signText = signTextTransform.GetComponent<TMP_Text>();
            }
        }
    }

    private void SubscribeToShopState()
    {
        if (serviceManager == null)
        {
            return;
        }

        serviceManager.OnShopStateChanged -= HandleShopStateChanged;
        serviceManager.OnShopStateChanged += HandleShopStateChanged;
    }

    private void HandleShopStateChanged(bool isOpen)
    {
        RefreshPrompt();
    }

    private void RefreshPrompt()
    {
        if (actionText == null || serviceManager == null)
        {
            return;
        }

        string text = serviceManager.IsOpen ? closePrompt : openPrompt;
        string signTextValue = serviceManager.IsOpen ? "OPEN" : "CLOSED";
        if (includeKeyHint && !string.IsNullOrWhiteSpace(keyHint))
        {
            text = $"{text} [{keyHint}]";
        }

        actionText.text = text;
        signText.text = signTextValue;
    }
}
