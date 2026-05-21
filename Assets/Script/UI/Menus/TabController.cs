using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TabController : MonoBehaviour
{
    public Image[] tabImages;
    public GameObject[] pages;

    [Header("Tab Size Settings")]
    [SerializeField] private float activeWidth = 105f;
    [SerializeField] private float inactiveWidth = 95f;
    [SerializeField] private int startupTabIndex = 0;

    private bool hasActivatedTab;

    private void Start()
    {
        if (!hasActivatedTab)
        {
            ActivateTab(startupTabIndex);
        }
    }

    public void ActivateTab(int tabIndex)
    {
        if (pages == null || pages.Length == 0 || tabImages == null || tabImages.Length == 0) return;
        if (pages.Length != tabImages.Length)
        {
            Debug.LogWarning("[TabController] pages/tabImages size mismatch.");
        }

        int safeIndex = Mathf.Clamp(tabIndex, 0, Mathf.Min(pages.Length, tabImages.Length) - 1);

        for (int i = 0; i < pages.Length; i++)
        {
            if (pages[i] != null) pages[i].SetActive(false);
            if (i < tabImages.Length && tabImages[i] != null)
            {
                SetTabWitdh(tabImages[i].rectTransform, inactiveWidth);
            }
        }

        if (safeIndex < pages.Length && pages[safeIndex] != null) pages[safeIndex].SetActive(true);
        if (safeIndex < tabImages.Length && tabImages[safeIndex] != null)
        {
            SetTabWitdh(tabImages[safeIndex].rectTransform, activeWidth);
        }

        hasActivatedTab = true;
    }

    private void SetTabWitdh(RectTransform rect, float width)
    {
        if (rect == null) return;

        Vector2 size = rect.sizeDelta;
        size.x = width;
        rect.sizeDelta = size;
    }
}
