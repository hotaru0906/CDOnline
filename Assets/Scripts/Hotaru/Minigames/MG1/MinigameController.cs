using Fusion;
using UnityEngine;

/// <summary>
/// MG1 — Glass Bridge minigame controller.
/// Win condition: Elimination (last player standing).
/// Kế thừa BaseMinigameController — chỉ chứa logic riêng của MG1.
/// </summary>
public class MinigameController : BaseMinigameController
{
    /// <summary>
    /// Typed Instance cho MG1-specific code (VD: ScoreboardUI cần .Winner).
    /// Trả về null nếu scene hiện tại không phải MG1.
    /// </summary>
    public new static MinigameController Instance => BaseMinigameController.Instance as MinigameController;

    [Networked]
    public PlayerRef Winner { get; private set; }

    // ----------------------------------------------------------------
    //  Win Condition — Elimination (last man standing)
    // ----------------------------------------------------------------

    protected override void CheckWinCondition()
    {
        // Chỉ check elimination nếu minigame không cho respawn
        if (_minigameData != null && _minigameData.allowRespawn) return;

        if (AlivePlayerCount > 1) return;

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

    protected override void OnTimeUp()
    {
        Debug.Log("[MinigameController] Time's up!");
        EndGame(FindSurvivorByCheckpoint());
    }

    /// <summary>Tìm player còn sống có checkpoint cao nhất khi hết giờ.</summary>
    private PlayerRef FindSurvivorByCheckpoint()
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

    // ----------------------------------------------------------------
    //  PlayerFinished — MG1 chỉ có 1 winner, kết thúc ngay
    // ----------------------------------------------------------------

    public override void PlayerFinished(PlayerRef playerRef)
    {
        if (!HasStateAuthority) return;
        if (IsGameEnded) return;

        Debug.Log($"[MinigameController] Player {playerRef} finished!");
        EndGame(playerRef);
    }

    // ----------------------------------------------------------------
    //  EndGame — set Winner rồi gọi base
    // ----------------------------------------------------------------

    protected override void EndGame(PlayerRef winner)
    {
        if (IsGameEnded) return;

        Winner = winner;
        RPC_OnPlayerWon(winner);
        base.EndGame(winner);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_OnPlayerWon(PlayerRef winnerRef)
    {
        Debug.Log($"[MinigameController] Player {winnerRef} WON!");
    }

    // ----------------------------------------------------------------
    //  Scoreboard
    // ----------------------------------------------------------------

    protected override void LogScoreboardInfo()
    {
        Debug.Log("========== SCOREBOARD (MG1) ==========");

        var players = FindObjectsByType<PlayerNetworkData>(FindObjectsSortMode.None);
        foreach (var player in players)
        {
            bool isWinner = Winner != PlayerRef.None &&
                            player.Object.InputAuthority == Winner;
            string status = isWinner ? "WINNER" : "LOSER";
            Debug.Log($"[Scoreboard] {player.PlayerName}: {status}");
        }

        Debug.Log("=======================================");
    }
}
