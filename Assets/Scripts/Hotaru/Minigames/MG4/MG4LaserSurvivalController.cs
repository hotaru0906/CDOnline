using Fusion;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class MG4LaserSurvivalController : BaseMinigameController
{
    public new static MG4LaserSurvivalController Instance =>
        BaseMinigameController.Instance as MG4LaserSurvivalController;

    [Header("Phase Durations (seconds)")]
    [SerializeField] private float phase1Duration = 20f;
    [SerializeField] private float phase2Duration = 20f;
    [SerializeField] private float phase3Duration = 20f;
    [SerializeField] private float phase4Duration = 30f;

    [Header("Gameplay")]
    [SerializeField] private int startingLives = 3;

    [Header("Tank Spawn Per Phase")]
    [Tooltip("Số tank active mỗi batch — phase1: 1-4, phase2: 4-8, phase3: 8-12")]
    [SerializeField] private int tankCountPhase1 = 3;
    [SerializeField] private int tankCountPhase2 = 6;
    [SerializeField] private int tankCountPhase3 = 10;
    [SerializeField] private int tankCountPhase4 = 16;
    [Tooltip("Delay giữa các batch (giây)")]
    [SerializeField] private float batchDelay = 3f;

    private readonly List<PlayerRef> _eliminationOrder = new();
    private int _lastPhase = 0;
    private bool _batchRunning = false;

    private MG4Tank[] _allTanks;

    private float TotalPhaseTime =>
        phase1Duration + phase2Duration + phase3Duration + phase4Duration;

    // ----------------------------------------------------------------
    //  Lifecycle
    // ----------------------------------------------------------------

    protected override void OnGamePlayingStarted()
    {
        if (!HasStateAuthority) return;

        _eliminationOrder.Clear();
        _lastPhase = 0;
        _batchRunning = false;

        // Cache tất cả tank trong scene
        _allTanks = FindObjectsByType<MG4Tank>(FindObjectsSortMode.None);
        Debug.Log($"[MG4] Found {_allTanks.Length} tanks");

        // Đảm bảo tất cả tank đang Inactive
        foreach (var t in _allTanks)
            t.Deactivate();

        var allData = FindObjectsByType<PlayerMinigameData>(FindObjectsSortMode.None);
        foreach (var p in allData)
        {
            p.ResetForNewRound();
            p.SetLives(startingLives);
            p.OnPlayerEliminated += HandlePlayerEliminated;
        }

        MinigameHUDController.Instance?.RefreshPlayers();
        StartPhase(1);

        Debug.Log($"[MG4] Game started — {allData.Length} players, {startingLives} lives");
    }

    protected override void OnGameOver()
    {
        StopAllCoroutines();

        var allData = FindObjectsByType<PlayerMinigameData>(FindObjectsSortMode.None);
        foreach (var p in allData)
            p.OnPlayerEliminated -= HandlePlayerEliminated;

        // Deactivate tất cả tank khi game kết thúc
        if (_allTanks != null)
            foreach (var t in _allTanks)
                t.Deactivate();
    }

    // ----------------------------------------------------------------
    //  Phase
    // ----------------------------------------------------------------

    protected override void OnGameTimerChanged()
    {
        base.OnGameTimerChanged();

        if (!HasStateAuthority || IsGameEnded) return;

        float total = (_minigameData != null && _minigameData.timeLimit > 0f)
            ? _minigameData.timeLimit
            : TotalPhaseTime;

        float elapsed = Mathf.Clamp(total - GameTimer, 0f, total);

        if (elapsed < phase1Duration) StartPhase(1);
        else if (elapsed < phase1Duration + phase2Duration) StartPhase(2);
        else if (elapsed < phase1Duration + phase2Duration + phase3Duration) StartPhase(3);
        else StartPhase(4);
    }

    private void StartPhase(int phase)
    {
        if (!HasStateAuthority) return;
        if (phase == _lastPhase) return;
        _lastPhase = phase;

        Debug.Log($"[MG4] StartPhase {phase}");

        int tankCount = phase switch
        {
            1 => tankCountPhase1,
            2 => tankCountPhase2,
            3 => tankCountPhase3,
            _ => tankCountPhase4
        };

        StartCoroutine(RunTankBatchLoop(phase, tankCount));
    }

    /// <summary>
    /// Mỗi batch: chọn ngẫu nhiên tankCount tank, Activate tất cả cùng lúc,
    /// chờ batchDelay, Deactivate hết, nghỉ ngắn rồi lặp lại batch mới.
    /// </summary>
    private IEnumerator RunTankBatchLoop(int phase, int tankCount)
    {
        // Dừng batch cũ nếu có phase mới override
        _batchRunning = false;
        yield return null; // 1 frame để coroutine cũ nhận biết

        _batchRunning = true;

        if (_allTanks == null || _allTanks.Length == 0) yield break;

        int count = Mathf.Min(tankCount, _allTanks.Length);

        while (_batchRunning && !IsGameEnded)
        {
            // Deactivate tất cả trước khi chọn batch mới
            foreach (var t in _allTanks)
                t.Deactivate();

            // Shuffle để chọn ngẫu nhiên
            var shuffled = new List<MG4Tank>(_allTanks);
            for (int i = shuffled.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
            }

            // Activate đúng số tank, SetPhase trước
            for (int i = 0; i < count; i++)
            {
                shuffled[i].SetPhase(phase);
                shuffled[i].Activate(phaseDelay: 0f); // cùng lúc
            }

            Debug.Log($"[MG4] Batch: {count} tanks activated (phase {phase})");

            // Chờ batchDelay rồi đổi batch
            float elapsed = 0f;
            while (elapsed < batchDelay && _batchRunning && !IsGameEnded)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
        }
    }

    // ----------------------------------------------------------------
    //  Elimination
    // ----------------------------------------------------------------

    private void HandlePlayerEliminated(PlayerMinigameData data)
    {
        if (!HasStateAuthority) return;
        var playerRef = data.Object.InputAuthority;

        if (!_eliminationOrder.Contains(playerRef))
        {
            _eliminationOrder.Add(playerRef);
            Debug.Log($"[MG4] P{playerRef} eliminated #{_eliminationOrder.Count}");
        }

        RPC_FreezeEliminatedPlayer(playerRef);
        MinigameHUDController.Instance?.RefreshPlayers();
        UpdateAlivePlayerCount();
        CheckWinCondition();
    }

    // ----------------------------------------------------------------
    //  Win Condition
    // ----------------------------------------------------------------

    protected override void CheckWinCondition()
    {
        var allData = FindObjectsByType<PlayerMinigameData>(FindObjectsSortMode.None);
        if (allData.Length == 0) return;

        int aliveCount = 0;
        PlayerRef lastAlive = PlayerRef.None;
        foreach (var p in allData)
        {
            if (!p.IsEliminated) { aliveCount++; lastAlive = p.Object.InputAuthority; }
        }

        if (aliveCount <= 1)
        {
            if (lastAlive != PlayerRef.None && !_eliminationOrder.Contains(lastAlive))
                _eliminationOrder.Add(lastAlive);

            FinalizeRanks();
            EndGame(lastAlive);
        }
    }

    protected override void OnTimeUp()
    {
        if (!HasStateAuthority) return;

        var allData = FindObjectsByType<PlayerMinigameData>(FindObjectsSortMode.None);
        var alive = new List<PlayerMinigameData>();
        foreach (var p in allData)
            if (!p.IsEliminated) alive.Add(p);

        alive.Sort((a, b) => b.Lives.CompareTo(a.Lives));

        foreach (var p in alive)
        {
            var pRef = p.Object.InputAuthority;
            if (!_eliminationOrder.Contains(pRef))
                _eliminationOrder.Add(pRef);
        }

        FinalizeRanks();
        PlayerRef winner = _eliminationOrder.Count > 0
            ? _eliminationOrder[_eliminationOrder.Count - 1]
            : PlayerRef.None;
        EndGame(winner);
    }

    // ----------------------------------------------------------------
    //  Rank
    // ----------------------------------------------------------------

    private void FinalizeRanks()
    {
        int total = _eliminationOrder.Count;
        var allData = FindObjectsByType<PlayerMinigameData>(FindObjectsSortMode.None);

        for (int i = 0; i < _eliminationOrder.Count; i++)
        {
            int rank = total - i;
            var pRef = _eliminationOrder[i];
            foreach (var p in allData)
            {
                if (p.Object.InputAuthority == pRef) { p.SetFinished(rank, 0f); break; }
            }
        }

        ApplyHiddenScores(); // ← thêm dòng này
    }

    // protected override int[] BuildBoardRanking(PlayerRef winner)
    // {
    //     var allData = FindObjectsByType<PlayerMinigameData>(
    //         FindObjectsSortMode.None);

    //     var sorted = new List<PlayerMinigameData>(allData);

    //     //rank nho hon = xep hang cao hon
    //     sorted.Sort((a, b) =>
    //         a.FinishRank.CompareTo(b.FinishRank));

    //     var ranking = new List<int>();

    //     foreach (var p in sorted)
    //     {
    //         if (p.Object != null)
    //         {
    //             ranking.Add(
    //                 p.Object.InputAuthority.PlayerId);
    //         }
    //     }

    //     Debug.Log(
    //         "[MG4] Ranking = " +
    //         string.Join(", ", ranking));

    //     return ranking.ToArray();
    // }

    protected override void BuildScoreboardResults()
    {
        var allData = FindObjectsByType<PlayerMinigameData>(FindObjectsSortMode.None);
        var sorted = new List<PlayerMinigameData>(allData);
        sorted.Sort((a, b) => a.FinishRank.CompareTo(b.FinishRank));

        for (int i = 0; i < ScoreboardResults.Length; i++)
            ScoreboardResults.Set(i, default);

        for (int i = 0; i < sorted.Count && i < ScoreboardResults.Length; i++)
        {
            var p = sorted[i];
            ScoreboardResults.Set(i, new MinigameResultData
            {
                Player = p.Object.InputAuthority,
                Rank = p.FinishRank > 0 ? p.FinishRank : (i + 1),
                Score = p.Lives,
                FinishTime = p.FinishTime,
                IsValid = true
            });
        }
    }

    // ----------------------------------------------------------------
    //  RPC
    // ----------------------------------------------------------------

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_FreezeEliminatedPlayer(PlayerRef playerRef)
    {
        var players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        foreach (var p in players)
        {
            if (p.Object.InputAuthority != playerRef) continue;
            p.SetFrozen(true);
            break;
        }
    }
}