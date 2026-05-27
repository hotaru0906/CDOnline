using Fusion;
using UnityEngine;

/// <summary>
/// Base class cho tất cả FinishLine trong minigame.
/// Xử lý phần chung: OnTriggerEnter → tìm PlayerController → gọi HandlePlayerReachedFinish().
///
/// Derived class implement:
///   HandlePlayerReachedFinish(PlayerController) — logic riêng của từng minigame.
///
/// MG1: MinigameFinishLine — chỉ 1 winner, set HasWinner = true.
/// MG2: MG2RacingFinishLine — nhiều players, lưu finish order.
/// </summary>
[RequireComponent(typeof(Collider))]
public abstract class BaseFinishLine : NetworkBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (!Runner.IsServer) return;
        if (!other.TryGetComponent(out PlayerController player)) return;

        HandlePlayerReachedFinish(player);
    }

    /// <summary>
    /// Xử lý khi 1 player chạm FinishLine.
    /// Mỗi minigame override method này với logic riêng.
    /// </summary>
    protected abstract void HandlePlayerReachedFinish(PlayerController player);
}
