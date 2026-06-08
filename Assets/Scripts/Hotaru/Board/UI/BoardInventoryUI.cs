using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Hiện Board items của local player ở phía dưới màn hình.
/// SETUP:
///   1. Tạo Panel góc dưới màn hình, attach script này
///   2. Tạo 4 child GameObject từ prefab BoardItemSlotUI, gán vào slots[]
/// </summary>
public class BoardInventoryUI : MonoBehaviour
{
    [SerializeField] private BoardItemSlotUI[] slots = new BoardItemSlotUI[4];

    private void Start()
    {
        if (BoardManager.Instance != null)
            BoardManager.Instance.OnTurnStarted += _ => Refresh();
        Refresh();
    }

    private void OnDestroy()
    {
        if (BoardManager.Instance != null)
            BoardManager.Instance.OnTurnStarted -= _ => Refresh();
    }

    public void Refresh()
    {
        int myId = GetLocalPlayerId();
        if (myId < 0) { ClearAll(); return; }

        var inv = PlayerItemInventory.GetForPlayer(myId);
        if (inv == null) { ClearAll(); return; }

        var pool = BoardItemPool.Current;

        // Nhóm theo effect, giữ thứ tự xuất hiện
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

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null) continue;

            if (i < order.Count)
            {
                var eff  = order[i];
                var data = pool?.GetByEffect(eff);
                slots[i].SetItem(data, countMap[eff]);
            }
            else
            {
                slots[i].SetEmpty();
            }
        }
    }

    private int GetLocalPlayerId()
    {
        if (PlayerNetworkData.Local != null && PlayerNetworkData.Local.Object != null)
            return PlayerNetworkData.Local.Object.InputAuthority.PlayerId;
        return -1;
    }

    private void ClearAll()
    {
        foreach (var s in slots) s?.SetEmpty();
    }
}