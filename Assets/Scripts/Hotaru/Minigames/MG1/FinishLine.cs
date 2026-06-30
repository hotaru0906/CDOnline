using Fusion;
using UnityEngine;

public class MinigameFinishLine : BaseFinishLine
{
    [Header("Visual")]
    [SerializeField] private ParticleSystem winEffect;
    [SerializeField] private AudioSource winSound;

    protected override void HandlePlayerReachedFinish(PlayerController player)
    {
        if (MinigameController.Instance == null)
        {
            Debug.LogError("[FinishLine] MinigameController null!");
            return;
        }

        if (!MinigameController.Instance.IsGameStarted) return;
        if (MinigameController.Instance.IsGameEnded) return;

        // Kiểm tra player này đã về đích chưa (tránh trigger 2 lần)
        var mgData = player.GetComponent<PlayerMinigameData>();
        if (mgData != null && mgData.HasFinished) return;

        Debug.Log($"[FinishLine] Player finished: {player.Object.InputAuthority}");

        MinigameController.Instance.PlayerFinished(player.Object.InputAuthority);

        RPC_PlayWinEffects();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayWinEffects()
    {
        if (winEffect != null) winEffect.Play();
        if (winSound != null) winSound.Play();
    }
}