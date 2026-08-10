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
    }

    private void OnDestroy()
    {
        _instances.Remove(this);
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