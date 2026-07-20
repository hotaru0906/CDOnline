using Fusion;
using UnityEngine;

/// <summary>
/// Gắn lên Player prefab.
/// Khi PlayerController xử lý attack hit → gọi TryTransferCrown() nếu target đang giữ crown.
/// Chỉ active trong MG7 scene.
///
/// Khác MG5BombTagPlayer: ở đây ATTACKER là người cướp, TARGET là người đang giữ crown bị mất.
/// Stun cho target đã được xử lý tự động trong PlayerController.CheckAttackHit()
/// (other.TryApplyStun() chạy trước, không phụ thuộc minigame nào).
/// </summary>
public class MG7CrownPlayer : NetworkBehaviour
{
    /// <summary>
    /// Gọi từ PlayerController.CheckAttackHit() khi attack (this) trúng target.
    /// </summary>
    public void OnAttackHit(PlayerController target)
    {
        // Chỉ chạy trên host
        if (!HasStateAuthority) return;

        if (MG7CrownController.Instance == null) return;
        if (!MG7CrownController.Instance.IsGameStarted) return;
        if (MG7CrownController.Instance.IsGameEnded) return;

        PlayerRef attackerRef = Object.InputAuthority;
        PlayerRef targetRef = target.Object.InputAuthority;

        // Controller tự check target có đang giữ crown không
        MG7CrownController.Instance.TryTransferCrown(attackerRef, targetRef);
    }
}