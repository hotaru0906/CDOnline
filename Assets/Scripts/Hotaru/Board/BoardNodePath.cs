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

                    // Vừa đến dead-end nhưng còn bước dư → bắt đầu bounce từ lượt sau
                    if (traveled[tIdx].isDeadEnd && i < steps - 1)
                        bouncing = true;
                }
                else if (cursor.isDeadEnd)
                {
                    // Đang đứng trên dead-end mà vẫn còn steps (edge case) → bounce ngay
                    bouncing = true;
                    if (tIdx > 0)
                    {
                        tIdx--;
                        path.Add(traveled[tIdx].nodeID);
                    }
                }
                else
                {
                    // Không có nextNodes, không phải dead-end → fallback circular
                    int idx = nodes.IndexOf(cursor);
                    if (idx >= 0)
                    {
                        next = nodes[(idx + 1) % nodes.Count];
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
