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
    [SerializeField] private Color eliminatedColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);

    [Networked]
    public int CurrentCheckpointIndex { get; private set; }

    [Networked]
    public Vector3 CurrentRespawnPosition { get; private set; }

    [Networked]
    public NetworkBool IsInvincible { get; private set; }

    [Networked]
    public NetworkBool IsDead { get; private set; }

    /// <summary>
    /// Player đã bị loại khỏi minigame (không được respawn)
    /// </summary>
    [Networked]
    public NetworkBool IsEliminated { get; private set; }

    // ----------------------------------------------------------------
    //  Ranking fields — dùng cho Racing và các mode có thứ hạng
    // ----------------------------------------------------------------

    /// <summary>Thứ hạng về đích: 1 = nhất, 2 = nhì... 0 = chưa về đích.</summary>
    [Networked]
    public int FinishRank { get; private set; }

    /// <summary>Đã về đích chưa.</summary>
    [Networked]
    public NetworkBool HasFinished { get; private set; }

    /// <summary>Thời điểm về đích (giây kể từ khi game bắt đầu). 0 nếu chưa về.</summary>
    [Networked]
    public float FinishTime { get; private set; }

    /// <summary>
    /// Tiến độ khi chưa về đích — dùng để tính rank khi timeout.
    /// Gán từ MG2RacingController: checkpointIndex * 1000 + worldPos.
    /// </summary>
    [Networked]
    public float DistanceProgress { get; private set; }

    /// <summary>Điểm số — dùng cho Score mode sau này.</summary>
    [Networked]
    public int Score { get; private set; }

    // Dùng TickTimer thay vì Coroutine
    [Networked]
    private TickTimer RespawnTimer { get; set; }

    [Networked]
    private TickTimer InvincibilityTimer { get; set; }

    private PlayerController playerController;
    private Color[] originalColors;
    private bool _lastInvincibleState; // Track để detect thay đổi
    private bool _lastEliminatedState;
    
    // Event khi player bị loại
    public event System.Action<PlayerMinigameData> OnPlayerEliminated;

    public override void Spawned()
    {
        playerController = GetComponent<PlayerController>();

        // Cache original colors cho visual feedback
        CacheOriginalColors();

        // Init state tracking
        _lastInvincibleState = IsInvincible;
        _lastEliminatedState = IsEliminated;

        if (HasStateAuthority && CurrentRespawnPosition == Vector3.zero)
        {
            CurrentRespawnPosition = transform.position;
        }
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

        // Check respawn timer - không respawn nếu đã bị loại
        if (IsDead && !IsEliminated && RespawnTimer.Expired(Runner))
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
    /// Reset checkpoint - GỌI TỪ BaseMinigameController SAU KHI TELEPORT XONG
    /// </summary>
    public void ResetCheckpoint(Vector3 spawnPosition)
    {
        if (!HasStateAuthority) return;

        CurrentCheckpointIndex = 0;
        CurrentRespawnPosition = spawnPosition;
        IsDead = false;
        IsInvincible = false;
        IsEliminated = false;

        // Reset ranking fields
        FinishRank = 0;
        HasFinished = false;
        FinishTime = 0f;
        DistanceProgress = 0f;
        Score = 0;

        if (playerController != null)
            playerController.SetFrozen(false);

        Debug.Log($"[PlayerMinigameData] Checkpoint reset to position {spawnPosition}");
    }

    /// <summary>
    /// Ghi nhận player đã về đích với thứ hạng và thời gian.
    /// Gọi từ BaseMinigameController.PlayerFinished().
    /// </summary>
    public void SetFinished(int rank, float finishTime)
    {
        if (!HasStateAuthority) return;
        if (HasFinished) return;

        FinishRank = rank;
        HasFinished = true;
        FinishTime = finishTime;

        Debug.Log($"[PlayerMinigameData] Player finished — Rank: {rank}, Time: {finishTime:F2}s");
    }

    /// <summary>
    /// Cập nhật DistanceProgress — gọi định kỳ từ MG2RacingController.
    /// progress = checkpointIndex * 1000 + worldPositionAlongRaceAxis
    /// </summary>
    public void UpdateDistanceProgress(float progress)
    {
        if (!HasStateAuthority) return;
        DistanceProgress = progress;
    }

    /// <summary>Cộng điểm — dùng cho Score mode.</summary>
    public void AddScore(int amount)
    {
        if (!HasStateAuthority) return;
        Score += amount;
    }

    /// <summary>
    /// Lưu checkpoint mới - gọi từ local player khi chạm checkpoint
    /// </summary>
    public void SetCheckpoint(int index, Vector3 respawnPosition)
    {
        if (!HasStateAuthority) return;

        if (index > CurrentCheckpointIndex)
        {
            CurrentCheckpointIndex = index;
            CurrentRespawnPosition = respawnPosition;

            Debug.Log($"[PlayerMinigameData] Checkpoint set to {index}");
        }
    }

    /// <summary>
    /// Gọi khi player chết - trigger respawn hoặc elimination
    /// </summary>
    public void Die()
    {
        // Check ngay từ đầu để tránh spam
        if (!CanTakeDamage()) return;

        if (HasStateAuthority)
        {
            // HOST gọi trực tiếp (ví dụ: từ obstacle) — có StateAuthority → xử lý ngay
            ExecuteDeath();
        }
        else if (Object.HasInputAuthority)
        {
            // Client gọi trên chính player của mình — gửi RPC lên host
            RPC_RequestDeath();
        }
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_RequestDeath()
    {
        ExecuteDeath();
    }

    /// <summary>
    /// Logic death thực sự — chỉ chạy trên StateAuthority (host).
    /// </summary>
    private void ExecuteDeath()
    {
        if (!CanTakeDamage()) return;

        // Đọc allowRespawn từ synced Networked property (thay vì lookup MinigameData)
        bool canRespawn = true;
        
        if (GameManager.Instance != null)
        {
            canRespawn = GameManager.Instance.MG_AllowRespawn;
            Debug.Log($"[PlayerMinigameData] MG_AllowRespawn from GameManager: {canRespawn}");
        }
        
        if (canRespawn)
        {
            // Respawn bình thường
            Debug.Log($"[PlayerMinigameData] Player {Object.InputAuthority} died, respawning in {respawnDelay}s...");
            IsDead = true;
            RespawnTimer = TickTimer.CreateFromSeconds(Runner, respawnDelay);
        }
        else
        {
            // Loại player khỏi minigame
            Debug.Log($"[PlayerMinigameData] Player {Object.InputAuthority} eliminated!");
            IsDead = true;
            IsEliminated = true;
            
            // Disable player input/movement
            if (playerController != null)
            {
                playerController.SetFrozen(true);
            }
        }
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_RequestRespawn()
    {
        // Legacy - giữ lại cho backward compatibility
        if (!CanTakeDamage()) return;

        Debug.Log($"[PlayerMinigameData] Player {Object.InputAuthority} died, respawning in {respawnDelay}s...");
        IsDead = true;

        // Dùng TickTimer thay vì Coroutine
        RespawnTimer = TickTimer.CreateFromSeconds(Runner, respawnDelay);
    }

    private void DoRespawn()
    {
        if (playerController != null)
        {
            playerController.Teleport(CurrentRespawnPosition);
        }
        else
        {
            transform.position = CurrentRespawnPosition;
        }

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
    /// Kích hoạt invincibility ngắn sau khi bị knockback
    /// Dùng để player không bị hit liên tục bởi trap
    /// </summary>
    public void TriggerKnockbackInvincibility(float duration = 0.25f)
    {
        if (!HasStateAuthority)
        {
            RPC_TriggerKnockbackInvincibility(duration);
            return;
        }
        
        IsInvincible = true;
        InvincibilityTimer = TickTimer.CreateFromSeconds(Runner, duration);
        Debug.Log($"[PlayerMinigameData] Knockback invincibility: {duration}s");
    }
    
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_TriggerKnockbackInvincibility(float duration)
    {
        IsInvincible = true;
        InvincibilityTimer = TickTimer.CreateFromSeconds(Runner, duration);
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
            UpdateVisual();
        }
        
        // Check nếu IsEliminated thay đổi
        if (_lastEliminatedState != IsEliminated)
        {
            _lastEliminatedState = IsEliminated;
            UpdateVisual();
            
            // Fire event khi player bị loại
            if (IsEliminated)
            {
                OnPlayerEliminated?.Invoke(this);
            }
        }
    }

    private void UpdateVisual()
    {
        if (playerRenderers == null) return;

        Color targetColor;
        if (IsEliminated)
        {
            targetColor = eliminatedColor;
        }
        else if (IsInvincible)
        {
            targetColor = invincibleColor;
        }
        else
        {
            // Original color - handled per renderer
            for (int i = 0; i < playerRenderers.Length; i++)
            {
                if (playerRenderers[i] != null && playerRenderers[i].material != null && i < originalColors.Length)
                {
                    playerRenderers[i].material.color = originalColors[i];
                }
            }
            return;
        }
        
        // Apply target color
        for (int i = 0; i < playerRenderers.Length; i++)
        {
            if (playerRenderers[i] != null && playerRenderers[i].material != null)
            {
                playerRenderers[i].material.color = targetColor;
            }
        }
    }

    private void UpdateInvincibleVisual()
    {
        // Legacy - giữ lại nhưng redirect sang UpdateVisual
        UpdateVisual();
    }

    public bool CanTakeDamage()
    {
        return !IsInvincible && !IsDead && !IsEliminated;
    }
    
    /// <summary>
    /// Reset trạng thái cho round mới
    /// </summary>
    public void ResetForNewRound()
    {
        if (!HasStateAuthority) return;

        IsDead = false;
        IsEliminated = false;
        IsInvincible = false;
        CurrentCheckpointIndex = 0;
        FinishRank = 0;
        HasFinished = false;
        FinishTime = 0f;
        DistanceProgress = 0f;
        Score = 0;

        if (playerController != null)
            playerController.SetFrozen(false);
    }
}
