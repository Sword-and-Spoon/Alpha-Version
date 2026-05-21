using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(RestaurantCustomerMotor2D))]
public class RestaurantCustomerAI : MonoBehaviour
{
    private enum CustomerState
    {
        None,
        GoingToCounter,
        ChoosingFood,
        FindingSeat,
        GoingToSeat,
        Eating,
        Exiting,
    }

    [Header("Timing")]
    [SerializeField] private Vector2 choosingFoodDelayRange = new Vector2(0.5f, 1.5f);
    [SerializeField] private Vector2 eatingDurationRange = new Vector2(8f, 16f);
    [SerializeField] private float seatRetryInterval = 2f;
    [SerializeField] private float maxSeatWaitDuration = 20f;
    [SerializeField] private float maxMoveDurationPerTarget = 14f;
    [HideInInspector][SerializeField] private float stuckMoveBreakDuration = 1.1f;
    [HideInInspector][SerializeField] private float stuckMoveDistanceEpsilon = 0.01f;
    [HideInInspector][SerializeField] private int counterMoveRetryCount = 4;
    [HideInInspector][SerializeField] private float counterArrivalDistance = 0.45f;
    [HideInInspector][SerializeField] private float counterAcceptanceDistance = 2.4f;

    [Header("Seat Movement")]
    [HideInInspector][SerializeField] private float seatApproachArrivalExtraDistance = 1f;
    [HideInInspector][SerializeField] private float seatSnapDistance = 1.2f;
    [HideInInspector][SerializeField] private float seatAcceptanceDistance = 2.4f;
    [HideInInspector][SerializeField] private float seatExitMoveExtraDistance = 0.45f;
    [HideInInspector][SerializeField] private float seatReleaseDistance = 0.55f;

    [HideInInspector][SerializeField] private bool autoSortByY = true;
    [HideInInspector][SerializeField] private int ySortBaseOrder = 5000;
    [HideInInspector][SerializeField] private int ySortUnitsPerWorldUnit = 100;

    private RestaurantServiceManager serviceManager;
    private RestaurantCounter counter;
    private Transform exitTarget;

    private RestaurantSeat reservedSeat;
    private RestaurantCustomerMotor2D motor;
    private Rigidbody2D customerBody;

    private SpriteRenderer[] customerRenderers;
    private int[] rendererOrderOffsets;
    private SortingGroup sortingGroup;

    private Coroutine behaviorRoutine;
    private CustomerState currentState;
    private bool forceExit;
    private bool counterReservationReleased;

    public bool IsExiting => currentState == CustomerState.Exiting;

    private void Awake()
    {
        motor = GetComponent<RestaurantCustomerMotor2D>();
        customerBody = GetComponent<Rigidbody2D>();

        CacheSortingTargets();
        ApplyYSortOrder();
    }

    private void LateUpdate()
    {
        ApplyYSortOrder();
    }

    private void OnDisable()
    {
        SetSeatedState(false);

        if (reservedSeat != null)
        {
            reservedSeat.Release(this);
            reservedSeat = null;
        }
    }

    public void Initialize(
        RestaurantServiceManager serviceManager,
        RestaurantCounter counter,
        Transform exitTarget)
    {
        this.serviceManager = serviceManager;
        this.counter = counter;
        this.exitTarget = exitTarget;

        forceExit = false;
        currentState = CustomerState.None;
        counterReservationReleased = false;

        seatApproachArrivalExtraDistance = Mathf.Max(0.8f, seatApproachArrivalExtraDistance);
        seatSnapDistance = Mathf.Max(1.5f, seatSnapDistance);
        seatAcceptanceDistance = Mathf.Max(seatSnapDistance + 0.25f, seatAcceptanceDistance);
        seatExitMoveExtraDistance = Mathf.Max(0.3f, seatExitMoveExtraDistance);
        seatReleaseDistance = Mathf.Max(0.45f, seatReleaseDistance);
        counterMoveRetryCount = Mathf.Clamp(counterMoveRetryCount, 0, 5);
        counterArrivalDistance = Mathf.Max(0.2f, counterArrivalDistance);
        counterAcceptanceDistance = Mathf.Max(counterArrivalDistance + 0.25f, counterAcceptanceDistance);
        maxMoveDurationPerTarget = Mathf.Clamp(maxMoveDurationPerTarget, 4f, 45f);
        stuckMoveBreakDuration = Mathf.Clamp(stuckMoveBreakDuration, 0.35f, 4f);
        stuckMoveDistanceEpsilon = Mathf.Clamp(stuckMoveDistanceEpsilon, 0.0015f, 0.08f);

        SetSeatedState(false);

        if (behaviorRoutine != null)
        {
            StopCoroutine(behaviorRoutine);
        }

        behaviorRoutine = StartCoroutine(CustomerFlowRoutine());
    }

