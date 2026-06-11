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
        if (MG2RacingController.Instance == null) return;
        if (!MG2RacingController.Instance.IsGameStarted) return;
        if (MG2RacingController.Instance.IsGameEnded) return;

        var mgData = player.GetComponent<PlayerMinigameData>();
        if (mgData == null) return;
        if (mgData.HasFinished) return;
        if (mgData.IsEliminated) return;

        Debug.Log($"[MG2RacingFinishLine] Player {player.Object.InputAuthority} crossed finish line!");

        // Freeze player ngay khi về đích
        player.SetFrozen(true);

        MG2RacingController.Instance.PlayerFinished(player.Object.InputAuthority);
        RPC_PlayFinishEffect();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayFinishEffect()
    {
        if (finishEffect != null) finishEffect.Play();
        if (finishSound != null) finishSound.Play();
    }
}
