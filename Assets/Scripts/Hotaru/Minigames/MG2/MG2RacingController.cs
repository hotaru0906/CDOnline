using Fusion;
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// MG2 — Racing minigame controller.
/// Win condition: Ranked (thứ tự về đích).
/// Kế thừa BaseMinigameController — chỉ chứa logic riêng của racing minigame.
/// 
/// Flow:
///   Player chạm MG2RacingFinishLine
///       → PlayerFinished() lưu rank + thời gian
///       → Tiếp tục cho đến khi TẤT CẢ players về đích
///       → Hoặc hết giờ → FinalizeRanks() theo DistanceProgress
/// </summary>
public class MG2RacingController : BaseMinigameController
{
    /// <summary>
    /// Typed Instance — dùng cho MG2-specific code (VD: MG2RacingFinishLine).
    /// Trả về null nếu scene hiện tại không phải MG2.
    /// </summary>
    public new static MG2RacingController Instance => BaseMinigameController.Instance as MG2RacingController;

    // ----------------------------------------------------------------
    //  Win Condition — chạy đến khi TẤT CẢ players về đích hoặc bị loại
    // ----------------------------------------------------------------

    protected override void CheckWinCondition()
    {
        var allPlayers = FindObjectsByType<PlayerMinigameData>(FindObjectsSortMode.None);
        if (allPlayers.Length == 0) return;

        // Game kết thúc khi TẤT CẢ players đã về đích hoặc bị eliminated
        foreach (var p in allPlayers)
        {
            if (!p.HasFinished && !p.IsEliminated)
                return; // còn ít nhất 1 player đang racing
        }

        Debug.Log("[MG2RacingController] All players done — ending game.");
        FinalizeRanks(); // xử lý eliminated players chưa có rank
        PlayerRef winner = _finishOrder.Count > 0 ? _finishOrder[0] : PlayerRef.None;
        EndGame(winner);
    }

    protected override void OnTimeUp()
    {
        Debug.Log("[MG2RacingController] Time's up! Assigning remaining ranks...");
        FinalizeRanks();
        PlayerRef winner = _finishOrder.Count > 0 ? _finishOrder[0] : PlayerRef.None;
        EndGame(winner);
    }

    // ----------------------------------------------------------------
    //  PlayerFinished — Racing: nhiều player có thể về đích theo thứ tự
    // ----------------------------------------------------------------

    public override void PlayerFinished(PlayerRef playerRef)
    {
        if (!HasStateAuthority) return;
        if (IsGameEnded) return;
        if (_finishOrder.Contains(playerRef)) return; // đã finish rồi

        _finishOrder.Add(playerRef);
        int rank = _finishOrder.Count;
        float elapsed = (_minigameData != null && _minigameData.timeLimit > 0f)
            ? _minigameData.timeLimit - GameTimer
            : 0f;

        // Ghi nhận vào PlayerMinigameData
        var allPlayers = FindObjectsByType<PlayerMinigameData>(FindObjectsSortMode.None);
        foreach (var p in allPlayers)
        {
            if (p.Object.InputAuthority == playerRef)
            {
                p.SetFinished(rank, elapsed);
                break;
            }
        }

        OnPlayerFinished(playerRef);
        RPC_OnPlayerRanked(playerRef, rank);
        Debug.Log($"[MG2RacingController] Player {playerRef} finished — Rank: {rank}, Time: {elapsed:F2}s");

        // Check nếu tất cả players non-eliminated đã về đích
        int activePlayers = 0;
        foreach (var p in allPlayers)
            if (!p.IsEliminated) activePlayers++;

        if (_finishOrder.Count >= activePlayers)
        {
            Debug.Log("[MG2RacingController] All active players finished!");
            FinalizeRanks(); // rank nốt những ai còn IsEliminated=true chưa có rank
            EndGame(_finishOrder[0]);
        }
    }

    // ----------------------------------------------------------------
    //  FinalizeRanks — assign rank cho tất cả players chưa về đích
    //  (timeout hoặc eliminated) theo DistanceProgress giảm dần
    // ----------------------------------------------------------------

