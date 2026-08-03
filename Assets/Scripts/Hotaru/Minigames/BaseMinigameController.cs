using Fusion;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Base class dùng chung cho tất cả MinigameController.
/// Quản lý logic chung: phase machine, timer, spawn, freeze, trap spawner, networking.
/// 
/// Derived class phải implement:
///   - CheckWinCondition()  : kiểm tra điều kiện thắng mỗi tick
///   - OnTimeUp()           : xử lý khi hết giờ
/// 
/// Derived class có thể override:
///   - PlayerFinished()     : khi 1 player về đích
///   - EndGame()            : kết thúc game (luôn gọi base.EndGame())
///   - LogScoreboardInfo()  : log kết quả ra console
///   - OnGamePlayingStarted() / OnGameOver()  : hook vào các phase
/// </summary>
public abstract class BaseMinigameController : NetworkBehaviour
{
    public static BaseMinigameController Instance { get; private set; }

    [Header("Spawn Settings")]
    [SerializeField] protected Transform[] spawnPoints;

    [Header("Game Settings")]
    [SerializeField] protected bool freezePlayersOnStart = true;

    [Header("Trap Spawner (Optional)")]
    [SerializeField] protected TrapSpawner trapSpawner;

    public enum MinigamePhase
    {
        WaitingForPlayers,
        Tutorial,
        Countdown,
        Playing,
        GameOver,
        Scoreboard
    }

    #region Networked Properties

    [Networked, OnChangedRender(nameof(OnPhaseChanged))]
    public MinigamePhase CurrentPhase { get; protected set; } = MinigamePhase.WaitingForPlayers;

    [Networked]
    public NetworkBool IsGameStarted { get; protected set; }

    [Networked]
    public NetworkBool IsGameEnded { get; protected set; }

    [Networked, OnChangedRender(nameof(OnGameTimerChanged))]
    public float GameTimer { get; protected set; }

    [Networked]
    public int AlivePlayerCount { get; protected set; }

    /// <summary>
    /// Kết quả cuối game — tự replicate xuống tất cả client sau EndGame().
    /// UI đọc mảng này khi OnScoreboardReady fires.
    /// </summary>
    [Networked, Capacity(8)]
    public NetworkArray<MinigameResultData> ScoreboardResults { get; }

    /// <summary>Timer chuyển từ GameOver → Scoreboard phase sau 2.5s.</summary>
    [Networked]
    private TickTimer ScoreboardTransitionTimer { get; set; }

    #endregion

    public static event System.Action OnGameStarted;
    public static event System.Action OnScoreboardReady;

    protected MinigameData _minigameData;

    protected List<PlayerRef> _finishOrder = new List<PlayerRef>();

    protected NetworkRunner _minigameRunner;

    // ============================================================
    // KEY REWARD SYSTEM
    // ============================================================
    // Flow: Minigame kết thúc → BuildBoardRanking() trả về thứ tự
    // rank (PlayerId theo rank 1 → N) → GrantKeysToPlayers() cộng
    // key trực tiếp vào PlayerItemInventory.KeyCount của từng player
    // → Board phase đọc KeyCount để cho phép mở khóa (logic mở khóa
    // nằm bên Board, không thuộc phạm vi minigame).
    //
    // Chỉnh số lượng key mỗi rank tại các const bên dưới — áp dụng
    // chung cho tất cả MG (MG1-MG5).
    // ============================================================

    [Header("Key Reward Settings")]
    public const int KEY_REWARD_RANK_1 = 3; // Số key player hạng 1 nhận
    public const int KEY_REWARD_RANK_2 = 2; // Số key player hạng 2 nhận
    public const int KEY_REWARD_RANK_3 = 1; // Số key player hạng 3 nhận
    public const int KEY_REWARD_RANK_4 = 0; // Số key player hạng 4 nhận

    /// <summary>Trả về số key theo rank.</summary>
    public static int GetKeyRewardForRank(int rank) => rank switch
    {
        1 => KEY_REWARD_RANK_1,
        2 => KEY_REWARD_RANK_2,
        3 => KEY_REWARD_RANK_3,
        _ => KEY_REWARD_RANK_4
    };

