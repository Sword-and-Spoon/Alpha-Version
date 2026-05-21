using System;
using System.Collections.Generic;
using UnityEngine;

public class RestaurantCounter : MonoBehaviour
{
    public struct ServedFood
    {
        public string itemName;
        public ItemCategory category;
        public ItemQuality quality;
        public int totalPrice;
        public Sprite icon;
    }

    [Header("Counter Slot (Stack)")]
    [SerializeField] private Transform slotPoint;
    [SerializeField] private bool useCounterTransformIfSlotPointMissing = true;
    [HideInInspector] [SerializeField] private int maxStackAmount = 10;
    [HideInInspector] [SerializeField] private string customerPickupPointName = "CustomerPoint";
    [HideInInspector] [SerializeField] private Vector2 defaultCustomerPickupOffset = new Vector2(0f, -0.65f);
    [HideInInspector] [SerializeField] private float pickupCandidateDistance = 0.8f;
    [HideInInspector] [SerializeField] private float pickupProbeRadius = 0.16f;
    [HideInInspector] [SerializeField] private LayerMask pickupObstacleMask = ~0;
    [HideInInspector] [SerializeField] private bool pickupIncludeTriggers;

    [Header("Validation")]
    [SerializeField] private bool allowConsumableType = true;
    [SerializeField] private bool allowFoodSuppliesCategory = false;

    [Header("Restaurant Pricing")]
    [SerializeField][Min(1f)] private float restaurantPriceMultiplier = 1.5f;

    [Header("Visual")]
    [SerializeField] private GameObject visualPrefab;
    [HideInInspector] [SerializeField] private int defaultSortingOrder = 15;
    [HideInInspector] [SerializeField] private Vector3 defaultVisualScale = Vector3.one;

    [Header("Persistence")]
    [SerializeField] private string counterId;

    private Item storedFood;
    private int storedUnitPrice;
    private GameObject visualObject;
    private SpriteRenderer visualRenderer;
    private Transform cachedCustomerPickupPoint;
    private bool restoringState;

    public event Action OnCounterStockChanged;

    public bool HasFood => storedFood != null && storedFood.itemSO != null && storedFood.amount > 0;
    public int TotalFoodCount => HasFood ? storedFood.amount : 0;
    public int OccupiedSlotCount => HasFood ? 1 : 0;
    public int TotalSlotCount => 1;
    public int MaxStackAmount => Mathf.Max(1, maxStackAmount);
    public string CounterId => ResolveCounterId();

    private Transform ActiveSlotPoint
    {
        get
        {
            if (slotPoint != null)
            {
                return slotPoint;
            }

            return useCounterTransformIfSlotPointMissing ? transform : null;
        }
    }

    private void OnEnable()
    {
        RestoreFromCache();
    }

    private void OnDisable()
    {
        SaveStateToCache();
    }

    public Vector3 GetCustomerPickupPosition()
    {
        return GetCustomerPickupPosition(transform.position);
    }

    public Vector3 GetCustomerPickupPosition(Vector3 fromWorldPosition)
    {
        Transform pickup = ResolveCustomerPickupPoint();
        if (pickup != null)
        {
            return pickup.position;
        }

        Transform originTransform = ActiveSlotPoint != null ? ActiveSlotPoint : transform;
        Vector2 origin = originTransform.position;
        float d = Mathf.Max(0.2f, pickupCandidateDistance);

        List<Vector2> candidates = new List<Vector2>
        {
            origin + defaultCustomerPickupOffset,
            origin + new Vector2(0f, -d),
            origin + new Vector2(0f, d),
            origin + new Vector2(-d, 0f),
            origin + new Vector2(d, 0f),
            origin + new Vector2(-d, -d),
            origin + new Vector2(d, -d),
            origin + new Vector2(-d, d),
            origin + new Vector2(d, d),
            origin,
        };

        Vector2 from = fromWorldPosition;
        Vector2 preferredDirection = defaultCustomerPickupOffset.sqrMagnitude > 0.0001f
            ? defaultCustomerPickupOffset.normalized
            : Vector2.down;

        float bestFrontScore = float.MaxValue;
        Vector2 bestFront = origin;
        bool hasBestFront = false;

        for (int i = 0; i < candidates.Count; i++)
        {
            Vector2 candidate = candidates[i];
            if (IsPickupPointBlocked(candidate))
            {
                continue;
            }

            if (IsPickupPathBlocked(candidate, origin))
            {
                continue;
            }

            Vector2 offset = candidate - origin;
            if (offset.sqrMagnitude > 0.0001f)
            {
                float alignment = Vector2.Dot(offset.normalized, preferredDirection);
                if (alignment < 0.2f)
                {
                    continue;
                }
            }

            float score = (candidate - from).sqrMagnitude + (candidate - origin).sqrMagnitude * 0.12f;
            if (score < bestFrontScore)
            {
                bestFrontScore = score;
                bestFront = candidate;
                hasBestFront = true;
            }
        }

        if (hasBestFront)
        {
            return bestFront;
        }

        float bestPathScore = float.MaxValue;
        Vector2 bestPathCandidate = origin;
        bool hasBestPath = false;

        for (int i = 0; i < candidates.Count; i++)
        {
            Vector2 candidate = candidates[i];
            if (IsPickupPointBlocked(candidate))
            {
                continue;
            }

            if (IsPickupPathBlocked(candidate, origin))
            {
                continue;
            }

            float score = (candidate - from).sqrMagnitude;
            if (score < bestPathScore)
            {
                bestPathScore = score;
                bestPathCandidate = candidate;
                hasBestPath = true;
            }
        }

        if (hasBestPath)
        {
            return bestPathCandidate;
        }

        float bestFallbackScore = float.MaxValue;
        Vector2 bestFallback = origin;
        bool hasFallback = false;

        for (int i = 0; i < candidates.Count; i++)
        {
            Vector2 candidate = candidates[i];
            if (IsPickupPointBlocked(candidate))
            {
                continue;
            }

            float score = (candidate - from).sqrMagnitude;
            if (score < bestFallbackScore)
            {
                bestFallbackScore = score;
                bestFallback = candidate;
                hasFallback = true;
            }
        }

        return hasFallback ? bestFallback : origin;
    }

