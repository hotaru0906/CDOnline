using Fusion;
using UnityEngine;
using System;

public enum GameState
{
    Lobby,
    Voting,
    Tutorial,
    Playing,
    Scoreboard,
    Result
}

public class GameManager : NetworkBehaviour
{
    #region Singleton
    public static GameManager Instance { get; private set; }
    #endregion
    public bool IsHost => HasStateAuthority;

    #region UI References
    [Header("UI Panels")]
    [SerializeField] private GameObject lobbyUI;
    [SerializeField] private GameObject votingUI;
    [SerializeField] private GameObject scoreboardUI;
    [SerializeField] private GameObject resultUI;
    #endregion

    #region Networked Properties
    [Networked, OnChangedRender(nameof(OnGameStateChanged))]
    public GameState CurrentState { get; private set; } = GameState.Lobby;

    [Networked]
    public int CurrentRound { get; private set; } = 0;

    [Networked]
    public int TotalRounds { get; private set; } = 3;

    [Networked]
    public int CurrentMinigameIndex { get; private set; } = -1;
    #endregion

    #region Events
    public event Action<GameState, GameState> OnStateChanged;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public override void Spawned()
    {
        // Called when NetworkObject is spawned
        Debug.Log($"[GameManager] Spawned. IsHost: {HasStateAuthority}");
        
        // Find UI references in current scene
        FindUIReferences();
        
        // Apply initial state UI (since OnChangedRender won't trigger on spawn)
        OnGameStateChanged();
    }

    /// <summary>
    /// Find UI references dynamically. Call this after scene load.
    /// </summary>
    public void FindUIReferences()
    {
        // Find by tag or name - adjust these to match your UI naming convention
        if (lobbyUI == null)
            lobbyUI = GameObject.FindWithTag("LobbyUI") ?? GameObject.Find("LobbyUI");
        if (votingUI == null)
            votingUI = GameObject.FindWithTag("VotingUI") ?? GameObject.Find("VotingUI");
        if (scoreboardUI == null)
            scoreboardUI = GameObject.FindWithTag("ScoreboardUI") ?? GameObject.Find("ScoreboardUI");
        if (resultUI == null)
            resultUI = GameObject.FindWithTag("ResultUI") ?? GameObject.Find("ResultUI");
    }

    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        Debug.Log($"[GameManager] Scene loaded: {scene.name}");
        FindUIReferences();
        
        // Re-apply current state UI
        OnGameStateChanged();
    }

    private void OnEnable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
    }
    public bool AreAllPlayersReady()
    {
        var players = FindObjectsByType<PlayerNetworkData>(FindObjectsSortMode.None);

        if (players.Length == 0)
            return false;

        // Need at least 2 players (host + 1 client)
        if (players.Length < 2)
            return false;

        // Get host's local player ref
        var hostPlayerRef = Runner != null ? Runner.LocalPlayer : default;

        foreach (var p in players)
        {
            if (p.Object == null) continue;

            // Skip host's own player - host doesn't need to ready up
            // Check InputAuthority to identify which player belongs to the host
            if (p.Object.InputAuthority == hostPlayerRef)
                continue;

            // All non-host players must be ready
            if (!p.IsReady)
                return false;
        }

        return true;
    }
    #endregion

    #region State Change Callback
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
    protected virtual void HandleLobbyState()
    {
        Debug.Log("[GameManager] Entered Lobby state");

        // Show lobby UI, hide others
        SetActiveUI(lobbyUI, true);
        SetActiveUI(votingUI, false);
        SetActiveUI(scoreboardUI, false);
        SetActiveUI(resultUI, false);

        // Reset player ready states (host only)
        if (HasStateAuthority)
        {
            ResetAllPlayersReady();
        }
    }

    protected virtual void HandleVotingState()
    {
        Debug.Log("[GameManager] Entered Voting state");

        // Show voting UI, hide others
        SetActiveUI(lobbyUI, false);
        SetActiveUI(votingUI, true);
        SetActiveUI(scoreboardUI, false);
        SetActiveUI(resultUI, false);

        // Start voting (host only)
        if (HasStateAuthority && VotingManager.Instance != null)
        {
            VotingManager.Instance.StartVoting();
        }
    }

    protected virtual void HandleTutorialState()
    {
        // TODO: Show tutorial/instructions for current minigame
        Debug.Log("[GameManager] Entered Tutorial state");
    }

    protected virtual void HandlePlayingState()
    {
        Debug.Log("[GameManager] Entered Playing state");

        if (!HasStateAuthority) return;

        Debug.Log("[GameManager] Loading Minigame Scene...");

        Runner.LoadScene(SceneRef.FromIndex(1));
    }
    protected virtual void HandleScoreboardState()
    {
        Debug.Log("[GameManager] Entered Scoreboard state");

        SetActiveUI(lobbyUI, false);
        SetActiveUI(votingUI, false);
        SetActiveUI(scoreboardUI, true);
        SetActiveUI(resultUI, false);
    }

    protected virtual void HandleResultState()
    {
        Debug.Log("[GameManager] Entered Result state");

        SetActiveUI(lobbyUI, false);
        SetActiveUI(votingUI, false);
        SetActiveUI(scoreboardUI, false);
        SetActiveUI(resultUI, true);
    }
    #endregion

    #region Private Helpers
    private void SetActiveUI(GameObject uiObject, bool active)
    {
        if (uiObject != null)
        {
            uiObject.SetActive(active);
        }
    }

    private void ResetAllPlayersReady()
    {
        if (!HasStateAuthority) return;

        var players = FindObjectsByType<PlayerNetworkData>(FindObjectsSortMode.None);
        foreach (var player in players)
        {
            // Host has StateAuthority over all NetworkObjects, so can directly modify
            if (player != null)
            {
                player.IsReady = false;
            }
        }
        Debug.Log($"[GameManager] Reset {players.Length} players ready state");
    }

    private void ChangeState(GameState newState)
    {
        if (!HasStateAuthority) return;

        var oldState = CurrentState;
        CurrentState = newState;

        Debug.Log($"[GameManager] State: {oldState} -> {newState}");
        OnStateChanged?.Invoke(oldState, newState);
    }
    #endregion

}
