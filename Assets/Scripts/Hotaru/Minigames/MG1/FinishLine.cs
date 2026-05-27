using Fusion;
using UnityEngine;

public class MinigameFinishLine : BaseFinishLine
{
    [Header("Visual")]
    [SerializeField] private ParticleSystem winEffect;
    [SerializeField] private AudioSource winSound;

    [Networked]
    private NetworkBool HasWinner { get; set; }

    protected override void HandlePlayerReachedFinish(PlayerController player)
    {
        if (HasWinner) return;

        if (MinigameController.Instance == null)
        {
            Debug.LogError("[FinishLine] MinigameController null!");
            return;
        }

        if (!MinigameController.Instance.IsGameStarted) return;
        if (MinigameController.Instance.IsGameEnded) return;

        HasWinner = true;

        Debug.Log($"[FinishLine] WINNER: {player.Object.InputAuthority}");

        // Host quyết định winner
        MinigameController.Instance.PlayerFinished(player.Object.InputAuthority);

        // Gửi effect xuống client
        RPC_PlayWinEffects();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayWinEffects()
    {
        if (winEffect != null) winEffect.Play();
        if (winSound != null) winSound.Play();
    }
}