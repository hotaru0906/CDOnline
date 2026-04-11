using Fusion;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Controller chính cho mỗi minigame scene.
/// Quản lý logic game: WaitingForPlayers -> Tutorial -> Countdown -> Playing -> GameOver -> Scoreboard
/// UI (Tutorial, Countdown, Scoreboard) được quản lý bởi GameManager
/// </summary>
public class MinigameController : NetworkBehaviour
{
    public static MinigameController Instance { get; private set; }

    [Header("Spawn Settings")]
    [SerializeField] private Transform[] spawnPoints;

    [Header("Game Settings")]
    [SerializeField] private bool freezePlayersOnStart = true;

    [Header("Trap Spawner (Optional)")]
    [SerializeField] private TrapSpawner trapSpawner;

    #region Networked Properties
    [Networked, OnChangedRender(nameof(OnPhaseChanged))]
    public MinigamePhase CurrentPhase { get; private set; } = MinigamePhase.WaitingForPlayers;

    [Networked]
    public NetworkBool IsGameStarted { get; private set; }

    [Networked]
    public NetworkBool IsGameEnded { get; private set; }

    [Networked]
    public PlayerRef Winner { get; private set; }

    [Networked, OnChangedRender(nameof(OnGameTimerChanged))]
    public float GameTimer { get; private set; }

    [Networked]
    public int AlivePlayerCount { get; private set; }
    #endregion

    private MinigameData _minigameData;

    private List<PlayerController> spawnedPlayers = new List<PlayerController>();
    private NetworkRunner _runner;

    public enum MinigamePhase
    {
        WaitingForPlayers,
        Tutorial,
        Countdown,
        Playing,
        GameOver,
        Scoreboard
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Disable trap spawner initially
        if (trapSpawner != null)
        {
            trapSpawner.enabled = false;
        }
    }

    public override void Spawned()
    {
        Debug.Log($"[MinigameController] Spawned. IsHost: {HasStateAuthority}");
        _runner = Runner;

        // Lấy MinigameData từ GameManager
        if (GameManager.Instance != null)
        {
            _minigameData = GameManager.Instance.CurrentMinigameData;
        }

        if (HasStateAuthority)
        {
            StartCoroutine(WaitForPlayersThenSetup());
        }
    }
    private IEnumerator WaitForPlayersThenSetup()
    {
        Debug.Log("[Minigame] Waiting for players...");

        if (_minigameData == null)
        {
            Debug.LogError("[Minigame] MinigameData is NULL!");
            yield break;
        }

        while (true)
        {
            var players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);

            if (players.Length >= _minigameData.minPlayers)
            {
                Debug.Log("[Minigame] All players ready!");
                break;
            }

            yield return null;
        }

