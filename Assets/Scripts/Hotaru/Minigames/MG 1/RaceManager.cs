using Fusion;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Race phases for minigame flow.
/// </summary>
public enum RacePhase
{
    Waiting,    // Waiting for players
    Countdown,  // 3, 2, 1, GO!
    Racing,     // Race in progress
    Finished    // Race completed
}

/// <summary>
/// Distance-based game phase for minigame mechanics.
/// Changes based on leader's progress.
/// </summary>
public enum DistancePhase
{
    Phase1,     // 0 - 700
    Phase2,     // 700 - 1500
    Phase3      // 1500 - 2400 (end)
}

/// <summary>
/// Data structure for tracking individual player's race progress.
/// </summary>
public struct RacePlayerData : INetworkStruct
{
    public PlayerRef Player;
    public float Distance;
    public float Progress;      // 0-1
    public int Rank;            // 1st, 2nd, 3rd, 4th
    public NetworkBool HasFinished;
    public float FinishTime;    // Time when player finished (0 if not finished)
}

/// <summary>
/// Manages race minigame: distance tracking, phases, rankings, and finish detection.
/// Host-authoritative using Photon Fusion.
/// </summary>
public class RaceManager : NetworkBehaviour
{
    #region Singleton
    private static RaceManager _instance;
    public static RaceManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<RaceManager>();
            }
            return _instance;
        }
    }
    #endregion

    #region Serialized Fields
    [Header("Track Reference")]
    [SerializeField] private TrackSystem trackSystem;

    [Header("Race Settings")]
    [SerializeField] private float countdownDuration = 3f;
    [SerializeField] private int maxPlayers = 4;
    [SerializeField] private bool endRaceOnFirstFinish = true;

    [Header("Distance Phase Thresholds")]
    [SerializeField] private float phase1End = 700f;
    [SerializeField] private float phase2End = 1500f;
    [SerializeField] private float phase3End = 2400f;

    [Header("Debug")]
    [SerializeField] private bool debugMode = false;
    #endregion

    #region Networked Properties
    /// <summary>
    /// Current race phase - synced across all clients.
    /// </summary>
    [Networked, OnChangedRender(nameof(OnPhaseChanged))]
    public RacePhase CurrentPhase { get; private set; } = RacePhase.Waiting;

    /// <summary>
    /// Current distance phase based on leader's progress.
    /// </summary>
    [Networked, OnChangedRender(nameof(OnDistancePhaseChanged))]
    public DistancePhase CurrentDistancePhase { get; private set; } = DistancePhase.Phase1;

    /// <summary>
    /// Total track length - synced with callback for late joiners.
    /// </summary>
    [Networked, OnChangedRender(nameof(OnTrackLengthChanged))]
    public float TrackLength { get; private set; }

    /// <summary>
    /// Countdown timer (counts down from countdownDuration to 0).
    /// </summary>
    [Networked, OnChangedRender(nameof(OnCountdownChanged))]
    public float CountdownTimer { get; private set; }

    /// <summary>
    /// Race elapsed time (starts when racing begins).
    /// </summary>
    [Networked]
    public float RaceTime { get; private set; }

    /// <summary>
    /// Number of players who have finished.
    /// </summary>
    [Networked]
    public int FinishedPlayerCount { get; private set; }

    /// <summary>
    /// Player race data - networked array for all players.
    /// </summary>
    [Networked, Capacity(4)]
    public NetworkArray<RacePlayerData> PlayerDataArray => default;

    /// <summary>
    /// Number of active players in the race.
    /// </summary>
    [Networked]
    public int ActivePlayerCount { get; private set; }

    /// <summary>
    /// Leader's current distance (for phase calculation).
    /// </summary>
    [Networked]
    public float LeaderDistance { get; private set; }

    /// <summary>
    /// Winner's PlayerRef (first to finish).
    /// </summary>
    [Networked]
    public PlayerRef Winner { get; private set; }
    #endregion

    #region Events
    public event Action<RacePhase, RacePhase> OnPhaseChangedEvent;
    public event Action<DistancePhase, DistancePhase> OnDistancePhaseChangedEvent;
    public event Action<int> OnCountdownTick;               // Fires every second during countdown
    public event Action OnRaceStarted;
    public event Action<PlayerRef, int> OnPlayerFinished;   // PlayerRef, Rank
    public event Action OnRaceCompleted;
    public event Action OnRankingsUpdated;
    public event Action<float> OnTrackLengthSynced;         // For late joiners
    #endregion

    #region Private Fields
    private Dictionary<PlayerRef, NetworkObject> _playerObjects = new Dictionary<PlayerRef, NetworkObject>();
    private Dictionary<PlayerRef, int> _playerIndexMap = new Dictionary<PlayerRef, int>();  // O(1) lookup
    private Dictionary<PlayerRef, TrackSystem.PlayerTrackState> _playerTrackStates = new Dictionary<PlayerRef, TrackSystem.PlayerTrackState>();  // Per-player tracking state
    private int _lastCountdownSecond = -1;
    private DistancePhase _previousDistancePhase = DistancePhase.Phase1;
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
        Debug.Log($"[RaceManager] Spawned. HasStateAuthority: {HasStateAuthority}");

        if (HasStateAuthority && trackSystem != null)
        {
            TrackLength = trackSystem.TrackLength;
            Debug.Log($"[RaceManager] Track length set: {TrackLength:F2}");
        }

        // For late joiners - check if TrackLength is already set
        if (!HasStateAuthority && TrackLength > 0)
        {
            OnTrackLengthSynced?.Invoke(TrackLength);
            Debug.Log($"[RaceManager] Client received TrackLength: {TrackLength:F2}");
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;

        switch (CurrentPhase)
        {
            case RacePhase.Countdown:
                UpdateCountdown();
                break;
            case RacePhase.Racing:
                UpdateRace();
                break;
        }
    }
    #endregion

    #region Phase Management (Host Only)
    /// <summary>
    /// Initialize the race. Call this when minigame starts.
    /// </summary>
    public void InitializeRace()
    {
        if (!HasStateAuthority)
        {
            Debug.LogWarning("[RaceManager] Only Host can initialize race!");
            return;
        }

        // Reset all data
        RaceTime = 0f;
        FinishedPlayerCount = 0;
        CountdownTimer = countdownDuration;
        _lastCountdownSecond = -1;
        LeaderDistance = 0f;
        Winner = default;
        CurrentDistancePhase = DistancePhase.Phase1;
        _previousDistancePhase = DistancePhase.Phase1;

        // Clear player data
        for (int i = 0; i < PlayerDataArray.Length; i++)
        {
            PlayerDataArray.Set(i, default);
        }
        ActivePlayerCount = 0;
        _playerIndexMap.Clear();
        _playerObjects.Clear();
        _playerTrackStates.Clear();

        CurrentPhase = RacePhase.Waiting;
        Debug.Log("[RaceManager] Race initialized.");
    }

    /// <summary>
    /// Register a player for the race.
    /// </summary>
    public void RegisterPlayer(PlayerRef player, NetworkObject playerObject)
    {
        if (!HasStateAuthority) return;

        if (!_playerIndexMap.ContainsKey(player))
        {
            _playerObjects[player] = playerObject;

            // Add to networked array
            if (ActivePlayerCount < maxPlayers)
            {
                int index = ActivePlayerCount;
                var data = new RacePlayerData
                {
                    Player = player,
                    Distance = 0f,
                    Progress = 0f,
                    Rank = index + 1,
                    HasFinished = false,
                    FinishTime = 0f
                };
                PlayerDataArray.Set(index, data);
                _playerIndexMap[player] = index;  // O(1) lookup
                _playerTrackStates[player] = new TrackSystem.PlayerTrackState();  // Create track state
                ActivePlayerCount++;

                Debug.Log($"[RaceManager] Player {player} registered at index {index}. Total players: {ActivePlayerCount}");
            }
        }
    }

    /// <summary>
    /// Unregister a player from the race.
    /// </summary>
    public void UnregisterPlayer(PlayerRef player)
    {
        if (!HasStateAuthority) return;

        if (_playerIndexMap.TryGetValue(player, out int index))
        {
            _playerObjects.Remove(player);
            _playerIndexMap.Remove(player);
            _playerTrackStates.Remove(player);
            
            // Shift remaining players down
            for (int i = index; i < ActivePlayerCount - 1; i++)
            {
                var nextData = PlayerDataArray[i + 1];
                PlayerDataArray.Set(i, nextData);
                _playerIndexMap[nextData.Player] = i;
            }
            ActivePlayerCount--;

            Debug.Log($"[RaceManager] Player {player} unregistered. Remaining players: {ActivePlayerCount}");
        }
    }

    /// <summary>
    /// Start the countdown phase.
    /// </summary>
    public void StartCountdown()
    {
        if (!HasStateAuthority)
        {
            Debug.LogWarning("[RaceManager] Only Host can start countdown!");
            return;
        }

        if (CurrentPhase != RacePhase.Waiting)
        {
            Debug.LogWarning("[RaceManager] Cannot start countdown - not in Waiting phase!");
            return;
        }

        CountdownTimer = countdownDuration;
        _lastCountdownSecond = Mathf.CeilToInt(countdownDuration);
        CurrentPhase = RacePhase.Countdown;
        Debug.Log("[RaceManager] Countdown started!");
    }

    /// <summary>
    /// Start the race immediately (skip countdown).
    /// </summary>
    public void StartRace()
    {
        if (!HasStateAuthority)
        {
            Debug.LogWarning("[RaceManager] Only Host can start race!");
            return;
        }

        RaceTime = 0f;
        CurrentPhase = RacePhase.Racing;
        Debug.Log("[RaceManager] Race started!");
    }

    /// <summary>
    /// End the race.
    /// </summary>
    public void EndRace()
    {
        if (!HasStateAuthority) return;

        CurrentPhase = RacePhase.Finished;
        Debug.Log($"[RaceManager] Race finished! Total time: {RaceTime:F2}s, Winner: {Winner}");
    }
    #endregion

    #region Update Methods (Host Only)
    private void UpdateCountdown()
    {
        CountdownTimer -= Runner.DeltaTime;

        int currentSecond = Mathf.CeilToInt(CountdownTimer);
        if (currentSecond != _lastCountdownSecond && currentSecond > 0)
        {
            _lastCountdownSecond = currentSecond;
            // Countdown tick will be triggered by OnCountdownChanged
        }

        if (CountdownTimer <= 0f)
        {
            CountdownTimer = 0f;
            StartRace();
        }
    }

    private void UpdateRace()
    {
        RaceTime += Runner.DeltaTime;

        // 1. Update player distances first
        UpdatePlayerDistances();

        // 2. Update rankings based on new distances
        UpdateRankings();

        // 3. Check for finish (after rankings are updated)
        CheckForFinish();

        // 4. Update distance phase based on leader
        UpdateDistancePhase();
    }

    private void UpdatePlayerDistances()
    {
        float maxDistance = 0f;

        foreach (var kvp in _playerObjects)
        {
            PlayerRef player = kvp.Key;
            NetworkObject playerObj = kvp.Value;

            if (playerObj == null) continue;

            if (!_playerIndexMap.TryGetValue(player, out int index)) continue;
            if (!_playerTrackStates.TryGetValue(player, out var trackState)) continue;

            var data = PlayerDataArray[index];
            if (data.HasFinished) continue;

            // Calculate distance using optimized TrackSystem (with anti-cheat)
            float newDistance = trackSystem.GetPlayerDistanceOptimized(playerObj.transform.position, trackState);
            float newProgress = trackSystem.GetPlayerProgressOptimized(playerObj.transform.position, trackState);

            // Check off-track status
            if (trackState.IsOffTrack)
            {
                // Optional: Handle off-track behavior (slow down, warning, etc.)
                // For now, we still update distance but it's validated by checkpoint system
            }

            data.Distance = newDistance;
            data.Progress = newProgress;
            PlayerDataArray.Set(index, data);

            // Track leader distance
            if (newDistance > maxDistance)
            {
                maxDistance = newDistance;
            }
        }

        LeaderDistance = maxDistance;
    }

    private void UpdateRankings()
    {
        // Get all player data and sort by distance (descending)
        List<(int index, RacePlayerData data)> players = new List<(int, RacePlayerData)>();

        for (int i = 0; i < ActivePlayerCount; i++)
        {
            players.Add((i, PlayerDataArray[i]));
        }

        // Sort by: finished players first (by finish time), then by distance (descending)
        players = players
            .OrderByDescending(p => p.data.HasFinished)
            .ThenBy(p => p.data.HasFinished ? p.data.FinishTime : float.MaxValue)
            .ThenByDescending(p => p.data.Distance)
            .ToList();

        // Assign ranks
        bool rankChanged = false;
        for (int rank = 0; rank < players.Count; rank++)
        {
            var (index, data) = players[rank];
            int newRank = rank + 1;
            if (data.Rank != newRank)
            {
                rankChanged = true;
                data.Rank = newRank;
                PlayerDataArray.Set(index, data);
            }
        }

        if (rankChanged)
        {
            RPC_NotifyRankingsUpdated();
        }
    }

    private void CheckForFinish()
    {
        for (int i = 0; i < ActivePlayerCount; i++)
        {
            var data = PlayerDataArray[i];
            if (data.HasFinished) continue;

            // Check if player crossed finish line
            if (data.Progress >= 1f)
            {
                // Calculate correct rank at finish time
                int finishRank = CalculateFinishRank();

                data.HasFinished = true;
                data.FinishTime = RaceTime;
                data.Rank = finishRank;
                PlayerDataArray.Set(i, data);
                FinishedPlayerCount++;

                // Set winner (first to finish)
                if (FinishedPlayerCount == 1)
                {
                    Winner = data.Player;
                }

                Debug.Log($"[RaceManager] Player {data.Player} finished at rank {finishRank}! Time: {RaceTime:F2}s");
                RPC_NotifyPlayerFinished(data.Player, finishRank);

                // End race on first finish if configured
                if (endRaceOnFirstFinish)
                {
                    EndRace();
                    return;
                }
            }
        }

        // Also end if all players finished (fallback)
        if (!endRaceOnFirstFinish && FinishedPlayerCount >= ActivePlayerCount && ActivePlayerCount > 0)
        {
            EndRace();
        }
    }

    /// <summary>
    /// Calculate the finish rank based on already finished players.
    /// </summary>
    private int CalculateFinishRank()
    {
        return FinishedPlayerCount + 1;  // Next available rank
    }

    private void UpdateDistancePhase()
    {
        DistancePhase newPhase;

        if (LeaderDistance < phase1End)
        {
            newPhase = DistancePhase.Phase1;
        }
        else if (LeaderDistance < phase2End)
        {
            newPhase = DistancePhase.Phase2;
        }
        else
        {
            newPhase = DistancePhase.Phase3;
        }

        if (newPhase != CurrentDistancePhase)
        {
            CurrentDistancePhase = newPhase;
        }
    }
    #endregion

    #region Public Getters
    /// <summary>
    /// Get player's current distance.
    /// </summary>
    public float GetPlayerDistance(PlayerRef player)
    {
        if (_playerIndexMap.TryGetValue(player, out int index))
        {
            return PlayerDataArray[index].Distance;
        }
        return 0f;
    }

    /// <summary>
    /// Get player's progress (0-1).
    /// </summary>
    public float GetPlayerProgress(PlayerRef player)
    {
        if (_playerIndexMap.TryGetValue(player, out int index))
        {
            return PlayerDataArray[index].Progress;
        }
        return 0f;
    }

    /// <summary>
    /// Get player's current rank.
    /// </summary>
    public int GetPlayerRank(PlayerRef player)
    {
        if (_playerIndexMap.TryGetValue(player, out int index))
        {
            return PlayerDataArray[index].Rank;
        }
        return 0;
    }

    /// <summary>
    /// Check if player has finished.
    /// </summary>
    public bool HasPlayerFinished(PlayerRef player)
    {
        if (_playerIndexMap.TryGetValue(player, out int index))
        {
            return PlayerDataArray[index].HasFinished;
        }
        return false;
    }

    /// <summary>
    /// Get player's finish time.
    /// </summary>
    public float GetPlayerFinishTime(PlayerRef player)
    {
        if (_playerIndexMap.TryGetValue(player, out int index))
        {
            return PlayerDataArray[index].FinishTime;
        }
        return 0f;
    }

    /// <summary>
    /// Get all player data for UI display.
    /// </summary>
    public RacePlayerData[] GetAllPlayerData()
    {
        RacePlayerData[] result = new RacePlayerData[ActivePlayerCount];
        for (int i = 0; i < ActivePlayerCount; i++)
        {
            result[i] = PlayerDataArray[i];
        }
        return result;
    }

    /// <summary>
    /// Get rankings sorted by rank.
    /// </summary>
    public RacePlayerData[] GetRankings()
    {
        return GetAllPlayerData().OrderBy(p => p.Rank).ToArray();
    }

    /// <summary>
    /// Get player data by PlayerRef.
    /// </summary>
    public bool TryGetPlayerData(PlayerRef player, out RacePlayerData data)
    {
        if (_playerIndexMap.TryGetValue(player, out int index))
        {
            data = PlayerDataArray[index];
            return true;
        }
        data = default;
        return false;
    }

    /// <summary>
    /// Get the current distance phase thresholds.
    /// </summary>
    public (float phase1End, float phase2End, float phase3End) GetPhaseThresholds()
    {
        return (phase1End, phase2End, phase3End);
    }

    /// <summary>
    /// Check if player is registered.
    /// </summary>
    public bool IsPlayerRegistered(PlayerRef player)
    {
        return _playerIndexMap.ContainsKey(player);
    }

    /// <summary>
    /// Check if player is off-track (outside track boundaries).
    /// </summary>
    public bool IsPlayerOffTrack(PlayerRef player)
    {
        if (_playerTrackStates.TryGetValue(player, out var state))
        {
            return state.IsOffTrack;
        }
        return false;
    }

    /// <summary>
    /// Get player's off-track distance (0 if on track).
    /// </summary>
    public float GetPlayerOffTrackDistance(PlayerRef player)
    {
        if (_playerTrackStates.TryGetValue(player, out var state))
        {
            return trackSystem.GetOffTrackDistance(state);
        }
        return 0f;
    }

    /// <summary>
    /// Get player's current checkpoint index.
    /// </summary>
    public int GetPlayerCheckpoint(PlayerRef player)
    {
        if (_playerTrackStates.TryGetValue(player, out var state))
        {
            return state.LastCheckpoint;
        }
        return 0;
    }

    /// <summary>
    /// Get player's lap count (for loop tracks).
    /// </summary>
    public int GetPlayerLapCount(PlayerRef player)
    {
        if (_playerTrackStates.TryGetValue(player, out var state))
        {
            return state.LapCount;
        }
        return 0;
    }
    #endregion

    #region Callbacks
    private void OnPhaseChanged()
    {
        Debug.Log($"[RaceManager] Phase changed to: {CurrentPhase}");

        if (CurrentPhase == RacePhase.Racing)
        {
            OnRaceStarted?.Invoke();
        }
        else if (CurrentPhase == RacePhase.Finished)
        {
            OnRaceCompleted?.Invoke();
        }
    }

    private void OnDistancePhaseChanged()
    {
        Debug.Log($"[RaceManager] Distance phase changed to: {CurrentDistancePhase}");
        OnDistancePhaseChangedEvent?.Invoke(_previousDistancePhase, CurrentDistancePhase);
        _previousDistancePhase = CurrentDistancePhase;
    }

    private void OnCountdownChanged()
    {
        int currentSecond = Mathf.CeilToInt(CountdownTimer);
        if (currentSecond > 0 && currentSecond <= countdownDuration)
        {
            OnCountdownTick?.Invoke(currentSecond);
        }
    }

    private void OnTrackLengthChanged()
    {
        Debug.Log($"[RaceManager] TrackLength synced: {TrackLength:F2}");
        OnTrackLengthSynced?.Invoke(TrackLength);
    }
    #endregion

    #region RPCs
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_NotifyPlayerFinished(PlayerRef player, int rank)
    {
        OnPlayerFinished?.Invoke(player, rank);
        Debug.Log($"[RaceManager] RPC: Player {player} finished at rank {rank}!");
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_NotifyRankingsUpdated()
    {
        OnRankingsUpdated?.Invoke();
    }
    #endregion

    #region Debug
    private void OnGUI()
    {
        if (!debugMode) return;

        GUILayout.BeginArea(new Rect(10, 10, 400, 600));
        GUILayout.Label($"=== Race Manager Debug ===");
        GUILayout.Label($"Phase: {CurrentPhase}");
        GUILayout.Label($"Distance Phase: {CurrentDistancePhase}");
        GUILayout.Label($"Track Length: {TrackLength:F2}");
        GUILayout.Label($"Leader Distance: {LeaderDistance:F2}");
        GUILayout.Label($"Race Time: {RaceTime:F2}s");
        GUILayout.Label($"Countdown: {CountdownTimer:F1}s");
        GUILayout.Label($"Players: {ActivePlayerCount}");
        GUILayout.Label($"Finished: {FinishedPlayerCount}");
        GUILayout.Label($"Winner: {(Winner != default ? Winner.ToString() : "None")}");

        GUILayout.Space(5);
        var thresholds = GetPhaseThresholds();
        GUILayout.Label($"Phase1: 0 - {thresholds.phase1End}");
        GUILayout.Label($"Phase2: {thresholds.phase1End} - {thresholds.phase2End}");
        GUILayout.Label($"Phase3: {thresholds.phase2End} - {thresholds.phase3End}");

        GUILayout.Space(10);
        GUILayout.Label("--- Player Details ---");
        foreach (var kvp in _playerTrackStates)
        {
            PlayerRef player = kvp.Key;
            var state = kvp.Value;
            
            if (!TryGetPlayerData(player, out var data)) continue;

            string finishStatus = data.HasFinished ? $"DONE ({data.FinishTime:F2}s)" : $"{data.Progress * 100:F1}%";
            string offTrackStatus = state.IsOffTrack ? $" [OFF-TRACK: {state.OffTrackDistance:F1}]" : "";
            
            GUILayout.Label($"#{data.Rank} P{player}: {data.Distance:F1}m | CP{state.LastCheckpoint} | Lap{state.LapCount}{offTrackStatus}");
            GUILayout.Label($"    {finishStatus}");
        }

        GUILayout.EndArea();
    }
    #endregion
}
