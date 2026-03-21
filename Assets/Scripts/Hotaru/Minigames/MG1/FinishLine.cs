using Fusion;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class MinigameFinishLine : NetworkBehaviour
{
    [Header("Visual")]
    [SerializeField] private ParticleSystem winEffect;
    [SerializeField] private AudioSource winSound;

    [Networked]
    private NetworkBool HasWinner { get; set; }

    private void OnTriggerEnter(Collider other)
    {
        if (!Runner.IsServer) return;
        if (HasWinner) return;

        if (!other.TryGetComponent(out PlayerController player)) return;

        ProcessFinish(player);
    }

    private void ProcessFinish(PlayerController player)
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

        // 🔥 Host quyết định winner
        MinigameController.Instance.PlayerFinished(player.Object.InputAuthority);

        // 🔥 Gửi effect xuống client
        RPC_PlayWinEffects();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayWinEffects()
    {
        if (winEffect != null) winEffect.Play();
        if (winSound != null) winSound.Play();
    }
}