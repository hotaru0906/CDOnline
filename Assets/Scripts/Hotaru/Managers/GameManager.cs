using Fusion;
using UnityEngine;
using System;
using System.Collections;

public enum GameState
{
    Lobby,
    Voting,          // Vote chọn minigame hoặc Roulette
    Tutorial,
    Playing,         // Đang chơi minigame
    Scoreboard,
    Roulette,        // Đang chơi Cò Quay Nga
    Result           // Kết quả cuối cùng (còn 1 người)
}

/// <summary>
/// Loại voting hiện tại
/// </summary>
public enum VotingType
{
    MinigameOnly,    // Chỉ vote minigame (lần đầu)
    RouletteOrMinigame // Vote giữa Roulette và Minigame
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
    [SerializeField] private GameObject votingUI;           // UI vote chọn minigame (MinigameOnly)
    [SerializeField] private GameObject rouletteVotingUI;   // UI vote Roulette/Minigame (RouletteOrMinigame)
    [SerializeField] private GameObject scoreboardUI;
    [SerializeField] private GameObject resultUI;

    [Header("Minigame UI (Main UI - dùng chung)")]
    [SerializeField] private GameObject minigameCountdownUI;  // Countdown UI chính (dùng chung)
    [SerializeField] private TMPro.TMP_Text countdownText;    // Text hiển thị countdown

    [Header("Minigame Scene UI (Tìm khi scene load)")]
    [SerializeField] private GameObject minigameTutorialUI;   // Tutorial trong minigame scene (mỗi scene khác nhau)
    private UnityEngine.UI.Button _tutorialStartButton;       // Button Start trong tutorial (host only)

    [Header("Countdown Settings")]
    [SerializeField] private float countdownTime = 3f;        // Thời gian countdown

    [Header("Scoreboard Settings")]
    [SerializeField] private float scoreboardDisplayDuration = 3f; // Thời gian hiển thị scoreboard trước khi chuyển sang Voting
    private Coroutine _scoreboardCoroutine;
    private Coroutine _countdownCoroutine;
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

    /// <summary>
    /// Loại voting hiện tại
    /// </summary>
    [Networked]
    public VotingType CurrentVotingType { get; private set; } = VotingType.MinigameOnly;

    /// <summary>
    /// PlayerId của người cuối cùng còn sống (winner)
    /// </summary>
    [Networked]
    public int FinalWinnerId { get; private set; } = -1;

    #region Synced Minigame Settings (từ MinigameData, sync cho tất cả clients)
    [Networked] public NetworkBool MG_CanMove { get; private set; } = true;
    [Networked] public NetworkBool MG_CanJump { get; private set; } = true;
    [Networked] public NetworkBool MG_CanCrouch { get; private set; } = true;
    [Networked] public NetworkBool MG_CanAttack { get; private set; } = true;
    [Networked] public NetworkBool MG_CanRun { get; private set; } = true;
    [Networked] public NetworkBool MG_AllowRespawn { get; private set; } = true;
    #endregion
    #endregion

    #region Public Properties

    public MinigameData CurrentMinigameData
    {
        get
        {
            if (CurrentMinigameIndex < 0)
                return null;

            // Ưu tiên từ MinigameVotingManager
            if (MinigameVotingManager.Instance != null && MinigameVotingManager.Instance.IsReady)
            {
                var data = MinigameVotingManager.Instance.GetMinigameByAvailableIndex(CurrentMinigameIndex);
                if (data != null) return data;
            }

            // Fallback về availableMinigames
            if (availableMinigames != null && CurrentMinigameIndex < availableMinigames.Length)
            {
                return availableMinigames[CurrentMinigameIndex];
            }

            return null;
        }
    }
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
        // Không dùng DontDestroyOnLoad cho NetworkBehaviour
        // NetworkRunner quản lý lifecycle của NetworkObject
        // VotingManager, MinigameVotingManager nên là CHILD của GameManager prefab
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
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

