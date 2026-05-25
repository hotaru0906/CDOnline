using UnityEngine;
using System.Collections;

/// <summary>
/// Token đại diện cho 1 player trên bàn cờ.
/// Phase 0: MonoBehaviour thuần — movement được điều khiển hoàn toàn bởi BoardManager qua RPC.
/// Cần pre-place 4 token trong BoardScene, mỗi token set sẵn playerSlotIndex (0-3).
/// </summary>
public class BoardPlayerToken : MonoBehaviour
{
    [Header("Identity")]
    public int ownerPlayerId = -1;    // PlayerId của player sở hữu
    public int playerSlotIndex = 0;   // 0-3, khớp với slot trong BoardManager.TurnOrder

    [Header("Visual")]
    [SerializeField] private Renderer tokenRenderer;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 4f;    // nodes per second
    [SerializeField] private float hopHeight = 0.4f;  // độ cao nhảy mỗi ô

    [Header("Debug")]
    [SerializeField] private bool showLabel = true;

    public int CurrentNodeID { get; private set; } = 0;
    public bool IsMoving { get; private set; } = false;

    // Callback khi animation di chuyển xong
    public System.Action<BoardPlayerToken> OnMoveFinished;

    private static readonly Color[] SlotColors =
    {
        new Color(0.9f, 0.2f, 0.2f),   // slot 0 — đỏ
        new Color(0.2f, 0.4f, 0.9f),   // slot 1 — xanh dương
        new Color(0.2f, 0.8f, 0.2f),   // slot 2 — xanh lá
        new Color(0.95f, 0.8f, 0.1f)   // slot 3 — vàng
    };

    /// <summary>
    /// Gọi bởi BoardManager khi board phase bắt đầu để gán player và snap về node 0.
    /// </summary>
    public void Initialize(int playerId, int slotIndex, int startNodeID)
    {
        ownerPlayerId  = playerId;
        playerSlotIndex = slotIndex;

        if (tokenRenderer != null)
            tokenRenderer.material.color = SlotColors[Mathf.Clamp(slotIndex, 0, 3)];

        SnapToNode(startNodeID);
    }

    /// <summary>Teleport ngay lập tức đến node chỉ định.</summary>
    public void SnapToNode(int nodeID)
    {
        CurrentNodeID = nodeID;
        var node = BoardNodePath.Instance?.GetNodeByID(nodeID);
        if (node != null)
            transform.position = node.WorldPosition + Vector3.up * 0.5f;
    }

    /// <summary>
    /// Gọi bởi BoardManager (qua RPC) để chạy animation di chuyển.
    /// pathNodeIDs: danh sách nodeID cần đi qua theo thứ tự.
    /// </summary>
    public void AnimateMovement(int[] pathNodeIDs)
    {
        if (IsMoving) StopAllCoroutines();
        StartCoroutine(MoveCoroutine(pathNodeIDs));
    }

    private IEnumerator MoveCoroutine(int[] pathNodeIDs)
    {
        IsMoving = true;

        foreach (int nodeID in pathNodeIDs)
        {
            var node = BoardNodePath.Instance?.GetNodeByID(nodeID);
            if (node == null) continue;

            Vector3 from     = transform.position;
            Vector3 to       = node.WorldPosition + Vector3.up * 0.5f;
            float   duration = 1f / moveSpeed;
            float   elapsed  = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t   = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                float hop = Mathf.Sin(t * Mathf.PI) * hopHeight;
                transform.position = Vector3.Lerp(from, to, t) + Vector3.up * hop;
                yield return null;
            }

            transform.position = to;
            CurrentNodeID      = nodeID;

            yield return new WaitForSeconds(0.08f); // pause nhỏ giữa mỗi ô
        }

        IsMoving = false;
        OnMoveFinished?.Invoke(this);
    }

    private void OnGUI()
    {
        if (!showLabel || Camera.main == null) return;

        Vector3 sp = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * 1.2f);
        if (sp.z > 0)
            GUI.Label(
                new Rect(sp.x - 40, Screen.height - sp.y - 20, 80, 20),
                $"P{ownerPlayerId} N{CurrentNodeID}"
            );
    }
}
