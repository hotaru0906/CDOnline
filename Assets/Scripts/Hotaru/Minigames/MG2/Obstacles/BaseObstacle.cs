using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>
/// Base class cho tất cả obstacle trong MG2 Racing (và các minigame sau này).
///
/// NETWORKING RULE:
///   - Chỉ HOST xử lý hit logic (HasStateAuthority check).
///   - RPC gửi xuống tất cả client để play VFX / SFX.
///
/// Derived class chỉ cần override:
///   protected override void ApplyEffect(PlayerController player) { }
///
/// Optional override:
///   protected override void PlayFX() { }
///   protected override void PlaySFX() { }
/// </summary>
public abstract class BaseObstacle : NetworkBehaviour
{
    [Header("Obstacle Settings")]
    [SerializeField] protected bool isActive = true;

    [Header("Effects")]
    [SerializeField] protected ParticleSystem hitEffect;
    [SerializeField] protected AudioSource hitSound;

    [Header("Hit Cooldown")]
    [SerializeField] private float hitCooldown = 0.5f; // giây giữa 2 lần hit cùng player

    // Server-only: track thời điểm hit tiếp theo được phép cho từng player
    private readonly Dictionary<PlayerRef, float> _hitCooldowns = new();

    // ----------------------------------------------------------------
    //  Trigger — entry point dùng chung
    // ----------------------------------------------------------------

    protected virtual void OnTriggerEnter(Collider other)
    {
        if (!isActive) return;
        // Guard: Runner null nếu obstacle không có NetworkObject component
        if (Runner == null || !Runner.IsServer) return;
        if (!other.TryGetComponent(out PlayerController player)) return;

        if (!IsGameActive()) return;

        HandleHit(player);
    }

    /// <summary>
    /// Gọi bởi ObstacleTriggerRelay trên child object có Collider trigger.
    /// Dùng cho obstacle có Collider ở child (Hammer, JumpPad, RotatingSpikeTrap...).
    /// </summary>
    public void OnChildTriggerEnter(Collider other)
    {
        if (!isActive) return;
        if (Runner == null || !Runner.IsServer) return;
        if (!other.TryGetComponent(out PlayerController player)) return;

        if (!IsGameActive()) return;

        HandleHit(player);
    }

    private bool IsGameActive()
    {
        if (BaseMinigameController.Instance == null) return true; // test không có controller
        return BaseMinigameController.Instance.IsGameStarted &&
               !BaseMinigameController.Instance.IsGameEnded;
    }

    /// <summary>
    /// Host xử lý hit: apply effect lên player + broadcast FX xuống client.
    /// Có per-player cooldown để tránh multi-trigger trong cùng 1 frame (Fusion resimulation).
    /// </summary>
    protected virtual void HandleHit(PlayerController player)
    {
        var playerRef = player.Object.InputAuthority;
        float now = Runner.SimulationTime;

        if (_hitCooldowns.TryGetValue(playerRef, out float nextAllowed) && now < nextAllowed)
            return;

        _hitCooldowns[playerRef] = now + hitCooldown;

        ApplyEffect(player);
        RPC_PlayHitEffects();
    }

    // ----------------------------------------------------------------
    //  Abstract / Virtual — derived class override
    // ----------------------------------------------------------------

    /// <summary>
    /// Logic chính của obstacle: knockback, kill, teleport...
    /// Chỉ chạy trên HOST.
    /// </summary>
    protected abstract void ApplyEffect(PlayerController player);

    /// <summary>Play particle effect. Override để customize.</summary>
    protected virtual void PlayFX()
    {
        if (hitEffect != null) hitEffect.Play();
    }

    /// <summary>Play sound effect. Override để customize.</summary>
    protected virtual void PlaySFX()
    {
        if (hitSound != null) hitSound.Play();
    }

    // ----------------------------------------------------------------
    //  RPC — broadcast effects xuống tất cả client
    // ----------------------------------------------------------------

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    protected void RPC_PlayHitEffects()
    {
        PlayFX();
        PlaySFX();
    }

    // ----------------------------------------------------------------
    //  Helpers dùng trong derived class
    // ----------------------------------------------------------------

    /// <summary>
    /// Tìm PlayerMinigameData của player. Dùng cho obstacle cần check
    /// IsEliminated, checkpoint index, v.v.
    /// </summary>
    protected PlayerMinigameData GetMinigameData(PlayerController player)
    {
        return player.GetComponent<PlayerMinigameData>();
    }
}