    private float ScorePickupCandidate(Vector2 candidate, Vector2 from)
    {
        float score = (candidate - from).sqrMagnitude;

        if (IsPickupPointBlocked(candidate))
        {
            score += 100000f;
        }

        if (IsPickupPathBlocked(from, candidate))
        {
            score += 1500f;
        }

        Vector2 origin = ActiveSlotPoint != null ? (Vector2)ActiveSlotPoint.position : (Vector2)transform.position;
        if (IsPickupPathBlocked(candidate, origin))
        {
            score += 4000f;
        }

        return score;
    }

    public void RefreshSlots()
    {
        RefreshVisual();
    }

    public bool IsValidFood(ItemSO itemSO)
    {
        if (itemSO == null) return false;

        if (itemSO.category == ItemCategory.FoodSales) return true;
        if (allowFoodSuppliesCategory && itemSO.category == ItemCategory.FoodSupplies) return true;
        if (allowConsumableType && itemSO.itemType == ItemType.Consumable) return true;

        return false;
    }

    public bool CanPlaceItem(Item item)
    {
        if (item == null || item.itemSO == null) return false;
        if (!IsValidFood(item.itemSO)) return false;

        if (!HasFood)
        {
            return true;
        }

        return IsSameFoodTypeAndQuality(item) && TotalFoodCount < MaxStackAmount;
    }

    public bool CanAddToStack(int amount = 1)
    {
        if (!HasFood) return amount > 0;
        if (amount <= 0) return false;

        return TotalFoodCount + amount <= MaxStackAmount;
    }

    public bool TryPlaceFood(Item item, int amount = 1)
    {
        if (item == null || item.itemSO == null) return false;
        if (!IsValidFood(item.itemSO)) return false;

        int requestedAmount = Mathf.Max(1, Mathf.Max(amount, item.amount));

        if (!HasFood)
        {
            int placeAmount = Mathf.Clamp(requestedAmount, 1, MaxStackAmount);
            storedFood = new Item(item.itemSO, placeAmount, item.quality);
            storedUnitPrice = GetRestaurantUnitPrice(item);

            RefreshVisual();
            SaveStateToCache();
            OnCounterStockChanged?.Invoke();
            return true;
        }

        if (!IsSameFoodTypeAndQuality(item))
        {
            return false;
        }

        if (!CanAddToStack(requestedAmount))
        {
            return false;
        }

        storedFood.amount += requestedAmount;
        storedUnitPrice = GetRestaurantUnitPrice(item);

        RefreshVisual();
        SaveStateToCache();
        OnCounterStockChanged?.Invoke();
        return true;
    }

    public Item GetStoredFoodCopy()
    {
        if (!HasFood) return null;

        return new Item(storedFood.itemSO, storedFood.amount, storedFood.quality);
    }

    public bool TryTakeOneFood(out Item takenItem)
    {
        takenItem = null;
        if (!HasFood)
        {
            return false;
        }

        takenItem = new Item(storedFood.itemSO, 1, storedFood.quality);

        storedFood.amount -= 1;
        if (storedFood.amount <= 0)
        {
            ClearStoredFood();
        }
        else
        {
            RefreshVisual();
            SaveStateToCache();
            OnCounterStockChanged?.Invoke();
        }

        return true;
    }

