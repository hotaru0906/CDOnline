using Fusion;
using UnityEngine;

public class PlayerMinigameData : NetworkBehaviour
{
    #region Inspector

    [Header("Respawn Settings")]
    [SerializeField] private float respawnDelay = 2f;
    [SerializeField] private float invincibilityTime = 2f;

    [Header("Visual Feedback")]
    [SerializeField] private Renderer[] playerRenderers;
    [SerializeField] private Color invincibleColor = new Color(1f, 1f, 1f, 0.5f);
    [SerializeField] private Color eliminatedColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);

    #endregion

    #region Networked State

    [Networked] public int CurrentCheckpointIndex { get; private set; }
    [Networked] public Vector3 CurrentRespawnPosition { get; private set; }
    [Networked] public NetworkBool IsInvincible { get; private set; }
    [Networked] public NetworkBool IsDead { get; private set; }

    // Thêm OnChangedRender để client nhận callback khi IsEliminated thay đổi
    [Networked, OnChangedRender(nameof(OnIsEliminatedChangedRender))]
    public NetworkBool IsEliminated { get; private set; }

    [Networked] public int FinishRank { get; private set; }
    [Networked] public NetworkBool HasFinished { get; private set; }
    [Networked] public float FinishTime { get; private set; }
    [Networked] public float DistanceProgress { get; private set; }
    [Networked] public int Score { get; private set; }

    // Thêm OnChangedRender để client nhận callback khi Lives thay đổi
    [Networked, OnChangedRender(nameof(OnLivesChangedRender))]
    public int Lives { get; private set; } = 0;

    [Networked] private TickTimer RespawnTimer { get; set; }
    [Networked] private TickTimer InvincibilityTimer { get; set; }

    #endregion

    #region Events

    // Fired locally on the instance when player becomes eliminated (Render() detects change)
    public event System.Action<PlayerMinigameData> OnPlayerEliminated;

    // Host-side event when lives change: (playerId, newLives)
    public event System.Action<int, int> OnLivesChangedHost;

    #endregion

    #region Private Fields

    private PlayerController playerController;
    private Color[] originalColors;
    private bool _lastInvincibleState;
    private bool _lastEliminatedState;

    #endregion

    #region Lifecycle

    public override void Spawned()
    {
        playerController = GetComponent<PlayerController>();
        CacheOriginalColors();

        _lastInvincibleState = IsInvincible;
        _lastEliminatedState = IsEliminated;

        if (HasStateAuthority && CurrentRespawnPosition == Vector3.zero)
            CurrentRespawnPosition = transform.position;
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;

        if (IsDead && !IsEliminated && RespawnTimer.Expired(Runner))
            DoRespawn();

        if (IsInvincible && InvincibilityTimer.Expired(Runner))
            IsInvincible = false;
    }

    public override void Render()
    {
        if (_lastInvincibleState != IsInvincible)
        {
            _lastInvincibleState = IsInvincible;
            UpdateVisual();
        }

        if (_lastEliminatedState != IsEliminated)
        {
            _lastEliminatedState = IsEliminated;
            UpdateVisual();

            if (IsEliminated)
                OnPlayerEliminated?.Invoke(this);
        }
    }

    #endregion

    #region Public API

    public void ResetCheckpoint(Vector3 spawnPosition)
    {
        if (!HasStateAuthority) return;

        CurrentCheckpointIndex = 0;
        CurrentRespawnPosition = spawnPosition;
        IsDead = false;
        IsInvincible = false;
        IsEliminated = false;
        FinishRank = 0;
        HasFinished = false;
        FinishTime = 0f;
        DistanceProgress = 0f;
        Score = 0;
        Lives = 0;

        // Fire host-side lives changed event
        OnLivesChangedHost?.Invoke(Object.InputAuthority.PlayerId, Lives);

        playerController?.SetFrozen(false);
        Debug.Log($"[PlayerMinigameData] Checkpoint reset to {spawnPosition}");
    }

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
        Lives = 0;

        // Fire host-side lives changed event
        OnLivesChangedHost?.Invoke(Object.InputAuthority.PlayerId, Lives);

        playerController?.SetFrozen(false);
    }

    public void SetCheckpoint(int index, Vector3 respawnPosition)
    {
        if (!HasStateAuthority) return;

        if (index > CurrentCheckpointIndex)
        {
            CurrentCheckpointIndex = index;
            CurrentRespawnPosition = respawnPosition;
            Debug.Log($"[PlayerMinigameData] Checkpoint → {index}");
        }
    }

    public void SetFinished(int rank, float finishTime)
    {
        if (!HasStateAuthority || HasFinished) return;

        FinishRank = rank;
        HasFinished = true;
        FinishTime = finishTime;
        Debug.Log($"[PlayerMinigameData] Finished — Rank {rank}, Time {finishTime:F2}s");
    }

    public void UpdateDistanceProgress(float progress)
    {
        if (!HasStateAuthority) return;
        DistanceProgress = progress;
    }

    public void AddScore(int amount)
    {
        if (!HasStateAuthority) return;
        Score += amount;
    }

    /// <summary>
    /// Set số mạng (host only). Gọi từ MG controller khi game start.
    /// </summary>
    public void SetLives(int lives)
    {
        if (!HasStateAuthority) return;
        Lives = lives;
        OnLivesChangedHost?.Invoke(Object.InputAuthority.PlayerId, Lives);
        Debug.Log($"[PlayerMinigameData] P{Object.InputAuthority} lives set to {Lives}");
    }

    public void LoseLife()
    {
        if (!HasStateAuthority) return;
        if (IsEliminated) return;
        if (IsInvincible) return; // đang bất tử, không bị trừ thêm

        Lives = Mathf.Max(0, Lives - 1);
        Debug.Log($"[PlayerMinigameData] P{Object.InputAuthority} lost a life — {Lives} remaining");

        if (Lives <= 0)
        {
            // Hết mạng — die vĩnh viễn, không respawn
            IsEliminated = true;
            IsDead = true;
            if (playerController != null)
                playerController.SetFrozen(true);
            Debug.Log($"[PlayerMinigameData] P{Object.InputAuthority} ELIMINATED");
        }
        else
        {
            // Còn mạng — respawn về spawn point, bất tử 3s
            if (playerController != null)
            {
                playerController.Teleport(CurrentRespawnPosition);
                playerController.ResetVelocity();
            }

            IsInvincible = true;
            InvincibilityTimer = TickTimer.CreateFromSeconds(Runner, 3f);

            RPC_OnRespawn(Object.InputAuthority);
        }
    }

    public void Die()
    {
        if (!CanTakeDamage()) return;

        if (HasStateAuthority)
            ExecuteDeath();
        else if (Object.HasInputAuthority)
            RPC_RequestDeath();
    }

    public void TriggerKnockbackInvincibility(float duration = 0.25f)
    {
        if (!HasStateAuthority)
        {
            RPC_TriggerKnockbackInvincibility(duration);
            return;
        }

        IsInvincible = true;
        InvincibilityTimer = TickTimer.CreateFromSeconds(Runner, duration);
    }

    public bool CanTakeDamage() => !IsInvincible && !IsDead && !IsEliminated;

    #endregion

    #region Death & Respawn

    private void ExecuteDeath()
    {
        if (!CanTakeDamage()) return;

        bool canRespawn = GameManager.Instance?.MG_AllowRespawn ?? true;
        Debug.Log($"[PlayerMinigameData] MG_AllowRespawn: {canRespawn}");

        IsDead = true;

        if (canRespawn)
        {
            Debug.Log($"[PlayerMinigameData] P{Object.InputAuthority} died — respawning in {respawnDelay}s");
            RespawnTimer = TickTimer.CreateFromSeconds(Runner, respawnDelay);
        }
        else
        {
            Debug.Log($"[PlayerMinigameData] P{Object.InputAuthority} eliminated");
            IsEliminated = true;
            playerController?.SetFrozen(true);
        }
    }

    private void DoRespawn()
    {
        if (playerController != null)
        {
            playerController.Teleport(CurrentRespawnPosition);
            playerController.ResetVelocity();
        }
        else
        {
            transform.position = CurrentRespawnPosition;
        }

        IsDead = false;
        GetComponent<PlayerModelSwitcher>()?.ShowCharacter();

        IsInvincible = true;
        InvincibilityTimer = TickTimer.CreateFromSeconds(Runner, invincibilityTime);

        RPC_OnRespawn(Object.InputAuthority);
        Debug.Log("[PlayerMinigameData] Respawn complete");
    }

    #endregion

    #region RPCs

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_RequestDeath() => ExecuteDeath();

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_TriggerKnockbackInvincibility(float duration)
    {
        IsInvincible = true;
        InvincibilityTimer = TickTimer.CreateFromSeconds(Runner, duration);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_OnRespawn(PlayerRef playerRef)
    {
        // TODO Phase 8: play respawn VFX/sound
        Debug.Log($"[PlayerMinigameData] RPC_OnRespawn → P{playerRef}");
    }

    #endregion

    #region Visual

    private void CacheOriginalColors()
    {
        if (playerRenderers == null || playerRenderers.Length == 0)
            playerRenderers = GetComponentsInChildren<Renderer>();

        originalColors = new Color[playerRenderers.Length];
        for (int i = 0; i < playerRenderers.Length; i++)
        {
            if (playerRenderers[i]?.material != null)
                originalColors[i] = playerRenderers[i].material.color;
        }
    }

    private void UpdateVisual()
    {
        if (playerRenderers == null) return;

        if (!IsEliminated && !IsInvincible)
        {
            for (int i = 0; i < playerRenderers.Length; i++)
            {
                if (playerRenderers[i]?.material != null && i < originalColors.Length)
                    playerRenderers[i].material.color = originalColors[i];
            }
            return;
        }

        Color target = IsEliminated ? eliminatedColor : invincibleColor;
        foreach (var r in playerRenderers)
        {
            if (r?.material != null)
                r.material.color = target;
        }
    }

    #endregion

    #region OnChangedRender callbacks (client-side) to update HUD

    // Called on clients when Lives networked value changes
    private void OnLivesChangedRender()
    {
        // Update HUD entry for this player on clients
        MinigameHUDController.Instance?.UpdatePlayerLives(Object.InputAuthority.PlayerId, Lives);
    }

    // Called on clients when IsEliminated networked value changes
    private void OnIsEliminatedChangedRender()
    {
        MinigameHUDController.Instance?.MarkPlayerEliminated(Object.InputAuthority.PlayerId);
    }

    #endregion
}
