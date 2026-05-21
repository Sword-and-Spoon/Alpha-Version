using UnityEngine;

public class APVendorNPC : InteractableObject
{
    [SerializeField] private string vendorName = "Town Shop";
    [SerializeField] private GameObject hasDebtIndicator;
    [SerializeField] private GameObject shopUIRoot;
    [SerializeField] private UI_Shop shopUI;

    private void OnEnable()
    {
        APQuestManager.OnAPQuestRepaid += OnDebtRepaid;
        TimeManager.OnDateTimeChanged += OnTimeChanged;
    }

    private void OnDisable()
    {
        APQuestManager.OnAPQuestRepaid -= OnDebtRepaid;
        TimeManager.OnDateTimeChanged -= OnTimeChanged;
    }

    private void Start()
    {
        ResolveUIReferences();
        RefreshIndicator();
    }

    private void ResolveUIReferences()
    {
        if (shopUI == null)
            shopUI = FindObjectOfType<UI_Shop>(true);

        if (shopUIRoot == null && shopUI != null)
        {
            Canvas canvas = shopUI.GetComponentInParent<Canvas>(true);
            shopUIRoot = canvas != null ? canvas.gameObject : shopUI.gameObject;
        }

        shopUI.BindOwner(this);
    }

    public override bool CanInteract() => true;

    public override void Interact()
    {
        // ตรวจสอบว่าถ้า Reference ขาดหายไป (Missing/null) ตอนที่กดคุย ให้ลองหา UI ใหม่ก่อน
        if (shopUIRoot == null || shopUI == null)
        {
            ResolveUIReferences();
        }

        // ถ้าหาใหม่แล้วยังไม่เจออีก ค่อย Return ออก
        if (shopUIRoot == null) return;

        openUI(shopUIRoot);
        if (shopUIRoot.activeSelf)
            // shopUI?.ShowDebtsTab();
            shopUI?.ShowBuyTab();
    }

    private void RefreshIndicator()
    {
        bool hasDebt = APQuestManager.Instance?.HasAnyDebt(vendorName) ?? false;
        hasDebtIndicator?.SetActive(hasDebt);
    }

    private void OnDebtRepaid(APQuestData _) => RefreshIndicator();
    private void OnTimeChanged(TimeManager.DateTime _) => RefreshIndicator();
}
