using Fusion;
using UnityEngine;

/// <summary>
/// Gắn lên Player prefab.
/// Khi PlayerController xử lý attack hit → gọi TryTransferBomb().
/// Chỉ active trong MG5 scene.
/// </summary>
public class MG5BombTagPlayer : NetworkBehaviour
{
    /// <summary>
    /// Gọi từ PlayerController.CheckAttackHit() (hoặc tương đương)
    /// khi attack trúng một player khác.
    /// </summary>
    public void OnAttackHit(PlayerController target)
    {
        // Chỉ chạy trên host
        if (!HasStateAuthority) return;

        if (MG5BombTagController.Instance == null) return;
        if (!MG5BombTagController.Instance.IsGameStarted) return;
        if (MG5BombTagController.Instance.IsGameEnded) return;

        PlayerRef attackerRef = Object.InputAuthority;
        PlayerRef targetRef = target.Object.InputAuthority;

        // Báo controller xử lý transfer (controller tự check có phải holder không)
        MG5BombTagController.Instance.TryTransferBomb(attackerRef, targetRef);
    }
}