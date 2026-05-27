using Fusion;
using UnityEngine;

/// <summary>
/// MG2 Racing Finish Line — cho phép NHIỀU player về đích theo thứ tự.
/// 
/// Khác với MG1 FinishLine:
///   - Không có HasWinner block → player thứ 2, 3... đều được ghi nhận
///   - Gọi MG2RacingController.Instance.PlayerFinished() thay vì MinigameController
///   - Player đã về đích (HasFinished = true) hoặc bị loại sẽ bị bỏ qua
/// </summary>
public class MG2RacingFinishLine : BaseFinishLine
{
    [Header("Effects")]
    [SerializeField] private ParticleSystem finishEffect;
    [SerializeField] private AudioSource finishSound;

    protected override void HandlePlayerReachedFinish(PlayerController player)
    {
        if (MG2RacingController.Instance == null)
        {
            Debug.LogError("[MG2RacingFinishLine] MG2RacingController not found in scene!");
            return;
        }

        if (!MG2RacingController.Instance.IsGameStarted) return;
        if (MG2RacingController.Instance.IsGameEnded) return;

        var mgData = player.GetComponent<PlayerMinigameData>();
        if (mgData == null) return;
        if (mgData.HasFinished) return;   // đã về đích rồi
        if (mgData.IsEliminated) return;  // bị loại, không tính

        Debug.Log($"[MG2RacingFinishLine] Player {player.Object.InputAuthority} crossed finish line!");

        // Host ghi nhận rank (logic trong MG2RacingController)
        MG2RacingController.Instance.PlayerFinished(player.Object.InputAuthority);

        // Phát hiệu ứng xuống tất cả client
        RPC_PlayFinishEffect();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayFinishEffect()
    {
        if (finishEffect != null) finishEffect.Play();
        if (finishSound != null) finishSound.Play();
    }
}
