using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Oven : InteractableObject
{
    [SerializeField] private GameObject ovenUI;

    public override bool CanInteract() => true;

    public override void Interact()
    {
        var session = gameObject.GetComponent<CookingSession>();

        // If there is cooked dish that waiting for player to collect.
        if (session != null && session.isWaitingForPickup)
        {
            session.CollectItem();
            return;
        }

        // If there is an active session
        if (session != null && session.isActive)
        {
            session.OnOvenClicked();
            return;
        }

        // otherwise open or toggle the normal oven UI
        openUI(ovenUI);
        // when the oven UI becomes visible again, make sure the recipe panel is shown
        if (ovenUI != null && ovenUI.activeInHierarchy)
        {
            var menu = ovenUI.GetComponentInChildren<UI_Menu_Advanced>(true);
            if (menu != null && menu.cookingMenu != null)
                menu.cookingMenu.SetActive(true);
        }
    }

    private void Start()
    {
        ovenUI.GetComponent<CookingUIToggle>().BindOwner(this);
    }

    public bool HasUI(GameObject ui)
    {
        return ovenUI == ui;
    }

    public void CloseUI()
    {
        Interact();
    }
}

