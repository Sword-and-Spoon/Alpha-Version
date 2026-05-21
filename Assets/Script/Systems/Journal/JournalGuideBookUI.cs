using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class JournalGuideBookUI : MonoBehaviour
{
    [Serializable]
    public class GuideTab
    {
        [SerializeField] private string tabName;
        [SerializeField] private Button tabButton;
        [SerializeField] private RectTransform liftTarget;
        [SerializeField] private Graphic backgroundGraphic;
        [SerializeField] private TMP_Text tmpLabel;
        [SerializeField] private Text legacyLabel;
        [SerializeField] private GameObject selectedVisual;
        [SerializeField] private List<GameObject> pages = new List<GameObject>();

        public string TabName => tabName;
        public Button TabButton => tabButton;
        public RectTransform LiftTarget => liftTarget;
        public Graphic BackgroundGraphic => backgroundGraphic;
        public TMP_Text TmpLabel => tmpLabel;
        public Text LegacyLabel => legacyLabel;
        public GameObject SelectedVisual => selectedVisual;
        public List<GameObject> Pages => pages;
    }

    [Header("Controls")]
    [SerializeField] private Button closeButton;
    [SerializeField] private Button previousButton;
    [SerializeField] private Button nextButton;

    [Header("Tabs And Pages")]
    [SerializeField] private List<GuideTab> tabs = new List<GuideTab>();
    [SerializeField] private bool resetToFirstPageOnOpen = true;
    [SerializeField] private bool hideOnAwake = true;
    [SerializeField] private TMP_Text pageIndicatorText;
    [SerializeField] private Text pageIndicatorLegacyText;

    [Header("Tab Feedback")]
    [SerializeField] private float selectedTabYOffset = 14f;
    [SerializeField] private Color selectedTabBackgroundColor = new Color(0.94f, 0.78f, 0.42f, 1f);
    [SerializeField] private Color unselectedTabBackgroundColor = new Color(0.71f, 0.43f, 0.25f, 0.92f);
    [SerializeField] private Color selectedTabLabelColor = new Color(0.34f, 0.16f, 0.08f, 1f);
    [SerializeField] private Color unselectedTabLabelColor = new Color(0.97f, 0.93f, 0.82f, 1f);

    [Header("Page Turn Animation")]
    [SerializeField] private Image pageTurnImage;
    [SerializeField] private List<Sprite> nextPageTurnFrames = new List<Sprite>();
    [SerializeField] private List<Sprite> previousPageTurnFrames = new List<Sprite>();
    [SerializeField] private float pageTurnFrameDuration = 0.035f;
    [SerializeField] private bool animateTabChange = true;

    private readonly Dictionary<Button, UnityAction> tabClickHandlers = new Dictionary<Button, UnityAction>();
    private readonly Dictionary<RectTransform, Vector2> tabBasePositions = new Dictionary<RectTransform, Vector2>();
    private Coroutine pageTurnRoutine;
    private int currentTabIndex;
    private int currentPageIndex;
    private bool controlsBound;
    private bool isTurningPage;

    private void Awake()
    {
        BindControls();
        BindTabs();
        SetPageTurnVisible(false);
        RefreshImmediate();

        if (hideOnAwake)
        {
            gameObject.SetActive(false);
        }
    }

    private void OnEnable()
    {
        BindControls();
        BindTabs();
        RefreshImmediate();
        transform.SetAsLastSibling();
    }

    private void OnDisable()
    {
        StopPageTurn();
    }

    public void Open()
    {
        if (resetToFirstPageOnOpen)
        {
            currentTabIndex = 0;
            currentPageIndex = 0;
        }

        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        RefreshImmediate();
    }

    public void Close()
    {
        gameObject.SetActive(false);
    }

    public void NextPage()
    {
        GuideTab tab = GetCurrentTab();
        if (tab == null || currentPageIndex >= GetLastPageIndex(tab) || isTurningPage)
        {
            return;
        }

        ShowPage(currentTabIndex, currentPageIndex + 1, true, true);
    }

    public void PreviousPage()
    {
        if (currentPageIndex <= 0 || isTurningPage)
        {
            return;
        }

        ShowPage(currentTabIndex, currentPageIndex - 1, true, false);
    }

    public void SelectTab(int tabIndex)
    {
        if (tabIndex < 0 || tabIndex >= tabs.Count || isTurningPage)
        {
            return;
        }

        bool changedTab = tabIndex != currentTabIndex;
        bool forward = IsForwardNavigation(tabIndex, 0);
        ShowPage(tabIndex, 0, animateTabChange && changedTab, forward);
    }

    [ContextMenu("Preview Current Page")]
    private void RefreshImmediate()
    {
        ClampCurrentIndexes();
        SetActivePage(currentTabIndex, currentPageIndex);
        RefreshNavigation();
        RefreshTabs();
        SetPageTurnVisible(false);
    }

    private void BindControls()
    {
        if (controlsBound)
        {
            return;
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(Close);
            closeButton.onClick.AddListener(Close);
        }

        if (previousButton != null)
        {
            previousButton.onClick.RemoveListener(PreviousPage);
            previousButton.onClick.AddListener(PreviousPage);
        }

        if (nextButton != null)
        {
            nextButton.onClick.RemoveListener(NextPage);
            nextButton.onClick.AddListener(NextPage);
        }

        controlsBound = true;
    }

    private void BindTabs()
    {
        foreach (KeyValuePair<Button, UnityAction> tabHandler in tabClickHandlers)
        {
            if (tabHandler.Key != null)
            {
                tabHandler.Key.onClick.RemoveListener(tabHandler.Value);
            }
        }

        tabClickHandlers.Clear();

        for (int i = 0; i < tabs.Count; i++)
        {
            Button tabButton = tabs[i]?.TabButton;
            if (tabButton == null)
            {
                continue;
            }

            int tabIndex = i;
            UnityAction handler = () => SelectTab(tabIndex);
            tabButton.onClick.AddListener(handler);
            tabClickHandlers[tabButton] = handler;

            RectTransform liftTarget = GetLiftTarget(tabs[i]);
            if (liftTarget != null && !tabBasePositions.ContainsKey(liftTarget))
            {
                tabBasePositions[liftTarget] = liftTarget.anchoredPosition;
            }
        }
    }

    private void ShowPage(int tabIndex, int pageIndex, bool animate, bool forward)
    {
        tabIndex = Mathf.Clamp(tabIndex, 0, Mathf.Max(0, tabs.Count - 1));
        GuideTab targetTab = tabs.Count > 0 ? tabs[tabIndex] : null;
        pageIndex = Mathf.Clamp(pageIndex, 0, GetLastPageIndex(targetTab));

        if (tabIndex == currentTabIndex && pageIndex == currentPageIndex)
        {
            RefreshImmediate();
            return;
        }

        StopPageTurn();

        int previousTabIndex = currentTabIndex;
        int previousPageIndex = currentPageIndex;
        currentTabIndex = tabIndex;
        currentPageIndex = pageIndex;

        if (animate && gameObject.activeInHierarchy && HasPageTurnFrames(forward))
        {
            pageTurnRoutine = StartCoroutine(PlayPageTurn(previousTabIndex, previousPageIndex, forward));
            return;
        }

        RefreshImmediate();
    }

    private IEnumerator PlayPageTurn(int previousTabIndex, int previousPageIndex, bool forward)
    {
        isTurningPage = true;
        SetNavigationInteractable(false);
        SetActivePage(previousTabIndex, previousPageIndex);
        RefreshTabs();

        List<Sprite> frames = GetPageTurnFrames(forward);
        SetPageTurnVisible(true);

        bool targetShown = false;
        int halfwayFrame = Mathf.Max(0, frames.Count / 2);
        for (int i = 0; i < frames.Count; i++)
        {
            if (pageTurnImage != null)
            {
                pageTurnImage.sprite = frames[i];
            }

            if (!targetShown && i >= halfwayFrame)
            {
                SetActivePage(currentTabIndex, currentPageIndex);
                targetShown = true;
            }

            yield return new WaitForSecondsRealtime(Mathf.Max(0.001f, pageTurnFrameDuration));
        }

        if (!targetShown)
        {
            SetActivePage(currentTabIndex, currentPageIndex);
        }

        SetPageTurnVisible(false);
        isTurningPage = false;
        pageTurnRoutine = null;
        RefreshNavigation();
        RefreshTabs();
    }

    private void SetActivePage(int tabIndex, int pageIndex)
    {
        for (int i = 0; i < tabs.Count; i++)
        {
            GuideTab tab = tabs[i];
            if (tab == null || tab.Pages == null)
            {
                continue;
            }

            for (int j = 0; j < tab.Pages.Count; j++)
            {
                GameObject page = tab.Pages[j];
                if (page != null)
                {
                    page.SetActive(i == tabIndex && j == pageIndex);
                }
            }
        }
    }

    private void RefreshNavigation()
    {
        GuideTab tab = GetCurrentTab();
        int pageCount = tab?.Pages?.Count ?? 0;

        if (previousButton != null)
        {
            previousButton.interactable = !isTurningPage && currentPageIndex > 0;
        }

        if (nextButton != null)
        {
            nextButton.interactable = !isTurningPage && currentPageIndex < pageCount - 1;
        }

        if (pageIndicatorText != null)
        {
            pageIndicatorText.text = pageCount <= 0 ? "0 / 0" : $"{currentPageIndex + 1} / {pageCount}";
        }

        if (pageIndicatorLegacyText != null)
        {
            pageIndicatorLegacyText.text = pageCount <= 0 ? "0 / 0" : $"{currentPageIndex + 1} / {pageCount}";
        }
    }

    private void RefreshTabs()
    {
        RefreshTabLayoutBaseline();

        for (int i = 0; i < tabs.Count; i++)
        {
            GuideTab tab = tabs[i];
            if (tab == null)
            {
                continue;
            }

            if (tab.SelectedVisual != null)
            {
                tab.SelectedVisual.SetActive(i == currentTabIndex);
            }

            ApplyTabVisualState(tab, i == currentTabIndex);
        }
    }

    private void RefreshTabLayoutBaseline()
    {
        RectTransform layoutRoot = GetTabLayoutRoot();
        if (layoutRoot == null)
        {
            return;
        }

        LayoutGroup layoutGroup = layoutRoot.GetComponent<LayoutGroup>();
        if (layoutGroup == null)
        {
            return;
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(layoutRoot);

        for (int i = 0; i < tabs.Count; i++)
        {
            RectTransform liftTarget = GetLiftTarget(tabs[i]);
            if (liftTarget != null)
            {
                tabBasePositions[liftTarget] = liftTarget.anchoredPosition;
            }
        }
    }

    private void ApplyTabVisualState(GuideTab tab, bool selected)
    {
        RectTransform liftTarget = GetLiftTarget(tab);
        if (liftTarget != null)
        {
            if (!tabBasePositions.TryGetValue(liftTarget, out Vector2 basePosition))
            {
                basePosition = liftTarget.anchoredPosition;
                tabBasePositions[liftTarget] = basePosition;
            }

            float yOffset = selected ? selectedTabYOffset : 0f;
            liftTarget.anchoredPosition = new Vector2(basePosition.x, basePosition.y + yOffset);
        }

        Graphic background = GetBackgroundGraphic(tab);
        if (background != null)
        {
            background.color = selected ? selectedTabBackgroundColor : unselectedTabBackgroundColor;
        }

        TMP_Text tmpLabel = GetTmpLabel(tab);
        if (tmpLabel != null)
        {
            tmpLabel.color = selected ? selectedTabLabelColor : unselectedTabLabelColor;
        }

        Text legacyLabel = GetLegacyLabel(tab);
        if (legacyLabel != null)
        {
            legacyLabel.color = selected ? selectedTabLabelColor : unselectedTabLabelColor;
        }
    }

    private void ClampCurrentIndexes()
    {
        currentTabIndex = Mathf.Clamp(currentTabIndex, 0, Mathf.Max(0, tabs.Count - 1));
        currentPageIndex = Mathf.Clamp(currentPageIndex, 0, GetLastPageIndex(GetCurrentTab()));
    }

    private GuideTab GetCurrentTab()
    {
        if (tabs.Count == 0 || currentTabIndex < 0 || currentTabIndex >= tabs.Count)
        {
            return null;
        }

        return tabs[currentTabIndex];
    }

    private int GetLastPageIndex(GuideTab tab)
    {
        int pageCount = tab?.Pages?.Count ?? 0;
        return Mathf.Max(0, pageCount - 1);
    }

    private bool IsForwardNavigation(int targetTabIndex, int targetPageIndex)
    {
        if (targetTabIndex != currentTabIndex)
        {
            return targetTabIndex > currentTabIndex;
        }

        return targetPageIndex > currentPageIndex;
    }

    private RectTransform GetLiftTarget(GuideTab tab)
    {
        if (tab == null)
        {
            return null;
        }

        if (tab.LiftTarget != null)
        {
            return tab.LiftTarget;
        }

        return tab.TabButton != null ? tab.TabButton.transform as RectTransform : null;
    }

    private Graphic GetBackgroundGraphic(GuideTab tab)
    {
        if (tab == null)
        {
            return null;
        }

        if (tab.BackgroundGraphic != null)
        {
            return tab.BackgroundGraphic;
        }

        return tab.TabButton != null ? tab.TabButton.targetGraphic : null;
    }

    private TMP_Text GetTmpLabel(GuideTab tab)
    {
        if (tab == null)
        {
            return null;
        }

        if (tab.TmpLabel != null)
        {
            return tab.TmpLabel;
        }

        return tab.TabButton != null ? tab.TabButton.GetComponentInChildren<TMP_Text>(true) : null;
    }

    private Text GetLegacyLabel(GuideTab tab)
    {
        if (tab == null)
        {
            return null;
        }

        if (tab.LegacyLabel != null)
        {
            return tab.LegacyLabel;
        }

        return tab.TabButton != null ? tab.TabButton.GetComponentInChildren<Text>(true) : null;
    }

    private RectTransform GetTabLayoutRoot()
    {
        for (int i = 0; i < tabs.Count; i++)
        {
            Button tabButton = tabs[i]?.TabButton;
            if (tabButton != null)
            {
                return tabButton.transform.parent as RectTransform;
            }
        }

        return null;
    }

    private void SetNavigationInteractable(bool interactable)
    {
        if (previousButton != null)
        {
            previousButton.interactable = interactable;
        }

        if (nextButton != null)
        {
            nextButton.interactable = interactable;
        }
    }

    private bool HasPageTurnFrames(bool forward)
    {
        return pageTurnImage != null && GetPageTurnFrames(forward).Count > 0;
    }

    private List<Sprite> GetPageTurnFrames(bool forward)
    {
        if (forward || previousPageTurnFrames.Count == 0)
        {
            return nextPageTurnFrames;
        }

        return previousPageTurnFrames;
    }

    private void SetPageTurnVisible(bool visible)
    {
        if (pageTurnImage != null)
        {
            pageTurnImage.gameObject.SetActive(visible);
        }
    }

    private void StopPageTurn()
    {
        if (pageTurnRoutine != null)
        {
            StopCoroutine(pageTurnRoutine);
            pageTurnRoutine = null;
        }

        isTurningPage = false;
        SetPageTurnVisible(false);
        RefreshNavigation();
    }
}