    /// <summary>
    /// Cấp key cho từng player dựa theo ranking cuối game.
    /// ranking[0] = PlayerId hạng 1, ranking[1] = hạng 2, v.v.
    /// Gọi trên host, ngay sau khi có ranking cuối cùng trong EndGame().
    /// </summary>
    protected virtual void GrantKeysToPlayers(int[] ranking)
    {
        if (ranking == null) return;

        for (int i = 0; i < ranking.Length; i++)
        {
            int playerId = ranking[i];
            int rank = i + 1; // ranking[0] = rank 1

            int keyAmount = GetKeyRewardForRank(rank);
            if (keyAmount <= 0) continue;

            var inventory = PlayerItemInventory.GetForPlayer(playerId);
            if (inventory == null)
            {
                StartCoroutine(GrantKeyWhenInventoryReady(playerId, rank, keyAmount));
                continue;
            }

            inventory.AddKey(keyAmount);
            Debug.Log($"[{GetType().Name}] KeyReward: P{playerId} rank {rank} → +{keyAmount} key");
        }
    }

    private IEnumerator GrantKeyWhenInventoryReady(int playerId, int rank, int keyAmount)
    {
        const float timeoutSeconds = 3f;
        float timer = 0f;

        while (timer < timeoutSeconds)
        {
            var inventory = PlayerItemInventory.GetForPlayer(playerId);
            if (inventory != null)
            {
                inventory.AddKey(keyAmount);
                Debug.Log($"[{GetType().Name}] KeyReward (delayed): P{playerId} rank {rank} → +{keyAmount} key");
                yield break;
            }

            timer += Time.deltaTime;
            yield return null;
        }

        Debug.LogWarning($"[{GetType().Name}] KeyReward FAILED: Inventory not found for P{playerId} after {timeoutSeconds:0.0}s (rank {rank})");
    }

    // ----------------------------------------------------------------
    //  Lifecycle
    // ----------------------------------------------------------------

