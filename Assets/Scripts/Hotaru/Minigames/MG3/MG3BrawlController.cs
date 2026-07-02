using Fusion;
using UnityEngine;
using System.Collections.Generic;
public class MG3BrawlController : BaseMinigameController
{
    public new static MG3BrawlController Instance =>
        BaseMinigameController.Instance as MG3BrawlController;
    private readonly List<PlayerRef> _eliminationOrder = new();

    #region Overrides
    protected override void OnGamePlayingStarted()
    {
        if (!HasStateAuthority) return;

        var allData = FindObjectsByType<PlayerMinigameData>(FindObjectsSortMode.None);
        foreach (var p in allData)
            p.SetHP(100);  // ← đổi từ SetLives(startingLives)

        foreach (var p in allData)
            p.OnPlayerEliminated += HandlePlayerEliminated;
        MinigameHUDController.Instance?.RefreshPlayers();
        Debug.Log($"[MG3BrawlController] Game started — {allData.Length} players, 100 HP each");
    }

    protected override void OnGameOver()
    {
        // Unsubscribe events
        var allData = FindObjectsByType<PlayerMinigameData>(FindObjectsSortMode.None);
        foreach (var p in allData)
            p.OnPlayerEliminated -= HandlePlayerEliminated;

        // MG3 rule: once picked up, item is kept for the whole match.
        // Only clear item-in-hand visuals/state when minigame ends.
        if (HasStateAuthority)
        {
            var allBrawlData = FindObjectsByType<MG3PlayerBrawlData>(FindObjectsSortMode.None);
            foreach (var brawlData in allBrawlData)
                brawlData.DropItem();
        }
    }

    #endregion
    #region Hit Logic
    public void OnPlayerHit(PlayerController attacker, PlayerController target)
    {
        if (!HasStateAuthority) return;
        if (!IsGameStarted || IsGameEnded) return;

        var targetMgData = target.GetComponent<PlayerMinigameData>();
        if (targetMgData == null || !targetMgData.CanTakeDamage()) return;

        var attackerBrawl = attacker.GetComponent<MG3PlayerBrawlData>();

        if (attackerBrawl != null && attackerBrawl.HasItem)
        {
            targetMgData.TakeDamage(20);

            RPC_OnHitWithItem(
                attacker.Object.InputAuthority,
                target.Object.InputAuthority
            );
            Debug.Log($"[MG3Brawl] P{attacker.Object.InputAuthority} HIT P{target.Object.InputAuthority} — 20 damage!");
        }
        else
        {
            // Không có item → chỉ knockback, không mất mạng
            Debug.Log($"[MG3Brawl] P{attacker.Object.InputAuthority} stunned P{target.Object.InputAuthority}");
        }
    }
    #endregion
    #region Elimination & Win Condition

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

    protected override void OnGameTimerChanged()
    {
        base.OnGameTimerChanged();
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

        alive.Sort((a, b) => b.HP.CompareTo(a.HP));

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
    private void FinalizeRanks()
    {
        int total = _eliminationOrder.Count;
        var allData = FindObjectsByType<PlayerMinigameData>(FindObjectsSortMode.None);

        for (int i = 0; i < _eliminationOrder.Count; i++)
        {
            // eliminationOrder[0] = chết đầu tiên = rank cao nhất số = rank total (VD: 4th)
            // eliminationOrder[last] = sống lâu nhất = rank 1
            int rank = total - i;

            foreach (var p in allData)
            {
                if (p.Object.InputAuthority == _eliminationOrder[i])
                {
                    p.SetFinished(rank, 0f);
                    break;
                }
            }
        }

        ApplyHiddenScores(); // ← thêm dòng này
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
                Score = p.HP,
                IsValid = true
            });
        }
    }

    protected override void LogScoreboardInfo()
    {
        Debug.Log("========== SCOREBOARD (MG3 Brawl) ==========");
        var allData = FindObjectsByType<PlayerMinigameData>(FindObjectsSortMode.None);
        var sorted = new List<PlayerMinigameData>(allData);
        sorted.Sort((a, b) => a.FinishRank.CompareTo(b.FinishRank));

        foreach (var p in sorted)
        {
            var netData = p.GetComponent<PlayerNetworkData>();
            string name = netData != null
                ? netData.PlayerName.ToString()
                : $"P{p.Object.InputAuthority.PlayerId}";
            Debug.Log($"[Scoreboard] #{p.FinishRank}: {name} — {p.HP} HP remaining");
        }
        Debug.Log("=============================================");
    }
    #endregion
    #region RPCs

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_OnHitWithItem(PlayerRef attackerId, PlayerRef targetId)
    {
        Debug.Log($"[MG3Brawl] P{attackerId} dealt damage to P{targetId}!");
        // Play hit sound (3D) at target position on all clients
        if (SFXManager.Instance != null)
        {
            var allData = FindObjectsByType<PlayerMinigameData>(FindObjectsSortMode.None);
            foreach (var p in allData)
            {
                if (p.Object.InputAuthority == targetId)
                {
                    var pos = p.transform.position;
                    var clip = SFXManager.Instance.AttackSound;
                    if (clip != null)
                        SFXManager.Instance.PlaySFX3D(clip, pos, 1f);
                    break;
                }
            }
        }
    }
    #endregion
}