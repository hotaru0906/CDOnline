using Fusion;
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// MG1 — Glass Bridge minigame controller.
/// Win condition: Ai về đích sớm nhất (Racing style).
/// Kết thúc khi TẤT CẢ players về đích hoặc hết giờ.
/// </summary>
public class MinigameController : BaseMinigameController
{
    public new static MinigameController Instance => BaseMinigameController.Instance as MinigameController;

    [Networked]
    public PlayerRef Winner { get; private set; }

    // ----------------------------------------------------------------
    //  Win Condition — kết thúc khi tất cả done
    // ----------------------------------------------------------------

    protected override void CheckWinCondition()
    {
        var allPlayers = FindObjectsByType<PlayerMinigameData>(FindObjectsSortMode.None);
        if (allPlayers.Length == 0) return;

        foreach (var p in allPlayers)
        {
            if (!p.HasFinished && !p.IsEliminated)
                return; // còn ít nhất 1 player chưa xong
        }

        Debug.Log("[MinigameController] All players done — ending game.");
        FinalizeRemainingByCheckpoint(); // xử lý ai chưa có rank (trường hợp eliminated)
        PlayerRef winner = _finishOrder.Count > 0 ? _finishOrder[0] : PlayerRef.None;
        EndGame(winner);
    }

    protected override void OnTimeUp()
    {
        Debug.Log("[MinigameController] Time's up!");
        FinalizeRemainingByCheckpoint();
        PlayerRef winner = _finishOrder.Count > 0 ? _finishOrder[0] : PlayerRef.None;
        EndGame(winner);
    }

    // ----------------------------------------------------------------
    //  PlayerFinished — ghi nhận thứ tự, KHÔNG EndGame ngay
    // ----------------------------------------------------------------

    public override void PlayerFinished(PlayerRef playerRef)
    {
        if (!HasStateAuthority) return;
        if (IsGameEnded) return;
        if (_finishOrder.Contains(playerRef)) return;

        _finishOrder.Add(playerRef);
        int rank = _finishOrder.Count; // rank 1, 2, 3...
        float elapsed = (_minigameData != null && _minigameData.timeLimit > 0f)
            ? _minigameData.timeLimit - GameTimer
            : 0f;

        // Gán FinishRank cho player
        var allPlayers = FindObjectsByType<PlayerMinigameData>(FindObjectsSortMode.None);
        foreach (var p in allPlayers)
        {
            if (p.Object.InputAuthority == playerRef)
            {
                p.SetFinished(rank, elapsed);
                break;
            }
        }

        RPC_FreezeFinishedPlayer(playerRef);
        RPC_ShowFinishUI(playerRef);

        Debug.Log($"[MinigameController] Player {playerRef} finished — Rank {rank}");

        CheckWinCondition();
    }

    // ----------------------------------------------------------------
    //  FinalizeRemainingByCheckpoint — timeout hoặc eliminated
    // ----------------------------------------------------------------

    private void FinalizeRemainingByCheckpoint()
    {
        var allPlayers = FindObjectsByType<PlayerMinigameData>(FindObjectsSortMode.None);

        var unfinished = new List<PlayerMinigameData>();
        foreach (var p in allPlayers)
            if (!p.HasFinished) unfinished.Add(p);

        // Checkpoint cao hơn = gần đích hơn = rank tốt hơn
        unfinished.Sort((a, b) => b.CurrentCheckpointIndex.CompareTo(a.CurrentCheckpointIndex));

        int nextRank = _finishOrder.Count + 1;
        foreach (var p in unfinished)
        {
            p.SetFinished(nextRank, 0f);
            _finishOrder.Add(p.Object.InputAuthority);
            Debug.Log($"[MinigameController] Timeout rank {nextRank} → P{p.Object.InputAuthority}");
            nextRank++;
        }
    }

    // ----------------------------------------------------------------
    //  BuildBoardRanking — dùng _finishOrder như MG2
    // ----------------------------------------------------------------

    protected override int[] BuildBoardRanking(PlayerRef winner)
    {
        if (_finishOrder != null && _finishOrder.Count > 0)
        {
            var ranking = new int[_finishOrder.Count];
            for (int i = 0; i < _finishOrder.Count; i++)
                ranking[i] = _finishOrder[i].PlayerId;

            Debug.Log($"[MinigameController] BoardRanking: [{string.Join(", ", ranking)}]");
            return ranking;
        }

        return base.BuildBoardRanking(winner);
    }

    // ----------------------------------------------------------------
    //  EndGame
    // ----------------------------------------------------------------

    protected override void EndGame(PlayerRef winner)
    {
        if (IsGameEnded) return;

        Winner = winner;
        RPC_OnPlayerWon(winner);
        base.EndGame(winner);
    }

    // ----------------------------------------------------------------
    //  RPCs
    // ----------------------------------------------------------------

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_FreezeFinishedPlayer(PlayerRef playerRef)
    {
        var players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        foreach (var p in players)
        {
            if (p.Object.InputAuthority != playerRef) continue;
            p.SetFrozen(true);
            break;
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ShowFinishUI(PlayerRef playerRef)
    {
        if (Runner.LocalPlayer == playerRef)
            FinishUI.Instance?.ShowFinish();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_OnPlayerWon(PlayerRef winnerRef)
    {
        Debug.Log($"[MinigameController] Player {winnerRef} WON!");
    }

    // ----------------------------------------------------------------
    //  Scoreboard
    // ----------------------------------------------------------------

    protected override void BuildScoreboardResults()
    {
        var players = FindObjectsByType<PlayerMinigameData>(FindObjectsSortMode.None);
        var sorted = new List<PlayerMinigameData>(players);
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
                FinishTime = p.FinishTime,
                Score = p.Score,
                IsValid = true
            });
        }
    }

    protected override void LogScoreboardInfo()
    {
        Debug.Log("========== SCOREBOARD (MG1 Glass Bridge) ==========");
        var players = FindObjectsByType<PlayerMinigameData>(FindObjectsSortMode.None);
        var sorted = new List<PlayerMinigameData>(players);
        sorted.Sort((a, b) => a.FinishRank.CompareTo(b.FinishRank));

        foreach (var p in sorted)
        {
            var netData = p.GetComponent<PlayerNetworkData>();
            string name = netData != null ? netData.PlayerName.ToString() : $"P{p.Object.InputAuthority.PlayerId}";
            string timeStr = p.FinishTime > 0f ? $"{p.FinishTime:F2}s" : "DNF";
            Debug.Log($"[Scoreboard] #{p.FinishRank}: {name} — {timeStr}");
        }
        Debug.Log("====================================================");
    }
}