using Fusion;
using UnityEngine;
using System.Collections.Generic;
public class MG2RacingController : BaseMinigameController
{
    private bool _isSpectating = false;
    private int _spectateIndex = 0;
    private List<PlayerController> _activePlayers = new();
    public new static MG2RacingController Instance => BaseMinigameController.Instance as MG2RacingController;


    private void Update()
    {
        if (!_isSpectating) return;

        if (Input.GetMouseButtonDown(0))
            CycleSpectateTarget();
    }

    private void CycleSpectateTarget()
    {
        // Rebuild danh sách player còn đang racing
        _activePlayers.Clear();
        var allData = FindObjectsByType<PlayerMinigameData>(FindObjectsSortMode.None);
        foreach (var data in allData)
        {
            if (data.HasFinished || data.IsEliminated) continue;
            var pc = data.GetComponent<PlayerController>();
            if (pc != null) _activePlayers.Add(pc);
        }

        if (_activePlayers.Count == 0) return;

        _spectateIndex = (_spectateIndex + 1) % _activePlayers.Count;

        var target = _activePlayers[_spectateIndex];
        if (CameraManager.Instance != null)
        {
            CameraManager.Instance.UpdatePlayerTarget(target.transform);
            CameraManager.Instance.SwitchToThirdPersonCamera();
        }

        Debug.Log($"[Spectate] Camera → P{_activePlayers[_spectateIndex].Object.InputAuthority}");
    }
    // ----------------------------------------------------------------
    //  Win Condition — chạy đến khi TẤT CẢ players về đích hoặc bị loại
    // ----------------------------------------------------------------

    protected override void CheckWinCondition()
    {
        var allPlayers = FindObjectsByType<PlayerMinigameData>(FindObjectsSortMode.None);
        if (allPlayers.Length == 0) return;

        int totalPlayers = allPlayers.Length;
        int doneCount = 0;

        // Đếm số player đã về đích hoặc bị eliminated
        foreach (var p in allPlayers)
        {
            if (p.HasFinished || p.IsEliminated)
                doneCount++;
        }

        // End game khi N-1 players done (không cần đợi player cuối cùng)
        // Ví dụ: 4 players → end khi 3 player về đích/loại
        if (doneCount >= totalPlayers - 1)
        {
            Debug.Log($"[MG2RacingController] {doneCount}/{totalPlayers} players done — ending game.");
            FinalizeRanks(); // xử lý eliminated players chưa có rank
            PlayerRef winner = _finishOrder.Count > 0 ? _finishOrder[0] : PlayerRef.None;
            EndGame(winner);
        }
    }

    protected override void OnTimeUp()
    {
        Debug.Log("[MG2RacingController] Time's up! Assigning remaining ranks...");
        FinalizeRanks();
        PlayerRef winner = _finishOrder.Count > 0 ? _finishOrder[0] : PlayerRef.None;
        EndGame(winner);
    }
    protected override void OnGameOver()
    {
        _isSpectating = false;
    }

    // ----------------------------------------------------------------
    //  PlayerFinished — Racing: nhiều player có thể về đích theo thứ tự
    // ----------------------------------------------------------------

    public override void PlayerFinished(PlayerRef playerRef)
    {
        if (!HasStateAuthority) return;
        if (IsGameEnded) return;
        if (_finishOrder.Contains(playerRef)) return;

        _finishOrder.Add(playerRef);
        int rank = _finishOrder.Count;
        float elapsed = (_minigameData != null && _minigameData.timeLimit > 0f)
            ? _minigameData.timeLimit - GameTimer
            : 0f;

        var allPlayers = FindObjectsByType<PlayerMinigameData>(FindObjectsSortMode.None);
        foreach (var p in allPlayers)
        {
            if (p.Object.InputAuthority == playerRef)
            {
                p.SetFinished(rank, elapsed);
                break;
            }
        }

        // Sync freeze xuống tất cả clients
        RPC_FreezeFinishedPlayer(playerRef);

        RPC_ShowFinishUI(playerRef);
        OnPlayerFinished(playerRef);
        RPC_OnPlayerRanked(playerRef, rank);

        Debug.Log($"[MG2RacingController] Player {playerRef} finished — Rank: {rank}");

        CheckWinCondition();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_FreezeFinishedPlayer(PlayerRef playerRef)
    {
        var players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        foreach (var p in players)
        {
            if (p.Object.InputAuthority != playerRef) continue;

            p.SetFrozen(true);

            // Nếu đây là local player → chuyển camera sang player khác
            if (Runner.LocalPlayer == playerRef)
                SwitchCameraToActivePlayer();

            break;
        }
    }

    private void SwitchCameraToActivePlayer()
    {
        _isSpectating = true;
        _spectateIndex = 0;
        _activePlayers.Clear();

        var allData = FindObjectsByType<PlayerMinigameData>(FindObjectsSortMode.None);
        foreach (var data in allData)
        {
            if (data.HasFinished || data.IsEliminated) continue;
            if (data.Object.InputAuthority == Runner.LocalPlayer) continue;
            var pc = data.GetComponent<PlayerController>();
            if (pc != null) _activePlayers.Add(pc);
        }

        if (_activePlayers.Count == 0) return;

        var target = _activePlayers[0];
        if (CameraManager.Instance != null)
        {
            CameraManager.Instance.UpdatePlayerTarget(target.transform);
            CameraManager.Instance.SwitchToThirdPersonCamera();
        }

        Debug.Log($"[Spectate] Started — Camera → P{target.Object.InputAuthority}");
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
        ApplyHiddenScores();
    }

    // ----------------------------------------------------------------
    //  RPC
    // ----------------------------------------------------------------

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_OnPlayerRanked(PlayerRef playerRef, int rank)
    {
        string[] ordinals = { "1st", "2nd", "3rd" };

        string rankStr = rank <= 3
            ? ordinals[rank - 1]
            : $"{rank}th";

        Debug.Log($"[MG2RacingController] Player {playerRef}: {rankStr}!");
        // TODO Phase 5 (UI): Hiện rank badge UI cho player tương ứng
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ShowFinishUI(PlayerRef playerRef)
    {
        if (Runner.LocalPlayer == playerRef)
        {
            FinishUI.Instance?.ShowFinish();
        }
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
    return b.FinishRank.CompareTo(a.FinishRank);  // ← Đảo từ 'a' sang 'b' để sắp xếp đúng
});

        // Clear toàn bộ array trước
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

        Debug.Log($"[MG2RacingController] ScoreboardResults built — {sorted.Count} entries.");
    }

    // protected override int[] BuildBoardRanking(PlayerRef winner)
    // {
    //     if (_finishOrder != null && _finishOrder.Count > 0)
    //     {
    //         var ranking = new int[_finishOrder.Count];
    //         for (int i = 0; i < _finishOrder.Count; i++)
    //             ranking[i] = _finishOrder[i].PlayerId;
    //         return ranking;
    //     }

    //     return base.BuildBoardRanking(winner);
    // }

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

    private PlayerMinigameData GetNextActivePlayer(PlayerRef finishedPlayer)
    {
        var players =
            FindObjectsByType<PlayerMinigameData>(FindObjectsSortMode.None);

        foreach (var p in players)
        {
            if (p.Object.InputAuthority == finishedPlayer)
                continue;

            if (!p.HasFinished && !p.IsEliminated)
                return p;
        }

        return null;
    }
}
