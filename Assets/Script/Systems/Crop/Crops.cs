using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class Crops : InteractableObject
{
    [Header("Sprites (0 = lv1, 1 = lv2, 2 = lv3 full)")]
    public Sprite[] growthSprites; // assign 3 sprites: lv1, lv2, lv3(full)

    [Header("Harvest")]
    public ItemSO harvestItemSO; // the ItemSO to give when harvested (e.g. Pumpkin)

    [Header("Growth")]
    public float secondsPerStage = 60f; // time to advance one stage
    [SerializeField] private int currentStage = 2; // default full grown

    [Header("Spawner (if this GameObject is an area)")]
    public bool isAreaSpawner = false; // enable on area GameObject (tagged e.g. "Pumpkin Forest")
    public int spawnCount = 10;
    public GameObject plantParent;

    private SpriteRenderer spriteRenderer;
    private Collider2D areaCollider2D;
    private Collider2D mainCollider;
    private Coroutine growthCoroutine;

    protected override void Awake()
    {
        base.Awake();
        spriteRenderer = GetComponent<SpriteRenderer>();
        mainCollider = GetComponent<Collider2D>();
        if (isAreaSpawner)
        {
            areaCollider2D = GetComponent<Collider2D>();
            if (areaCollider2D == null)
            {
                Debug.LogWarning("Area spawner should have a Collider2D to define bounds. Spawner: " + gameObject.name);
            }
        }
    }

    private void Start()
    {
        if (isAreaSpawner)
        {
            // spawn plants inside this area's bounds
            SpawnPlants();
            // hide the spawner visual if any
            if (spriteRenderer != null) spriteRenderer.enabled = false;
            return;
        }

        // set the initial sprite based on currentStage
        UpdateSprite();

        // set collider state according to stage
        UpdateColliderByStage();

        // if not full, start growing
        if (!IsFullGrown())
        {
            StartGrowing();
        }
    }

    public override bool CanInteract()
    {
        // player can interact (press F) only when full grown
        return IsFullGrown();
    }

    public override void Interact()
    {
        // log Harvest and perform harvest
        Debug.Log("Harvest");
        TryHarvest();
    }

    private void UpdateSprite()
    {
        if (growthSprites != null && growthSprites.Length > 0)
        {
            int idx = Mathf.Clamp(currentStage, 0, growthSprites.Length - 1);
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            spriteRenderer.sprite = growthSprites[idx];
        }
    }

    private void UpdateColliderByStage()
    {
        if (mainCollider == null) mainCollider = GetComponent<Collider2D>();
        if (mainCollider != null)
        {
            // make stage 0 (seedling) walk-through by setting trigger true
            mainCollider.isTrigger = (currentStage == 0);
        }
    }

    private bool IsFullGrown()
    {
        return growthSprites != null && currentStage >= growthSprites.Length - 1;
    }

    private void OnMouseDown()
    {
        // allow picking only if full grown and player clicked
        if (IsFullGrown())
        {
            TryHarvest();
        }
    }

    public void TryHarvest()
    {
        // add item to player inventory
        if (harvestItemSO == null)
        {
            Debug.LogWarning("Harvest ItemSO not set on plant: " + gameObject.name);
        }
        else
        {
            // Find player's inventory via GameManager like other scripts in project
            if (GameManager.instance != null && GameManager.instance.player != null)
            {
                var player = GameManager.instance.player.GetComponent<Player>();
                if (player != null)
                {
                    var inv = player.GetInventoryController();
                    if (inv != null)
                    {
                        inv.AddItem(new Item(harvestItemSO, 1));
                    }
                }
            }
        }

        // Reset to small plant (lv1)
        currentStage = 0;
        UpdateSprite();

        // update collider so player can walk over it
        UpdateColliderByStage();

        // hide interaction UI so it cannot be collected until regrown
        HideUI();

        // restart growth
        StartGrowing();
    }

    private void StartGrowing()
    {
        if (growthCoroutine != null) StopCoroutine(growthCoroutine);
        growthCoroutine = StartCoroutine(GrowRoutine());
    }

    private IEnumerator GrowRoutine()
    {
        while (!IsFullGrown())
        {
            yield return new WaitForSeconds(secondsPerStage);
            currentStage = Mathf.Min(currentStage + 1, growthSprites.Length - 1);
            UpdateSprite();
            UpdateColliderByStage();
        }
        growthCoroutine = null;
    }

    // Spawner logic: instantiate simple plant GameObjects inside this area's collider bounds
    private void SpawnPlants()
    {
        if (growthSprites == null || growthSprites.Length == 0)
        {
            Debug.LogWarning("Spawner has no growth sprites set. Cannot spawn plants.");
            return;
        }

        // ensure a parent for spawned plants
        if (plantParent == null)
        {
            plantParent = new GameObject("SpawnedPlants");
            plantParent.transform.SetParent(transform, true);
        }

        Bounds bounds = new Bounds(transform.position, Vector3.zero);
        if (areaCollider2D != null)
        {
            bounds = areaCollider2D.bounds;
        }
        else
        {
            // use transform scale as fallback area
            bounds = new Bounds(transform.position, transform.localScale);
        }

        int attempts = 0;
        int spawned = 0;
        while (spawned < spawnCount && attempts < spawnCount * 5)
        {
            attempts++;
            Vector3 pos = new Vector3(
                Random.Range(bounds.min.x, bounds.max.x),
                Random.Range(bounds.min.y, bounds.max.y),
                transform.position.z);

            // create a new GameObject for the plant
            GameObject go = new GameObject("PumpkinPlant");
            go.transform.position = pos;
            go.transform.SetParent(plantParent.transform, true);

            // add sprite renderer
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = growthSprites[growthSprites.Length - 1]; // spawn full grown

            // copy sorting settings from the spawner's sprite renderer so spawned plants keep layer/order
            if (spriteRenderer != null)
            {
                sr.sortingLayerID = spriteRenderer.sortingLayerID;
                sr.sortingLayerName = spriteRenderer.sortingLayerName;
                sr.sortingOrder = spriteRenderer.sortingOrder;
            }

            // add collider for interaction
            var col = go.AddComponent<BoxCollider2D>();

            // add Crops component and copy data
            var crops = go.AddComponent<Crops>();
            crops.growthSprites = growthSprites;
            crops.harvestItemSO = harvestItemSO;
            crops.secondsPerStage = secondsPerStage;
            crops.currentStage = growthSprites.Length - 1; // full grown

            // ensure collider state on the spawned crop matches stage
            crops.UpdateColliderByStage();

            spawned++;
        }
    }

    // Optional: visually show area in editor
    private void OnDrawGizmosSelected()
    {
        if (isAreaSpawner)
        {
            Gizmos.color = new Color(0.1f, 0.8f, 0.1f, 0.25f);
            if (TryGetComponent<Collider2D>(out var c))
            {
                Gizmos.DrawCube(c.bounds.center, c.bounds.size);
            }
            else
            {
                Gizmos.DrawWireCube(transform.position, transform.localScale);
            }
        }
    }
}
