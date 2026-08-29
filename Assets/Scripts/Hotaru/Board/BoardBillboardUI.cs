using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Quản lý 4 entry Billboard trên 1 mặt. Billboard có 3 mặt → 3 instance cùng tồn tại.
/// UI hiện xuyên suốt game, không toggle Show/Hide nữa — chỉ Populate 1 lần lúc intro.
/// </summary>
public class BoardBillboardUI : MonoBehaviour
{
    private static readonly List<BoardBillboardUI> _instances = new();

    [SerializeField] private BoardBillboardEntryUI[] entries = new BoardBillboardEntryUI[4];

    private void Awake()
    {
        _instances.Add(this);
        if (BoardManager.Instance != null)
            BoardManager.Instance.OnTurnStarted += HandleTurnStarted;
    }

    private void OnEnable()
    {
        if (BoardManager.Instance != null)
            BoardManager.Instance.OnTurnStarted += HandleTurnStarted;

        RefreshActiveTurn();
    }

    private void OnDisable()
    {
        if (BoardManager.Instance != null)
            BoardManager.Instance.OnTurnStarted -= HandleTurnStarted;
    }

    private void OnDestroy()
    {
        if (BoardManager.Instance != null)
            BoardManager.Instance.OnTurnStarted -= HandleTurnStarted;
        _instances.Remove(this);
    }

    private void HandleTurnStarted(int playerId)
    {
        RefreshActiveTurn();
    }

    // =====================================================================
    // STATIC API
    // =====================================================================

    public static void PopulateAll()
    {
        foreach (var instance in _instances)
            instance?.PopulateSelf();
    }

    public static void StartFirstTurnGlowAll(int firstSlot)
    {
        foreach (var instance in _instances)
            instance?.StartGlowSelf(firstSlot);
    }

    public static void StopFirstTurnGlowAll()
    {
        foreach (var instance in _instances)
            instance?.StopGlowSelf();
    }

    // =====================================================================
    // INSTANCE
    // =====================================================================

    private void RefreshActiveTurn()
    {
        var bm = BoardManager.Instance;
        if (bm == null) return;

        for (int i = 0; i < entries.Length; i++)
        {
            if (entries[i] == null) continue;

            int pid = bm.GetPlayerIDAtSlot(i);
            bool activeTurn = pid >= 0 && pid == bm.CurrentPlayerID;
            entries[i].SetTurnActive(activeTurn);
        }
    }

    private void PopulateSelf()
    {
        var bm = BoardManager.Instance;
        if (bm == null) return;

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
            entries[i].SetPlayerId(pid);
            entries[i].SetTurnActive(pid == bm.CurrentPlayerID);
        }
    }

    private void StartGlowSelf(int firstSlot)
    {
        if (firstSlot < 0 || firstSlot >= entries.Length) return;
        entries[firstSlot]?.StartFirstTurnGlow();
    }

    private void StopGlowSelf()
    {
        foreach (var e in entries)
            e?.StopFirstTurnGlow();
    }
}