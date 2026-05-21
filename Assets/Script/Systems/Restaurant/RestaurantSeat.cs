using System;
using UnityEngine;

public class RestaurantSeat : MonoBehaviour
{
    [Header("Seat Points")]
    [HideInInspector][SerializeField] private Transform sitPoint;
    [HideInInspector][SerializeField] private Transform approachPoint;

    [Header("Table Food Visual")]
    [HideInInspector][SerializeField] private Transform foodPoint;
    [HideInInspector][SerializeField] private GameObject foodVisualPrefab;
    [HideInInspector][SerializeField] private int foodSortingOrder = 18;
    [HideInInspector][SerializeField] private Vector3 generatedFoodScale = Vector3.one;

    [HideInInspector][SerializeField] private bool autoResolvePointsFromChildren = true;
    [HideInInspector][SerializeField] private string sitPointChildName = "SitPoint";
    [HideInInspector][SerializeField] private string approachPointChildName = "ApproachPoint";
    [HideInInspector][SerializeField] private string foodPointChildName = "FoodPoint";
    [HideInInspector][SerializeField] private bool generateApproachPointIfMissing = true;
    [HideInInspector][SerializeField] private float generatedApproachDistance = 0.58f;
    [HideInInspector][SerializeField] private string siblingTableName = "Table";
    [HideInInspector][SerializeField] private float generatedApproachProbeRadius = 0.12f;
    [HideInInspector][SerializeField] private LayerMask generatedApproachObstacleMask = ~0;
    [HideInInspector][SerializeField] private bool generatedApproachIncludeTriggers;

    private RestaurantCustomerAI reservedBy;
    private RestaurantCustomerAI occupiedBy;

    private GameObject foodVisualObject;
    private SpriteRenderer foodRenderer;

    private Transform cachedSitPoint;
    private Transform cachedApproachPoint;
    private Transform cachedFoodPoint;

    public Transform SitPoint => ResolveSitPoint();
    public Transform ApproachPoint => ResolveApproachPoint();
    public bool IsAvailable => reservedBy == null && occupiedBy == null;

    private Transform ActiveFoodPoint => ResolveFoodPoint();

    private void Awake()
    {
        RefreshPointCache();
    }

    private void OnDisable()
    {
        ClearFoodVisual();
    }

    private void OnTransformChildrenChanged()
    {
        ClearPointCache();
    }

    public Vector3 GetApproachPosition()
    {
        Transform explicitApproach = ResolveApproachPoint();
        if (explicitApproach != null)
        {
            return explicitApproach.position;
        }

        if (generateApproachPointIfMissing)
        {
            return ComputeGeneratedApproachPosition();
        }

        Transform fallbackSit = ResolveSitPoint();
        return fallbackSit != null ? fallbackSit.position : transform.position;
    }

    public bool TryReserve(RestaurantCustomerAI customer)
    {
        if (customer == null) return false;
        if (!IsAvailable) return false;

        reservedBy = customer;
        return true;
    }

    public bool MarkOccupied(RestaurantCustomerAI customer)
    {
        return MarkOccupied(customer, default);
    }

    public bool MarkOccupied(RestaurantCustomerAI customer, RestaurantCounter.ServedFood servedFood)
    {
        if (customer == null) return false;
        if (occupiedBy != null && occupiedBy != customer) return false;
        if (reservedBy != null && reservedBy != customer) return false;

        reservedBy = customer;
        occupiedBy = customer;

        ShowFoodVisual(servedFood.icon);
        return true;
    }

    public void Release(RestaurantCustomerAI customer = null)
    {
        if (customer == null)
        {
            reservedBy = null;
            occupiedBy = null;
            ClearFoodVisual();
            return;
        }

        if (reservedBy == customer)
        {
            reservedBy = null;
        }

        if (occupiedBy == customer)
        {
            occupiedBy = null;
            ClearFoodVisual();
        }
    }

    private void ShowFoodVisual(Sprite sprite)
    {
        ClearFoodVisual();

        if (sprite == null)
        {
            return;
        }

        Transform target = ActiveFoodPoint;
        if (target == null)
        {
            return;
        }

        if (foodVisualPrefab != null)
        {
            foodVisualObject = Instantiate(foodVisualPrefab, target.position, Quaternion.identity, target);
            foodVisualObject.transform.localPosition = Vector3.zero;
        }
        else
        {
            foodVisualObject = new GameObject($"SeatFood_{sprite.name}");
            foodVisualObject.transform.SetParent(target, false);
            foodVisualObject.transform.localPosition = Vector3.zero;
            foodVisualObject.transform.localScale = generatedFoodScale;
        }

        if (foodVisualObject == null)
        {
            return;
        }

        foodRenderer = foodVisualObject.GetComponent<SpriteRenderer>();
        if (foodRenderer == null)
        {
            foodRenderer = foodVisualObject.GetComponentInChildren<SpriteRenderer>();
        }

        if (foodRenderer == null)
        {
            foodRenderer = foodVisualObject.AddComponent<SpriteRenderer>();
        }

        foodRenderer.sprite = sprite;
        foodRenderer.sortingOrder = foodSortingOrder;
    }

