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
    [Header("UI Panels (Auto-found via UIPanel component)")]
    [SerializeField] private GameObject lobbyUI;
    [SerializeField] private GameObject votingUI;
    [SerializeField] private GameObject scoreboardUI;
    [SerializeField] private GameObject resultUI;
    #endregion

    #region Minigame Data
    [Header("Minigames")]
    [SerializeField] private MinigameData[] availableMinigames;
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

        // Tìm UI references bằng tag
        FindUIReferences();
        InitializeUIState();
    }

    /// <summary>
    /// Tìm UI references - dùng FindObjectsByType với Include Inactive để tìm cả inactive objects
    /// </summary>
    public void FindUIReferences()
    {
        // Tìm tất cả UIPanel kể cả inactive
        var panels = FindObjectsByType<UIPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        
        foreach (var panel in panels)
        {
            RegisterUIPanel(panel);
        }

        Debug.Log($"[GameManager] FindUIReferences - Lobby:{lobbyUI != null}, Voting:{votingUI != null}, Scoreboard:{scoreboardUI != null}, Result:{resultUI != null}");
    }
    
    /// <summary>
    /// Đăng ký UI Panel - được gọi bởi UIPanel component
    /// </summary>
    public void RegisterUIPanel(UIPanel panel)
    {
        if (panel == null) return;
        
        switch (panel.PanelType)
        {
            case UIPanelType.Lobby:
                lobbyUI = panel.gameObject;
                break;
            case UIPanelType.Voting:
                votingUI = panel.gameObject;
                break;
            case UIPanelType.Scoreboard:
                scoreboardUI = panel.gameObject;
                break;
            case UIPanelType.Result:
                resultUI = panel.gameObject;
                break;
        }
    }
    private void InitializeUIState()
    {
        // Đảm bảo luôn tìm lại UI trước khi set
        FindUIReferences();

        // Ẩn tất cả trước
        SetActiveUI(lobbyUI, false);
        SetActiveUI(votingUI, false);
        SetActiveUI(scoreboardUI, false);
        SetActiveUI(resultUI, false);

        // Chỉ bật Lobby
        SetActiveUI(lobbyUI, true);

        Debug.Log("[GameManager] Initialize UI: Only LobbyUI is active");
    }
    public bool AreAllPlayersReady()
    {
        var players = FindObjectsByType<PlayerNetworkData>(FindObjectsSortMode.None);

        // Không có player nào
        if (players.Length == 0)
            return false;

        // Cần tối thiểu 2 players để start (không cho phép solo)
        if (players.Length < 2)
            return false;

        int readyCount = 0;
        int clientCount = 0;

        // Check tất cả players NGOẠI TRỪ HOST
        // Dùng InputAuthority thay vì HasStateAuthority (vì Host là StateAuthority của mọi thứ trong Hosted mode)
        foreach (var p in players)
        {
            // Bỏ qua host (host không cần ready vì host là người start game)
            // Host's InputAuthority = Runner.LocalPlayer (trên máy host)
            if (p.Object.InputAuthority == Runner.LocalPlayer)
                continue;

            clientCount++;

            // Client đã ready
            if (p.IsReady)
                readyCount++;
        }

        bool allReady = clientCount > 0 && readyCount == clientCount;
        return allReady;
    }
    #endregion

    #region State Change Callback
    private void OnGameStateChanged()
    {
        Debug.Log($"[GameManager] State changed to: {CurrentState}");

        // Tìm lại UI references (vì có thể scene đã thay đổi)
        FindUIReferences();

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
        Debug.Log($"[GameManager] StartMinigame called with index: {minigameIndex}, HasStateAuthority: {HasStateAuthority}");
        
        if (!HasStateAuthority)
        {
            Debug.LogWarning("[GameManager] Only Host can call StartMinigame()");
            return;
        }

        if (availableMinigames == null)
        {
            Debug.LogError("[GameManager] availableMinigames is NULL!");
            return;
        }
        
        if (minigameIndex < 0 || minigameIndex >= availableMinigames.Length)
        {
            Debug.LogError($"[GameManager] Invalid minigame index: {minigameIndex}, availableMinigames.Length: {availableMinigames.Length}");
            return;
        }

        Debug.Log($"[GameManager] Starting minigame #{minigameIndex}: {availableMinigames[minigameIndex].minigameName}");
        CurrentMinigameIndex = minigameIndex;
        CurrentRound++;

        Debug.Log("[GameManager] Calling ChangeState(Playing)");
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
        
        // Ẩn player input trong voting phase
        if (PlayerInputHandler.Instance != null)
        {
            PlayerInputHandler.Instance.InputEnabled = false;
        }
        
        // Hiện cursor để vote
        if (CursorManager.Instance != null)
        {
            CursorManager.Instance.ShowCursor();
        }

        // Start voting (host only)
        if (HasStateAuthority && VotingManager.Instance != null)
        {
            Debug.Log("[GameManager] Host starting VotingManager.StartVoting()");
            VotingManager.Instance.StartVoting();
        }
        else if (VotingManager.Instance == null)
        {
            Debug.LogError("[GameManager] VotingManager.Instance is NULL!");
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

        // Ẩn tất cả UI
        SetActiveUI(lobbyUI, false);
        SetActiveUI(votingUI, false);
        SetActiveUI(scoreboardUI, false);
        SetActiveUI(resultUI, false);
        
        // Bật lại player input
        if (PlayerInputHandler.Instance != null)
        {
            PlayerInputHandler.Instance.InputEnabled = true;
        }
        
        // Ẩn cursor khi chơi
        if (CursorManager.Instance != null)
        {
            CursorManager.Instance.HideCursor();
        }

        if (!HasStateAuthority)
        {
            Debug.Log("[GameManager] Not host, skipping scene load");
            return;
        }

        // Lấy MinigameData
        Debug.Log($"[GameManager] availableMinigames: {(availableMinigames != null ? availableMinigames.Length.ToString() : "NULL")}, CurrentMinigameIndex: {CurrentMinigameIndex}");
        
        if (availableMinigames == null || CurrentMinigameIndex < 0 || CurrentMinigameIndex >= availableMinigames.Length)
        {
            Debug.LogError("[GameManager] No valid minigame data!");
            return;
        }

        var minigameData = availableMinigames[CurrentMinigameIndex];
        Debug.Log($"[GameManager] Loading minigame scene: {minigameData.sceneName}");

        // Notify về camera mode trước khi load scene
        RPC_SetupMinigameCamera(minigameData.useSharedCamera);

        // Load scene - Fusion sẽ tự động sync tất cả clients
        int sceneIndex = GetSceneIndex(minigameData.sceneName);
        Debug.Log($"[GameManager] Scene index for '{minigameData.sceneName}': {sceneIndex}");
        
        var sceneRef = SceneRef.FromIndex(sceneIndex);
        if (sceneRef.IsValid)
        {
            Debug.Log($"[GameManager] Loading scene via Runner.LoadScene...");
            Runner.LoadScene(sceneRef);
        }
        else
        {
            Debug.LogError($"[GameManager] Invalid scene: {minigameData.sceneName}");
        }
    }

    /// <summary>
    /// Lấy scene index từ tên scene (cần setup trong Build Settings)
    /// </summary>
    private int GetSceneIndex(string sceneName)
    {
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCountInBuildSettings; i++)
        {
            string path = UnityEngine.SceneManagement.SceneUtility.GetScenePathByBuildIndex(i);
            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            if (name == sceneName)
            {
                return i;
            }
        }
        Debug.LogWarning($"[GameManager] Scene '{sceneName}' not found in Build Settings!");
        return 1; // Fallback to index 1
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_SetupMinigameCamera(bool useSharedCamera)
    {
        // Minigame scene sẽ tự setup shared camera nếu cần
        // Thông báo cho CameraManager về mode
        Debug.Log($"[GameManager] Minigame camera mode: {(useSharedCamera ? "Shared" : "Player")}");

        if (!useSharedCamera && CameraManager.Instance != null)
        {
            // Nếu không dùng shared camera, đảm bảo player camera active
            CameraManager.Instance.SwitchToPlayerCamera();
        }
        // Nếu dùng shared camera, MinigameCamera component trong scene sẽ xử lý
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
            Debug.Log($"[GameManager] SetActiveUI: {uiObject.name} = {active}");
        }
        else
        {
            Debug.LogWarning("[GameManager] SetActiveUI: uiObject is NULL!");
        }
    }

    private void ResetAllPlayersReady()
    {
        var players = FindObjectsByType<PlayerNetworkData>(FindObjectsSortMode.None);
        foreach (var player in players)
        {
            // Note: This requires adding a ResetReady RPC to PlayerNetworkData
            // Or handling via networked property changes
        }
    }

    private void ChangeState(GameState newState)
    {
        Debug.Log($"[GameManager] ChangeState called: {newState}, HasStateAuthority: {HasStateAuthority}");
        
        if (!HasStateAuthority)
        {
            Debug.LogWarning("[GameManager] ChangeState rejected - not host");
            return;
        }

        var oldState = CurrentState;
        CurrentState = newState;

        Debug.Log($"[GameManager] State: {oldState} -> {newState}");
        OnStateChanged?.Invoke(oldState, newState);
    }
    #endregion

}
