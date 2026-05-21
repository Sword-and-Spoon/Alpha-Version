using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MenuController : MonoBehaviour
{
    [SerializeField] private GameObject menuCanvas;
    [SerializeField] private TabController tabController;
    [SerializeField] private Button closeButton;

    [Header("Default Tabs")]
    [SerializeField] private int inventoryTabIndex = 1;
    [SerializeField] private int questTabIndex = 3;

    private int currentTabIndex = -1;

    private void Awake()
    {
        if (menuCanvas == null)
        {
            // พยายามหา MenuCanvas จากลูกๆ (รวมถึงตัวที่ปิดอยู่)
            foreach (Transform child in GetComponentsInChildren<Transform>(true))
            {
                if (child.name == "MenuCanvas")
                {
                    menuCanvas = child.gameObject;
                    break;
                }
            }
        }

        if (menuCanvas == null)
        {
            Debug.LogError("[MenuController] Menu canvas is not assigned.");
            return;
        }

        if (tabController == null)
        {
            tabController = menuCanvas.GetComponentInChildren<TabController>(true);
        }

        if (closeButton == null)
        {
            closeButton = menuCanvas.GetComponentInChildren<Button>(true);
        }

        closeButton.onClick.AddListener(() => SetMenuOpen(false));
    }

    // E — toggle เปิด/ปิด ที่ Inventory tab
    public void ToggleMenu()
    {
        if (!UI_StateManager.Instance.menuOpen && !UI_StateManager.Instance.CanOpenMenu()) return;

        bool open = !UI_StateManager.Instance.menuOpen;
        SetMenuOpen(open);

        if (open)
        {
            ActivateTab(inventoryTabIndex);
        }
    }

    // Q — toggle เปิด/ปิด ที่ Quest tab
    public void ToggleQuestTab()
    {
        if (UI_StateManager.Instance.menuOpen)
        {
            if (currentTabIndex == questTabIndex)
            {
                // อยู่ที่ Quest tab อยู่แล้ว → ปิดเมนู
                SetMenuOpen(false);
            }
            else
            {
                // อยู่ tab อื่น → สลับมา Quest tab
                ActivateTab(questTabIndex);
            }
        }
        else
        {
            if (!UI_StateManager.Instance.CanOpenMenu()) return;
            SetMenuOpen(true);
            ActivateTab(questTabIndex);
        }
    }

    public void OpenQuestTab() => OpenAtTab(questTabIndex);
    public void OpenInventoryTab() => OpenAtTab(inventoryTabIndex);

    private void OpenAtTab(int tabIndex)
    {
        if (!UI_StateManager.Instance.menuOpen && !UI_StateManager.Instance.CanOpenMenu()) return;

        if (!UI_StateManager.Instance.menuOpen)
        {
            SetMenuOpen(true);
        }

        ActivateTab(tabIndex);
    }

    private void SetMenuOpen(bool open)
    {
        if (menuCanvas == null) return;
        menuCanvas.SetActive(open);
        Time.timeScale = open ? 0f : 1f;
        UI_StateManager.Instance.menuOpen = open;
    }

    private void ActivateTab(int tabIndex)
    {
        if (tabController == null) return;
        tabController.ActivateTab(tabIndex);
        currentTabIndex = tabIndex;
    }
}
