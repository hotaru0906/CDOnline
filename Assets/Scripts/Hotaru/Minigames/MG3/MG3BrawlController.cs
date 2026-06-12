using Fusion;
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// MG3 Brawl — Last player standing wins.
/// 
/// Rules:
///   - Mỗi player có Lives (3-5 mạng)
///   - Không có item: tấn công chỉ stun (knockback)
///   - Có item trên tay: tấn công gây -1 Lives + drop item
///   - Hết mạng: bị loại (IsEliminated)
///   - Win condition: còn 1 player sống
///   - Rank: sống lâu nhất = 1st, chết sớm nhất = 4th
///
/// SETUP trong scene:
///   1. Attach script này vào NetworkObject trong MG3 scene
///   2. Assign spawnPoints (4 điểm)
///   3. Attach MG3ItemSpawner vào scene riêng
///   4. Attach MG3PlayerBrawlData vào player prefab
/// </summary>
public class MG3BrawlController : BaseMinigameController
{
    public new static MG3BrawlController Instance =>
        BaseMinigameController.Instance as MG3BrawlController;

    [Header("Brawl Settings")]
    [SerializeField] private int startingLives = 3;

    // Thứ tự bị loại — chết trước = rank thấp hơn
    // eliminationOrder[0] = player bị loại đầu tiên (rank 4th nếu 4 players)
    private readonly List<PlayerRef> _eliminationOrder = new();

    // ----------------------------------------------------------------
    //  Setup
    // ----------------------------------------------------------------

    protected override void OnGamePlayingStarted()
    {
        if (!HasStateAuthority) return;

        // Set lives cho tất cả players
        var allData = FindObjectsByType<PlayerMinigameData>(FindObjectsSortMode.None);
        foreach (var p in allData)
            p.SetLives(startingLives);

        // Subscribe elimination event
        foreach (var p in allData)
            p.OnPlayerEliminated += HandlePlayerEliminated;

        Debug.Log($"[MG3BrawlController] Game started — {allData.Length} players, {startingLives} lives each");
    }

    protected override void OnGameOver()
    {
        // Unsubscribe events
        var allData = FindObjectsByType<PlayerMinigameData>(FindObjectsSortMode.None);
        foreach (var p in allData)
            p.OnPlayerEliminated -= HandlePlayerEliminated;
    }

    // ----------------------------------------------------------------
    //  Hit Logic — gọi từ CheckAttackHit override
    // ----------------------------------------------------------------

    /// <summary>
    /// Gọi khi attacker tấn công trúng target.
    /// Nếu attacker có item → -1 lives target + drop item.
    /// Nếu không có item → chỉ knockback (xử lý bởi PlayerController).
    /// </summary>
    public void OnPlayerHit(PlayerController attacker, PlayerController target)
    {
        if (!HasStateAuthority) return;
        if (!IsGameStarted || IsGameEnded) return;

        var targetMgData = target.GetComponent<PlayerMinigameData>();
        if (targetMgData == null || !targetMgData.CanTakeDamage()) return;

        var attackerBrawl = attacker.GetComponent<MG3PlayerBrawlData>();
        var targetBrawl   = target.GetComponent<MG3PlayerBrawlData>();

        if (attackerBrawl != null && attackerBrawl.HasItem)
        {
            // Có item → gây sát thương thật
            attackerBrawl.DropItem();
            targetMgData.LoseLife();

            // Nếu target đang có item → drop item của target
            if (targetBrawl != null && targetBrawl.HasItem)
                targetBrawl.DropItem();

            RPC_OnHitWithItem(
                attacker.Object.InputAuthority,
                target.Object.InputAuthority
            );

            Debug.Log($"[MG3Brawl] P{attacker.Object.InputAuthority} HIT P{target.Object.InputAuthority} with item!");
        }
        else
        {
            // Không có item → chỉ knockback, không mất mạng
            Debug.Log($"[MG3Brawl] P{attacker.Object.InputAuthority} stunned P{target.Object.InputAuthority}");
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
            Debug.Log($"[MG3Brawl] P{playerRef} eliminated — #{_eliminationOrder.Count} out");
        }

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
            if (!p.IsEliminated)
            {
                aliveCount++;
                lastAlive = p.Object.InputAuthority;
            }
        }