        SetupMinigame();
    }

    private void SetupMinigame()
    {
        TeleportPlayersToSpawnPoints();

        if (freezePlayersOnStart)
        {
            RPC_SetPlayersFrozen(true);
        }

        // Chuyển sang phase Tutorial
        CurrentPhase = MinigamePhase.Tutorial;

        // Báo GameManager hiển thị Tutorial UI
        RPC_ShowTutorialUI();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ShowTutorialUI()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ShowMinigameTutorial();
        }
    }

    #region Phase Handlers
    private void OnPhaseChanged()
    {
        Debug.Log($"[MinigameController] Phase changed to: {CurrentPhase}");

        switch (CurrentPhase)
        {
            case MinigamePhase.Tutorial:
                HandleTutorialPhase();
                break;
            case MinigamePhase.Countdown:
                HandleCountdownPhase();
                break;
            case MinigamePhase.Playing:
                HandlePlayingPhase();
                break;
            case MinigamePhase.GameOver:
                HandleGameOverPhase();
                break;
            case MinigamePhase.Scoreboard:
                HandleScoreboardPhase();
                break;
        }
    }

    private void HandleTutorialPhase()
    {
        Debug.Log("[MinigameController] Tutorial phase started");
        // UI được quản lý bởi GameManager.ShowMinigameTutorial()
    }

    private void HandleCountdownPhase()
    {
        Debug.Log("[MinigameController] Countdown phase started");
        // Countdown UI được quản lý bởi GameManager
    }

    private void HandlePlayingPhase()
    {
        Debug.Log("[MinigameController] Playing phase started");

        // Ẩn countdown UI via GameManager
        if (GameManager.Instance != null)
        {
            GameManager.Instance.HideMinigameCountdown();
        }

        IsGameStarted = true;

        // Khởi tạo game timer từ MinigameData
        if (HasStateAuthority)
        {
            if (_minigameData != null && _minigameData.timeLimit > 0)
            {
                GameTimer = _minigameData.timeLimit;
            }
            else
            {
                GameTimer = 0; // Không giới hạn thời gian
            }

            // Đếm số player ban đầu
            UpdateAlivePlayerCount();
        }

        // Enable trap spawner
        if (trapSpawner != null)
        {
            trapSpawner.enabled = true;
            trapSpawner.StartSpawning();
        }
    }

    private void HandleGameOverPhase()
    {
        Debug.Log("[MinigameController] GameOver phase");

        // Disable trap spawner
        if (trapSpawner != null)
        {
            trapSpawner.StopSpawning();
            trapSpawner.enabled = false;
        }

        // Freeze all players
        RPC_SetPlayersFrozen(true);
    }

    private void HandleScoreboardPhase()
    {
        Debug.Log("[MinigameController] Scoreboard phase");

        // Hiển thị Scoreboard via GameManager
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ShowMinigameScoreboard();
        }

        // Debug scoreboard info
        LogScoreboardInfo();
    }

    private void LogScoreboardInfo()
    {
        Debug.Log("========== SCOREBOARD ==========");

        var players = FindObjectsByType<PlayerNetworkData>(FindObjectsSortMode.None);
        foreach (var player in players)
        {
            bool isWinner = Winner != PlayerRef.None &&
                           player.Object.InputAuthority == Winner;
            string status = isWinner ? "🏆 WINNER" : "❌ LOSER";
            Debug.Log($"[Scoreboard] {player.PlayerName}: {status}");
        }

        Debug.Log("================================");
    }
    #endregion

    #region Countdown Logic (Managed by GameManager)
    /// <summary>
    /// Gọi bởi GameManager khi countdown bắt đầu
    /// </summary>
    public void OnCountdownStarted()
    {
        if (CurrentPhase != MinigamePhase.Tutorial)
        {
            Debug.LogWarning($"[MinigameController] OnCountdownStarted called but phase is {CurrentPhase}");
            return;
        }

        Debug.Log("[MinigameController] Countdown started (managed by GameManager)");

        if (HasStateAuthority)
        {
            CurrentPhase = MinigamePhase.Countdown;
        }
    }

    /// <summary>
    /// Gọi bởi GameManager khi countdown kết thúc
    /// </summary>
    public void OnCountdownComplete()
    {
        Debug.Log("[MinigameController] Countdown complete - starting game");

        // Unfreeze players
        if (HasStateAuthority)
        {
            RPC_SetPlayersFrozen(false);
        }

        // Chờ GameManager chuyển state sang Playing
        // MinigameController sẽ detect trong FixedUpdateNetwork
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;

        // Detect khi GameManager chuyển sang Playing state
        if (GameManager.Instance != null &&
            GameManager.Instance.CurrentState == GameState.Playing &&
            CurrentPhase != MinigamePhase.Playing)
        {
            Debug.Log("[Minigame] GameManager allowed Playing!");
            CurrentPhase = MinigamePhase.Playing;
        }

        // Game timer logic (Playing phase)
        if (CurrentPhase == MinigamePhase.Playing && !IsGameEnded)
        {
            // Update game timer nếu có time limit
            if (GameTimer > 0)
            {
                GameTimer -= Runner.DeltaTime;

                if (GameTimer <= 0)
                {
                    GameTimer = 0;
                    OnTimeUp();
                    return;
                }
            }

            // Check elimination (nếu minigame không cho respawn)
            UpdateAlivePlayerCount();
            CheckEliminationWinCondition();
        }
    }

    /// <summary>
    /// Callback khi GameTimer thay đổi
    /// </summary>
    private void OnGameTimerChanged()
    {
        // Override này để UI có thể subscribe và hiển thị timer
    }

    /// <summary>
    /// Cập nhật số player còn sống
    /// </summary>
    private void UpdateAlivePlayerCount()
    {
        var players = FindObjectsByType<PlayerMinigameData>(FindObjectsSortMode.None);
        int alive = 0;

        foreach (var p in players)
        {
            if (!p.IsEliminated)
            {
                alive++;
            }
        }

        AlivePlayerCount = alive;
    }

    /// <summary>
    /// Kiểm tra điều kiện thắng khi có elimination
    /// </summary>
    private void CheckEliminationWinCondition()
    {
        // Chỉ check nếu minigame không cho respawn
        if (_minigameData != null && _minigameData.allowRespawn)
            return;

        // Nếu chỉ còn 1 player -> kết thúc
        if (AlivePlayerCount <= 1)
        {
            // Tìm player còn sống
            var players = FindObjectsByType<PlayerMinigameData>(FindObjectsSortMode.None);
            PlayerRef lastSurvivor = PlayerRef.None;

            foreach (var p in players)
            {
                if (!p.IsEliminated)
                {
                    lastSurvivor = p.Object.InputAuthority;
                    break;
                }
            }

            Debug.Log($"[MinigameController] Only 1 player remaining: {lastSurvivor}");
            EndGame(lastSurvivor);
        }
    }

    /// <summary>
    /// Gọi khi hết thời gian
    /// </summary>
    private void OnTimeUp()
    {
        Debug.Log("[MinigameController] Time's up!");

        // Tìm winner dựa trên tiêu chí (có thể là người sống lâu nhất, checkpoint xa nhất, etc.)
        // Mặc định: player còn sống với checkpoint cao nhất
        PlayerRef winner = FindBestPlayer();

        EndGame(winner);
    }

    /// <summary>
    /// Tìm player tốt nhất (winner khi hết giờ)
    /// </summary>
    private PlayerRef FindBestPlayer()
    {
        var players = FindObjectsByType<PlayerMinigameData>(FindObjectsSortMode.None);
        PlayerRef best = PlayerRef.None;
        int bestCheckpoint = -1;

        foreach (var p in players)
        {
            if (!p.IsEliminated && p.CurrentCheckpointIndex > bestCheckpoint)
            {
                bestCheckpoint = p.CurrentCheckpointIndex;
                best = p.Object.InputAuthority;
            }
        }

        return best;
    }

    /// <summary>
    /// Kết thúc game với winner
    /// </summary>
    private void EndGame(PlayerRef winner)
    {
        if (IsGameEnded) return;

        Debug.Log($"[MinigameController] Game ended. Winner: {winner}");

        Winner = winner;
        IsGameEnded = true;
        CurrentPhase = MinigamePhase.GameOver;

        RPC_OnPlayerWon(winner);
        RPC_SetPlayersFrozen(true);
        // HandleGameOverPhase();
        // HandleScoreboardPhase();

        if (HasStateAuthority && GameManager.Instance != null)
        {
            GameManager.Instance.ShowMinigameScoreboard();
            GameManager.Instance.EndMinigame(winner.PlayerId);
        }
    }
    #endregion

    #region Player Management
    private void TeleportPlayersToSpawnPoints()
    {
        var players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);

        // Sort theo PlayerRef để spawn order deterministic
        System.Array.Sort(players, (a, b) =>
            a.Object.InputAuthority.PlayerId.CompareTo(b.Object.InputAuthority.PlayerId)
        );

        int spawnIndex = 0;

        foreach (var player in players)
        {
            var spawnPoint = spawnPoints[spawnIndex % spawnPoints.Length];
            var targetPos = spawnPoint.position;

            var cc = player.GetComponent<CharacterController>();
            if (cc != null)
            {
                cc.enabled = false;
                player.transform.position = targetPos;
                cc.enabled = true;
            }
            else
            {
                player.transform.position = targetPos;
            }

            var minigameData = player.GetComponent<PlayerMinigameData>();
            if (minigameData != null)
            {
                minigameData.ResetCheckpoint(targetPos);
            }

            spawnIndex++;
        }

        Debug.Log($"[MinigameController] Teleported {spawnIndex} players to spawn points");
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_SetPlayersFrozen(bool frozen)
    {
        var players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        foreach (var player in players)
        {
            player.SetFrozen(frozen);
        }
        Debug.Log($"[MinigameController] Players frozen: {frozen}");
    }
    #endregion

    #region Win Condition
    /// <summary>
    /// Gọi khi player về đích - chỉ host gọi
    /// </summary>
    public void PlayerFinished(PlayerRef playerRef)
    {
        if (!HasStateAuthority) return;
        if (IsGameEnded) return;

        Debug.Log($"[MinigameController] Player {playerRef} finished!");
        EndGame(playerRef);
    }

    /// <summary>
    /// Gọi khi player bị loại (để cập nhật count)
    /// </summary>
    public void OnPlayerEliminated(PlayerRef playerRef)
    {
        if (!HasStateAuthority) return;

        Debug.Log($"[MinigameController] Player {playerRef} eliminated!");
        UpdateAlivePlayerCount();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_OnPlayerWon(PlayerRef winnerRef)
    {
        Debug.Log($"[MinigameController] Player {winnerRef} WON!");
    }

    private IEnumerator ShowScoreboardAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        CurrentPhase = MinigamePhase.Scoreboard;
    }

    private IEnumerator EndGameAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (GameManager.Instance != null)
        {
            GameManager.Instance.EndMinigame(Winner.PlayerId);
        }
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Lấy spawn point cho player
    /// </summary>
    public Vector3 GetSpawnPoint(int playerIndex)
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
            return Vector3.zero;

        int index = playerIndex % spawnPoints.Length;
        return spawnPoints[index].position;
    }

    /// <summary>
    /// Lấy spawn rotation cho player
    /// </summary>
    public Quaternion GetSpawnRotation(int playerIndex)
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
            return Quaternion.identity;

        int index = playerIndex % spawnPoints.Length;
        return spawnPoints[index].rotation;
    }
    #endregion
}