    public bool TryTakeRandomFood(out ServedFood servedFood)
    {
        servedFood = default;
        if (!HasFood) return false;

        servedFood = BuildServedFood(storedFood, storedUnitPrice);

        storedFood.amount -= 1;
        if (storedFood.amount <= 0)
        {
            ClearStoredFood();
        }
        else
        {
            RefreshVisual();
            SaveStateToCache();
            OnCounterStockChanged?.Invoke();
        }

        return true;
    }

    public void ClearStoredFood()
    {
        storedFood = null;
        storedUnitPrice = 0;
        ClearVisual();
        SaveStateToCache();
        OnCounterStockChanged?.Invoke();
    }

    public RestaurantCounterDTO CaptureState()
    {
        return new RestaurantCounterDTO
        {
            counterId = CounterId,
            storedFood = ItemSerializer.ToDTO(storedFood),
            storedUnitPrice = storedUnitPrice,
        };
    }

    public void ApplyState(RestaurantCounterDTO state)
    {
        if (state == null || state.counterId != CounterId)
        {
            return;
        }

        restoringState = true;
        storedFood = ItemSerializer.FromDTO(state.storedFood);
        storedUnitPrice = state.storedUnitPrice;

        if (HasFood && storedUnitPrice <= 0)
        {
            storedUnitPrice = GetRestaurantUnitPrice(storedFood);
        }

        if (!HasFood)
        {
            storedFood = null;
            storedUnitPrice = 0;
        }

        RefreshVisual();
        restoringState = false;
        OnCounterStockChanged?.Invoke();
    }

    private bool IsSameFoodTypeAndQuality(Item item)
    {
        if (!HasFood || item == null || item.itemSO == null)
        {
            return false;
        }

        return storedFood.itemSO == item.itemSO && storedFood.quality == item.quality;
    }

    private ServedFood BuildServedFood(Item food, int unitPrice)
    {
        string itemName = food.itemSO.GetDisplayName();
        if (food.itemSO.UsesQuality())
        {
            itemName = $"{itemName} ({food.quality})";
        }

        return new ServedFood
        {
            itemName = itemName,
            category = food.itemSO.category,
            quality = food.quality,
            totalPrice = Mathf.Max(1, unitPrice),
            icon = food.itemSO.icon,
        };
    }

    private int GetRestaurantUnitPrice(Item item)
    {
        if (item == null || item.itemSO == null)
        {
            return 1;
        }

        int vendorSellPrice = Mathf.Max(1, item.GetSellPrice());
        int restaurantPrice = Mathf.RoundToInt(vendorSellPrice * Mathf.Max(1f, restaurantPriceMultiplier));
        return Mathf.Max(vendorSellPrice + 1, restaurantPrice);
    }

    private void SaveStateToCache()
    {
        if (restoringState)
        {
            return;
        }

        RestaurantCounterStateCache.Save(CaptureState());
    }

    private void RestoreFromCache()
    {
        if (RestaurantCounterStateCache.TryGetState(CounterId, out RestaurantCounterDTO state))
        {
            ApplyState(state);
        }
    }

    private string ResolveCounterId()
    {
        if (!string.IsNullOrWhiteSpace(counterId))
        {
            return counterId.Trim();
        }

        return $"{gameObject.scene.name}/{BuildHierarchyPath(transform)}";
    }

    private static string BuildHierarchyPath(Transform target)
    {
        if (target == null)
        {
            return string.Empty;
        }

        Stack<string> names = new Stack<string>();
        Transform current = target;
        while (current != null)
        {
            names.Push(current.name);
            current = current.parent;
        }

        return string.Join("/", names);
    }

    private Transform ResolveCustomerPickupPoint()
    {
        if (cachedCustomerPickupPoint != null)
        {
            return cachedCustomerPickupPoint;
        }

        if (string.IsNullOrWhiteSpace(customerPickupPointName))
        {
            return null;
        }

        Transform[] allChildren = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < allChildren.Length; i++)
        {
            Transform child = allChildren[i];
            if (child == null || child == transform)
            {
                continue;
            }

            if (string.Equals(child.name, customerPickupPointName, StringComparison.OrdinalIgnoreCase))
            {
                cachedCustomerPickupPoint = child;
                return cachedCustomerPickupPoint;
            }
        }