    private void ClearFoodVisual()
    {
        if (foodVisualObject != null)
        {
            Destroy(foodVisualObject);
        }

        foodVisualObject = null;
        foodRenderer = null;
    }

    private Transform ResolveSitPoint()
    {
        if (sitPoint != null)
        {
            return sitPoint;
        }

        if (cachedSitPoint == null)
        {
            cachedSitPoint = FindPointByName(sitPointChildName);
        }

        return cachedSitPoint != null ? cachedSitPoint : transform;
    }

    private Transform ResolveApproachPoint()
    {
        if (approachPoint != null)
        {
            return approachPoint;
        }

        if (cachedApproachPoint == null)
        {
            cachedApproachPoint = FindPointByName(approachPointChildName);
        }

        return cachedApproachPoint;
    }

    private Transform ResolveFoodPoint()
    {
        if (foodPoint != null)
        {
            return foodPoint;
        }

        if (cachedFoodPoint == null)
        {
            cachedFoodPoint = FindPointByName(foodPointChildName);
        }

        return cachedFoodPoint != null ? cachedFoodPoint : ResolveSitPoint();
    }

    private Transform FindPointByName(string pointName)
    {
        if (!autoResolvePointsFromChildren || string.IsNullOrWhiteSpace(pointName))
        {
            return null;
        }

        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child == null || child == transform)
            {
                continue;
            }

            if (string.Equals(child.name, pointName, StringComparison.OrdinalIgnoreCase))
            {
                return child;
            }
        }

        return null;
    }

    private Vector3 ComputeGeneratedApproachPosition()
    {
        Transform sit = ResolveSitPoint();
        Vector2 seatPosition = sit != null ? (Vector2)sit.position : (Vector2)transform.position;

        Vector2 awayFromTable = Vector2.zero;
        Transform tableTransform = FindSiblingTableTransform();
        if (tableTransform != null)
        {
            awayFromTable = seatPosition - (Vector2)tableTransform.position;
        }

        if (awayFromTable.sqrMagnitude < 0.0001f)
        {
            awayFromTable = Vector2.down;
        }

        awayFromTable.Normalize();
        float distance = Mathf.Max(0.7f, generatedApproachDistance);

        Vector2 perpendicular = new Vector2(-awayFromTable.y, awayFromTable.x);
        Vector2[] candidates =
        {
            seatPosition + awayFromTable * (distance * 1.25f),
            seatPosition + awayFromTable * distance,
            seatPosition + (awayFromTable + perpendicular).normalized * distance,
            seatPosition + (awayFromTable - perpendicular).normalized * distance,
            seatPosition + perpendicular * distance,
            seatPosition - perpendicular * distance,
            seatPosition - awayFromTable * (distance * 0.6f),
        };

        for (int i = 0; i < candidates.Length; i++)
        {
            if (!IsApproachPointBlocked(candidates[i]))
            {
                return candidates[i];
            }
        }

        return candidates[0];
    }

    private bool IsApproachPointBlocked(Vector2 worldPoint)
    {
        float radius = Mathf.Max(0.03f, generatedApproachProbeRadius);
        Collider2D[] hits = Physics2D.OverlapCircleAll(worldPoint, radius, generatedApproachObstacleMask);

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null)
            {
                continue;
            }

            if (!generatedApproachIncludeTriggers && hit.isTrigger)
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

    private Transform FindSiblingTableTransform()
    {
        Transform parent = transform.parent;
        if (parent == null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(siblingTableName))
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child == null || child == transform)
                {
                    continue;
                }

                if (string.Equals(child.name, siblingTableName, StringComparison.OrdinalIgnoreCase))
                {
                    return child;
                }
            }
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child == null || child == transform)
            {
                continue;
            }

            if (child.name.IndexOf("Table", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return child;
            }
        }

        return null;
    }

    private void RefreshPointCache()
    {
        ClearPointCache();

        if (sitPoint == null)
        {
            cachedSitPoint = FindPointByName(sitPointChildName);
        }

        if (approachPoint == null)
        {
            cachedApproachPoint = FindPointByName(approachPointChildName);
        }

        if (foodPoint == null)
        {
            cachedFoodPoint = FindPointByName(foodPointChildName);
        }
    }

    private void ClearPointCache()
    {
        cachedSitPoint = null;
        cachedApproachPoint = null;
        cachedFoodPoint = null;
    }
}




