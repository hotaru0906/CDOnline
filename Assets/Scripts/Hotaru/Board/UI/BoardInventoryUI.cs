using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Hiện Board items của LOCAL player — nhóm theo loại item, hiện số lượng mỗi loại.
/// Ví dụ: 2x PushBack + 1x EvenDice → slot0 = PushBack(x2), slot1 = EvenDice(x1), slot2-3 trống.
///
/// SETUP TRONG UNITY EDITOR:
///   1. Tạo Panel "InventoryPanel" trong Canvas (góc dưới màn hình).
///   2. Tạo 4 GameObject con từ prefab BoardItemSlotUI, gán vào mảng slots[0..3].
///   3. Gắn script này vào InventoryPanel.
///
/// LƯU Ý: BoardItemPool.Current phải có giá trị (assign asset vào scene).
/// </summary>
public class BoardInventoryUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BoardItemSlotUI[] slots = new BoardItemSlotUI[4];

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
    /// Nhóm Board items theo BoardItemEffect, hiện 1 slot/loại kèm count badge.
    /// Có thể gọi thủ công từ BoardHUDController.
    /// </summary>
    public void Refresh()
    {
        int myId = GetLocalPlayerId();
        if (myId < 0) { ClearAll(); return; }

        var inv = PlayerItemInventory.GetForPlayer(myId);
        if (inv == null) { ClearAll(); return; }

        var pool = BoardItemPool.Current;

        // Đếm số lượng theo từng BoardItemEffect, giữ thứ tự xuất hiện
        var order    = new List<BoardItemEffect>();
        var countMap = new Dictionary<BoardItemEffect, int>();
        for (int i = 0; i < 4; i++)
        {
            int raw = inv.BoardItems.Get(i);
            if (raw == -1) continue;
            var eff = (BoardItemEffect)raw;
            if (!countMap.ContainsKey(eff))
            {
                order.Add(eff);
                countMap[eff] = 0;
            }
            countMap[eff]++;
        }

        for (int slotIdx = 0; slotIdx < slots.Length; slotIdx++)
        {
            if (slots[slotIdx] == null) continue;

            if (slotIdx < order.Count)
            {
                var effect = order[slotIdx];
                var data   = pool?.GetByEffect(effect);
                slots[slotIdx].SetItem(data, countMap[effect]);
            }
            else
            {
                slots[slotIdx].SetEmpty();
            }
        }
    }

    // =====================================================================
    // HELPERS
    // =====================================================================

    private int GetLocalPlayerId()
    {
        var bm = BoardManager.Instance;
        if (bm != null && bm.Runner != null && bm.Runner.LocalPlayer != Fusion.PlayerRef.None)
            return bm.Runner.LocalPlayer.PlayerId;

        if (PlayerNetworkData.Local != null && PlayerNetworkData.Local.Object != null)
            return PlayerNetworkData.Local.Object.InputAuthority.PlayerId;

        return -1;
    }

    private void ClearAll()
    {
        foreach (var s in slots)
            s?.SetEmpty();
    }
}