    protected virtual void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (trapSpawner != null)
            trapSpawner.enabled = false;
    }

    public override void Spawned()
    {
        Debug.Log($"[{GetType().Name}] Spawned. IsHost: {HasStateAuthority}");
        _minigameRunner = Runner;

        if (GameManager.Instance != null)
            _minigameData = GameManager.Instance.CurrentMinigameData;

        if (HasStateAuthority)
            StartCoroutine(WaitForPlayersThenSetup());
    }

    private IEnumerator WaitForPlayersThenSetup()
    {
        Debug.Log($"[{GetType().Name}] Waiting for players...");

        if (_minigameData == null)
        {
            Debug.LogError($"[{GetType().Name}] MinigameData is NULL!");
            yield break;
        }

        while (true)
        {
            var players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
            Debug.Log($"[{GetType().Name}] Found {players.Length} players, need {_minigameData.minPlayers}");
            if (players.Length >= _minigameData.minPlayers)
            {
                Debug.Log($"[{GetType().Name}] All players ready!");
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
            RPC_SetPlayersFrozen(true);

        CurrentPhase = MinigamePhase.Tutorial;
        RPC_ShowTutorialUI();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ShowTutorialUI()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.ShowMinigameTutorial();
    }

    // ----------------------------------------------------------------
    //  Phase Machine
    // ----------------------------------------------------------------

    private void OnPhaseChanged()
    {
        Debug.Log($"[{GetType().Name}] Phase → {CurrentPhase}");

        switch (CurrentPhase)
        {
            case MinigamePhase.Tutorial: HandleTutorialPhase(); break;
            case MinigamePhase.Countdown: HandleCountdownPhase(); break;
            case MinigamePhase.Playing: HandlePlayingPhase(); break;
            case MinigamePhase.GameOver: HandleGameOverPhase(); break;
            case MinigamePhase.Scoreboard: HandleScoreboardPhase(); break;
        }
    }

    private void HandleTutorialPhase()
    {
        Debug.Log($"[{GetType().Name}] Tutorial phase started");
    }

    private void HandleCountdownPhase()
    {
        Debug.Log($"[{GetType().Name}] Countdown phase started");
    }

    private void HandlePlayingPhase()
    {
        Debug.Log($"[{GetType().Name}] Playing phase started");

        if (GameManager.Instance != null)
            GameManager.Instance.HideMinigameCountdown();

        IsGameStarted = true;
        OnGameStarted?.Invoke();

        if (HasStateAuthority)
        {
            GameTimer = (_minigameData != null && _minigameData.timeLimit > 0)
                ? _minigameData.timeLimit
                : 0f;

            // Cập nhật HUD ngay khi vào phase Playing để hiện đúng mốc thời gian ban đầu.
            OnGameTimerChanged();

            UpdateAlivePlayerCount();
        }

        if (trapSpawner != null)
        {
            trapSpawner.enabled = true;
            trapSpawner.StartSpawning();
        }

        OnGamePlayingStarted();
    }

    private void HandleGameOverPhase()
    {
        Debug.Log($"[{GetType().Name}] GameOver phase");

        if (trapSpawner != null)
        {
            trapSpawner.StopSpawning();
            trapSpawner.enabled = false;
        }

        if (HasStateAuthority)
            RPC_SetPlayersFrozen(true);

        OnGameOver();
    }

    private void HandleScoreboardPhase()
    {
        Debug.Log($"[{GetType().Name}] Scoreboard phase");

        OnScoreboardReady?.Invoke();

        if (GameManager.Instance != null)
            GameManager.Instance.ShowMinigameScoreboard();

        LogScoreboardInfo();
    }

    protected virtual void OnGamePlayingStarted() { }

    protected virtual void OnGameOver() { }

    protected virtual void LogScoreboardInfo() { }

    protected virtual void BuildScoreboardResults() { }

    // ----------------------------------------------------------------
    //  Countdown — managed by GameManager
    // ----------------------------------------------------------------

    public void OnCountdownStarted()
    {
        if (CurrentPhase != MinigamePhase.Tutorial)
        {
            Debug.LogWarning($"[{GetType().Name}] OnCountdownStarted called in wrong phase: {CurrentPhase}");
            return;
        }

        Debug.Log($"[{GetType().Name}] Countdown started");

        if (HasStateAuthority)
            CurrentPhase = MinigamePhase.Countdown;
    }

    public void OnCountdownComplete()
    {
        Debug.Log($"[{GetType().Name}] Countdown complete — starting game");

        if (HasStateAuthority)
            RPC_SetPlayersFrozen(false);
    }

    // ----------------------------------------------------------------
    //  FixedUpdateNetwork — Timer + Win Condition
    // ----------------------------------------------------------------

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;

        if (GameManager.Instance != null &&
            GameManager.Instance.CurrentState == GameState.Playing &&
            CurrentPhase != MinigamePhase.Playing)
        {
            Debug.Log($"[{GetType().Name}] GameManager → Playing!");
            CurrentPhase = MinigamePhase.Playing;
        }

        if (CurrentPhase == MinigamePhase.GameOver && ScoreboardTransitionTimer.Expired(Runner))
        {
            ScoreboardTransitionTimer = default;
            CurrentPhase = MinigamePhase.Scoreboard;
        }

        if (CurrentPhase != MinigamePhase.Playing || IsGameEnded) return;

        if (GameTimer > 0f)
        {
            GameTimer -= Runner.DeltaTime;

            OnGameTimerChanged();

            if (GameTimer <= 0f)
            {
                GameTimer = 0f;
                OnGameTimerChanged();
                OnTimeUp();
                return;
            }
        }

        UpdateAlivePlayerCount();
        CheckWinCondition();
    }

    protected virtual void OnGameTimerChanged()
    {
        MinigameHUDController.Instance?.SetTime(GameTimer);
    }

    // ----------------------------------------------------------------
    //  Abstract / Virtual — bắt buộc override trong derived class
    // ----------------------------------------------------------------

    protected abstract void CheckWinCondition();

    protected abstract void OnTimeUp();

    public virtual void PlayerFinished(PlayerRef playerRef) { }

    protected virtual void OnPlayerFinished(PlayerRef playerRef) { }

    protected virtual void CalculateResults() { }

    protected virtual bool CheckGameEnd() { return false; }

    /// <summary>
    /// Rank 1 = 20pt, 2 = 10pt, 3 = 5pt, 4+ = 0pt.
    /// </summary>
    protected static int RankToHiddenScore(int rank) => rank switch
    {
        1 => 20,
        2 => 10,
        3 => 5,
        _ => 0
    };

    /// <summary>
    /// Gán HiddenScore cho tất cả players dựa theo FinishRank đã được set.
    /// Gọi ở cuối FinalizeRanks() trong từng derived class.
    /// </summary>
    protected void ApplyHiddenScores()
    {
        var allData = FindObjectsByType<PlayerMinigameData>(FindObjectsSortMode.None);
        foreach (var p in allData)
            p.SetHiddenScore(RankToHiddenScore(p.FinishRank));
    }

    /// <summary>
    /// BuildBoardRanking mặc định: sort theo HiddenScore giảm dần.
    /// Derived class KHÔNG cần override nữa — chỉ cần gọi ApplyHiddenScores()
    /// trong FinalizeRanks() là đủ.
    /// </summary>
    protected virtual int[] BuildBoardRanking(PlayerRef winner)
    {
        var allData = FindObjectsByType<PlayerMinigameData>(FindObjectsSortMode.None);
        var sorted = new List<PlayerMinigameData>(allData);

        sorted.Sort((a, b) => b.HiddenScore.CompareTo(a.HiddenScore));

        var ranking = new List<int>();
        foreach (var p in sorted)
        {
            if (p.Object != null)
                ranking.Add(p.Object.InputAuthority.PlayerId);
        }

        Debug.Log($"[{GetType().Name}] BoardRanking (by HiddenScore): [{string.Join(", ", ranking)}]");
        return ranking.ToArray();
    }

    protected virtual void EndGame(PlayerRef winner)
    {
        if (IsGameEnded) return;

        Debug.Log($"[{GetType().Name}] Game ended. Winner: {winner}");

        IsGameEnded = true;
        CurrentPhase = MinigamePhase.GameOver;

        if (HasStateAuthority)
        {
            BuildScoreboardResults();
            ScoreboardTransitionTimer = TickTimer.CreateFromSeconds(Runner, 2.5f);

            int[] finalRanking = BuildBoardRanking(winner);

            if (GameManager.Instance != null)
                GameManager.Instance.SetMinigameRanking(finalRanking);

            // Cấp key cho player theo ranking — finalRanking[0] = rank 1, v.v.
            GrantKeysToPlayers(finalRanking);

            if (GameManager.Instance != null)
                GameManager.Instance.EndMinigame(winner.PlayerId);
        }
    }

    // ----------------------------------------------------------------
    //  Player Management
    // ----------------------------------------------------------------

    public virtual void OnPlayerEliminated(PlayerRef playerRef)
    {
        if (!HasStateAuthority) return;
        Debug.Log($"[{GetType().Name}] Player {playerRef} eliminated!");
        UpdateAlivePlayerCount();
    }

    protected void UpdateAlivePlayerCount()
    {
        var players = FindObjectsByType<PlayerMinigameData>(FindObjectsSortMode.None);
        int alive = 0;
        foreach (var p in players)
            if (!p.IsEliminated) alive++;
        AlivePlayerCount = alive;
    }

    private void TeleportPlayersToSpawnPoints()
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogError($"[{GetType().Name}] No spawn points assigned!");
            return;
        }

        var players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);

        System.Array.Sort(players, (a, b) =>
            a.Object.InputAuthority.PlayerId.CompareTo(b.Object.InputAuthority.PlayerId));

        for (int i = 0; i < players.Length; i++)
        {
            var player = players[i];
            var spawnPoint = spawnPoints[i % spawnPoints.Length];

            Debug.Log($"[Spawn] Player {players[i].Object.InputAuthority.PlayerId} → SpawnPoint {i} at {spawnPoints[i % spawnPoints.Length].position}");
            player.Teleport(spawnPoint.position);
            player.transform.rotation = spawnPoint.rotation;

            var mgData = player.GetComponent<PlayerMinigameData>();
            if (mgData != null) mgData.ResetCheckpoint(spawnPoint.position);
        }

        var positions = new Vector3[players.Length];
        var rotations = new Quaternion[players.Length];
        var playerRefs = new int[players.Length];

        for (int i = 0; i < players.Length; i++)
        {
            positions[i] = spawnPoints[i % spawnPoints.Length].position;
            rotations[i] = spawnPoints[i % spawnPoints.Length].rotation;
            playerRefs[i] = players[i].Object.InputAuthority.PlayerId;
        }

        RPC_SyncSpawnPositions(playerRefs, positions, rotations);

        Debug.Log($"[{GetType().Name}] Teleported {players.Length} players to spawn points");
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_SyncSpawnPositions(int[] playerIds, Vector3[] positions, Quaternion[] rotations)
    {
        var players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);

        for (int i = 0; i < playerIds.Length; i++)
        {
            foreach (var player in players)
            {
                if (player.Object.InputAuthority.PlayerId != playerIds[i]) continue;

                var cc = player.GetComponent<CharacterController>();
                if (cc != null)
                {
                    cc.enabled = false;
                    player.transform.position = positions[i];
                    player.transform.rotation = rotations[i];
                    cc.enabled = true;
                }
                else
                {
                    player.transform.position = positions[i];
                    player.transform.rotation = rotations[i];
                }
                break;
            }
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_SetPlayersFrozen(bool frozen)
    {
        var players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        foreach (var player in players)
            player.SetFrozen(frozen);
        Debug.Log($"[{GetType().Name}] Players frozen: {frozen}");
    }

    // ----------------------------------------------------------------
    //  Helpers
    // ----------------------------------------------------------------

    public Vector3 GetSpawnPoint(int playerIndex)
    {
        if (spawnPoints == null || spawnPoints.Length == 0) return Vector3.zero;
        return spawnPoints[playerIndex % spawnPoints.Length].position;
    }

    public Quaternion GetSpawnRotation(int playerIndex)
    {
        if (spawnPoints == null || spawnPoints.Length == 0) return Quaternion.identity;
        return spawnPoints[playerIndex % spawnPoints.Length].rotation;
    }

    protected void RespawnPlayer(PlayerController player)
    {
        if (!HasStateAuthority || player == null)
            return;

        int playerIndex = player.Object.InputAuthority.PlayerId - 1;

        Vector3 spawnPos = GetSpawnPoint(playerIndex);
        Quaternion spawnRot = GetSpawnRotation(playerIndex);

        player.Teleport(spawnPos);
        player.transform.rotation = spawnRot;

        player.ResetVelocity();
        player.ForceIdle();

        var mgData = player.GetComponent<PlayerMinigameData>();
        if (mgData != null)
        {
            mgData.RespawnForMG3();
        }

        Debug.Log($"[{GetType().Name}] Respawn Player {player.Object.InputAuthority.PlayerId}");
    }

    public void ResetAlivePlayersToSpawn()
    {
        if (!HasStateAuthority)
            return;

        var players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);

        foreach (var player in players)
        {
            var mgData = player.GetComponent<PlayerMinigameData>();

            if (mgData == null)
                continue;

            // Người đã bị loại thì không reset
            if (mgData.IsEliminated)
                continue;

            int playerIndex = player.Object.InputAuthority.PlayerId - 1;

            Vector3 spawnPos = GetSpawnPoint(playerIndex);
            Quaternion spawnRot = GetSpawnRotation(playerIndex);

            player.Teleport(spawnPos);
            player.transform.rotation = spawnRot;

            player.ResetVelocity();
            player.ForceIdle();
        }
    }
}