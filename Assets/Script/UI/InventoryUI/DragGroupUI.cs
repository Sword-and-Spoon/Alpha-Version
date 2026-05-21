using UnityEngine;
using UnityEngine.EventSystems;

public class DragGroupUI : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private Canvas canvas;
    private CanvasGroup canvasGroup;

    public string itemName;
    public ItemCategory category;
    public TransactionType transactionType;
    public StoreType storeType;
    public int quantity;
    public int totalPrice;
    public TimeManager.DateTime gameTime;
    public bool isBalancingEntry;

    private GameObject dragGhost;

    void Awake()
    {
        canvas = GetComponentInParent<Canvas>();
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
    }

    public void OnPointerDown(PointerEventData eventData) { }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (canvas == null)
        {
            canvas = GetComponentInParent<Canvas>();
        }

        if (canvas == null || canvasGroup == null)
        {
            return;
        }

        canvasGroup.blocksRaycasts = false;

        dragGhost = Instantiate(gameObject, canvas.transform);
        dragGhost.transform.position = gameObject.transform.position;

        CanvasGroup ghostCG = dragGhost.GetComponent<CanvasGroup>();
        if (ghostCG == null)
        {
            ghostCG = dragGhost.AddComponent<CanvasGroup>();
        }

        ghostCG.alpha = 0.6f;
        ghostCG.blocksRaycasts = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (dragGhost == null || canvas == null)
        {
            return;
        }

        dragGhost.transform.position += (Vector3)eventData.delta / canvas.scaleFactor;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (dragGhost != null)
        {
            Destroy(dragGhost);
        }

        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = true;
        }
    }

    public void SetValue(ItemCategory category, TransactionType transactionType, int totalPrice, TimeManager.DateTime gameTime)
    {
        SetValue(string.Empty, category, transactionType, StoreType.None, 0, totalPrice, gameTime, true);
    }

    public void SetValue(
        string itemName,
        ItemCategory category,
        TransactionType transactionType,
        StoreType storeType,
        int quantity,
        int totalPrice,
        TimeManager.DateTime gameTime,
        bool isBalancingEntry)
    {
        this.itemName = itemName;
        this.category = category;
        this.transactionType = transactionType;
        this.storeType = storeType;
        this.quantity = quantity;
        this.totalPrice = totalPrice;
        this.gameTime = gameTime;
        this.isBalancingEntry = isBalancingEntry;
    }
}
