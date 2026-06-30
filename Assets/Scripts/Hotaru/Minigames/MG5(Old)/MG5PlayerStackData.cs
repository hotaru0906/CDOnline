using Fusion;
using UnityEngine;

/// <summary>
/// MG5 — Player stack data.
/// Attach vào Player Prefab.
/// Lưu trạng thái player trong MG5.
/// </summary>
public class MG5PlayerStackData : NetworkBehaviour
{
    [Networked] public int CurrentStackHeight { get; private set; }
    [Networked] public int CurrentRank { get; set; }
    [Networked] public NetworkBool IsFinished { get; set; }

    public void ResetData()
    {
        if (!HasStateAuthority) return;

        CurrentStackHeight = 0;
        CurrentRank = -1;
        IsFinished = false;
    }

    /// <summary>
    /// Gọi khi player đặt thành công một box vào stack.
    /// </summary>
    public void IncreaseHeight()
    {
        if (!HasStateAuthority) return;

        CurrentStackHeight++;
        Debug.Log($"[MG5PlayerStackData] Player {Object.InputAuthority} stack height = {CurrentStackHeight}");
    }

    /// <summary>
    /// Đánh dấu player đã hoàn thành (đạt target height).
    /// </summary>
    public void MarkFinished(int rank)
    {
        if (!HasStateAuthority) return;

        IsFinished = true;
        CurrentRank = rank;
        Debug.Log($"[MG5PlayerStackData] Player {Object.InputAuthority} finished — Rank {rank}");
    }
}
