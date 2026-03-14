using Fusion;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Manages player spawning for race minigames.
/// Works with StartGrid for positions and RaceManager for registration.
/// Host-authoritative using Photon Fusion.
/// </summary>
public class RaceSpawnManager : NetworkBehaviour
{
    #region Singleton
    private static RaceSpawnManager _instance;
    public static RaceSpawnManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<RaceSpawnManager>();
            }
            return _instance;
        }
    }
    #endregion

    #region Serialized Fields
    [Header("References")]
    [SerializeField] private StartGrid startGrid;
    [SerializeField] private RaceManager raceManager;
    [SerializeField] private NetworkPrefabRef playerPrefab;

    [Header("Spawn Settings")]
    [SerializeField] private bool autoRegisterWithRaceManager = true;
    [SerializeField] private bool respawnOnDeath = false;
    [SerializeField] private float respawnDelay = 2f;
    [SerializeField] private int maxPlayersInRace = 4;

    [Header("Debug")]
    [SerializeField] private bool debugMode = false;
    #endregion

    #region Networked Properties
    /// <summary>
    /// Number of players currently spawned.
    /// </summary>
    [Networked]
    public int SpawnedPlayerCount { get; private set; }

    /// <summary>
    /// Expected number of players for this race.
    /// </summary>
    [Networked]
    public int ExpectedPlayerCount { get; private set; }

    /// <summary>
    /// Are all players spawned and ready?
    /// </summary>
    [Networked, OnChangedRender(nameof(OnAllPlayersSpawnedChanged))]
    public NetworkBool AllPlayersSpawned { get; private set; }

    /// <summary>
    /// Next spawn index (networked for consistency).
    /// </summary>
    [Networked]
    public int NextSpawnIndex { get; private set; }

    /// <summary>
    /// Player spawn order mapping - PlayerRef to spawn index.
    /// Using NetworkArray for proper sync.
    /// </summary>
    [Networked, Capacity(4)]
    public NetworkArray<int> PlayerSpawnIndices => default;

    /// <summary>
    /// Player references in spawn order.
    /// </summary>
    [Networked, Capacity(4)]
    public NetworkArray<PlayerRef> SpawnedPlayerRefs => default;
    #endregion

    #region Events
    public event Action<PlayerRef, NetworkObject> OnPlayerSpawned;
    public event Action<PlayerRef> OnPlayerDespawned;
    public event Action OnAllPlayersReady;
    #endregion

    #region Private Fields
    // Local cache for quick lookups (rebuilt from networked data)
    private Dictionary<PlayerRef, int> _localPlayerIndexCache = new Dictionary<PlayerRef, int>();
    private Queue<PlayerRef> _spawnQueue = new Queue<PlayerRef>();
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }

    public override void Spawned()
    {
        Debug.Log($"[RaceSpawnManager] Spawned. HasStateAuthority: {HasStateAuthority}");

        // Auto-find references if not set
        if (startGrid == null)
        {
            startGrid = FindAnyObjectByType<StartGrid>();
        }
        if (raceManager == null)
        {
            raceManager = RaceManager.Instance;
        }

        // Rebuild local cache from networked data (for late joiners)
        RebuildLocalCache();
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;

        // Process spawn queue
        ProcessSpawnQueue();

        // Check if all expected players are spawned
        CheckAllPlayersSpawned();
    }
    #endregion

    #region Public Spawn Methods (Host Only)

    /// <summary>
    /// Initialize spawn system for a new race.
    /// </summary>
    /// <param name="expectedPlayers">Number of players expected to join. Use -1 for current active players.</param>
    public void InitializeForRace(int expectedPlayers = -1)
    {
        if (!HasStateAuthority)
        {
            Debug.LogWarning("[RaceSpawnManager] Only Host can initialize!");
            return;
        }

        // Reset networked state
        NextSpawnIndex = 0;
        SpawnedPlayerCount = 0;
        AllPlayersSpawned = false;

        // Set expected player count
        if (expectedPlayers > 0)
        {
            ExpectedPlayerCount = Mathf.Min(expectedPlayers, maxPlayersInRace);
        }
        else
        {
            // Use current active player count from Runner
            ExpectedPlayerCount = Runner.ActivePlayers.Count();
        }

        // Clear networked arrays
        for (int i = 0; i < PlayerSpawnIndices.Length; i++)
        {
            PlayerSpawnIndices.Set(i, -1);
            SpawnedPlayerRefs.Set(i, default);
        }

        // Clear local state
        _localPlayerIndexCache.Clear();
        _spawnQueue.Clear();

        Debug.Log($"[RaceSpawnManager] Initialized for race. Expected players: {ExpectedPlayerCount}");
    }

    /// <summary>
    /// Queue a player for spawning. Players are spawned in order queued.
    /// </summary>
    /// <param name="player">Player to spawn.</param>
    public void QueuePlayerForSpawn(PlayerRef player)
    {
        if (!HasStateAuthority)
        {
            Debug.LogWarning("[RaceSpawnManager] Only Host can queue spawns!");
            return;
        }

        if (IsPlayerSpawned(player))
        {
            Debug.LogWarning($"[RaceSpawnManager] Player {player} already spawned!");
            return;
        }

        if (!_spawnQueue.Contains(player))
        {
            _spawnQueue.Enqueue(player);
            Debug.Log($"[RaceSpawnManager] Player {player} queued for spawn.");
        }
    }

    /// <summary>
    /// Spawn a player immediately at the next available spawn point.
    /// </summary>
    /// <param name="player">Player to spawn.</param>
    /// <returns>Spawned NetworkObject or null if failed.</returns>
    public NetworkObject SpawnPlayer(PlayerRef player)
    {
        if (!HasStateAuthority)
        {
            Debug.LogWarning("[RaceSpawnManager] Only Host can spawn players!");
            return null;
        }

        if (IsPlayerSpawned(player))
        {
            Debug.LogWarning($"[RaceSpawnManager] Player {player} already spawned!");
            return GetPlayerObject(player);
        }

        if (!playerPrefab.IsValid)
        {
            Debug.LogError("[RaceSpawnManager] Player prefab not assigned!");
            return null;
        }

        // Get spawn point with wraparound
        int spawnIndex = NextSpawnIndex % startGrid.MaxSpawnPoints;
        var spawnPoint = startGrid.GetSpawnPoint(spawnIndex);

        // Spawn player
        NetworkObject playerObject = Runner.Spawn(
            playerPrefab,
            spawnPoint.Position,
            spawnPoint.Rotation,
            player  // Input authority
        );

        if (playerObject != null)
        {
            // Track in networked arrays
            int arrayIndex = SpawnedPlayerCount;
            if (arrayIndex < SpawnedPlayerRefs.Length)
            {
                SpawnedPlayerRefs.Set(arrayIndex, player);
                PlayerSpawnIndices.Set(arrayIndex, spawnIndex);
            }

            // Update local cache
            _localPlayerIndexCache[player] = spawnIndex;

            // Update networked counters
            NextSpawnIndex++;
            SpawnedPlayerCount++;

            Debug.Log($"[RaceSpawnManager] Player {player} spawned at grid position {spawnIndex} (Row {spawnPoint.GridRow}, Col {spawnPoint.GridColumn})");

            // Register with RaceManager
            if (autoRegisterWithRaceManager && raceManager != null)
            {
                raceManager.RegisterPlayer(player, playerObject);
            }

            // Notify
            RPC_NotifyPlayerSpawned(player, spawnIndex);

            return playerObject;
        }
        else
        {
            Debug.LogError($"[RaceSpawnManager] Failed to spawn player {player}!");
            return null;
        }
    }

    /// <summary>
    /// Spawn a player at a specific grid position.
    /// </summary>
    /// <param name="player">Player to spawn.</param>
    /// <param name="gridIndex">Grid position index (0-3 for 2x2 grid).</param>
    /// <returns>Spawned NetworkObject or null if failed.</returns>
    public NetworkObject SpawnPlayerAtPosition(PlayerRef player, int gridIndex)
    {
        if (!HasStateAuthority)
        {
            Debug.LogWarning("[RaceSpawnManager] Only Host can spawn players!");
            return null;
        }

        if (IsPlayerSpawned(player))
        {
            Debug.LogWarning($"[RaceSpawnManager] Player {player} already spawned!");
            return GetPlayerObject(player);
        }

        if (!playerPrefab.IsValid)
        {
            Debug.LogError("[RaceSpawnManager] Player prefab not assigned!");
            return null;
        }

        // Wraparound grid index
        gridIndex = gridIndex % startGrid.MaxSpawnPoints;
        var spawnPoint = startGrid.GetSpawnPoint(gridIndex);

        NetworkObject playerObject = Runner.Spawn(
            playerPrefab,
            spawnPoint.Position,
            spawnPoint.Rotation,
            player
        );

        if (playerObject != null)
        {
            int arrayIndex = SpawnedPlayerCount;
            if (arrayIndex < SpawnedPlayerRefs.Length)
            {
                SpawnedPlayerRefs.Set(arrayIndex, player);
                PlayerSpawnIndices.Set(arrayIndex, gridIndex);
            }

            _localPlayerIndexCache[player] = gridIndex;
            SpawnedPlayerCount++;

            Debug.Log($"[RaceSpawnManager] Player {player} spawned at specific grid position {gridIndex}");

            if (autoRegisterWithRaceManager && raceManager != null)
            {
                raceManager.RegisterPlayer(player, playerObject);
            }

            RPC_NotifyPlayerSpawned(player, gridIndex);
            return playerObject;
        }

        return null;
    }

    /// <summary>
    /// Spawn all queued players at once.
    /// </summary>
    public void SpawnAllQueuedPlayers()
    {
        if (!HasStateAuthority) return;

        while (_spawnQueue.Count > 0)
        {
            PlayerRef player = _spawnQueue.Dequeue();
            SpawnPlayer(player);
        }
    }

    /// <summary>
    /// Despawn a player.
    /// </summary>
    public void DespawnPlayer(PlayerRef player)
    {
        if (!HasStateAuthority) return;

        NetworkObject playerObject = GetPlayerObject(player);
        if (playerObject != null)
        {
            Runner.Despawn(playerObject);

            // Remove from networked arrays (shift remaining)
            RemovePlayerFromArrays(player);

            _localPlayerIndexCache.Remove(player);
            SpawnedPlayerCount = Mathf.Max(0, SpawnedPlayerCount - 1);

            if (raceManager != null)
            {
                raceManager.UnregisterPlayer(player);
            }

            RPC_NotifyPlayerDespawned(player);
            Debug.Log($"[RaceSpawnManager] Player {player} despawned.");
        }
    }

    /// <summary>
    /// Despawn all players.
    /// </summary>
    public void DespawnAllPlayers()
    {
        if (!HasStateAuthority) return;

        // Get all spawned players
        var playersToRemove = new List<PlayerRef>();
        for (int i = 0; i < SpawnedPlayerCount; i++)
        {
            var playerRef = SpawnedPlayerRefs[i];
            if (playerRef != default)
            {
                playersToRemove.Add(playerRef);
            }
        }

        foreach (var player in playersToRemove)
        {
            DespawnPlayer(player);
        }

        NextSpawnIndex = 0;
        AllPlayersSpawned = false;
    }

    /// <summary>
    /// Respawn a player at their original position using network-safe teleport.
    /// </summary>
    public void RespawnPlayer(PlayerRef player)
    {
        if (!HasStateAuthority) return;

        int spawnIndex = GetPlayerSpawnIndex(player);
        if (spawnIndex < 0)
        {
            Debug.LogWarning($"[RaceSpawnManager] Cannot respawn {player} - no spawn order recorded.");
            return;
        }

        NetworkObject playerObject = GetPlayerObject(player);
        if (playerObject != null)
        {
            var spawnPoint = startGrid.GetSpawnPoint(spawnIndex);

            // Use NetworkCharacterController.Teleport for network-safe teleport
            var networkCC = playerObject.GetComponent<NetworkCharacterController>();
            if (networkCC != null)
            {
                networkCC.Teleport(spawnPoint.Position, spawnPoint.Rotation);
                Debug.Log($"[RaceSpawnManager] Player {player} teleported via NetworkCharacterController.");
            }
            else
            {
                // Fallback: Use PlayerController.Teleport if available
                var controller = playerObject.GetComponent<PlayerController>();
                if (controller != null)
                {
                    controller.Teleport(spawnPoint.Position);
                    // Set rotation via RPC or direct state authority
                    RPC_SetPlayerRotation(player, spawnPoint.Rotation);
                }
                else
                {
                    // Last resort: RPC to set position (not ideal but works)
                    RPC_TeleportPlayer(player, spawnPoint.Position, spawnPoint.Rotation);
                }
            }

            Debug.Log($"[RaceSpawnManager] Player {player} respawned at position {spawnIndex}");
        }
    }

    /// <summary>
    /// Teleport all players to their start positions.
    /// </summary>
    public void TeleportAllToStart()
    {
        if (!HasStateAuthority) return;

        for (int i = 0; i < SpawnedPlayerCount; i++)
        {
            var playerRef = SpawnedPlayerRefs[i];
            if (playerRef != default)
            {
                RespawnPlayer(playerRef);
            }
        }

        Debug.Log("[RaceSpawnManager] All players teleported to start.");
    }

    /// <summary>
    /// Set the expected player count for ready check.
    /// </summary>
    public void SetExpectedPlayerCount(int count)
    {
        if (!HasStateAuthority) return;
        ExpectedPlayerCount = Mathf.Min(count, maxPlayersInRace);
        Debug.Log($"[RaceSpawnManager] Expected player count set to {ExpectedPlayerCount}");
    }
    #endregion

    #region Public Getters

    /// <summary>
    /// Get the NetworkObject for a player using Fusion's network-safe lookup.
    /// </summary>
    public NetworkObject GetPlayerObject(PlayerRef player)
    {
        // Use Fusion's built-in method for network-safe lookup
        if (Runner != null && Runner.TryGetPlayerObject(player, out NetworkObject playerObject))
        {
            return playerObject;
        }
        return null;
    }

    /// <summary>
    /// Get all spawned player references.
    /// </summary>
    public List<PlayerRef> GetAllSpawnedPlayerRefs()
    {
        var players = new List<PlayerRef>();
        for (int i = 0; i < SpawnedPlayerCount; i++)
        {
            var playerRef = SpawnedPlayerRefs[i];
            if (playerRef != default)
            {
                players.Add(playerRef);
            }
        }
        return players;
    }

    /// <summary>
    /// Get player's spawn position index.
    /// </summary>
    public int GetPlayerSpawnIndex(PlayerRef player)
    {
        // Check local cache first
        if (_localPlayerIndexCache.TryGetValue(player, out int cachedIndex))
        {
            return cachedIndex;
        }

        // Search in networked array
        for (int i = 0; i < SpawnedPlayerCount; i++)
        {
            if (SpawnedPlayerRefs[i] == player)
            {
                int index = PlayerSpawnIndices[i];
                _localPlayerIndexCache[player] = index;  // Cache it
                return index;
            }
        }

        return -1;
    }

    /// <summary>
    /// Check if a player is spawned.
    /// </summary>
    public bool IsPlayerSpawned(PlayerRef player)
    {
        for (int i = 0; i < SpawnedPlayerCount; i++)
        {
            if (SpawnedPlayerRefs[i] == player)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Get spawn point data for a player.
    /// </summary>
    public StartGrid.SpawnPointData GetPlayerSpawnPoint(PlayerRef player)
    {
        int index = GetPlayerSpawnIndex(player);
        if (index >= 0)
        {
            return startGrid.GetSpawnPoint(index);
        }
        return default;
    }
    #endregion

    #region Private Methods

    private void ProcessSpawnQueue()
    {
        // Process one spawn per tick to avoid overload
        if (_spawnQueue.Count > 0)
        {
            PlayerRef player = _spawnQueue.Dequeue();
            SpawnPlayer(player);
        }
    }

    private void CheckAllPlayersSpawned()
    {
        // Only check if we have an expected count set
        if (ExpectedPlayerCount <= 0) return;

        // Check if we've reached expected count
        if (SpawnedPlayerCount >= ExpectedPlayerCount && !AllPlayersSpawned)
        {
            AllPlayersSpawned = true;
            Debug.Log($"[RaceSpawnManager] All {SpawnedPlayerCount} players spawned!");
        }
    }

    private void RemovePlayerFromArrays(PlayerRef player)
    {
        // Find player index
        int foundIndex = -1;
        for (int i = 0; i < SpawnedPlayerRefs.Length; i++)
        {
            if (SpawnedPlayerRefs[i] == player)
            {
                foundIndex = i;
                break;
            }
        }

        if (foundIndex < 0) return;

        // Shift remaining entries down
        for (int i = foundIndex; i < SpawnedPlayerRefs.Length - 1; i++)
        {
            SpawnedPlayerRefs.Set(i, SpawnedPlayerRefs[i + 1]);
            PlayerSpawnIndices.Set(i, PlayerSpawnIndices[i + 1]);
        }

        // Clear last slot
        int lastIndex = SpawnedPlayerRefs.Length - 1;
        SpawnedPlayerRefs.Set(lastIndex, default);
        PlayerSpawnIndices.Set(lastIndex, -1);
    }

    private void RebuildLocalCache()
    {
        _localPlayerIndexCache.Clear();
        for (int i = 0; i < SpawnedPlayerCount; i++)
        {
            var playerRef = SpawnedPlayerRefs[i];
            if (playerRef != default)
            {
                _localPlayerIndexCache[playerRef] = PlayerSpawnIndices[i];
            }
        }
    }

    private void OnAllPlayersSpawnedChanged()
    {
        if (AllPlayersSpawned)
        {
            OnAllPlayersReady?.Invoke();
            Debug.Log("[RaceSpawnManager] All players ready event fired.");
        }
    }
    #endregion

    #region RPCs
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_NotifyPlayerSpawned(PlayerRef player, int spawnIndex)
    {
        // Update local cache on all clients
        _localPlayerIndexCache[player] = spawnIndex;

        NetworkObject playerObj = GetPlayerObject(player);
        OnPlayerSpawned?.Invoke(player, playerObj);
        Debug.Log($"[RaceSpawnManager] RPC: Player {player} spawned at index {spawnIndex}.");
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_NotifyPlayerDespawned(PlayerRef player)
    {
        _localPlayerIndexCache.Remove(player);
        OnPlayerDespawned?.Invoke(player);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_TeleportPlayer(PlayerRef player, Vector3 position, Quaternion rotation)
    {
        NetworkObject playerObj = GetPlayerObject(player);
        if (playerObj != null && playerObj.HasStateAuthority)
        {
            var networkCC = playerObj.GetComponent<NetworkCharacterController>();
            if (networkCC != null)
            {
                networkCC.Teleport(position, rotation);
            }
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_SetPlayerRotation(PlayerRef player, Quaternion rotation)
    {
        NetworkObject playerObj = GetPlayerObject(player);
        if (playerObj != null && playerObj.HasStateAuthority)
        {
            playerObj.transform.rotation = rotation;
        }
    }
    #endregion

    #region Debug
    private void OnGUI()
    {
        if (!debugMode) return;

        GUILayout.BeginArea(new Rect(420, 10, 350, 350));
        GUILayout.Label("=== Race Spawn Manager ===");
        GUILayout.Label($"Spawned: {SpawnedPlayerCount} / Expected: {ExpectedPlayerCount}");
        GUILayout.Label($"Next Index: {NextSpawnIndex}");
        GUILayout.Label($"Queue: {_spawnQueue.Count}");
        GUILayout.Label($"All Ready: {AllPlayersSpawned}");
        GUILayout.Label($"Max Grid Slots: {(startGrid != null ? startGrid.MaxSpawnPoints : 0)}");

        GUILayout.Space(5);
        GUILayout.Label("--- Spawned Players ---");
        for (int i = 0; i < SpawnedPlayerCount; i++)
        {
            var playerRef = SpawnedPlayerRefs[i];
            if (playerRef != default)
            {
                int pos = PlayerSpawnIndices[i];
                NetworkObject obj = GetPlayerObject(playerRef);
                string status = obj != null ? "OK" : "NULL";
                GUILayout.Label($"P{playerRef}: Grid {pos} [{status}]");
            }
        }

        GUILayout.EndArea();
    }
    #endregion
}
