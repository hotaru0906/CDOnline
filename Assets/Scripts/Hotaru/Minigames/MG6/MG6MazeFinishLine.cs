using Fusion;
using UnityEngine;

/// <summary>
/// MG6 finish line: ghi nhận thứ tự về đích cho nhiều người chơi.
/// Freeze/camera switch được xử lý trong MG6MazeController để đồng bộ toàn mạng.
/// </summary>
public class MG6MazeFinishLine : BaseFinishLine
{
    [Header("Effects")]
    [SerializeField] private ParticleSystem finishEffect;
    [SerializeField] private AudioSource finishSound;

    protected override void HandlePlayerReachedFinish(PlayerController player)
    {
        if (MG6MazeController.Instance == null) return;
        if (!MG6MazeController.Instance.IsGameStarted) return;
        if (MG6MazeController.Instance.IsGameEnded) return;

        var mgData = player.GetComponent<PlayerMinigameData>();
        if (mgData == null) return;
        if (mgData.HasFinished) return;
        if (mgData.IsEliminated) return;

        MG6MazeController.Instance.PlayerFinished(player.Object.InputAuthority);
        RPC_PlayFinishEffect();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayFinishEffect()
    {
        if (finishEffect != null) finishEffect.Play();
        if (finishSound != null) finishSound.Play();
    }
}