using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Bảng xếp hạng 4 player theo vị trí trên bàn cờ — cập nhật mỗi frame.
/// Player đứng xa nhất (nodeID lớn nhất) = Rank #1.
///
/// SETUP TRONG UNITY EDITOR:
///   1. Tạo Panel "PlayerRankPanel" trong Canvas.
///   2. Thêm Vertical Layout Group.
///   3. (Optional) Thêm TMP_Text "PlayerRankTitle" làm tiêu đề.
///   4. Tạo 4 child GameObject từ prefab BoardPlayerRankEntryUI, gán vào mảng entries[0..3].
///   5. Gắn script này vào PlayerRankPanel.
/// </summary>
public class BoardPlayerRankUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BoardPlayerRankEntryUI[] entries = new BoardPlayerRankEntryUI[4];

    // =====================================================================
    // LIFECYCLE
    // =====================================================================

    private void Update()
    {
        Refresh();
    }

    // =====================================================================
    // REFRESH
    // =====================================================================

    /// <summary>
    /// Đọc state từ BoardManager, sắp xếp theo nodeID giảm dần, cập nhật entries.
    /// Có thể gọi thủ công (ví dụ từ BoardHUDController khi lượt mới bắt đầu).
    /// </summary>
    public void Refresh()
    {
        var bm = BoardManager.Instance;
        if (bm == null) return;

        // Xây dựng danh sách player kèm slot gốc để lấy màu token
        var list = new List<(int slot, int playerId, int nodeId)>();
        for (int i = 0; i < bm.ActivePlayerCount; i++)
        {
            int pid = bm.GetPlayerIDAtSlot(i);
            if (pid >= 0)
                list.Add((i, pid, bm.GetNodeIDAtSlot(i)));
        }

        // Sắp xếp theo nodeID giảm dần → người đi xa nhất lên đầu
        list.Sort((a, b) => b.nodeId.CompareTo(a.nodeId));

        for (int rank = 0; rank < entries.Length; rank++)
        {
            if (entries[rank] == null) continue;

            if (rank < list.Count)
            {
                var (slot, pid, nodeId) = list[rank];
                bool isCurrent = (pid == bm.CurrentPlayerID);
                string name = BoardHUDController.Instance != null
                    ? BoardHUDController.Instance.GetPlayerName(pid)
                    : $"P{pid}";

                entries[rank].SetData(rank + 1, slot, name, nodeId, isCurrent);
            }
            else
            {
                entries[rank].SetEmpty();
            }
        }
    }
}
