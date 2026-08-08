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

    [Header("Nodes — index sẽ tự map theo nodeID")]
    [SerializeField] private List<BoardNode> nodes = new List<BoardNode>();

    [Header("Auto Setup")]
    [SerializeField] private bool autoAssignNodesById = true;
    [SerializeField] private bool includeInactiveNodes = false;

    private int _validNodeCount;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (autoAssignNodesById)
            RebuildNodeIndexById();
    }

    private void OnValidate()
    {
        if (!autoAssignNodesById) return;
        RebuildNodeIndexById();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public int NodeCount => _validNodeCount;

    /// <summary>
    /// Tự map node vào list theo đúng index = nodeID.
    /// Ví dụ nodeID = 0 => nodes[0], nodeID = 5 => nodes[5].
    /// </summary>
    public void RebuildNodeIndexById()
    {
        var found = FindObjectsByType<BoardNode>(
            includeInactiveNodes ? FindObjectsInactive.Include : FindObjectsInactive.Exclude,
            FindObjectsSortMode.None
        );

        int maxId = -1;
        _validNodeCount = 0;

        foreach (var n in found)
        {
            if (n == null || n.nodeID < 0) continue;
            if (n.nodeID > maxId) maxId = n.nodeID;
        }

        if (maxId < 0)
        {
            nodes.Clear();
            return;
        }

        var remapped = new List<BoardNode>(maxId + 1);
        for (int i = 0; i <= maxId; i++)
            remapped.Add(null);

        foreach (var n in found)
        {
            if (n == null || n.nodeID < 0) continue;

            if (remapped[n.nodeID] != null)
            {
                Debug.LogWarning($"[BoardNodePath] Duplicate nodeID {n.nodeID}: {remapped[n.nodeID].name} & {n.name}. Keeping first one.");
                continue;
            }

            remapped[n.nodeID] = n;
            _validNodeCount++;
        }

        nodes = remapped;
    }

    /// <summary>Lấy node theo index trong list.</summary>
    public BoardNode GetNodeByIndex(int index)
    {
        if (index < 0 || index >= nodes.Count) return null;
        return nodes[index];
    }

    /// <summary>Tìm node theo nodeID.</summary>
    public BoardNode GetNodeByID(int nodeID)
    {
        if (nodeID >= 0 && nodeID < nodes.Count)
            return nodes[nodeID];

        foreach (var n in nodes)
            if (n != null && n.nodeID == nodeID) return n;
        return null;
    }

    private int GetNextValidIndex(int currentIndex, int direction)
    {
        if (nodes == null || nodes.Count == 0) return -1;

        int count = nodes.Count;
        int idx = currentIndex;

        for (int i = 0; i < count; i++)
        {
            idx = (idx + direction + count) % count;
            if (nodes[idx] != null)
                return idx;
        }

        return -1;
    }

    /// <summary>
    /// Tính đường đi sau 'steps' bước từ currentNode.
    /// visitedNodeIDs: tất cả nodeID đi qua (kể cả ô cuối).
    /// Phase 0: luôn đi nextNodes[0], fallback circular qua list.
    ///
    /// DEAD-END BOUNCE-BACK:
    ///   Nếu một node có isDeadEnd = true và player còn bước dư sau khi đến đó,
    ///   token sẽ QUAY NGƯỢC lại số bước còn dư (không nhận hiệu ứng ô dead-end).
    ///   Ví dụ: dead-end tại node 11, player từ node 8 đổ 5 → đến 11 (3 bước) → quay về 10 → 9.
    ///   Final node: 9 (nhận hiệu ứng ô 9). Ô 11 KHÔNG kích hoạt hiệu ứng.
    /// </summary>
    public BoardNode GetNodeAfterSteps(BoardNode current, int steps, out int[] visitedNodeIDs)
    {
        var path     = new List<int>();
        var traveled = new List<BoardNode>() { current }; // lịch sử đường đi để bounce back
        int tIdx     = 0;   // index trong traveled hiện tại
        bool bouncing = false;

        for (int i = 0; i < steps; i++)
        {
            var cursor = traveled[tIdx];

            if (!bouncing)
            {
                BoardNode next = null;

                if (cursor.nextNodes != null && cursor.nextNodes.Count > 0)
                    next = cursor.nextNodes[0]; // Phase 0: luôn đi thẳng

                if (next != null)
                {
                    tIdx++;
                    if (tIdx >= traveled.Count)
                        traveled.Add(next);

                    path.Add(traveled[tIdx].nodeID);

                }
                else
                {
                    // Không có nextNodes, không phải dead-end → fallback circular
                    int idx = nodes.IndexOf(cursor);
                    if (idx >= 0)
                    {
                        int nextIdx = GetNextValidIndex(idx, 1);
                        if (nextIdx >= 0)
                            next = nodes[nextIdx];

                        if (next == cursor)
                            next = null;

                        if (next == null)
                            continue;

                        tIdx++;
                        if (tIdx >= traveled.Count)
                            traveled.Add(next);
                        path.Add(traveled[tIdx].nodeID);
                    }
                }
            }
            else
            {
                // Đang bounce ngược: đi lùi theo lịch sử đường đi
                if (tIdx > 0)
                {
                    tIdx--;
                    path.Add(traveled[tIdx].nodeID);
                }
                // tIdx == 0: đã về điểm xuất phát, không di chuyển thêm (cực hiếm)
            }
        }

        visitedNodeIDs = path.ToArray();
        return traveled[tIdx];
    }

    public BoardNode GetNextNode(BoardNode current, int branchIndex = 0, int targetNodeId = -1)
    {
        if (current == null)
            return null;

        if (current.nextNodes != null &&
            current.nextNodes.Count > 0)
        {
            if (targetNodeId >= 0)
            {
                foreach (var next in current.nextNodes)
                {
                    if (next != null && next.nodeID == targetNodeId)
                        return next;
                }
            }

            branchIndex = Mathf.Clamp(
                branchIndex,
                0,
                current.nextNodes.Count - 1);

            return current.nextNodes[branchIndex];
        }

        int idx = nodes.IndexOf(current);

        if (idx < 0)
            return null;

        int nextIdx = GetNextValidIndex(idx, 1);

        if (nextIdx < 0)
            return null;

        return nodes[nextIdx];
    }

    /// <summary>
    /// Tính đường đi LÙI 'steps' bước từ currentNode, dựa theo thứ tự trong nodes list.
    /// Dùng cho PushBack Board item — di chuyển target về phía sau.
    /// Nếu currentNode không có trong list (ví dụ dead-end branch), trả về currentNode.
    /// </summary>
    public BoardNode GetNodeBeforeSteps(BoardNode current, int steps, out int[] visitedNodeIDs)
    {
        var path = new List<int>();
        int idx = nodes.IndexOf(current);

        if (idx < 0)
        {
            visitedNodeIDs = path.ToArray();
            return current;
        }

        for (int i = 0; i < steps; i++)
        {
            int prevIdx = GetNextValidIndex(idx, -1);
            if (prevIdx < 0 || prevIdx == idx)
                break;

            idx = prevIdx;
            path.Add(nodes[idx].nodeID);
        }

        visitedNodeIDs = path.ToArray();
        return nodes[idx];
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
