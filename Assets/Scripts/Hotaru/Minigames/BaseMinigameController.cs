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

    #endregion

    protected MinigameData _minigameData;

    /// <summary>
    /// Danh sách thứ tự về đích — chỉ dùng trên Host.
    /// Derived class (racing, score...) có thể dùng để tính rank.
    /// </summary>
    protected List<PlayerRef> _finishOrder = new List<PlayerRef>();

    protected NetworkRunner _runner;

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
        _runner = Runner;

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
            case MinigamePhase.Tutorial:   HandleTutorialPhase();   break;
            case MinigamePhase.Countdown:  HandleCountdownPhase();  break;
            case MinigamePhase.Playing:    HandlePlayingPhase();    break;
            case MinigamePhase.GameOver:   HandleGameOverPhase();   break;
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

        if (HasStateAuthority)
        {
            GameTimer = (_minigameData != null && _minigameData.timeLimit > 0)
                ? _minigameData.timeLimit
                : 0f;

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

        if (GameManager.Instance != null)
            GameManager.Instance.ShowMinigameScoreboard();

        LogScoreboardInfo();
    }

    /// <summary>Hook gọi khi bước vào Playing phase. Override trong derived class nếu cần.</summary>
    protected virtual void OnGamePlayingStarted() { }

    /// <summary>Hook gọi khi bước vào GameOver phase. Override trong derived class nếu cần.</summary>
    protected virtual void OnGameOver() { }

    /// <summary>Log kết quả ra console. Override trong derived class.</summary>
    protected virtual void LogScoreboardInfo() { }

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

        // Detect khi GameManager chuyển sang Playing state
        if (GameManager.Instance != null &&
            GameManager.Instance.CurrentState == GameState.Playing &&
            CurrentPhase != MinigamePhase.Playing)
        {
            Debug.Log($"[{GetType().Name}] GameManager → Playing!");
            CurrentPhase = MinigamePhase.Playing;
        }

        if (CurrentPhase != MinigamePhase.Playing || IsGameEnded) return;

        // Đếm ngược timer
        if (GameTimer > 0f)
        {
            GameTimer -= Runner.DeltaTime;
            if (GameTimer <= 0f)
            {
                GameTimer = 0f;
                OnTimeUp();
                return;
            }
        }

        UpdateAlivePlayerCount();
        CheckWinCondition();
    }

    /// <summary>Callback khi GameTimer thay đổi — UI có thể subscribe để hiện timer.</summary>
    protected virtual void OnGameTimerChanged() { }

    // ----------------------------------------------------------------
    //  Abstract / Virtual — bắt buộc override trong derived class
    // ----------------------------------------------------------------

    /// <summary>
    /// Kiểm tra điều kiện thắng mỗi FixedUpdate.
    /// Gọi EndGame() khi điều kiện đạt được.
    /// </summary>
    protected abstract void CheckWinCondition();

    /// <summary>
    /// Xử lý khi GameTimer về 0.
    /// Tính rank cho những player chưa về đích, rồi gọi EndGame().
    /// </summary>
    protected abstract void OnTimeUp();

    /// <summary>
    /// Gọi khi 1 player chạm FinishLine.
    /// MG1: EndGame() ngay. MG2: lưu rank, tiếp tục game.
    /// </summary>
    public virtual void PlayerFinished(PlayerRef playerRef) { }

    /// <summary>
    /// Hook gọi ngay sau khi 1 player về đích thành công.
    /// Override để hiện rank UI, play effect riêng cho từng player, v.v.
    /// </summary>
    protected virtual void OnPlayerFinished(PlayerRef playerRef) { }

    /// <summary>
    /// Tính toán thêm kết quả cuối game (bonus, score, v.v.).
    /// Gọi trước EndGame(). Default: không làm gì.
    /// </summary>
    protected virtual void CalculateResults() { }

    /// <summary>
    /// Kiểm tra có nên EndGame() ngay khi 1 player về đích không.
    /// Default: false (tiếp tục game). MG1 override: return true (1 winner = game over ngay).
    /// </summary>
    protected virtual bool CheckGameEnd() { return false; }

    /// <summary>
    /// Kết thúc game.
    /// Derived class set Winner/rank trước, rồi gọi base.EndGame(winner).
    /// </summary>
    protected virtual void EndGame(PlayerRef winner)
    {
        if (IsGameEnded) return;

        Debug.Log($"[{GetType().Name}] Game ended. Winner: {winner}");

        IsGameEnded = true;
        CurrentPhase = MinigamePhase.GameOver;

        if (HasStateAuthority && GameManager.Instance != null)
        {
            GameManager.Instance.ShowMinigameScoreboard();
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
        var players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);

        System.Array.Sort(players, (a, b) =>
            a.Object.InputAuthority.PlayerId.CompareTo(b.Object.InputAuthority.PlayerId));

        int spawnIndex = 0;
        foreach (var player in players)
        {
            var spawnPoint = spawnPoints[spawnIndex % spawnPoints.Length];
            var targetPos  = spawnPoint.position;

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

            var mgData = player.GetComponent<PlayerMinigameData>();
            if (mgData != null) mgData.ResetCheckpoint(targetPos);

            spawnIndex++;
        }

        Debug.Log($"[{GetType().Name}] Teleported {spawnIndex} players to spawn points");
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    protected void RPC_SetPlayersFrozen(bool frozen)
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
}
