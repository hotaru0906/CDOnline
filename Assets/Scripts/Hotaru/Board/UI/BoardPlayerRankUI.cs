using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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
        StartCoroutine(WaitForBoardManager());
    }

    private void OnDestroy()
    {
        if (BoardManager.Instance != null)
            BoardManager.Instance.OnTurnStarted -= OnTurnStarted;
    }

    private IEnumerator WaitForBoardManager()
    {
        while (BoardManager.Instance == null)
            yield return null;

        BoardManager.Instance.OnTurnStarted += OnTurnStarted;

        // Chờ thêm đến khi ít nhất có đủ PlayerNetworkData đã spawn và có tên
        var bm = BoardManager.Instance;
        float timeout = 5f;
        float elapsed = 0f;
        while (elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;

            int readyCount = 0;
            var allPlayers = Object.FindObjectsByType<PlayerNetworkData>(FindObjectsSortMode.None);
            foreach (var p in allPlayers)
            {
                if (p.Object == null) continue;
                string name = p.PlayerName.ToString();
                if (!string.IsNullOrEmpty(name) && name != "0") readyCount++;
            }

            if (readyCount >= bm.ActivePlayerCount && bm.ActivePlayerCount > 0)
                break;
        }

        Refresh();
        Debug.Log("[BoardPlayerRankUI] Subscribed + Refreshed after player data ready");
    }

    // =====================================================================
    // EVENTS
    // =====================================================================

    private void OnTurnStarted(int playerId)
    {
        Refresh();
        SetActiveTurn(playerId);
    }

    // =====================================================================
    // REFRESH
    // =====================================================================

    public void Refresh()
    {
        var bm = BoardManager.Instance;
        if (bm == null) return;

        var list = new List<(int slot, int playerId, int nodeId)>();
        for (int i = 0; i < bm.ActivePlayerCount; i++)
        {
            int pid = bm.GetPlayerIDAtSlot(i);
            if (pid >= 0) list.Add((i, pid, bm.GetNodeIDAtSlot(i)));
        }
        list.Sort((a, b) => b.nodeId.CompareTo(a.nodeId));

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

            int pid = bm.GetPlayerIDAtSlot(i);
            string name = BoardHUDController.Instance?.GetPlayerName(pid) ?? $"P{pid}";

            entries[i].SetData(
                playerName: name,
                rank: rankBySlot[i],
                slotColor: SlotColors[i],
                isActiveTurn: false
            );
        }
    }

    public void SetActiveTurn(int playerId)
    {
        var bm = BoardManager.Instance;
        if (bm == null) return;

        for (int i = 0; i < bm.ActivePlayerCount; i++)
        {
            if (entries[i] == null) continue;
            entries[i].SetTurnActive(bm.GetPlayerIDAtSlot(i) == playerId);
        }
    }
}