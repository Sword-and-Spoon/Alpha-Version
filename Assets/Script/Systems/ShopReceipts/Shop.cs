using UnityEngine;

public class Shop : InteractableObject
{
    [SerializeField] private GameObject shopUI;

    public override bool CanInteract() => true;

    public override void Interact()
    {
        if (shopUI == null)
            shopUI = FindShopUI();

        if (shopUI == null)
        {
            Debug.LogError($"[Shop] shopUI reference is null on {name}. Re-assign it in the Inspector.");
            return;
        }

        openUI(shopUI);
    }

    private GameObject FindShopUI()
    {
        UI_Shop found = FindObjectOfType<UI_Shop>(true);
        return found != null ? found.gameObject : null;
    }
}
