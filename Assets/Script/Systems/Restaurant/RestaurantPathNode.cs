using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class RestaurantPathNode : MonoBehaviour
{
    [SerializeField] private bool walkable = true;
    [HideInInspector] [SerializeField] private Vector2Int gridIndex;
    [HideInInspector] [SerializeField] private List<RestaurantPathNode> neighbors = new();

    public bool IsWalkable => walkable;
    public Vector2Int GridIndex => gridIndex;
    public IReadOnlyList<RestaurantPathNode> Neighbors => neighbors;

    internal void SetGridIndex(Vector2Int index)
    {
        gridIndex = index;
    }

    internal void SetNeighbors(List<RestaurantPathNode> newNeighbors)
    {
        if (neighbors == null) 
        {
            neighbors = new List<RestaurantPathNode>();
        }
        neighbors.Clear();

        if (newNeighbors == null)
        {
            return;
        }

        for (int i = 0; i < newNeighbors.Count; i++)
        {
            RestaurantPathNode neighbor = newNeighbors[i];
            if (neighbor == null || neighbor == this || neighbors.Contains(neighbor))
            {
                continue;
            }

            neighbors.Add(neighbor);
        }
    }

    [ContextMenu("Mark Walkable")]
    private void MarkWalkable()
    {
        walkable = true;
    }

    [ContextMenu("Mark Blocked")]
    private void MarkBlocked()
    {
        walkable = false;
    }

    [ContextMenu("Snap To Grid")]
    private void SnapToGrid()
    {
        RestaurantPathGraph graph = GetComponentInParent<RestaurantPathGraph>();
        if (graph == null)
        {
            return;
        }

        transform.position = graph.SnapToGrid(transform.position);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = walkable ? new Color(0.2f, 0.9f, 0.35f, 0.9f) : new Color(0.95f, 0.3f, 0.3f, 0.9f);
        Gizmos.DrawSphere(transform.position, 0.06f);
    }
}