    private void FinalizeRanks()
    {
        var allPlayers = FindObjectsByType<PlayerMinigameData>(FindObjectsSortMode.None);

        var unfinished = new List<PlayerMinigameData>();
        foreach (var p in allPlayers)
            if (!p.HasFinished) unfinished.Add(p);

        if (unfinished.Count == 0) return;

        // Sắp xếp: DistanceProgress cao hơn = gần đích hơn = rank tốt hơn
        unfinished.Sort((a, b) => b.DistanceProgress.CompareTo(a.DistanceProgress));

        int nextRank = _finishOrder.Count + 1;
        foreach (var p in unfinished)
        {
            p.SetFinished(nextRank, 0f); // finishTime = 0 → DNF
            _finishOrder.Add(p.Object.InputAuthority);
            Debug.Log($"[MG2RacingController] DNF rank {nextRank} → Player {p.Object.InputAuthority} (progress: {p.DistanceProgress:F0})");
            nextRank++;
        }
    }

    // ----------------------------------------------------------------
    //  RPC
    // ----------------------------------------------------------------

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_OnPlayerRanked(PlayerRef playerRef, int rank)
    {
        string[] ordinals = { "1st", "2nd", "3rd" };
        string rankStr = rank <= 3 ? ordinals[rank - 1] : $"{rank}th";
        Debug.Log($"[MG2RacingController] Player {playerRef}: {rankStr}!");
        // TODO Phase 5 (UI): Hiện rank badge UI cho player tương ứng
    }

    // ----------------------------------------------------------------
    //  Scoreboard
    // ----------------------------------------------------------------

    protected override void BuildScoreboardResults()
    {
        var players = FindObjectsByType<PlayerMinigameData>(FindObjectsSortMode.None);
        var sorted = new List<PlayerMinigameData>(players);

        sorted.Sort((a, b) =>
        {
            if (a.FinishRank == 0 && b.FinishRank == 0)
                return b.DistanceProgress.CompareTo(a.DistanceProgress);
            if (a.FinishRank == 0) return 1;
            if (b.FinishRank == 0) return -1;
            return a.FinishRank.CompareTo(b.FinishRank);
        });

        // Clear toàn bộ array trước
        for (int i = 0; i < ScoreboardResults.Length; i++)
            ScoreboardResults.Set(i, default);

        for (int i = 0; i < sorted.Count && i < ScoreboardResults.Length; i++)
        {
            var p = sorted[i];
            ScoreboardResults.Set(i, new MinigameResultData
            {
                Player      = p.Object.InputAuthority,
                Rank        = p.FinishRank > 0 ? p.FinishRank : (i + 1),
                FinishTime  = p.FinishTime,
                Score       = p.Score,
                IsValid     = true
            });
        }

        Debug.Log($"[MG2RacingController] ScoreboardResults built — {sorted.Count} entries.");
    }

    protected override void LogScoreboardInfo()
    {
        Debug.Log("========== SCOREBOARD (MG2 Racing) ==========");

        var players = FindObjectsByType<PlayerMinigameData>(FindObjectsSortMode.None);
        var sorted = new List<PlayerMinigameData>(players);

        sorted.Sort((a, b) =>
        {
            // FinishRank = 0 không nên xảy ra sau FinalizeRanks, fallback theo DistanceProgress
            if (a.FinishRank == 0 && b.FinishRank == 0)
                return b.DistanceProgress.CompareTo(a.DistanceProgress);
            if (a.FinishRank == 0) return 1;
            if (b.FinishRank == 0) return -1;
            return a.FinishRank.CompareTo(b.FinishRank);
        });

        foreach (var p in sorted)
        {
            var netData = p.GetComponent<PlayerNetworkData>();
            string name = netData != null
                ? netData.PlayerName.ToString()
                : $"Player {p.Object.InputAuthority.PlayerId}";
            string timeStr = p.FinishTime > 0f ? $"{p.FinishTime:F2}s" : "DNF";
            Debug.Log($"[Scoreboard] #{p.FinishRank}: {name} — {timeStr}");
        }

        Debug.Log("==============================================");
    }
}
