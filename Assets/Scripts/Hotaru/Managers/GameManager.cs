using Fusion;
using UnityEngine;
using System;

/// <summary>
/// Game states for the main game flow.
/// </summary>
public enum GameState
{
    Lobby,      // Players waiting in lobby
    Voting,     // Players voting for minigame
    Tutorial,   // Showing tutorial/instructions
    Playing,    // Minigame in progress
    Scoreboard, // Showing scores after minigame
    Result      // Final results/winner announcement
}

/// <summary>
/// Central game flow manager. Host-authoritative using Photon Fusion.
/// All game state transitions are controlled by the Host and synced to all clients.
/// </summary>
public class GameManager : NetworkBehaviour
{
    #region Singleton
    public static GameManager Instance { get; private set; }
    #endregion

    #region Networked Properties
    /// <summary>
    /// Current game state - synced across all clients.
    /// Only Host can modify this value.
    /// </summary>
    [Networked, OnChangedRender(nameof(OnGameStateChanged))]
    public GameState CurrentState { get; private set; } = GameState.Lobby;

    /// <summary>
    /// Current round/minigame number.
    /// </summary>
    [Networked]
    public int CurrentRound { get; private set; } = 0;

    /// <summary>
    /// Total rounds to play.
    /// </summary>
    [Networked]
    public int TotalRounds { get; private set; } = 3;

    /// <summary>
    /// Index of current minigame being played.
    /// </summary>
    [Networked]
    public int CurrentMinigameIndex { get; private set; } = -1;
    #endregion

    #region Events
    /// <summary>
    /// Fired when game state changes. Subscribe to react to state transitions.
    /// </summary>
    public event Action<GameState, GameState> OnStateChanged;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        // Singleton setup
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public override void Spawned()
    {
        // Called when NetworkObject is spawned
        Debug.Log($"[GameManager] Spawned. IsHost: {HasStateAuthority}");
    }
    #endregion

    #region State Change Callback
    /// <summary>
    /// Called on all clients when CurrentState changes.
    /// </summary>
    private void OnGameStateChanged()
    {
        Debug.Log($"[GameManager] State changed to: {CurrentState}");
        
        // Handle state-specific logic for all clients
        switch (CurrentState)
        {
            case GameState.Lobby:
                HandleLobbyState();
                break;
            case GameState.Voting:
                HandleVotingState();
                break;
            case GameState.Tutorial:
                HandleTutorialState();
                break;
            case GameState.Playing:
                HandlePlayingState();
                break;
            case GameState.Scoreboard:
                HandleScoreboardState();
                break;
            case GameState.Result:
                HandleResultState();
                break;
        }
    }
    #endregion

    #region Host-Only Game Flow Methods
    /// <summary>
    /// Start a new match. Called by Host only.
    /// Resets round counter and transitions to Voting state.
    /// </summary>
    public void StartMatch()
    {
        if (!HasStateAuthority)
        {
            Debug.LogWarning("[GameManager] Only Host can call StartMatch()");
            return;
        }

        Debug.Log("[GameManager] Starting match...");
        CurrentRound = 0;
        ChangeState(GameState.Voting);
    }

    /// <summary>
    /// Start voting phase for next minigame. Called by Host only.
    /// </summary>
    public void StartVoting()
    {
        if (!HasStateAuthority)
        {
            Debug.LogWarning("[GameManager] Only Host can call StartVoting()");
            return;
        }

        Debug.Log("[GameManager] Starting voting phase...");
        ChangeState(GameState.Voting);
    }

    /// <summary>
    /// Start the minigame. Called by Host after voting/tutorial.
    /// </summary>
    /// <param name="minigameIndex">Index of the minigame to play.</param>
    public void StartMinigame(int minigameIndex)
    {
        if (!HasStateAuthority)
        {
            Debug.LogWarning("[GameManager] Only Host can call StartMinigame()");
            return;
        }

        Debug.Log($"[GameManager] Starting minigame #{minigameIndex}...");
        CurrentMinigameIndex = minigameIndex;
        CurrentRound++;

        // TODO: Load minigame scene or activate minigame
        // For now, go directly to Playing state
        // You may want to show Tutorial first
        ChangeState(GameState.Playing);
    }

