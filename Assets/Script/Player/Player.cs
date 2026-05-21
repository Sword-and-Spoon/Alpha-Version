using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Player : MonoBehaviour
{
    public static Player Instance { get; private set; }
    private InventoryController inventoryController;


    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Link to GameManager
            if (GameManager.instance != null)
            {
                GameManager.instance.player = gameObject;
            }
        }
        else
        {
            // If duplicate, disable immediately so it doesn't interfere, then destroy
            gameObject.SetActive(false);
            Destroy(gameObject);
            return;
        }

        RefreshInventoryController();
    }

    private void Start()
    {
        AddStarterItemsIfNeeded();
    }

    private void AddStarterItemsIfNeeded()
    {
        if (inventoryController == null) return;
        bool hasAnyItem = inventoryController.GetItems().Exists(i => i != null);
        if (hasAnyItem) return;

        ItemSO sugarDust = ItemSO.GetItemByName("Sugar Dust");
        if (sugarDust == null)
        {
            Debug.LogWarning("[Player] 'Sugar Dust' not found in ItemDatabase — add it to the allItems list.");
            return;
        }

        inventoryController.AddItem(new Item(sugarDust, 5, ItemQuality.Common));
    }

    private void RefreshInventoryController()
    {
        inventoryController = FindObjectOfType<InventoryController>();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RefreshInventoryController();
    }

    private float nextFullLogTime;

    private void OnTriggerStay2D(Collider2D collider)
    {
        if (inventoryController == null) RefreshInventoryController();

        ItemScene itemScene = collider.GetComponentInParent<ItemScene>();
        if (itemScene != null)
        {
            if (itemScene.collected) return;

            DroppedItem droppedItem = itemScene.GetComponent<DroppedItem>();
            if (droppedItem != null && !droppedItem.canBeCollected) return;

            Item itemData = itemScene.GetItem();

            if (inventoryController != null && itemData != null)
            {
                // Mark as collected IMMEDIATELY to prevent multiple adds in the same frame
                itemScene.collected = true;
                if (droppedItem != null) droppedItem.canBeCollected = false;

                // Capture original amount BEFORE it potentially gets depleted in AddItem
                int originalAmount = itemData.amount;
                bool wasAdded = inventoryController.AddItem(itemData);

                if (wasAdded)
                {
                    if (itemData.itemSO != null)
                    {
                        ARQuestManager.Instance?.NotifyItemCollected(itemData.itemSO.itemId, originalAmount);
                    }
                    itemData.PickUp(originalAmount);
                    itemScene.DestroySelf();
                }
                else
                {
                    // Rollback if inventory was truly full
                    itemScene.collected = false;
                    if (droppedItem != null) droppedItem.canBeCollected = true;

                    if (Time.time >= nextFullLogTime)
                    {
                        Debug.Log("Inventory is full!");
                        nextFullLogTime = Time.time + 2f;
                    }
                }
            }
        }
    }

    public InventoryController GetInventoryController()
    {
        if (inventoryController == null) RefreshInventoryController();
        return inventoryController;
    }

}
