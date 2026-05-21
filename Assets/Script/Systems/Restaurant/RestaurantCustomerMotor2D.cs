using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public class RestaurantCustomerMotor2D : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 4.8f;
    [SerializeField] private float acceleration = 24f;
    [SerializeField] private float stoppingDistance = 0.08f;
    [SerializeField] private float waypointReachDistance = 0.06f;

    [Header("Path")]
    [SerializeField] private float nodeSearchRadius = 3.6f;

    [Header("Animator (Optional)")]
    [SerializeField] private Animator animator;
    [SerializeField] private SpriteRenderer bodySprite;
    [SerializeField] private bool autoFlipX = true;

    [HideInInspector][SerializeField] private bool autoFindPathGraph = true;
    [HideInInspector][SerializeField] private bool forceRigidbodyConfiguration = true;

    private Rigidbody2D rb2D;
    private RestaurantPathGraph cachedGraph;

    private readonly List<Vector3> currentPath = new();
    private int pathIndex;
    private bool isMoving;
    private Vector3 requestedDestination;
    private Vector3 resolvedTarget;

    private Vector2 velocity;
    private float currentSpeed;

    private bool hasHorizontal;
    private bool hasVertical;
    private bool hasSpeed;

    public float MoveSpeed => moveSpeed;

    private void Awake()
    {
        rb2D = GetComponent<Rigidbody2D>();

        ConfigurePhysicsBody();

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        hasHorizontal = HasAnimatorParameter("Horizontal", AnimatorControllerParameterType.Float);
        hasVertical = HasAnimatorParameter("Vertical", AnimatorControllerParameterType.Float);
        hasSpeed = HasAnimatorParameter("Speed", AnimatorControllerParameterType.Float);

        if (bodySprite == null) bodySprite = GetComponentInChildren<SpriteRenderer>();
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (rb2D == null)
        {
            rb2D = GetComponent<Rigidbody2D>();
        }

        ConfigurePhysicsBody();
    }

    private void Update()
    {
        UpdateAnimator();
    }

    private void FixedUpdate()
    {
        TickMove(Time.fixedDeltaTime);
    }

    public bool MoveTo(Vector3 destination)
    {
        requestedDestination = destination;
        resolvedTarget = destination;

        currentPath.Clear();
        pathIndex = 0;
        currentSpeed = 0f;
        velocity = Vector2.zero;
        isMoving = false;

        RestaurantPathGraph graph = ResolvePathGraph();
        if (graph == null)
        {
            return false;
        }

        float searchRadius = Mathf.Max(0.1f, nodeSearchRadius);
        bool hasPath = graph.TryFindPath(transform.position, destination, currentPath, out Vector3 resolved, searchRadius);
        if (!hasPath || currentPath.Count == 0)
        {
            return false;
        }

        resolvedTarget = resolved;
        isMoving = true;
        return true;
    }

    public void StopMove()
    {
        isMoving = false;
        currentPath.Clear();
        pathIndex = 0;
        currentSpeed = 0f;
        velocity = Vector2.zero;
    }

    public void ForceIdleDownPose()
    {
        velocity = Vector2.zero;

        if (animator == null)
        {
            return;
        }

        if (hasHorizontal)
        {
            animator.SetFloat("Horizontal", 0f);
        }

        if (hasVertical)
        {
            animator.SetFloat("Vertical", -1f);
        }

        if (hasSpeed)
        {
            animator.SetFloat("Speed", 0f);
        }

        if (autoFlipX && bodySprite != null)
        {
            bodySprite.flipX = false;
        }
    }

    public bool HasReachedDestination(float extraDistance = 0.05f)
    {
        float threshold = Mathf.Max(0.02f, stoppingDistance + extraDistance);

        if (isMoving)
        {
            return false;
        }

        Vector2 current = transform.position;
        float distResolved = Vector2.Distance(current, resolvedTarget);
        float distRequested = Vector2.Distance(current, requestedDestination);
        return distResolved <= threshold || distRequested <= threshold;
    }

    private RestaurantPathGraph ResolvePathGraph()
    {
        if (cachedGraph != null && cachedGraph.isActiveAndEnabled)
        {
            return cachedGraph;
        }

        if (!autoFindPathGraph)
        {
            return null;
        }

        RestaurantPathGraph[] graphs = FindObjectsOfType<RestaurantPathGraph>(true);
        if (graphs == null || graphs.Length == 0)
        {
            return null;
        }

        Vector2 current = transform.position;
        float bestSqr = float.MaxValue;
        RestaurantPathGraph best = null;

        for (int i = 0; i < graphs.Length; i++)
        {
            RestaurantPathGraph graph = graphs[i];
            if (graph == null || !graph.isActiveAndEnabled)
            {
                continue;
            }

            float sqr = ((Vector2)graph.transform.position - current).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                best = graph;
            }
        }

        cachedGraph = best;
        return cachedGraph;
    }

    private void TickMove(float deltaTime)
    {
        if (!isMoving)
        {
            velocity = Vector2.zero;
            return;
        }

        if (currentPath.Count == 0)
        {
            StopMove();
            return;
        }

        Vector2 current = rb2D != null ? rb2D.position : (Vector2)transform.position;

        while (pathIndex < currentPath.Count)
        {
            Vector2 waypoint = currentPath[pathIndex];
            if (Vector2.Distance(current, waypoint) > Mathf.Max(0.01f, waypointReachDistance))
            {
                break;
            }

            pathIndex++;
        }

        if (pathIndex >= currentPath.Count)
        {
            StopMove();
            return;
        }

        Vector2 target = currentPath[pathIndex];
        Vector2 delta = target - current;
        float distance = delta.magnitude;

        if (distance <= 0.0001f)
        {
            pathIndex++;
            velocity = Vector2.zero;
            return;
        }

        Vector2 direction = delta / distance;
        float accel = Mathf.Max(0.01f, acceleration);
        currentSpeed = Mathf.MoveTowards(currentSpeed, moveSpeed, accel * deltaTime);

        float step = Mathf.Min(distance, currentSpeed * deltaTime);
        Vector2 next = current + direction * step;

        if (rb2D != null && rb2D.simulated)
        {
            rb2D.MovePosition(next);
        }
        else
        {
            transform.position = new Vector3(next.x, next.y, transform.position.z);
        }

        velocity = (next - current) / Mathf.Max(0.0001f, deltaTime);
    }

    private void ConfigurePhysicsBody()
    {
        if (!forceRigidbodyConfiguration || rb2D == null)
        {
            return;
        }

        rb2D.gravityScale = 0f;
        rb2D.freezeRotation = true;
        rb2D.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb2D.interpolation = RigidbodyInterpolation2D.Interpolate;

        if (rb2D.bodyType != RigidbodyType2D.Kinematic)
        {
            rb2D.bodyType = RigidbodyType2D.Kinematic;
        }
    }

    private void UpdateAnimator()
    {
        if (animator == null)
        {
            return;
        }

        if (velocity.sqrMagnitude > 0.0001f)
        {
            Vector2 dir = velocity.normalized;
            if (hasHorizontal)
            {
                animator.SetFloat("Horizontal", dir.x);
            }

            if (hasVertical)
            {
                animator.SetFloat("Vertical", dir.y);
            }
        }

        if (hasSpeed)
        {
            animator.SetFloat("Speed", velocity.sqrMagnitude);
        }

        if (autoFlipX && bodySprite != null && Mathf.Abs(velocity.x) > 0.01f)
        {
            bodySprite.flipX = velocity.x > 0f;
        }
    }

    private bool HasAnimatorParameter(string parameterName, AnimatorControllerParameterType parameterType)
    {
        if (animator == null)
        {
            return false;
        }

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.name == parameterName && parameter.type == parameterType)
            {
                return true;
            }
        }

        return false;
    }
}