    /// <summary>
    /// End the current minigame. Called by Host when minigame is complete.
    /// </summary>
    public void EndMinigame()
    {
        if (!HasStateAuthority)
        {
            Debug.LogWarning("[GameManager] Only Host can call EndMinigame()");
            return;
        }

        Debug.Log("[GameManager] Ending minigame...");

        // TODO: Calculate scores, update player stats

        // Show scoreboard after minigame ends
        ChangeState(GameState.Scoreboard);
    }

    /// <summary>
    /// Show the scoreboard. Called by Host.
    /// </summary>
    public void ShowScoreboard()
    {
        if (!HasStateAuthority)
        {
            Debug.LogWarning("[GameManager] Only Host can call ShowScoreboard()");
            return;
        }

        Debug.Log("[GameManager] Showing scoreboard...");
        ChangeState(GameState.Scoreboard);
    }

    /// <summary>
    /// Called after scoreboard to proceed to next round or final results.
    /// Host only.
    /// </summary>
    public void ProceedFromScoreboard()
    {
        if (!HasStateAuthority)
        {
            Debug.LogWarning("[GameManager] Only Host can call ProceedFromScoreboard()");
            return;
        }

        if (CurrentRound >= TotalRounds)
        {
            // All rounds completed, show final results
            Debug.Log("[GameManager] All rounds complete. Showing final results...");
            ChangeState(GameState.Result);
        }
        else
        {
            // More rounds to play, start voting for next minigame
            Debug.Log("[GameManager] Proceeding to next round voting...");
            ChangeState(GameState.Voting);
        }
    }

    /// <summary>
    /// Return to lobby after match ends. Host only.
    /// </summary>
    public void ReturnToLobby()
    {
        if (!HasStateAuthority)
        {
            Debug.LogWarning("[GameManager] Only Host can call ReturnToLobby()");
            return;
        }

        Debug.Log("[GameManager] Returning to lobby...");
        CurrentRound = 0;
        CurrentMinigameIndex = -1;
        ChangeState(GameState.Lobby);
    }
    #endregion

    #region State Handlers (Override in subclass or extend)
    /// <summary>
    /// Handle lobby state entry. Override or extend as needed.
    /// </summary>
    protected virtual void HandleLobbyState()
    {
        // TODO: Show lobby UI, enable ready buttons, etc.
        Debug.Log("[GameManager] Entered Lobby state");
    }

    /// <summary>
    /// Handle voting state entry. Override or extend as needed.
    /// </summary>
    protected virtual void HandleVotingState()
    {
        // TODO: Show voting UI, start vote timer
        Debug.Log("[GameManager] Entered Voting state");
    }

    /// <summary>
    /// Handle tutorial state entry. Override or extend as needed.
    /// </summary>
    protected virtual void HandleTutorialState()
    {
        // TODO: Show tutorial/instructions for current minigame
        Debug.Log("[GameManager] Entered Tutorial state");
    }

    /// <summary>
    /// Handle playing state entry. Override or extend as needed.
    /// </summary>
    protected virtual void HandlePlayingState()
    {
        // TODO: Start the actual minigame gameplay
        Debug.Log("[GameManager] Entered Playing state");
    }

    /// <summary>
    /// Handle scoreboard state entry. Override or extend as needed.
    /// </summary>
    protected virtual void HandleScoreboardState()
    {
        // TODO: Display scoreboard UI with current standings
        Debug.Log("[GameManager] Entered Scoreboard state");
    }

    /// <summary>
    /// Handle result state entry. Override or extend as needed.
    /// </summary>
    protected virtual void HandleResultState()
    {
        // TODO: Show final results, winner announcement
        Debug.Log("[GameManager] Entered Result state");
    }
    #endregion

    #region Private Helpers
    /// <summary>
    /// Change state and fire events. Host only.
    /// </summary>
    private void ChangeState(GameState newState)
    {
        if (!HasStateAuthority) return;

        var oldState = CurrentState;
        CurrentState = newState;

        Debug.Log($"[GameManager] State: {oldState} -> {newState}");
        OnStateChanged?.Invoke(oldState, newState);
    }
    #endregion

    #region Debug/Testing
    /// <summary>
    /// Force a state change. For testing only.
    /// </summary>
    [ContextMenu("Debug: Start Match")]
    private void DebugStartMatch() => StartMatch();

    [ContextMenu("Debug: Start Minigame 0")]
    private void DebugStartMinigame() => StartMinigame(0);

    [ContextMenu("Debug: End Minigame")]
    private void DebugEndMinigame() => EndMinigame();
    #endregion
}