    public void ForceExitNow()
    {
        forceExit = true;
        SetSeatedState(false);

        if (behaviorRoutine != null)
        {
            StopCoroutine(behaviorRoutine);
        }

        behaviorRoutine = StartCoroutine(ExitRoutine());
    }

    private IEnumerator CustomerFlowRoutine()
    {
        if (counter == null)
        {
            ReleaseCounterReservationIfNeeded();
            yield return ExitRoutine();
            yield break;
        }

        currentState = CustomerState.GoingToCounter;
        Vector3 targetCounterPosition = counter.GetCustomerPickupPosition(transform.position);
        bool reachedCounter = false;
        int counterAttempts = Mathf.Clamp(counterMoveRetryCount, 0, 5);
        float broadCounterDistance = Mathf.Max(counterArrivalDistance, 1.55f);

        for (int attempt = 0; attempt <= counterAttempts; attempt++)
        {
            yield return MoveToRoutine(targetCounterPosition);

            bool nearPickup = IsNearPosition(targetCounterPosition, counterArrivalDistance);
            bool nearCounterBody = IsNearPosition(counter.transform.position, broadCounterDistance);
            bool nearCounterExtended = IsNearPosition(counter.transform.position, counterAcceptanceDistance);
            if (nearPickup || nearCounterBody || nearCounterExtended)
            {
                reachedCounter = true;
                break;
            }

            if (forceExit)
            {
                break;
            }

            targetCounterPosition = counter.GetCustomerPickupPosition(transform.position);
        }

        if (!reachedCounter && IsNearPosition(counter.transform.position, Mathf.Max(broadCounterDistance * 1.15f, counterAcceptanceDistance)))
        {
            reachedCounter = true;
        }

        if (!reachedCounter)
        {
            Vector3 counterBodyTarget = counter.transform.position;
            yield return MoveToRoutine(counterBodyTarget, 0.65f);
            if (IsNearPosition(counterBodyTarget, Mathf.Max(broadCounterDistance * 1.2f, counterAcceptanceDistance)))
            {
                reachedCounter = true;
            }
        }

        if (!reachedCounter)
        {
            ReleaseCounterReservationIfNeeded();
            yield return ExitRoutine();
            yield break;
        }

        if (forceExit)
        {
            ReleaseCounterReservationIfNeeded();
            yield return ExitRoutine();
            yield break;
        }

        currentState = CustomerState.ChoosingFood;
        yield return new WaitForSeconds(Random.Range(choosingFoodDelayRange.x, choosingFoodDelayRange.y));

        ReleaseCounterReservationIfNeeded();

        if (!counter.TryTakeRandomFood(out RestaurantCounter.ServedFood servedFood))
        {
            yield return ExitRoutine();
            yield break;
        }

        Vector3 salePopupPosition = counter != null
            ? counter.GetCustomerPickupPosition(transform.position)
            : transform.position;
        serviceManager?.RegisterSale(servedFood, salePopupPosition);

        currentState = CustomerState.FindingSeat;
        float waitSeatTimer = 0f;

        while (reservedSeat == null && !forceExit)
        {
            if (serviceManager != null && serviceManager.TryReserveSeat(this, out RestaurantSeat seat))
            {
                reservedSeat = seat;
                break;
            }

            if (waitSeatTimer >= maxSeatWaitDuration)
            {
                break;
            }

            yield return new WaitForSeconds(seatRetryInterval);
            waitSeatTimer += seatRetryInterval;
        }

        if (reservedSeat == null || forceExit)
        {
            yield return ExitRoutine();
            yield break;
        }

        currentState = CustomerState.GoingToSeat;
        Vector3 seatApproachPosition = reservedSeat.GetApproachPosition();
        yield return MoveToRoutine(seatApproachPosition, seatApproachArrivalExtraDistance);

        if (forceExit)
        {
            SetSeatedState(false);
            yield return ExitRoutine();
            yield break;
        }

        if (reservedSeat == null)
        {
            SetSeatedState(false);
            yield return ExitRoutine();
            yield break;
        }

        Vector3 sitPosition = reservedSeat.SitPoint.position;
        bool nearSitPoint = IsNearPosition(sitPosition, seatSnapDistance);
        bool nearApproachPoint = IsNearPosition(seatApproachPosition, seatSnapDistance);
        bool nearSeatExtended = IsNearPosition(sitPosition, seatAcceptanceDistance) || IsNearPosition(seatApproachPosition, seatAcceptanceDistance);
        if (!nearSitPoint && !nearApproachPoint && !nearSeatExtended)
        {
            SetSeatedState(false);
            reservedSeat.Release(this);
            reservedSeat = null;

            yield return ExitRoutine();
            yield break;
        }

        SetSeatedState(true);
        SnapToPosition(sitPosition);

        bool occupied = reservedSeat.MarkOccupied(this, servedFood);
        if (!occupied)
        {
            SetSeatedState(false);
            if (reservedSeat != null)
            {
                reservedSeat.Release(this);
                reservedSeat = null;
            }

            yield return ExitRoutine();
            yield break;
        }

        currentState = CustomerState.Eating;
        yield return new WaitForSeconds(Random.Range(eatingDurationRange.x, eatingDurationRange.y));

        yield return ExitRoutine();
    }