        Debug.Log($"[GameManager] FindUIReferences - Lobby:{lobbyUI != null}, Voting:{votingUI != null}, RouletteVoting:{rouletteVotingUI != null}, Scoreboard:{scoreboardUI != null}, Result:{resultUI != null}, MGTutorial:{minigameTutorialUI != null}, MGCountdown:{minigameCountdownUI != null}");
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
            case UIPanelType.RouletteVoting:
                rouletteVotingUI = panel.gameObject;
                break;
            case UIPanelType.Scoreboard:
                scoreboardUI = panel.gameObject;
                break;
            case UIPanelType.Result:
                resultUI = panel.gameObject;
                break;
            case UIPanelType.MinigameTutorial:
                minigameTutorialUI = panel.gameObject;
                SetupTutorialStartButton();
                break;
            case UIPanelType.MinigameCountdown:
                minigameCountdownUI = panel.gameObject;
                // Tìm TMP_Text trong countdown panel (tìm theo tag hoặc tên "CountdownText")
                if (countdownText == null)
                {
                    var texts = panel.GetComponentsInChildren<TMPro.TMP_Text>(true);
                    foreach (var txt in texts)
                    {
                        if (txt.CompareTag("CountdownText") || txt.name.ToLower().Contains("countdown"))
                        {
                            countdownText = txt;
                            Debug.Log($"[GameManager] Found countdown text: {txt.name}");
                            break;
                        }
                    }
                    // Fallback: lấy TMP_Text đầu tiên nếu không tìm thấy
                    if (countdownText == null && texts.Length > 0)
                    {
                        countdownText = texts[0];
                        Debug.Log($"[GameManager] Using first TMP_Text as countdown: {countdownText.name}");
                    }
                }
                break;
        }
    }

    /// <summary>
    /// Tìm và setup Start button trong Tutorial panel
    /// </summary>
    private void SetupTutorialStartButton()
    {
        if (minigameTutorialUI == null) return;

        // Tìm button có tag "MinigameStartButton" hoặc component MinigameStartButton
        var buttons = minigameTutorialUI.GetComponentsInChildren<UnityEngine.UI.Button>(true);
        foreach (var btn in buttons)
        {
            if (btn.CompareTag("MinigameStartButton") || btn.name.ToLower().Contains("start"))
            {
                _tutorialStartButton = btn;
                _tutorialStartButton.onClick.RemoveAllListeners();
                _tutorialStartButton.onClick.AddListener(OnTutorialStartButtonClicked);

                // Chỉ host mới thấy button
                _tutorialStartButton.gameObject.SetActive(HasStateAuthority);
                Debug.Log($"[GameManager] Found and setup tutorial start button: {btn.name}");
                break;
            }
        }
    }

    /// <summary>
    /// Gọi khi Host nhấn nút Start trong Tutorial
    /// </summary>
    private void OnTutorialStartButtonClicked()
    {
        if (!HasStateAuthority)
        {
            Debug.LogWarning("[GameManager] Only host can start minigame");
            return;
        }

        if (CurrentState != GameState.Tutorial)
        {
            Debug.LogWarning($"[GameManager] Cannot start, current state: {CurrentState}");
            return;
        }

        Debug.Log("[GameManager] Host clicked Start - hiding tutorial and starting countdown");

        // Bắt đầu countdown (sync to all clients)
        RPC_StartCountdown();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_StartCountdown()
    {
        // Ẩn tutorial, hiện countdown
        SetActiveUI(minigameTutorialUI, false);
        SetActiveUI(minigameCountdownUI, true);

        // Báo MinigameController chuyển phase
        if (MinigameController.Instance != null)
        {
            MinigameController.Instance.OnCountdownStarted();
        }

        // Host chạy countdown coroutine
        if (HasStateAuthority)
        {
            if (_countdownCoroutine != null)
            {
                StopCoroutine(_countdownCoroutine);
            }
            _countdownCoroutine = StartCoroutine(RunCountdown());
        }
    }

    private IEnumerator RunCountdown()
    {
        float remaining = countdownTime;

        while (remaining > 0)
        {
            // Update UI for all clients
            RPC_UpdateCountdownUI(Mathf.CeilToInt(remaining));

            yield return new WaitForSeconds(1f);
            remaining -= 1f;
        }

        // Hiện "GO!"
        RPC_UpdateCountdownUI(0);

        yield return new WaitForSeconds(0.5f);

        // Countdown xong -> chuyển sang Playing
        RPC_CountdownComplete();

        _countdownCoroutine = null;
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_UpdateCountdownUI(int count)
    {
        if (countdownText != null)
        {
            countdownText.text = count > 0 ? count.ToString() : "GO!";
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_CountdownComplete()
    {
        // Ẩn countdown UI
        SetActiveUI(minigameCountdownUI, false);

        // Host chuyển state sang Playing
        if (HasStateAuthority)
        {
            CurrentState = GameState.Playing;
        }

        // Báo MinigameController bắt đầu game
        if (MinigameController.Instance != null)
        {
            MinigameController.Instance.OnCountdownComplete();
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ShowMinigameCountdown()
    {
        SetActiveUI(minigameTutorialUI, false);
        SetActiveUI(minigameCountdownUI, true);
    }

    /// <summary>
    /// Ẩn countdown UI - gọi khi countdown kết thúc
    /// </summary>
    public void HideMinigameCountdown()
    {
        SetActiveUI(minigameCountdownUI, false);
    }

    /// <summary>
    /// Hiển thị Tutorial UI - gọi bởi MinigameController khi scene đã load xong
    /// </summary>
    public void ShowMinigameTutorial()
    {
        // Tìm lại UI vì scene mới load
        FindUIReferences();

        // Hiện tutorial UI cho tất cả player
        SetActiveUI(minigameTutorialUI, true);
        SetActiveUI(minigameCountdownUI, false);

        // Unlock và show cursor cho tất cả player để có thể nhấn button
        if (CursorManager.Instance != null)
        {
            CursorManager.Instance.SetUIMode();
        }

        // Setup lại button nếu cần
        SetupTutorialStartButton();

        Debug.Log("[GameManager] Showing minigame tutorial UI (all players, cursor unlocked)");
    }

    /// <summary>
    /// Hiển thị Scoreboard trong minigame scene - thay thế cho scoreboard của MinigameController
    /// </summary>
    public void ShowMinigameScoreboard()
    {
        SetActiveUI(minigameTutorialUI, false);
        SetActiveUI(minigameCountdownUI, false);
        SetActiveUI(scoreboardUI, true);

        // Hiện cursor
        if (CursorManager.Instance != null)
        {
            CursorManager.Instance.ShowCursor();
        }

        Debug.Log("[GameManager] Showing scoreboard");
    }

    private void InitializeUIState()
    {
        // Đảm bảo luôn tìm lại UI trước khi set
        FindUIReferences();

        // Ẩn tất cả trước
        SetActiveUI(lobbyUI, false);
        SetActiveUI(votingUI, false);
        SetActiveUI(rouletteVotingUI, false);
        SetActiveUI(scoreboardUI, false);
        SetActiveUI(resultUI, false);
        SetActiveUI(minigameCountdownUI, false);

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
                // Đảm bảo tutorial luôn hiện cho mọi client khi vào Tutorial state
                ShowMinigameTutorial();
                break;
            case GameState.Playing:
                HandlePlayingState();
                break;
            case GameState.Scoreboard:
                HandleScoreboardState();
                break;
            case GameState.Roulette:
                HandleRouletteState();
                break;
            case GameState.Result:
                HandleResultState();
                break;
        }
    }
    #endregion

    #region Host-Only Game Flow Methods
    /// <summary>
    /// Bắt đầu match - Vote chọn minigame ngay từ đầu
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
        FinalWinnerId = -1;

        // Auto-assign tất cả players vào ghế
        if (SeatManager.Instance != null)
        {
            SeatManager.Instance.AutoAssignAllPlayersToSeats();
        }

        // Lưu seat mapping cho Roulette teleport
        if (RouletteManager.Instance != null)
        {
            RouletteManager.Instance.SaveSeatMapping();
            RouletteManager.Instance.ResetForNewGame();
        }

        // Reset MinigameVotingManager để chuẩn bị danh sách minigame mới
        if (MinigameVotingManager.Instance != null)
        {
            MinigameVotingManager.Instance.ResetPlayedMinigames();
            MinigameVotingManager.Instance.PrepareNextVotingRound();
        }

        // Vote chọn minigame ngay từ đầu
        StartVoting(VotingType.MinigameOnly);
    }

    /// <summary>
    /// Bắt đầu một minigame ngẫu nhiên
    /// </summary>
    public void StartRandomMinigame()
    {
        if (!HasStateAuthority) return;

        if (availableMinigames == null || availableMinigames.Length == 0)
        {
            Debug.LogError("[GameManager] No minigames available!");
            return;
        }

        int randomIndex = UnityEngine.Random.Range(0, availableMinigames.Length);
        Debug.Log($"[GameManager] Starting random minigame #{randomIndex}: {availableMinigames[randomIndex].minigameName}");

        StartMinigame(randomIndex);
    }

    /// <summary>
    /// Bắt đầu voting phase
    /// </summary>
    /// <param name="votingType">Loại voting</param>
    public void StartVoting(VotingType votingType = VotingType.MinigameOnly)
    {
        if (!HasStateAuthority)
        {
            Debug.LogWarning("[GameManager] Only Host can call StartVoting()");
            return;
        }
        CurrentVotingType = votingType;
        Debug.Log($"[GameManager] Starting voting phase... Type: {votingType}");
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

        MinigameData minigameData = null;

        // Ưu tiên lấy minigame từ MinigameVotingManager (đồng bộ với VotingManager)
        if (MinigameVotingManager.Instance != null && MinigameVotingManager.Instance.IsReady)
        {
            int availableCount = MinigameVotingManager.Instance.GetAvailableMinigameCount();
            if (minigameIndex < 0 || minigameIndex >= availableCount)
            {
                Debug.LogError($"[GameManager] Invalid minigame index: {minigameIndex}, AvailableCount: {availableCount}");
                return;
            }

            minigameData = MinigameVotingManager.Instance.GetMinigameByAvailableIndex(minigameIndex);
            if (minigameData == null)
            {
                Debug.LogError($"[GameManager] Failed to get minigame data for index: {minigameIndex}");
                return;
            }

            Debug.Log($"[GameManager] Starting minigame #{minigameIndex}: {minigameData.minigameName} (from MinigameVotingManager)");

            // Đánh dấu minigame đã được chơi
            MinigameVotingManager.Instance.MarkMinigamePlayed(minigameIndex);
        }
        else
        {
            // Fallback: sử dụng availableMinigames array
            if (availableMinigames == null)
            {
                Debug.LogError("[GameManager] availableMinigames is NULL and MinigameVotingManager not ready!");
                return;
            }

            if (minigameIndex < 0 || minigameIndex >= availableMinigames.Length)
            {
                Debug.LogError($"[GameManager] Invalid minigame index: {minigameIndex}, availableMinigames.Length: {availableMinigames.Length}");
                return;
            }

            minigameData = availableMinigames[minigameIndex];
            Debug.Log($"[GameManager] Starting minigame #{minigameIndex}: {minigameData.minigameName} (from availableMinigames)");
        }

        CurrentMinigameIndex = minigameIndex;
        CurrentRound++;

        // Sync minigame settings cho tất cả clients
        if (minigameData != null)
        {
            MG_CanMove = minigameData.canMove;
            MG_CanJump = minigameData.canJump;
            MG_CanCrouch = minigameData.canCrouch;
            MG_CanAttack = minigameData.canAttack;
            MG_CanRun = minigameData.canRun;
            MG_AllowRespawn = minigameData.allowRespawn;

            Debug.Log($"[GameManager] Synced MG settings - Move:{MG_CanMove}, Jump:{MG_CanJump}, Crouch:{MG_CanCrouch}, Attack:{MG_CanAttack}, Run:{MG_CanRun}, Respawn:{MG_AllowRespawn}");
        }

        // Vào Tutorial state trước, scene sẽ load trong HandleTutorialState
        Debug.Log("[GameManager] Calling ChangeState(Tutorial)");
        ChangeState(GameState.Tutorial);
    }

    /// <summary>
    /// Chuyển từ Tutorial sang Playing - được gọi bởi MinigameController sau countdown
    /// </summary>
    public void StartPlayingState()
    {
        if (!HasStateAuthority)
        {
            Debug.LogWarning("[GameManager] Only Host can call StartPlayingState()");
            return;
        }

        if (CurrentState != GameState.Tutorial)
        {
            Debug.LogWarning($"[GameManager] StartPlayingState called but current state is {CurrentState}");
            return;
        }

        Debug.Log("[GameManager] Tutorial complete, changing to Playing state");
        ChangeState(GameState.Playing);
    }

    /// <summary>
    /// Kết thúc minigame - gọi bởi MinigameController khi game kết thúc
    /// </summary>
    /// <param name="winnerId">PlayerId của người thắng (-1 nếu không có)</param>
    public void EndMinigame(int winnerId = -1)
    {
        if (!HasStateAuthority)
        {
            Debug.LogWarning("[GameManager] Only Host can call EndMinigame()");
            return;
        }

        Debug.Log($"[GameManager] Ending minigame... Winner: {winnerId}");

        // Notify RouletteManager về người thắng và minigame completed
        if (RouletteManager.Instance != null)
        {
            RouletteManager.Instance.OnMinigameCompleted();

            if (winnerId >= 0)
            {
                // Convert PlayerId to PlayerRef
                PlayerRef winnerRef = PlayerRefFromPlayerId(winnerId);
                if (winnerRef != PlayerRef.None)
                {
                    RouletteManager.Instance.OnMinigameWinner(winnerRef);
                }
            }
        }

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

    /// <summary>
    /// Xử lý flow sau khi hiển thị scoreboard
    /// Flow mới:
    /// - Nếu đã chơi đủ 2 MG -> Roulette
    /// - Nếu đã chơi 1 MG -> Vote (Roulette vs Minigame)
    /// </summary>
    public void ProceedFromScoreboard()
    {
        if (!HasStateAuthority)
        {
            Debug.LogWarning("[GameManager] Only Host can call ProceedFromScoreboard()");
            return;
        }

        // Check số player còn sống
        if (RouletteManager.Instance != null)
        {
            int aliveCount = RouletteManager.Instance.GetAlivePlayerCount();
            if (aliveCount <= 1)
            {
                // Chỉ còn 1 người - kết thúc game
                var aliveSlots = RouletteManager.Instance.GetAliveSlots();
                if (aliveSlots.Count > 0)
                {
                    // Convert slot to PlayerId
                    PlayerRef winnerRef = RouletteManager.Instance.GetPlayerRefFromSlot(aliveSlots[0]);
                    FinalWinnerId = winnerRef != PlayerRef.None ? winnerRef.PlayerId : -1;
                }
                else
                {
                    FinalWinnerId = -1;
                }
                Debug.Log($"[GameManager] Only {aliveCount} player(s) left. Showing final results...");
                ChangeState(GameState.Result);
                return;
            }
        }

        // Kiểm tra flow Roulette
        if (RouletteManager.Instance != null)
        {
            if (RouletteManager.Instance.ShouldTriggerRoulette())
            {
                // Đã chơi đủ 2 MG -> Bắt buộc vào Roulette
                Debug.Log("[GameManager] 2 minigames completed. Starting Roulette...");
                StartRoulette();
            }
            else if (RouletteManager.Instance.ShouldTriggerVoting())
            {
                // Đã chơi 1 MG -> Vote giữa Roulette và Minigame
                Debug.Log("[GameManager] 1 minigame completed. Starting voting (Roulette vs Minigame)...");
                StartVoting(VotingType.RouletteOrMinigame);
            }
            else
            {
                // Chưa chơi MG nào -> Minigame tiếp theo
                Debug.Log("[GameManager] Starting voting for next minigame...");
                StartVoting(VotingType.MinigameOnly);
            }
        }
        else
        {
            // Fallback: voting minigame
            Debug.LogWarning("[GameManager] RouletteManager not found. Falling back to minigame voting...");
            StartVoting(VotingType.MinigameOnly);
        }
    }

    /// <summary>
    /// Bắt đầu Roulette (Cò Quay Nga)
    /// </summary>
    public void StartRoulette()
    {
        if (!HasStateAuthority)
        {
            Debug.LogWarning("[GameManager] Only Host can call StartRoulette()");
            return;
        }

        Debug.Log("[GameManager] Starting Roulette...");
        ChangeState(GameState.Roulette);
    }

    /// <summary>
    /// Gọi bởi RouletteManager khi Roulette kết thúc
    /// </summary>
    /// <param name="winnerId">PlayerId của người thắng cuối cùng, -1 nếu không xác định</param>
    public void OnRouletteComplete(int winnerId)
    {
        if (!HasStateAuthority) return;

        Debug.Log($"[GameManager] Roulette complete. Winner: {winnerId}");

        // Check số player còn sống
        int aliveCount = RouletteManager.Instance?.GetAlivePlayerCount() ?? 0;

        if (aliveCount <= 1)
        {
            // Chỉ còn 1 người - kết thúc game
            FinalWinnerId = winnerId;
            Debug.Log("[GameManager] Only 1 player left. Showing final results...");
            ChangeState(GameState.Result);
        }
        else
        {
            // Còn nhiều người - tiếp tục với minigame mới
            Debug.Log("[GameManager] Multiple players left. Starting voting for next minigame...");
            StartVoting(VotingType.MinigameOnly);
        }
    }

    /// <summary>
    /// Gọi bởi VotingManager khi vote kết thúc (cho RouletteOrMinigame voting)
    /// </summary>
    /// <param name="chooseRoulette">True nếu vote Roulette thắng</param>
    public void OnVotingComplete(bool chooseRoulette)
    {
        if (!HasStateAuthority) return;

        if (chooseRoulette)
        {
            Debug.Log("[GameManager] Vote result: Roulette");
            StartRoulette();
        }
        else
        {
            Debug.Log("[GameManager] Vote result: Minigame");
            // VotingManager sẽ gọi StartMinigame với index được chọn
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
        FinalWinnerId = -1;

        // Reset Roulette state
        if (RouletteManager.Instance != null)
        {
            RouletteManager.Instance.ResetForNewGame();
        }

        ChangeState(GameState.Lobby);
    }
    public void OnRouletteSceneReady()
    {
        Debug.Log("[GameManager] Roulette scene ready, starting roulette gameplay");

        // Teleport players đến vị trí Roulette dựa trên seat từ Lobby
        if (RouletteManager.Instance != null)
        {
            RouletteManager.Instance.TeleportPlayersToRoulettePositions();
        }

        // Start Roulette (host only)
        if (HasStateAuthority && RouletteManager.Instance != null)
        {
            Debug.Log("[GameManager] Host starting RouletteManager.StartRoulette()");
            RouletteManager.Instance.StartRoulette();
        }
        else if (RouletteManager.Instance == null)
        {
            Debug.LogError("[GameManager] RouletteManager.Instance is NULL!");
        }
    }
    #endregion

    #region State Handlers (Override in subclass or extend)
    protected virtual void HandleLobbyState()
    {
        Debug.Log("[GameManager] Entered Lobby state");

        // Show lobby UI, hide others
        SetActiveUI(lobbyUI, true);
        SetActiveUI(votingUI, false);
        SetActiveUI(rouletteVotingUI, false);
        SetActiveUI(scoreboardUI, false);
        SetActiveUI(resultUI, false);
        SetActiveUI(minigameTutorialUI, false);
        SetActiveUI(minigameCountdownUI, false);

        // Chuyển camera sang First Person trong Lobby và cho phép xoay
        if (CameraManager.Instance != null)
        {
            CameraManager.Instance.SwitchToFirstPersonCamera();
            CameraManager.Instance.SetCameraRotationLocked(false);
        }

        // Hiện cursor trong lobby
        if (CursorManager.Instance != null)
        {
            CursorManager.Instance.ShowCursor();
        }

        // Reset player ready states (host only)
        if (HasStateAuthority)
        {
            ResetAllPlayersReady();
        }
    }

    protected virtual void HandleVotingState()
    {
        Debug.Log($"[GameManager] Entered Voting state. VotingType: {CurrentVotingType}");

        // Ẩn tất cả UI trước
        SetActiveUI(lobbyUI, false);
        SetActiveUI(votingUI, false);
        SetActiveUI(rouletteVotingUI, false);
        SetActiveUI(scoreboardUI, false);
        SetActiveUI(resultUI, false);
        SetActiveUI(minigameTutorialUI, false);
        SetActiveUI(minigameCountdownUI, false);

        // Hiện UI dựa vào VotingType
        if (CurrentVotingType == VotingType.MinigameOnly)
        {
            SetActiveUI(votingUI, true);
            Debug.Log("[GameManager] Showing VotingUI (MinigameOnly)");
            // Chuẩn bị danh sách minigame cho voting thường
            if (MinigameVotingManager.Instance != null && MinigameVotingManager.Instance.IsReady && HasStateAuthority)
            {
                MinigameVotingManager.Instance.PrepareNextVotingRound();
            }
        }
        else // RouletteOrMinigame
        {
            SetActiveUI(rouletteVotingUI, true);
            Debug.Log("[GameManager] Showing RouletteVotingUI (RouletteOrMinigame)");
            // Chỉ lấy các minigame chưa chơi cho voting Roulette
            if (MinigameVotingManager.Instance != null && MinigameVotingManager.Instance.IsReady && HasStateAuthority)
            {
                MinigameVotingManager.Instance.PrepareNextVotingRoundForRoulette();
            }
        }

        // Khóa xoay camera khi voting
        if (CameraManager.Instance != null)
        {
            CameraManager.Instance.SetCameraRotationLocked(true);
        }

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
            StartCoroutine(StartVotingWhenReady());
        }
        else if (VotingManager.Instance == null)
        {
            Debug.LogError("[GameManager] VotingManager.Instance is NULL!");
        }
    }
    IEnumerator StartVotingWhenReady()
    {
        yield return new WaitUntil(() =>
            VotingManager.Instance != null &&
            VotingManager.Instance.IsReady
        );

        VotingManager.Instance.StartVoting();
    }
    protected virtual void HandleTutorialState()
    {
        Debug.Log("[GameManager] Entered Tutorial state");

        // Ẩn tất cả UI panels (minigame UI sẽ được show sau khi scene load)
        SetActiveUI(lobbyUI, false);
        SetActiveUI(votingUI, false);
        SetActiveUI(rouletteVotingUI, false);
        SetActiveUI(scoreboardUI, false);
        SetActiveUI(resultUI, false);
        SetActiveUI(minigameTutorialUI, false);
        SetActiveUI(minigameCountdownUI, false);

        // Khóa xoay camera khi xem tutorial
        if (CameraManager.Instance != null)
        {
            CameraManager.Instance.SetCameraRotationLocked(true);
        }

        // Hiện cursor cho tutorial
        if (CursorManager.Instance != null)
        {
            CursorManager.Instance.ShowCursor();
        }

        // Tạm thời disable player input (sẽ enable lại khi Playing)
        if (PlayerInputHandler.Instance != null)
        {
            PlayerInputHandler.Instance.InputEnabled = false;
        }

        // HOST: Load scene minigame
        if (!HasStateAuthority)
        {
            Debug.Log("[GameManager] Not host, waiting for scene load");
            return;
        }

        // Lấy MinigameData - ưu tiên từ MinigameVotingManager
        MinigameData minigameData = null;

        if (MinigameVotingManager.Instance != null && MinigameVotingManager.Instance.IsReady)
        {
            minigameData = MinigameVotingManager.Instance.GetMinigameByAvailableIndex(CurrentMinigameIndex);
        }

        // Fallback về availableMinigames nếu không lấy được từ MinigameVotingManager
        if (minigameData == null && availableMinigames != null && CurrentMinigameIndex >= 0 && CurrentMinigameIndex < availableMinigames.Length)
        {
            minigameData = availableMinigames[CurrentMinigameIndex];
        }

        if (minigameData == null)
        {
            Debug.LogError($"[GameManager] No valid minigame data for index {CurrentMinigameIndex}!");
            return;
        }

        Debug.Log($"[GameManager] Loading minigame scene: {minigameData.sceneName}");

        // Setup camera mode
        RPC_SetupMinigameCamera(minigameData.useSharedCamera);

        // Load scene - Fusion sẽ sync tất cả clients
        int sceneIndex = GetSceneIndex(minigameData.sceneName);
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

    protected virtual void HandlePlayingState()
    {
        Debug.Log("[GameManager] Entered Playing state");

        // Ẩn tất cả UI panels
        SetActiveUI(lobbyUI, false);
        SetActiveUI(votingUI, false);
        SetActiveUI(rouletteVotingUI, false);
        SetActiveUI(scoreboardUI, false);
        SetActiveUI(resultUI, false);
        SetActiveUI(minigameTutorialUI, false);
        SetActiveUI(minigameCountdownUI, false);

        // Mở khóa xoay camera khi chơi
        if (CameraManager.Instance != null)
        {
            CameraManager.Instance.SetCameraRotationLocked(false);
        }

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

        // Scene đã được load trong Tutorial state
        // MinigameController sẽ xử lý logic game
        Debug.Log("[GameManager] Playing state active - game is now running");
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
        Debug.Log($"[GameManager] Minigame camera mode: {(useSharedCamera ? "Shared/Minigame" : "ThirdPerson")}");

        if (CameraManager.Instance != null)
        {
            if (useSharedCamera)
            {
                // Đặt flag để CameraManager biết đang chờ MinigameCamera setup
                // Điều này ngăn FirstPerson/ThirdPerson camera override trong khi chờ scene load
                CameraManager.Instance.SetPendingSharedCameraMode(true);
            }
            else
            {
                // Minigame dùng Third Person camera (như gameplay bình thường)
                CameraManager.Instance.SwitchToThirdPersonCamera();
            }
        }
    }
    protected virtual void HandleScoreboardState()
    {
        Debug.Log("[GameManager] Entered Scoreboard state");

        SetActiveUI(lobbyUI, false);
        SetActiveUI(votingUI, false);
        SetActiveUI(rouletteVotingUI, false);
        SetActiveUI(scoreboardUI, true);
        SetActiveUI(resultUI, false);
        SetActiveUI(minigameTutorialUI, false);
        SetActiveUI(minigameCountdownUI, false);

        // Khóa xoay camera khi xem scoreboard
        if (CameraManager.Instance != null)
        {
            CameraManager.Instance.SetCameraRotationLocked(true);
        }

        // Hiện cursor khi xem scoreboard
        if (CursorManager.Instance != null)
        {
            CursorManager.Instance.ShowCursor();
        }

        // Tắt player input khi xem scoreboard
        if (PlayerInputHandler.Instance != null)
        {
            PlayerInputHandler.Instance.InputEnabled = false;
        }

        // Auto-proceed to voting after delay (host only)
        if (HasStateAuthority)
        {
            if (_scoreboardCoroutine != null)
            {
                StopCoroutine(_scoreboardCoroutine);
            }
            _scoreboardCoroutine = StartCoroutine(AutoProceedFromScoreboard());
        }
    }

    private IEnumerator AutoProceedFromScoreboard()
    {
        Debug.Log($"[GameManager] Scoreboard will auto-proceed in {scoreboardDisplayDuration}s...");
        yield return new WaitForSeconds(scoreboardDisplayDuration);

        Debug.Log("[GameManager] Auto-proceeding from scoreboard...");
        ProceedFromScoreboard();
        _scoreboardCoroutine = null;
    }

    protected virtual void HandleRouletteState()
    {
        Debug.Log("[GameManager] Entered Roulette state");

        // Ẩn tất cả UI - Roulette xử lí bằng gameplay 3D
        SetActiveUI(lobbyUI, false);
        SetActiveUI(votingUI, false);
        SetActiveUI(rouletteVotingUI, false);
        SetActiveUI(scoreboardUI, false);
        SetActiveUI(resultUI, false);
        SetActiveUI(minigameTutorialUI, false);
        SetActiveUI(minigameCountdownUI, false);

        // Chuyển sang First Person camera trong Roulette
        if (CameraManager.Instance != null)
        {
            CameraManager.Instance.SwitchToFirstPersonCamera();
            CameraManager.Instance.SetCameraRotationLocked(false);
        }

        // Bật player input cho gameplay 3D
        if (PlayerInputHandler.Instance != null)
        {
            PlayerInputHandler.Instance.InputEnabled = true;
        }

        // Ẩn cursor cho gameplay 3D
        if (CursorManager.Instance != null)
        {
            CursorManager.Instance.HideCursor();
        }

        // Teleport players đến vị trí Roulette dựa trên seat từ Lobby
        if (RouletteManager.Instance != null)
        {
            RouletteManager.Instance.TeleportPlayersToRoulettePositions();
        }

        // Start Roulette (host only)
        if (HasStateAuthority && RouletteManager.Instance != null)
        {
            Debug.Log("[GameManager] Host starting RouletteManager.StartRoulette()");
            RouletteManager.Instance.StartRoulette();
        }
        else if (RouletteManager.Instance == null)
        {
            Debug.LogError("[GameManager] RouletteManager.Instance is NULL!");
        }
    }

    protected virtual void HandleResultState()
    {
        Debug.Log("[GameManager] Entered Result state");

        SetActiveUI(lobbyUI, false);
        SetActiveUI(votingUI, false);
        SetActiveUI(rouletteVotingUI, false);
        SetActiveUI(scoreboardUI, false);
        SetActiveUI(resultUI, true);
        SetActiveUI(minigameTutorialUI, false);
        SetActiveUI(minigameCountdownUI, false);

        // Hiện cursor
        if (CursorManager.Instance != null)
        {
            CursorManager.Instance.ShowCursor();
        }
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

    /// <summary>
    /// Convert PlayerId to PlayerRef
    /// </summary>
    private PlayerRef PlayerRefFromPlayerId(int playerId)
    {
        foreach (var playerRef in Runner.ActivePlayers)
        {
            if (playerRef.PlayerId == playerId)
                return playerRef;
        }
        return PlayerRef.None;
    }
    #endregion

}