        return null;
    }

    private bool IsPickupPointBlocked(Vector2 point)
    {
        float radius = Mathf.Max(0.04f, pickupProbeRadius);
        Collider2D[] hits = Physics2D.OverlapCircleAll(point, radius, pickupObstacleMask);

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null)
            {
                continue;
            }

            if (!pickupIncludeTriggers && hit.isTrigger)
            {
                continue;
            }

            if (hit.transform == transform || hit.transform.IsChildOf(transform))
            {
                continue;
            }

            if (hit.GetComponentInParent<RestaurantCustomerAI>() != null)
            {
                continue;
            }

            if (hit.GetComponentInParent<PlayerMovement>() != null)
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private bool IsPickupPathBlocked(Vector2 from, Vector2 to)
    {
        Vector2 delta = to - from;
        float distance = delta.magnitude;
        if (distance <= 0.01f)
        {
            return false;
        }

        Vector2 direction = delta / distance;
        float radius = Mathf.Max(0.03f, pickupProbeRadius * 0.8f);
        RaycastHit2D[] hits = Physics2D.CircleCastAll(from, radius, direction, distance, pickupObstacleMask);

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i].collider;
            if (hit == null)
            {
                continue;
            }

            if (!pickupIncludeTriggers && hit.isTrigger)
            {
                continue;
            }

            if (hit.transform == transform || hit.transform.IsChildOf(transform))
            {
                continue;
            }

            if (hit.GetComponentInParent<RestaurantCustomerAI>() != null)
            {
                continue;
            }

            if (hit.GetComponentInParent<PlayerMovement>() != null)
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private void OnTransformChildrenChanged()
    {
        cachedCustomerPickupPoint = null;
    }

    private void RefreshVisual()
    {
        ClearVisual();

        if (!HasFood) return;

        Transform target = ActiveSlotPoint;
        if (target == null) return;

        if (visualPrefab != null)
        {
            visualObject = Instantiate(visualPrefab, target.position, Quaternion.identity, target);
        }
        else
        {
            visualObject = new GameObject($"CounterFood_{storedFood.itemSO.name}_{storedFood.quality}");
            visualObject.transform.SetParent(target, false);
            visualObject.transform.localPosition = Vector3.zero;
            visualObject.transform.localScale = defaultVisualScale;
        }

        if (visualObject == null) return;

        visualRenderer = visualObject.GetComponent<SpriteRenderer>();
        if (visualRenderer == null)
        {
            visualRenderer = visualObject.AddComponent<SpriteRenderer>();
        }

        visualRenderer.sprite = storedFood.itemSO.icon;
        visualRenderer.sortingOrder = defaultSortingOrder;
    }

    private void ClearVisual()
    {
        if (visualObject != null)
        {
            Destroy(visualObject);
        }

        visualObject = null;
        visualRenderer = null;
    }
}

public static class RestaurantCounterStateCache
{
    private static readonly Dictionary<string, RestaurantCounterDTO> states = new Dictionary<string, RestaurantCounterDTO>();

    public static void Save(RestaurantCounterDTO state)
    {
        if (state == null || string.IsNullOrWhiteSpace(state.counterId))
        {
            return;
        }

        states[state.counterId] = Clone(state);
    }

    public static bool TryGetState(string counterId, out RestaurantCounterDTO state)
    {
        if (!string.IsNullOrWhiteSpace(counterId) && states.TryGetValue(counterId, out RestaurantCounterDTO cachedState))
        {
            state = Clone(cachedState);
            return true;
        }

        state = null;
        return false;
    }

    public static List<RestaurantCounterDTO> CaptureState()
    {
        RestaurantCounter[] activeCounters = UnityEngine.Object.FindObjectsOfType<RestaurantCounter>(true);
        foreach (RestaurantCounter counter in activeCounters)
        {
            if (counter != null)
            {
                Save(counter.CaptureState());
            }
        }

        return new List<RestaurantCounterDTO>(states.Values);
    }

    public static void RestoreState(List<RestaurantCounterDTO> restoredStates)
    {
        states.Clear();
        if (restoredStates != null)
        {
            foreach (RestaurantCounterDTO state in restoredStates)
            {
                Save(state);
            }
        }

        ApplyToActiveCounters();
    }

    private static void ApplyToActiveCounters()
    {
        RestaurantCounter[] activeCounters = UnityEngine.Object.FindObjectsOfType<RestaurantCounter>(true);
        foreach (RestaurantCounter counter in activeCounters)
        {
            if (counter != null && TryGetState(counter.CounterId, out RestaurantCounterDTO state))
            {
                counter.ApplyState(state);
            }
        }
    }

    private static RestaurantCounterDTO Clone(RestaurantCounterDTO source)
    {
        if (source == null)
        {
            return null;
        }

        return new RestaurantCounterDTO
        {
            counterId = source.counterId,
            storedUnitPrice = source.storedUnitPrice,
            storedFood = CloneItem(source.storedFood),
        };
    }

    private static ItemDTO CloneItem(ItemDTO source)
    {
        if (source == null)
        {
            return new ItemDTO();
        }

        return new ItemDTO
        {
            present = source.present,
            itemId = source.itemId,
            itemName = source.itemName,
            amount = source.amount,
            quality = source.quality,
            finalQualityScore = source.finalQualityScore,
        };
    }
}






