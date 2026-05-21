using UnityEngine;

public class UtilityPaymentInteractable : InteractableObject
{
    [SerializeField] private GameObject utilityPaymentUI;
    private GameObject resolvedUtilityPaymentUI;
    private bool ownsResolvedUtilityPaymentUI;

    protected override void Awake()
    {
        base.Awake();
        ResolveUtilityPaymentUI();
    }

    private void OnDestroy()
    {
        if (ownsResolvedUtilityPaymentUI && resolvedUtilityPaymentUI != null)
        {
            Destroy(resolvedUtilityPaymentUI);
        }
    }

    public override bool CanInteract() => GetResolvedUtilityPaymentUI() != null;

    public override void Interact()
    {
        GameObject targetUI = GetResolvedUtilityPaymentUI();
        if (targetUI == null)
        {
            Debug.LogError($"[UtilityPaymentInteractable] Utility Payment UI is not assigned on {name}.");
            return;
        }

        openUI(targetUI);
    }

    private GameObject GetResolvedUtilityPaymentUI()
    {
        if (resolvedUtilityPaymentUI == null)
        {
            ResolveUtilityPaymentUI();
        }

        return resolvedUtilityPaymentUI;
    }

    private void ResolveUtilityPaymentUI()
    {
        if (resolvedUtilityPaymentUI != null || utilityPaymentUI == null)
        {
            return;
        }

        if (utilityPaymentUI.scene.IsValid())
        {
            resolvedUtilityPaymentUI = utilityPaymentUI;
            ownsResolvedUtilityPaymentUI = false;
        }
        else
        {
            resolvedUtilityPaymentUI = Instantiate(utilityPaymentUI);
            resolvedUtilityPaymentUI.name = utilityPaymentUI.name;
            resolvedUtilityPaymentUI.SetActive(false);
            ownsResolvedUtilityPaymentUI = true;
        }

        UtilityPaymentUI paymentUI = resolvedUtilityPaymentUI.GetComponent<UtilityPaymentUI>();
        if (paymentUI == null)
        {
            paymentUI = resolvedUtilityPaymentUI.GetComponentInChildren<UtilityPaymentUI>(true);
        }

        paymentUI?.BindOwner(this);
    }
}
