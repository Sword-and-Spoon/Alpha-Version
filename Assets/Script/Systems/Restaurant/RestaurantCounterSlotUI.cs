using UnityEngine;
using UnityEngine.UI;

public class RestaurantCounterSlotUI : MonoBehaviour
{
    [Header("Layout")]
    [SerializeField] private Transform slotContainer;

    [HideInInspector] [SerializeField] private RestaurantCounter counter;
    [HideInInspector] [SerializeField] private InventoryController inventoryController;
    [HideInInspector] [SerializeField] private GameObject slotPrefab;
    [HideInInspector] [SerializeField] private GameObject uiItemPrefab;

    private Slot counterSlot;
    private RestaurantCounterDropZone dropZone;
    private bool isSubscribed;

    private void Awake()
    {
        AutoWire();
        EnsureSlot();
        Subscribe();
        Refresh();
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        AutoWire();
    }

    private void OnEnable()
    {
        Subscribe();
        Refresh();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    public void Bind(RestaurantCounter targetCounter)
    {
        Unsubscribe();
        counter = targetCounter;

        AutoWire();
        EnsureSlot();

        if (dropZone != null)
        {
            dropZone.SetCounter(counter);
        }

        Subscribe();
        Refresh();
    }

    public void Unbind()
    {
        Unsubscribe();
    }

    public void RefreshNow()
    {
        Refresh();
    }

    private void Subscribe()
    {
        if (isSubscribed || counter == null) return;

        counter.OnCounterStockChanged += Refresh;
        isSubscribed = true;
    }

    private void Unsubscribe()
    {
        if (!isSubscribed || counter == null) return;

        counter.OnCounterStockChanged -= Refresh;
        isSubscribed = false;
    }

    private void Refresh()
    {
        AutoWire();
        EnsureSlot();

        if (counterSlot == null)
        {
            return;
        }

        if (counterSlot.currentItem != null)
        {
            Destroy(counterSlot.currentItem);
            counterSlot.currentItem = null;
        }

        Image qualityBackground = GetQualityBackground(counterSlot);
        if (qualityBackground != null)
        {
            qualityBackground.color = Color.clear;
        }

        Item storedItem = counter != null ? counter.GetStoredFoodCopy() : null;
        bool hasFood = storedItem != null && storedItem.itemSO != null;

        if (!hasFood)
        {
            counterSlot.index = -1;
            counterSlot.Setup(-1, null, true);
            counterSlot.SetHighlight(false);
            return;
        }

        counterSlot.index = 0;
        counterSlot.Setup(0, null, true);

        if (uiItemPrefab == null)
        {
            return;
        }

        GameObject uiItemObj = Instantiate(uiItemPrefab, counterSlot.transform);
        RectTransform rect = uiItemObj.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.anchoredPosition = Vector2.zero;
            rect.localScale = Vector3.one;
        }

        ItemDragHandler dragHandler = uiItemObj.GetComponent<ItemDragHandler>();
        if (dragHandler != null)
        {
            dragHandler.ConfigureAsCounterSource(counter, inventoryController);
        }

        ItemUI itemUI = uiItemObj.GetComponent<ItemUI>();
        if (itemUI != null)
        {
            itemUI.Set(storedItem);
        }

        counterSlot.currentItem = uiItemObj;

        if (qualityBackground != null)
        {
            qualityBackground.color = storedItem.GetQualityColor();
        }
    }

    private void AutoWire()
    {
        if (slotContainer == null)
        {
            slotContainer = transform;
        }

        if (inventoryController == null)
        {
            inventoryController = FindObjectOfType<InventoryController>();
        }

        if (inventoryController != null)
        {
            if (slotPrefab == null)
            {
                slotPrefab = inventoryController.slotPrefab;
            }

            if (uiItemPrefab == null)
            {
                uiItemPrefab = inventoryController.uiItemPrefab;
            }
        }
    }

    private void EnsureSlot()
    {
        if (counterSlot == null)
        {
            counterSlot = slotContainer != null ? slotContainer.GetComponentInChildren<Slot>(true) : null;
        }

        if (counterSlot == null && slotContainer != null && slotPrefab != null)
        {
            GameObject slotObj = Instantiate(slotPrefab, slotContainer);
            counterSlot = slotObj.GetComponent<Slot>();
        }

        if (counterSlot == null)
        {
            return;
        }

        if (dropZone == null)
        {
            dropZone = counterSlot.GetComponent<RestaurantCounterDropZone>();
            if (dropZone == null)
            {
                dropZone = counterSlot.gameObject.AddComponent<RestaurantCounterDropZone>();
            }
        }

        if (dropZone != null)
        {
            dropZone.SetCounter(counter);
        }
    }

    private Image GetQualityBackground(Slot slot)
    {
        if (slot == null)
        {
            return null;
        }

        Transform qualityPath = slot.transform.Find("BackgroundQualityParent/BackgroundQuality");
        return qualityPath != null ? qualityPath.GetComponent<Image>() : null;
    }
}
