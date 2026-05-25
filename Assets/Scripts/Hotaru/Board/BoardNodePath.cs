using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Giữ toàn bộ danh sách BoardNode trong BoardScene theo thứ tự.
/// Đặt component này trên 1 GameObject duy nhất trong BoardScene.
/// Kéo thả các node theo đúng thứ tự từ start trong Inspector.
/// </summary>
public class BoardNodePath : MonoBehaviour
{
    public static BoardNodePath Instance { get; private set; }

    [Header("Nodes — kéo thả theo thứ tự từ start đến end")]
    [SerializeField] private List<BoardNode> nodes = new List<BoardNode>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public int NodeCount => nodes.Count;

    /// <summary>Lấy node theo index trong list.</summary>
    public BoardNode GetNodeByIndex(int index)
    {
        if (index < 0 || index >= nodes.Count) return null;
        return nodes[index];
    }

    /// <summary>Tìm node theo nodeID.</summary>
    public BoardNode GetNodeByID(int nodeID)
    {
        foreach (var n in nodes)
            if (n != null && n.nodeID == nodeID) return n;
        return null;
    }

    /// <summary>
    /// Tính đường đi sau 'steps' bước từ currentNode.
    /// visitedNodeIDs: tất cả nodeID đi qua (kể cả ô cuối).
    /// Phase 0: luôn đi nextNodes[0], fallback circular qua list.
    /// </summary>
    public BoardNode GetNodeAfterSteps(BoardNode current, int steps, out int[] visitedNodeIDs)
    {
        var path = new List<int>();
        var cursor = current;

        for (int i = 0; i < steps; i++)
        {
            BoardNode next = null;

            if (cursor.nextNodes != null && cursor.nextNodes.Count > 0)
            {
                next = cursor.nextNodes[0]; // Phase 0: luôn đi thẳng
            }
            else
            {
                // Fallback: circular qua nodes list
                int idx = nodes.IndexOf(cursor);
                if (idx >= 0)
                    next = nodes[(idx + 1) % nodes.Count];
            }

            if (next == null) break;

            path.Add(next.nodeID);
            cursor = next;
        }

        visitedNodeIDs = path.ToArray();
        return cursor;
    }

    private void OnDrawGizmos()
    {
#if UNITY_EDITOR
        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i] != null)
                UnityEditor.Handles.Label(
                    nodes[i].transform.position + Vector3.up * 0.7f,
                    $"[{nodes[i].nodeID}]"
                );
        }
#endif
    }
}
