using Fusion;
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// MG4 — Laser Survival minigame controller.
/// - Mỗi player có 3 mạng (startingLives).
/// - Mất mạng: bất tử 3s, không respawn.
/// - Hết mạng: eliminated vĩnh viễn.
/// - Win: sống sót cuối cùng hoặc hết giờ còn nhiều mạng nhất.
/// </summary>
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

    private readonly List<PlayerRef> _eliminationOrder = new();

    private float TotalPhaseTime =>
        phase1Duration + phase2Duration + phase3Duration + phase4Duration;

    // ----------------------------------------------------------------
    //  Lifecycle
    // ----------------------------------------------------------------

    protected override void OnGamePlayingStarted()
    {
        if (!HasStateAuthority) return;

        _eliminationOrder.Clear();

        var allData = FindObjectsByType<PlayerMinigameData>(FindObjectsSortMode.None);
        foreach (var p in allData)
        {
            p.ResetForNewRound(); // reset IsEliminated, IsDead, IsInvincible...
            p.SetLives(startingLives);
            p.OnPlayerEliminated += HandlePlayerEliminated;
        }

        MinigameHUDController.Instance?.RefreshPlayers();

        StartPhase(1);

        Debug.Log($"[MG4LaserSurvival] Game started — {allData.Length} players, {startingLives} lives each");
    }

    protected override void OnGameOver()
    {
        var allData = FindObjectsByType<PlayerMinigameData>(FindObjectsSortMode.None);
        foreach (var p in allData)
            p.OnPlayerEliminated -= HandlePlayerEliminated;
    }

    // ----------------------------------------------------------------
    //  Phase / Timer hooks
    // ----------------------------------------------------------------

    protected override void OnGameTimerChanged()
    {
        MinigameHUDController.Instance?.SetTime(GameTimer);

        if (!HasStateAuthority || IsGameEnded) return;

        float total = (_minigameData != null && _minigameData.timeLimit > 0f)
            ? _minigameData.timeLimit
            : TotalPhaseTime;

        float elapsed = Mathf.Clamp(total - GameTimer, 0f, total);

        if (elapsed < phase1Duration)
            StartPhase(1);
        else if (elapsed < phase1Duration + phase2Duration)
            StartPhase(2);
        else if (elapsed < phase1Duration + phase2Duration + phase3Duration)
            StartPhase(3);
        else
            StartPhase(4);
    }

    private int _lastPhase = 0;

    private void StartPhase(int phase)
    {
        if (!HasStateAuthority) return;
        if (phase == _lastPhase) return;
        _lastPhase = phase;

        Debug.Log($"[MG4LaserSurvival] StartPhase {phase}");

        var tanks = FindObjectsByType<MG4Tank>(FindObjectsSortMode.None);
        foreach (var t in tanks)
            t.SetPhase(phase);
    }

    // ----------------------------------------------------------------
    //  Elimination handling
    // ----------------------------------------------------------------

    private void HandlePlayerEliminated(PlayerMinigameData data)
    {
        if (!HasStateAuthority) return;
        var playerRef = data.Object.InputAuthority;

        if (!_eliminationOrder.Contains(playerRef))
        {
            _eliminationOrder.Add(playerRef);
            Debug.Log($"[MG4LaserSurvival] P{playerRef} eliminated — #{_eliminationOrder.Count} out");
        }

        RPC_FreezeEliminatedPlayer(playerRef);

        MinigameHUDController.Instance?.RefreshPlayers();
        UpdateAlivePlayerCount();
        CheckWinCondition();
    }

    // ----------------------------------------------------------------
    //  Win condition & time up
    // ----------------------------------------------------------------

    protected override void CheckWinCondition()
    {
        var allData = FindObjectsByType<PlayerMinigameData>(FindObjectsSortMode.None);
        if (allData.Length == 0) return;

        int aliveCount = 0;
        PlayerRef lastAlive = PlayerRef.None;
        foreach (var p in allData)
        {
            if (!p.IsEliminated)
            {
                aliveCount++;
                lastAlive = p.Object.InputAuthority;
            }
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

        Debug.Log("[MG4LaserSurvival] Time's up!");

        var allData = FindObjectsByType<PlayerMinigameData>(FindObjectsSortMode.None);
        var alive = new List<PlayerMinigameData>();
        foreach (var p in allData)
            if (!p.IsEliminated) alive.Add(p);

        // Sort theo lives giảm dần — nhiều mạng = thắng
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
    //  Rank finalization
    // ----------------------------------------------------------------

    private void FinalizeRanks()
    {
        int total = _eliminationOrder.Count;
        var allData = FindObjectsByType<PlayerMinigameData>(FindObjectsSortMode.None);

        for (int i = 0; i < _eliminationOrder.Count; i++)
        {
            int rank = total - i; // last element (winner) → rank 1
            var pRef = _eliminationOrder[i];

            foreach (var p in allData)
            {
                if (p.Object.InputAuthority == pRef)
                {
                    p.SetFinished(rank, 0f);
                    break;
                }
            }
        }
    }

    protected override int[] BuildBoardRanking(PlayerRef winner)
    {
        var ranking = new List<int>();
        for (int i = _eliminationOrder.Count - 1; i >= 0; i--)
            ranking.Add(_eliminationOrder[i].PlayerId);
        return ranking.ToArray();
    }

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