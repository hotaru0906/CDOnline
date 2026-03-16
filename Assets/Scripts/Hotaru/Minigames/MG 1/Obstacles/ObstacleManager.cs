using UnityEngine;
using Fusion;
using System;
using System.Collections.Generic;

/// <summary>
/// Data structure for tracking player effects.
/// </summary>
public struct PlayerEffectData : INetworkStruct
{
    public PlayerRef Player;
    public float BoostEndTime;
    public float BoostMultiplier;
    public float SlowEndTime;
    public float SlowMultiplier;
    public float StunEndTime;
}

/// <summary>
/// Manages obstacle spawning based on race phase.
/// Controls boss movement and obstacle/boost spawning.
/// </summary>
public class ObstacleManager : NetworkBehaviour
{
    #region Singleton
    private static ObstacleManager _instance;
    public static ObstacleManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<ObstacleManager>();
            }
            return _instance;
        }
    }
    #endregion

    [Header("Prefabs (fallback if no pool)")]
    [SerializeField] private NetworkPrefabRef jumpableObstaclePrefab;
    [SerializeField] private NetworkPrefabRef dodgeableObstaclePrefab;
    //[SerializeField] private NetworkPrefabRef boostPickupPrefab;

    [Header("References")]
    [SerializeField] private BossController bossController;
    [SerializeField] private TrackSystem trackSystem;
    [SerializeField] private RaceManager raceManager;
    [SerializeField] private ObstaclePool obstaclePool;

    [Header("Spawn Timing (seconds)")]
    [SerializeField] private float phase1SpawnInterval = 3f;
    [SerializeField] private float phase2SpawnInterval = 2f;
    [SerializeField] private float phase3SpawnInterval = 1f;

    [Header("Obstacle Settings")]
    [SerializeField] private float obstacleSpeed = 18f;         // Speed obstacles move toward start
    [SerializeField] private float spawnHeightOffset = 1f;      // Height above track to spawn
    [SerializeField, Range(0f, 1f)] 
    private float jumpableSpawnChance = 0.6f;                   // 60% jumpable, 40% dodgeable

    [Header("Boost Settings")]
    [SerializeField] private float boostSpawnChance = 0.2f;     // 20% chance to spawn boost instead of obstacle
    [SerializeField] private float boostSpawnDistanceAhead = 30f; // How far ahead of boss to spawn boost
    [SerializeField] private float defaultBoostDuration = 10f;
    [SerializeField] private float defaultBoostMultiplier = 1.5f;

    [Header("Effect Defaults")]
    [SerializeField] private float defaultSlowMultiplier = 0.5f; // 50% speed when slowed

    // Networked properties
    [Networked] public NetworkBool IsSpawningActive { get; set; }
    [Networked] public TickTimer SpawnTimer { get; set; }
    [Networked] public int TotalObstaclesSpawned { get; set; }
    [Networked] public int TotalBoostsSpawned { get; set; }

    // Player effects (max 16 players)
    [Networked, Capacity(16)] 
    public NetworkArray<PlayerEffectData> PlayerEffects => default;

    // Events
    public event Action<Obstacle> OnObstacleSpawned;
    public event Action<BoostPickup> OnBoostSpawned;
    public event Action<PlayerRef, float, float> OnPlayerBoosted;    // player, duration, multiplier
    public event Action<PlayerRef, float> OnPlayerSlowed;            // player, duration
    public event Action<PlayerRef, float> OnPlayerStunned;           // player, duration

    // Runtime
    private List<Obstacle> _activeObstacles = new List<Obstacle>();
    private List<BoostPickup> _activeBoosts = new List<BoostPickup>();
    private float _currentSpawnInterval;
    private DistancePhase _currentPhase = DistancePhase.Phase1;

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
        }
        else if (_instance != this)
        {
            Debug.LogWarning("[ObstacleManager] Duplicate instance detected!");
        }
    }

    public override void Spawned()
    {
        base.Spawned();

        // Find references if not set
        if (trackSystem == null)
            trackSystem = FindAnyObjectByType<TrackSystem>();
        if (raceManager == null)
            raceManager = RaceManager.Instance;
        if (bossController == null)
            bossController = FindAnyObjectByType<BossController>();
        if (obstaclePool == null)
            obstaclePool = FindAnyObjectByType<ObstaclePool>();

        if (Object.HasStateAuthority)
        {
            IsSpawningActive = false;
            _currentSpawnInterval = phase1SpawnInterval;
            TotalObstaclesSpawned = 0;
            TotalBoostsSpawned = 0;
        }

        // Subscribe to phase changes
        if (raceManager != null)
        {
            raceManager.OnDistancePhaseChangedEvent += OnDistancePhaseChanged;
        }

        Debug.Log("[ObstacleManager] Initialized.");
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        base.Despawned(runner, hasState);

        // Unsubscribe from events
        if (raceManager != null)
        {
            raceManager.OnDistancePhaseChangedEvent -= OnDistancePhaseChanged;
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority) return;

        // Only spawn during racing phase
        if (raceManager == null || raceManager.CurrentPhase != RacePhase.Racing)
        {
            return;
        }

        // Check spawn timer
        if (IsSpawningActive && SpawnTimer.Expired(Runner))
        {
            SpawnNextEntity();
            ResetSpawnTimer();
        }

        // Update player effects
        UpdatePlayerEffects();
    }

    #region Spawning Control

    /// <summary>
    /// Start spawning obstacles.
    /// </summary>
    public void StartSpawning()
    {
        if (!Object.HasStateAuthority) return;

        IsSpawningActive = true;
        ResetSpawnTimer();
        Debug.Log("[ObstacleManager] Spawning started!");
    }

    /// <summary>
    /// Stop spawning obstacles.
    /// </summary>
    public void StopSpawning()
    {
        if (!Object.HasStateAuthority) return;

        IsSpawningActive = false;
        Debug.Log("[ObstacleManager] Spawning stopped.");
    }

    /// <summary>
    /// Reset spawn timer based on current phase.
    /// </summary>
    private void ResetSpawnTimer()
    {
        SpawnTimer = TickTimer.CreateFromSeconds(Runner, _currentSpawnInterval);
    }

    /// <summary>
    /// Handle phase change - update spawn interval.
    /// </summary>
    private void OnDistancePhaseChanged(DistancePhase oldPhase, DistancePhase newPhase)
    {
        _currentPhase = newPhase;

        switch (newPhase)
        {
            case DistancePhase.Phase1:
                _currentSpawnInterval = phase1SpawnInterval;
                break;
            case DistancePhase.Phase2:
                _currentSpawnInterval = phase2SpawnInterval;
                break;
            case DistancePhase.Phase3:
                _currentSpawnInterval = phase3SpawnInterval;
                break;
        }

        Debug.Log($"[ObstacleManager] Phase changed to {newPhase}: spawn interval now {_currentSpawnInterval}s");
    }

    #endregion

    #region Entity Spawning

    /// <summary>
    /// Spawn next obstacle or boost.
    /// </summary>
    private void SpawnNextEntity()
    {
        if (bossController == null)
        {
            Debug.LogWarning("[ObstacleManager] No boss controller! Cannot spawn obstacles.");
            return;
        }

        // Random chance to spawn boost instead of obstacle
        if (UnityEngine.Random.value < boostSpawnChance)
        {
            SpawnBoost();
        }
        else
        {
            SpawnObstacle();
        }
    }

    /// <summary>
    /// Spawn an obstacle at boss position moving toward start.
    /// </summary>
    private void SpawnObstacle()
    {
        // Calculate spawn position (at boss, slightly elevated)
        Vector3 spawnPos = bossController.Position + Vector3.up * spawnHeightOffset;
        Quaternion spawnRot = Quaternion.LookRotation(bossController.DirectionTowardStart, Vector3.up);

        // Determine obstacle type
        bool isJumpable = UnityEngine.Random.value < jumpableSpawnChance;
        Obstacle obstacle = null;

        // Try to get from pool first
        if (obstaclePool != null && obstaclePool.IsInitialized)
        {
            obstacle = isJumpable 
                ? obstaclePool.GetJumpableObstacle(spawnPos, spawnRot)
                : obstaclePool.GetDodgeableObstacle(spawnPos, spawnRot);
        }
        else
        {
            // Fallback: direct spawn if no pool
            NetworkPrefabRef prefab = isJumpable ? jumpableObstaclePrefab : dodgeableObstaclePrefab;
            if (!prefab.IsValid)
            {
                Debug.LogWarning($"[ObstacleManager] {(isJumpable ? "Jumpable" : "Dodgeable")} obstacle prefab not assigned!");
                return;
            }

            var spawnedObj = Runner.Spawn(prefab, spawnPos, spawnRot, Object.StateAuthority);
            if (spawnedObj != null)
            {
                obstacle = spawnedObj.GetComponent<Obstacle>();
            }
        }

        if (obstacle != null)
        {
            obstacle.Initialize(bossController.DirectionTowardStart, obstacleSpeed);
            _activeObstacles.Add(obstacle);
            TotalObstaclesSpawned++;

            OnObstacleSpawned?.Invoke(obstacle);
            Debug.Log($"[ObstacleManager] Spawned {(isJumpable ? "Jumpable" : "Dodgeable")} obstacle #{TotalObstaclesSpawned}");
        }
    }

    /// <summary>
    /// Spawn a boost pickup ahead of boss.
    /// </summary>
    private void SpawnBoost()
    {
        // Calculate spawn position (ahead of boss on track)
        float boostDistance = bossController.CurrentDistance + boostSpawnDistanceAhead;
        
        // Don't spawn beyond track end
        if (trackSystem != null && boostDistance >= trackSystem.TrackLength)
        {
            boostDistance = trackSystem.TrackLength - 10f;
        }

        Vector3 spawnPos = trackSystem != null 
            ? trackSystem.GetPositionAtDistance(boostDistance) + Vector3.up * 0.5f
            : bossController.Position + bossController.Direction * boostSpawnDistanceAhead + Vector3.up * 0.5f;

        // Random lateral offset for variety
        Vector3 right = Vector3.Cross(bossController.Direction, Vector3.up).normalized;
        float lateralOffset = UnityEngine.Random.Range(-2f, 2f);
        spawnPos += right * lateralOffset;

        Quaternion spawnRot = Quaternion.identity;

        BoostPickup boost = null;

        // Try to get from pool first
        if (obstaclePool != null && obstaclePool.IsInitialized)
        {
            boost = obstaclePool.GetBoostPickup(spawnPos, spawnRot);
        }
        // else
        // {
        //     // Fallback: direct spawn if no pool
        //     if (!boostPickupPrefab.IsValid)
        //     {
        //         Debug.LogWarning("[ObstacleManager] Boost pickup prefab not assigned!");
        //         return;
        //     }

        //     var spawnedObj = Runner.Spawn(boostPickupPrefab, spawnPos, spawnRot, Object.StateAuthority);
        //     if (spawnedObj != null)
        //     {
        //         boost = spawnedObj.GetComponent<BoostPickup>();
        //     }
        // }

        if (boost != null)
        {
            boost.Initialize(defaultBoostDuration, defaultBoostMultiplier);
            _activeBoosts.Add(boost);
            TotalBoostsSpawned++;
            OnBoostSpawned?.Invoke(boost);
            Debug.Log($"[ObstacleManager] Spawned Boost #{TotalBoostsSpawned}");
        }
    }

    /// <summary>
    /// Clear all active obstacles (returns to pool if using pooling).
    /// </summary>
    public void ClearAllObstacles()
    {
        if (!Object.HasStateAuthority) return;

        // Use pool's return all if available
        if (obstaclePool != null)
        {
            obstaclePool.ReturnAllToPool();
        }
        else
        {
            // Fallback: despawn directly
            foreach (var obstacle in _activeObstacles)
            {
                if (obstacle != null && obstacle.Object != null && obstacle.Object.IsValid)
                {
                    Runner.Despawn(obstacle.Object);
                }
            }
            foreach (var boost in _activeBoosts)
            {
                if (boost != null && boost.Object != null && boost.Object.IsValid)
                {
                    Runner.Despawn(boost.Object);
                }
            }
        }

        _activeObstacles.Clear();
        _activeBoosts.Clear();
        Debug.Log("[ObstacleManager] All obstacles cleared.");
    }

    #endregion

    #region Player Effects

    /// <summary>
    /// Apply speed boost to a player.
    /// </summary>
    public void ApplyBoostEffect(PlayerRef player, float duration, float multiplier)
    {
        if (!Object.HasStateAuthority) return;

        int index = FindOrAddPlayerEffectIndex(player);
        if (index < 0) return;

        var data = PlayerEffects.Get(index);
        data.BoostEndTime = Runner.SimulationTime + duration;
        data.BoostMultiplier = multiplier;
        PlayerEffects.Set(index, data);

        OnPlayerBoosted?.Invoke(player, duration, multiplier);
        RPC_NotifyBoostApplied(player, duration, multiplier);

        Debug.Log($"[ObstacleManager] Player {player.PlayerId} boosted for {duration}s at {multiplier}x speed");
    }

    /// <summary>
    /// Apply slow effect to a player.
    /// </summary>
    public void ApplySlowEffect(PlayerRef player, float duration, float multiplier = -1f)
    {
        if (!Object.HasStateAuthority) return;

        if (multiplier < 0) multiplier = defaultSlowMultiplier;

        int index = FindOrAddPlayerEffectIndex(player);
        if (index < 0) return;

        var data = PlayerEffects.Get(index);
        data.SlowEndTime = Runner.SimulationTime + duration;
        data.SlowMultiplier = multiplier;
        PlayerEffects.Set(index, data);

        OnPlayerSlowed?.Invoke(player, duration);
        RPC_NotifySlowApplied(player, duration);

        Debug.Log($"[ObstacleManager] Player {player.PlayerId} slowed for {duration}s at {multiplier}x speed");
    }

    /// <summary>
    /// Apply stun effect to a player.
    /// </summary>
    public void ApplyStunEffect(PlayerRef player, float duration)
    {
        if (!Object.HasStateAuthority) return;

        int index = FindOrAddPlayerEffectIndex(player);
        if (index < 0) return;

        var data = PlayerEffects.Get(index);
        data.StunEndTime = Runner.SimulationTime + duration;
        PlayerEffects.Set(index, data);

        OnPlayerStunned?.Invoke(player, duration);
        RPC_NotifyStunApplied(player, duration);

        Debug.Log($"[ObstacleManager] Player {player.PlayerId} stunned for {duration}s");
    }

    /// <summary>
    /// Get the current speed multiplier for a player (considering all effects).
    /// </summary>
    public float GetPlayerSpeedMultiplier(PlayerRef player)
    {
        int index = FindPlayerEffectIndex(player);
        if (index < 0) return 1f;

        var data = PlayerEffects.Get(index);
        float currentTime = Runner.SimulationTime;

        // Check stun first (highest priority - can't move)
        if (data.StunEndTime > currentTime)
        {
            return 0f;
        }

        // Check boost and slow, boost takes priority
        if (data.BoostEndTime > currentTime)
        {
            return data.BoostMultiplier;
        }

        if (data.SlowEndTime > currentTime)
        {
            return data.SlowMultiplier;
        }

        return 1f;
    }

    /// <summary>
    /// Check if player is currently stunned.
    /// </summary>
    public bool IsPlayerStunned(PlayerRef player)
    {
        int index = FindPlayerEffectIndex(player);
        if (index < 0) return false;

        var data = PlayerEffects.Get(index);
        return data.StunEndTime > Runner.SimulationTime;
    }

    /// <summary>
    /// Check if player currently has boost.
    /// </summary>
    public bool IsPlayerBoosted(PlayerRef player)
    {
        int index = FindPlayerEffectIndex(player);
        if (index < 0) return false;

        var data = PlayerEffects.Get(index);
        return data.BoostEndTime > Runner.SimulationTime;
    }

    /// <summary>
    /// Update player effects (cleanup expired).
    /// </summary>
    private void UpdatePlayerEffects()
    {
        // Effects auto-expire based on time comparison
        // No explicit cleanup needed since we check time in GetPlayerSpeedMultiplier
    }

    /// <summary>
    /// Find player effect index.
    /// </summary>
    private int FindPlayerEffectIndex(PlayerRef player)
    {
        for (int i = 0; i < PlayerEffects.Length; i++)
        {
            if (PlayerEffects.Get(i).Player == player)
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>
    /// Find or add player to effects array.
    /// </summary>
    private int FindOrAddPlayerEffectIndex(PlayerRef player)
    {
        // First, try to find existing
        int existingIndex = FindPlayerEffectIndex(player);
        if (existingIndex >= 0) return existingIndex;

        // Find empty slot
        for (int i = 0; i < PlayerEffects.Length; i++)
        {
            if (PlayerEffects.Get(i).Player == PlayerRef.None)
            {
                var data = new PlayerEffectData { Player = player };
                PlayerEffects.Set(i, data);
                return i;
            }
        }

        Debug.LogWarning($"[ObstacleManager] No room for player {player} in effects array!");
        return -1;
    }

    /// <summary>
    /// Clear all player effects.
    /// </summary>
    public void ClearAllPlayerEffects()
    {
        if (!Object.HasStateAuthority) return;

        for (int i = 0; i < PlayerEffects.Length; i++)
        {
            PlayerEffects.Set(i, default);
        }

        Debug.Log("[ObstacleManager] All player effects cleared.");
    }

    #endregion

    #region RPCs

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_NotifyBoostApplied(PlayerRef player, float duration, float multiplier)
    {
        // Visual/audio feedback on client
        Debug.Log($"[ObstacleManager][Client] Player {player.PlayerId} received boost: {duration}s at {multiplier}x");
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_NotifySlowApplied(PlayerRef player, float duration)
    {
        Debug.Log($"[ObstacleManager][Client] Player {player.PlayerId} slowed for {duration}s");
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_NotifyStunApplied(PlayerRef player, float duration)
    {
        Debug.Log($"[ObstacleManager][Client] Player {player.PlayerId} stunned for {duration}s");
    }

    #endregion

    #region Public API

    /// <summary>
    /// Reset obstacle manager for new race.
    /// </summary>
    public void ResetForNewRace()
    {
        if (!Object.HasStateAuthority) return;

        StopSpawning();
        ClearAllObstacles();
        ClearAllPlayerEffects();

        TotalObstaclesSpawned = 0;
        TotalBoostsSpawned = 0;
        _currentPhase = DistancePhase.Phase1;
        _currentSpawnInterval = phase1SpawnInterval;

        if (bossController != null)
        {
            bossController.ResetBoss();
        }

        Debug.Log("[ObstacleManager] Reset for new race.");
    }

    /// <summary>
    /// Get current spawn interval based on phase.
    /// </summary>
    public float GetCurrentSpawnInterval() => _currentSpawnInterval;

    /// <summary>
    /// Get current phase.
    /// </summary>
    public DistancePhase GetCurrentPhase() => _currentPhase;

    #endregion

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        // Draw spawn zone at boss position (if available)
        if (bossController != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(bossController.Position + Vector3.up * spawnHeightOffset, 1f);
            UnityEditor.Handles.Label(bossController.Position + Vector3.up * 3f, "OBSTACLE SPAWN");

            // Draw boost spawn zone
            Gizmos.color = Color.yellow;
            Vector3 boostSpawnPos = bossController.Position + bossController.Direction * boostSpawnDistanceAhead;
            Gizmos.DrawWireSphere(boostSpawnPos + Vector3.up * 0.5f, 1f);
            UnityEditor.Handles.Label(boostSpawnPos + Vector3.up * 2f, "BOOST SPAWN");
        }
    }
#endif
}
