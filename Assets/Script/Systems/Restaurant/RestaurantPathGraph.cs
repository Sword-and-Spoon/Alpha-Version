using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class RestaurantPathGraph : MonoBehaviour
{
    [Header("Grid")]
    [SerializeField] private float cellSize = 1f;
    [SerializeField] private Vector2 gridOrigin = Vector2.zero;
    [SerializeField] private bool snapNodesToGrid = true;
    [SerializeField] private bool autoBuildCardinalLinks = true;

    [Header("Path")]
    [SerializeField] private float nearestNodeSearchRadius = 2.5f;

    [Header("Debug")]
    [SerializeField] private bool drawLinkGizmos = true;
    [SerializeField] private Color linkColor = new Color(0.1f, 0.8f, 1f, 0.6f);

    private readonly List<RestaurantPathNode> cachedNodes = new();
    private readonly Dictionary<Vector2Int, RestaurantPathNode> nodeByGrid = new();
    private bool cacheDirty = true;

    public float CellSize => Mathf.Max(0.05f, cellSize);
    public float SearchRadius => Mathf.Max(0.1f, nearestNodeSearchRadius);

    private static readonly Vector2Int[] CardinalOffsets =
    {
        Vector2Int.right,
        Vector2Int.left,
        Vector2Int.up,
        Vector2Int.down,
    };

    private void Awake()
    {
        RefreshGraph();
    }

    private void OnEnable()
    {
        cacheDirty = true;
    }

    private void OnTransformChildrenChanged()
    {
        cacheDirty = true;
    }

    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            cacheDirty = true;
            return;
        }

        RefreshGraph();
    }

    [ContextMenu("Refresh Graph")]
    public void RefreshGraph()
    {
        cacheDirty = false;
        cachedNodes.Clear();
        nodeByGrid.Clear();

        RestaurantPathNode[] foundNodes = GetComponentsInChildren<RestaurantPathNode>(true);
        for (int i = 0; i < foundNodes.Length; i++)
        {
            RestaurantPathNode node = foundNodes[i];
            if (node == null || cachedNodes.Contains(node))
            {
                continue;
            }

            if (snapNodesToGrid)
            {
                Vector3 snapped = SnapToGrid(node.transform.position);
                if (node.transform.position != snapped)
                {
                    node.transform.position = snapped;
                }
            }

            Vector2Int grid = WorldToGrid(node.transform.position);
            node.SetGridIndex(grid);
            cachedNodes.Add(node);

            if (!nodeByGrid.ContainsKey(grid))
            {
                nodeByGrid.Add(grid, node);
            }
            else
            {
                Debug.LogWarning($"Duplicate path node at grid {grid}. Keep only one node per tile.", node);
            }
        }

        if (autoBuildCardinalLinks)
        {
            BuildCardinalLinks();
        }
    }

    public bool TryFindPath(Vector3 startWorld, Vector3 targetWorld, List<Vector3> outPath, out Vector3 resolvedTarget, float searchRadiusOverride = -1f)
    {
        EnsureCache();
        if (outPath == null)
        {
            outPath = new List<Vector3>();
        }
        outPath.Clear();
        resolvedTarget = targetWorld;

        if (cachedNodes.Count == 0)
        {
            return false;
        }

        float searchRadius = searchRadiusOverride > 0f ? searchRadiusOverride : SearchRadius;
        RestaurantPathNode startNode = FindClosestWalkableNode(startWorld, searchRadius);
        RestaurantPathNode goalNode = FindClosestWalkableNode(targetWorld, searchRadius);

        if (startNode == null || goalNode == null)
        {
            return false;
        }

        if (startNode == goalNode)
        {
            Vector3 only = startNode.transform.position;
            outPath.Add(new Vector3(only.x, only.y, startWorld.z));
            resolvedTarget = new Vector3(only.x, only.y, targetWorld.z);
            return true;
        }

        List<RestaurantPathNode> nodePath = FindPathAStar(startNode, goalNode);
        if (nodePath == null || nodePath.Count == 0)
        {
            return false;
        }

        for (int i = 0; i < nodePath.Count; i++)
        {
            Vector3 world = nodePath[i].transform.position;
            outPath.Add(new Vector3(world.x, world.y, startWorld.z));
        }

        Vector3 goalWorld = goalNode.transform.position;
        resolvedTarget = new Vector3(goalWorld.x, goalWorld.y, targetWorld.z);
        return true;
    }

    public Vector3 SnapToGrid(Vector3 world)
    {
        float step = CellSize;
        float x = gridOrigin.x + Mathf.Round((world.x - gridOrigin.x) / step) * step;
        float y = gridOrigin.y + Mathf.Round((world.y - gridOrigin.y) / step) * step;
        return new Vector3(x, y, world.z);
    }

    public Vector2Int WorldToGrid(Vector3 world)
    {
        float step = CellSize;
        int x = Mathf.RoundToInt((world.x - gridOrigin.x) / step);
        int y = Mathf.RoundToInt((world.y - gridOrigin.y) / step);
        return new Vector2Int(x, y);
    }

    private void EnsureCache()
    {
        if (cacheDirty)
        {
            RefreshGraph();
        }
    }

    private RestaurantPathNode FindClosestWalkableNode(Vector3 world, float maxRadius)
    {
        EnsureCache();

        float maxDistance = Mathf.Max(0.1f, maxRadius);
        float bestSqr = maxDistance * maxDistance;
        RestaurantPathNode best = null;

        for (int i = 0; i < cachedNodes.Count; i++)
        {
            RestaurantPathNode node = cachedNodes[i];
            if (node == null || !node.IsWalkable)
            {
                continue;
            }

            float sqr = ((Vector2)node.transform.position - (Vector2)world).sqrMagnitude;
            if (sqr <= bestSqr)
            {
                bestSqr = sqr;
                best = node;
            }
        }

        return best;
    }

    private void BuildCardinalLinks()
    {
        for (int i = 0; i < cachedNodes.Count; i++)
        {
            RestaurantPathNode node = cachedNodes[i];
            if (node == null)
            {
                continue;
            }

            List<RestaurantPathNode> neighbors = new List<RestaurantPathNode>(4);
            if (!node.IsWalkable)
            {
                node.SetNeighbors(neighbors);
                continue;
            }

            Vector2Int origin = node.GridIndex;
            for (int j = 0; j < CardinalOffsets.Length; j++)
            {
                Vector2Int targetGrid = origin + CardinalOffsets[j];
                if (!nodeByGrid.TryGetValue(targetGrid, out RestaurantPathNode target) || target == null || !target.IsWalkable)
                {
                    continue;
                }

                neighbors.Add(target);
            }

            node.SetNeighbors(neighbors);
        }
    }

    private List<RestaurantPathNode> FindPathAStar(RestaurantPathNode start, RestaurantPathNode goal)
    {
        List<RestaurantPathNode> openList = new List<RestaurantPathNode> { start };
        HashSet<RestaurantPathNode> closedSet = new HashSet<RestaurantPathNode>();

        Dictionary<RestaurantPathNode, RestaurantPathNode> cameFrom = new Dictionary<RestaurantPathNode, RestaurantPathNode>();
        Dictionary<RestaurantPathNode, int> gScore = new Dictionary<RestaurantPathNode, int> { [start] = 0 };
        Dictionary<RestaurantPathNode, int> fScore = new Dictionary<RestaurantPathNode, int> { [start] = Heuristic(start, goal) };

        while (openList.Count > 0)
        {
            int bestIndex = 0;
            int bestF = int.MaxValue;
            int bestH = int.MaxValue;

            for (int i = 0; i < openList.Count; i++)
            {
                RestaurantPathNode candidate = openList[i];
                int f = fScore.TryGetValue(candidate, out int fValue) ? fValue : int.MaxValue;
                int h = Heuristic(candidate, goal);

                if (f < bestF || (f == bestF && h < bestH))
                {
                    bestIndex = i;
                    bestF = f;
                    bestH = h;
                }
            }

            RestaurantPathNode current = openList[bestIndex];
            openList.RemoveAt(bestIndex);

            if (current == goal)
            {
                return ReconstructPath(cameFrom, current);
            }

            closedSet.Add(current);

            IReadOnlyList<RestaurantPathNode> neighbors = current.Neighbors;
            for (int i = 0; i < neighbors.Count; i++)
            {
                RestaurantPathNode neighbor = neighbors[i];
                if (neighbor == null || !neighbor.IsWalkable || closedSet.Contains(neighbor))
                {
                    continue;
                }

                int tentativeG = (gScore.TryGetValue(current, out int gCurrent) ? gCurrent : int.MaxValue / 4) + 1;
                if (gScore.TryGetValue(neighbor, out int gKnown) && tentativeG >= gKnown)
                {
                    continue;
                }

                cameFrom[neighbor] = current;
                gScore[neighbor] = tentativeG;
                fScore[neighbor] = tentativeG + Heuristic(neighbor, goal);

                if (!openList.Contains(neighbor))
                {
                    openList.Add(neighbor);
                }
            }
        }

        return null;
    }

    private static List<RestaurantPathNode> ReconstructPath(
        Dictionary<RestaurantPathNode, RestaurantPathNode> cameFrom,
        RestaurantPathNode current)
    {
        List<RestaurantPathNode> path = new List<RestaurantPathNode> { current };
        while (cameFrom.TryGetValue(current, out RestaurantPathNode parent))
        {
            current = parent;
            path.Add(current);
        }

        path.Reverse();
        return path;
    }

    private static int Heuristic(RestaurantPathNode a, RestaurantPathNode b)
    {
        Vector2Int da = a.GridIndex;
        Vector2Int db = b.GridIndex;
        return Mathf.Abs(da.x - db.x) + Mathf.Abs(da.y - db.y);
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawLinkGizmos)
        {
            return;
        }

        EnsureCache();
        Gizmos.color = linkColor;

        for (int i = 0; i < cachedNodes.Count; i++)
        {
            RestaurantPathNode node = cachedNodes[i];
            if (node == null || !node.IsWalkable)
            {
                continue;
            }

            IReadOnlyList<RestaurantPathNode> neighbors = node.Neighbors;
            for (int j = 0; j < neighbors.Count; j++)
            {
                RestaurantPathNode target = neighbors[j];
                if (target == null || !target.IsWalkable)
                {
                    continue;
                }

                Gizmos.DrawLine(node.transform.position, target.transform.position);
            }
        }
    }
}