    private IEnumerator MoveToRoutine(Vector3 destination, float extraDistance = 0.05f)
    {
        if (motor == null)
        {
            yield break;
        }

        bool canMove = motor.MoveTo(destination);
        if (!canMove)
        {
            yield break;
        }

        float timeout = maxMoveDurationPerTarget;
        float stuckTimer = 0f;
        Vector2 lastPosition = transform.position;
        float moveEpsilon = Mathf.Max(0.001f, stuckMoveDistanceEpsilon);

        while (!motor.HasReachedDestination(extraDistance))
        {
            timeout -= Time.deltaTime;
            if (timeout <= 0f)
            {
                break;
            }

            if (forceExit && currentState != CustomerState.Exiting)
            {
                break;
            }

            Vector2 currentPosition = transform.position;
            float moved = Vector2.Distance(currentPosition, lastPosition);
            if (moved <= moveEpsilon)
            {
                stuckTimer += Time.deltaTime;
                if (stuckTimer >= stuckMoveBreakDuration)
                {
                    break;
                }
            }
            else
            {
                lastPosition = currentPosition;
                stuckTimer = 0f;
            }

            yield return null;
        }

        motor.StopMove();
    }

    private IEnumerator ExitRoutine()
    {
        currentState = CustomerState.Exiting;

        ReleaseCounterReservationIfNeeded();
        SetSeatedState(false);

        RestaurantSeat seatToRelease = reservedSeat;
        if (seatToRelease != null)
        {
            Vector3 seatExitPosition = BuildSafeSeatExitPosition(seatToRelease);

            // Release immediately so food visual clears at the first stand-up.
            seatToRelease.Release(this);
            if (reservedSeat == seatToRelease)
            {
                reservedSeat = null;
            }

            if (!IsNearPosition(seatExitPosition, seatReleaseDistance))
            {
                yield return MoveToRoutine(seatExitPosition, seatExitMoveExtraDistance);

                if (!IsNearPosition(seatExitPosition, seatReleaseDistance))
                {
                    SnapToPosition(seatExitPosition);
                }
            }
        }

        Transform targetExit = exitTarget;
        if (targetExit != null)
        {
            yield return MoveToRoutine(targetExit.position);
            if (!IsNearPosition(targetExit.position, 0.7f))
            {
                SnapToPosition(targetExit.position);
            }
        }

        if (serviceManager != null)
        {
            serviceManager.NotifyCustomerExited(this);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private Vector3 BuildSafeSeatExitPosition(RestaurantSeat seat)
    {
        if (seat == null)
        {
            return transform.position;
        }

        Vector3 sitPosition = seat.SitPoint != null ? seat.SitPoint.position : seat.transform.position;
        Vector3 approach = seat.GetApproachPosition();

        float minDistance = Mathf.Max(0.45f, seatReleaseDistance);
        float distance = Vector2.Distance(approach, sitPosition);
        if (distance >= minDistance)
        {
            return approach;
        }

        Vector2 dir = (Vector2)approach - (Vector2)sitPosition;
        if (dir.sqrMagnitude < 0.0001f)
        {
            dir = Vector2.down;
        }

        return sitPosition + (Vector3)(dir.normalized * minDistance);
    }

    private void ReleaseCounterReservationIfNeeded()
    {
        if (counterReservationReleased)
        {
            return;
        }

        counterReservationReleased = true;

        if (serviceManager != null && counter != null)
        {
            serviceManager.ReleasePendingCounterReservation(counter);
        }
    }

    private void SetSeatedState(bool seated)
    {
        if (!seated)
        {
            return;
        }

        if (motor != null)
        {
            motor.ForceIdleDownPose();
        }
    }

    private bool IsNearPosition(Vector3 worldPosition, float maxDistance)
    {
        Vector2 a = transform.position;
        Vector2 b = worldPosition;
        return Vector2.Distance(a, b) <= Mathf.Max(0.05f, maxDistance);
    }

    private void SnapToPosition(Vector3 worldPosition)
    {
        Vector3 target = worldPosition;
        target.z = transform.position.z;

        if (customerBody != null)
        {
            customerBody.position = new Vector2(target.x, target.y);
        }

        transform.position = target;
    }

    private void CacheSortingTargets()
    {
        sortingGroup = GetComponent<SortingGroup>();
        customerRenderers = GetComponentsInChildren<SpriteRenderer>(true);

        if (customerRenderers == null || customerRenderers.Length == 0)
        {
            rendererOrderOffsets = null;
            return;
        }

        int minOrder = customerRenderers[0].sortingOrder;
        for (int i = 1; i < customerRenderers.Length; i++)
        {
            if (customerRenderers[i] != null)
            {
                minOrder = Mathf.Min(minOrder, customerRenderers[i].sortingOrder);
            }
        }

        rendererOrderOffsets = new int[customerRenderers.Length];
        for (int i = 0; i < customerRenderers.Length; i++)
        {
            SpriteRenderer renderer = customerRenderers[i];
            rendererOrderOffsets[i] = renderer != null ? renderer.sortingOrder - minOrder : 0;
        }
    }

    private void ApplyYSortOrder()
    {
        if (!autoSortByY)
        {
            return;
        }

        int units = Mathf.Max(1, ySortUnitsPerWorldUnit);
        int baseOrder = ySortBaseOrder - Mathf.RoundToInt(transform.position.y * units);

        if (sortingGroup != null)
        {
            sortingGroup.sortingOrder = baseOrder;
            return;
        }

        if (customerRenderers == null || customerRenderers.Length == 0 || rendererOrderOffsets == null)
        {
            CacheSortingTargets();
            if (customerRenderers == null || customerRenderers.Length == 0 || rendererOrderOffsets == null)
            {
                return;
            }
        }

        for (int i = 0; i < customerRenderers.Length; i++)
        {
            SpriteRenderer renderer = customerRenderers[i];
            if (renderer == null)
            {
                continue;
            }

            int offset = i < rendererOrderOffsets.Length ? rendererOrderOffsets[i] : 0;
            renderer.sortingOrder = baseOrder + offset;
        }
    }
}