        if (aliveCount <= 1)
        {
            // Thêm người cuối vào elimination order (họ thắng)
            if (lastAlive != PlayerRef.None && !_eliminationOrder.Contains(lastAlive))
                _eliminationOrder.Add(lastAlive);

            FinalizeRanks();
            EndGame(lastAlive);
        }
    }

    protected override void OnTimeUp()
    {
        // Hết giờ — ai còn nhiều mạng nhất thắng
        Debug.Log("[MG3BrawlController] Time's up!");

        // Sort player còn sống theo số mạng giảm dần
        var allData = FindObjectsByType<PlayerMinigameData>(FindObjectsSortMode.None);
        var alive = new List<PlayerMinigameData>();
        foreach (var p in allData)
            if (!p.IsEliminated) alive.Add(p);

        alive.Sort((a, b) => b.Lives.CompareTo(a.Lives));

        // Thêm người còn sống vào elimination order (nhiều mạng nhất = cuối cùng = rank cao nhất)
        foreach (var p in alive)
        {
            var pRef = p.Object.InputAuthority;
            if (!_eliminationOrder.Contains(pRef))
                _eliminationOrder.Add(pRef);
        }

        PlayerRef winner = alive.Count > 0
            ? alive[0].Object.InputAuthority
            : PlayerRef.None;

        FinalizeRanks();
        EndGame(winner);
    }

    // ----------------------------------------------------------------
    //  Rank — elimination order ngược lại = rank
    // ----------------------------------------------------------------

    private void FinalizeRanks()
    {
        // eliminationOrder: index 0 = chết đầu tiên = rank cuối
        // Rank 1 = người cuối cùng trong list (sống lâu nhất)
        int total = _eliminationOrder.Count;

        var allData = FindObjectsByType<PlayerMinigameData>(FindObjectsSortMode.None);

        for (int i = 0; i < _eliminationOrder.Count; i++)
        {
            int rank = total - i; // index 0 → rank cao nhất (last alive), index cuối → rank 1 thấp nhất

            // Đảo ngược: người chết đầu tiên = rank cao nhất số (4th)
            // người sống cuối = rank 1
            rank = i + 1; // index 0 (chết đầu) = rank 4, index cuối (sống lâu) = rank 1
            rank = total - i; // 1st = sống lâu nhất

            foreach (var p in allData)
            {
                if (p.Object.InputAuthority == _eliminationOrder[i])
                {
                    p.SetFinished(total - i, 0f);
                    break;
                }
            }
        }
    }

    protected override int[] BuildBoardRanking(PlayerRef winner)
    {
        // Rank board: người thắng đi trước
        var ranking = new List<int>();

        // eliminationOrder cuối cùng = winner (rank 1)
        // Đảo ngược để rank 1 lên đầu
        for (int i = _eliminationOrder.Count - 1; i >= 0; i--)
            ranking.Add(_eliminationOrder[i].PlayerId);

        return ranking.ToArray();
    }

    // ----------------------------------------------------------------
    //  Scoreboard
    // ----------------------------------------------------------------

    protected override void BuildScoreboardResults()
    {
        var allData = FindObjectsByType<PlayerMinigameData>(FindObjectsSortMode.None);
        var sorted  = new List<PlayerMinigameData>(allData);

        sorted.Sort((a, b) => a.FinishRank.CompareTo(b.FinishRank));

        for (int i = 0; i < ScoreboardResults.Length; i++)
            ScoreboardResults.Set(i, default);

        for (int i = 0; i < sorted.Count && i < ScoreboardResults.Length; i++)
        {
            var p = sorted[i];
            ScoreboardResults.Set(i, new MinigameResultData
            {
                Player    = p.Object.InputAuthority,
                Rank      = p.FinishRank > 0 ? p.FinishRank : (i + 1),
                Score     = p.Lives,
                IsValid   = true
            });
        }
    }

    protected override void LogScoreboardInfo()
    {
        Debug.Log("========== SCOREBOARD (MG3 Brawl) ==========");
        var allData = FindObjectsByType<PlayerMinigameData>(FindObjectsSortMode.None);
        var sorted  = new List<PlayerMinigameData>(allData);
        sorted.Sort((a, b) => a.FinishRank.CompareTo(b.FinishRank));

        foreach (var p in sorted)
        {
            var netData = p.GetComponent<PlayerNetworkData>();
            string name = netData != null
                ? netData.PlayerName.ToString()
                : $"P{p.Object.InputAuthority.PlayerId}";
            Debug.Log($"[Scoreboard] #{p.FinishRank}: {name} — {p.Lives} lives remaining");
        }
        Debug.Log("=============================================");
    }

    // ----------------------------------------------------------------
    //  RPC
    // ----------------------------------------------------------------

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_OnHitWithItem(PlayerRef attackerId, PlayerRef targetId)
    {
        Debug.Log($"[MG3Brawl] P{attackerId} dealt damage to P{targetId}!");
        // TODO: play hit VFX / sound ở đây
    }
}