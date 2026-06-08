using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Danh sách 4 player ở góc trái, xếp theo vị trí node trên bàn cờ.
/// SETUP:
///   1. Tạo Vertical Layout Group góc trái màn hình
///   2. Tạo 4 child GameObject, mỗi cái attach BoardPlayerEntryUI
///   3. Gán 4 entry vào entries[] theo thứ tự slot 0-3
/// </summary>
public class BoardPlayerRankUI : MonoBehaviour
{
    [SerializeField] private BoardPlayerEntryUI[] entries = new BoardPlayerEntryUI[4];

    private static readonly Color[] SlotColors =
    {
        new Color(0.9f, 0.2f, 0.2f),
        new Color(0.2f, 0.4f, 0.9f),
        new Color(0.2f, 0.8f, 0.2f),
        new Color(0.95f, 0.8f, 0.1f)
    };

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
        var bm = BoardManager.Instance;
        if (bm == null) return;

        // Tính rank theo nodeID
        var list = new List<(int slot, int playerId, int nodeId)>();
        for (int i = 0; i < bm.ActivePlayerCount; i++)
        {
            int pid = bm.GetPlayerIDAtSlot(i);
            if (pid >= 0) list.Add((i, pid, bm.GetNodeIDAtSlot(i)));
        }
        list.Sort((a, b) => b.nodeId.CompareTo(a.nodeId));

        // Map slot -> rank
        var rankBySlot = new int[4];
        for (int r = 0; r < list.Count; r++)
            rankBySlot[list[r].slot] = r + 1;

        for (int i = 0; i < entries.Length; i++)
        {
            if (entries[i] == null) continue;

            if (i >= bm.ActivePlayerCount)
            {
                entries[i].gameObject.SetActive(false);
                continue;
            }

            entries[i].gameObject.SetActive(true);

            int pid    = bm.GetPlayerIDAtSlot(i);
            string name = BoardHUDController.Instance?.GetPlayerName(pid) ?? $"P{pid}";

            entries[i].SetData(
                playerName  : name,
                rank        : rankBySlot[i],
                slotColor   : SlotColors[i],
                isActiveTurn: false
            );
        }
    }

    public void SetActiveTurn(int playerId)
    {
        Refresh();

        var bm = BoardManager.Instance;
        if (bm == null) return;

        for (int i = 0; i < bm.ActivePlayerCount; i++)
        {
            if (entries[i] == null) continue;
            entries[i].SetTurnActive(bm.GetPlayerIDAtSlot(i) == playerId);
        }
    }
}