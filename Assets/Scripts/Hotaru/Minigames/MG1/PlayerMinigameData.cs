using Fusion;
using UnityEngine;

/// <summary>
/// Lưu trữ dữ liệu minigame cho mỗi player.
/// Bao gồm checkpoint hiện tại và xử lý respawn.
/// Sử dụng TickTimer thay vì Coroutine theo khuyến nghị của Fusion.
/// </summary>
public class PlayerMinigameData : NetworkBehaviour
{
    [Header("Respawn Settings")]
    [SerializeField] private float respawnDelay = 0.5f;
    [SerializeField] private float invincibilityTime = 2f;

    [Header("Visual Feedback")]
    [SerializeField] private Renderer[] playerRenderers;
    [SerializeField] private Color invincibleColor = new Color(1f, 1f, 1f, 0.5f);

    [Networked]
    public int CurrentCheckpointIndex { get; private set; }

    [Networked]
    public Vector3 CurrentRespawnPosition { get; private set; }

    [Networked]
    public NetworkBool IsInvincible { get; private set; }

    [Networked]
    public NetworkBool IsDead { get; private set; }

    // Dùng TickTimer thay vì Coroutine
    [Networked]
    private TickTimer RespawnTimer { get; set; }

    [Networked]
    private TickTimer InvincibilityTimer { get; set; }

    private PlayerController playerController;
    private Color[] originalColors;
    private bool _lastInvincibleState; // Track để detect thay đổi

    public override void Spawned()
    {
        playerController = GetComponent<PlayerController>();

        // Cache original colors cho visual feedback
        CacheOriginalColors();

        // Init state tracking
        _lastInvincibleState = IsInvincible;

        // KHÔNG reset checkpoint ở đây - để MinigameController gọi sau khi teleport xong
    }

    private void CacheOriginalColors()
    {
        if (playerRenderers == null || playerRenderers.Length == 0)
        {
            playerRenderers = GetComponentsInChildren<Renderer>();
        }

        originalColors = new Color[playerRenderers.Length];
        for (int i = 0; i < playerRenderers.Length; i++)
        {
            if (playerRenderers[i] != null && playerRenderers[i].material != null)
            {
                originalColors[i] = playerRenderers[i].material.color;
            }
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;

        // Check respawn timer
        if (IsDead && RespawnTimer.Expired(Runner))
        {
            DoRespawn();
        }

        // Check invincibility timer
        if (IsInvincible && InvincibilityTimer.Expired(Runner))
        {
            IsInvincible = false;
        }
    }

    /// <summary>
    /// Reset checkpoint - GỌI TỪ MinigameController SAU KHI TELEPORT XONG
    /// </summary>
    public void ResetCheckpoint(Vector3 spawnPosition)
    {
        if (HasStateAuthority)
        {
            CurrentCheckpointIndex = 0;
            CurrentRespawnPosition = spawnPosition;
            IsDead = false;
            IsInvincible = false;
            Debug.Log($"[PlayerMinigameData] Checkpoint reset to position {spawnPosition}");
        }
    }

    /// <summary>
    /// Lưu checkpoint mới - gọi từ local player khi chạm checkpoint
    /// </summary>
    public void SetCheckpoint(int index, Vector3 respawnPosition)
    {
        if (Object.HasInputAuthority)
        {
            // Gửi RPC lên host để update
            RPC_SetCheckpoint(index, respawnPosition);
        }
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_SetCheckpoint(int index, Vector3 respawnPosition)
    {
        // Chỉ update nếu index mới > index hiện tại (tránh client gửi checkpoint cũ override mới)
        if (index > CurrentCheckpointIndex)
        {
            CurrentCheckpointIndex = index;
            CurrentRespawnPosition = respawnPosition;
            Debug.Log($"[PlayerMinigameData] Checkpoint set to {index} at {respawnPosition}");
        }
        else
        {
            Debug.Log($"[PlayerMinigameData] Ignored checkpoint {index} (current: {CurrentCheckpointIndex})");
        }
    }

    /// <summary>
    /// Gọi khi player chết - trigger respawn
    /// </summary>
    public void Die()
    {
        // Check ngay từ đầu để tránh spam
        if (!CanTakeDamage()) return;

        if (Object.HasInputAuthority)
        {
            // Local player gọi RPC để thông báo host
            RPC_RequestRespawn();
        }
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_RequestRespawn()
    {
        if (!CanTakeDamage()) return;

        Debug.Log($"[PlayerMinigameData] Player {Object.InputAuthority} died, respawning in {respawnDelay}s...");
        IsDead = true;

        // Dùng TickTimer thay vì Coroutine
        RespawnTimer = TickTimer.CreateFromSeconds(Runner, respawnDelay);
    }

    private void DoRespawn()
    {
        // HOST teleport trực tiếp
        var cc = GetComponent<CharacterController>();
        if (cc != null)
        {
            cc.enabled = false;
            transform.position = CurrentRespawnPosition;
            cc.enabled = true;
        }
        else
        {
            transform.position = CurrentRespawnPosition;
        }

        // Reset velocity
        if (playerController != null)
        {
            playerController.ResetVelocity();
        }

        IsDead = false;
        IsInvincible = true;
        InvincibilityTimer = TickTimer.CreateFromSeconds(Runner, invincibilityTime);

        Debug.Log("[PlayerMinigameData] Respawn complete!");
    }

    /// <summary>
    /// Render() được gọi mỗi frame - dùng để sync visual
    /// </summary>
    public override void Render()
    {
        // Check nếu IsInvincible thay đổi
        if (_lastInvincibleState != IsInvincible)
        {
            _lastInvincibleState = IsInvincible;
            UpdateInvincibleVisual();
        }
    }

    private void UpdateInvincibleVisual()
    {
        if (playerRenderers == null) return;

        for (int i = 0; i < playerRenderers.Length; i++)
        {
            if (playerRenderers[i] != null && playerRenderers[i].material != null)
            {
                playerRenderers[i].material.color = IsInvincible ? invincibleColor : originalColors[i];
            }
        }
    }

    public bool CanTakeDamage()
    {
        return !IsInvincible && !IsDead;
    }
}
