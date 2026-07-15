using Fusion;
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// MG8 — Killer Chase.
/// - Host chọn ngẫu nhiên 1 player làm Killer khi phase Playing bắt đầu.
/// - Chỉ Killer được gây damage.
/// - Runner cố sống sót tới hết giờ.
///
/// Ranking:
/// 1) Killer giết hết Runner: Killer rank 1, Runner xếp theo thời gian sống lâu nhất.
/// 2) Killer không giết ai: Killer rank cuối, Runner xếp theo HP giảm dần.
/// 3) Killer giết một phần: Runner còn sống > Killer > Runner đã bị loại.
/// </summary>
public class MG8Controller : BaseMinigameController
{
    public new static MG8Controller Instance =>
        BaseMinigameController.Instance as MG8Controller;

    [Header("Killer Settings")]
    [SerializeField] private int startingHP = 100;
    [SerializeField] private int killerDamage = 20;

    [Networked, OnChangedRender(nameof(OnKillerChanged))]
    public PlayerRef Killer { get; private set; }

    private readonly List<PlayerRef> _eliminationOrder = new();
    private int _runnerKillCount;

    #region Overrides

    protected override void OnGamePlayingStarted()
    {
        if (!HasStateAuthority) return;

        _eliminationOrder.Clear();
        _runnerKillCount = 0;
        Killer = PlayerRef.None;

        var allData = FindObjectsByType<PlayerMinigameData>(FindObjectsSortMode.None);
        if (allData.Length == 0)
        {
            Debug.LogWarning("[MG8Killer] No players found.");
            return;
        }

        foreach (var playerData in allData)
        {
            playerData.SetHP(startingHP);
            playerData.OnPlayerEliminated -= HandlePlayerEliminated;
            playerData.OnPlayerEliminated += HandlePlayerEliminated;

            var brawlData = playerData.GetComponent<MG8PlayerData>();
            if (brawlData != null)
                brawlData.DropItem();
        }

        int randomIndex = UnityEngine.Random.Range(0, allData.Length);
        AssignKiller(allData[randomIndex].Object.InputAuthority);

        UpdateAlivePlayerCount();
        MinigameHUDController.Instance?.RefreshPlayers();

        Debug.Log($"[MG8Killer] Started with {allData.Length} players. Killer = P{Killer.PlayerId}");
    }

    protected override void OnGameOver()
    {
        var allData = FindObjectsByType<PlayerMinigameData>(FindObjectsSortMode.None);

        foreach (var playerData in allData)
        {
            playerData.OnPlayerEliminated -= HandlePlayerEliminated;

            if (!HasStateAuthority) continue;

            var brawlData = playerData.GetComponent<MG8PlayerData>();
            if (brawlData != null)
                brawlData.DropItem();
        }
    }

    #endregion

    #region Killer Setup

    private void AssignKiller(PlayerRef killerRef)
    {
        if (!HasStateAuthority) return;

        Killer = killerRef;

        var allBrawlData = FindObjectsByType<MG8PlayerData>(FindObjectsSortMode.None);
        foreach (var brawlData in allBrawlData)
        {
            if (brawlData.Object.InputAuthority == Killer)
                brawlData.PickupItem();
            else
                brawlData.DropItem();
        }

        RPC_AnnounceKiller(Killer);
    }

    public bool IsKiller(PlayerRef playerRef)
    {
        return Killer != PlayerRef.None && Killer == playerRef;
    }

    private void OnKillerChanged()
    {
        Debug.Log($"[MG8Killer] Killer changed -> P{Killer.PlayerId}");
    }

    #endregion

    #region Hit Logic

    public void OnPlayerHit(PlayerController attacker, PlayerController target)
    {
        if (!HasStateAuthority) return;
        if (!IsGameStarted || IsGameEnded) return;
        if (attacker == null || target == null) return;

        PlayerRef attackerRef = attacker.Object.InputAuthority;
        PlayerRef targetRef = target.Object.InputAuthority;

        // Chỉ Killer được gây damage và không thể tự đánh chính mình.
        if (!IsKiller(attackerRef) || attackerRef == targetRef)
            return;

        var targetMinigameData = target.GetComponent<PlayerMinigameData>();
        if (targetMinigameData == null || !targetMinigameData.CanTakeDamage())
            return;

        var attackerBrawlData = attacker.GetComponent<MG8PlayerData>();
        if (attackerBrawlData == null || !attackerBrawlData.HasItem)
            return;

        targetMinigameData.TakeDamage(killerDamage);
        RPC_OnKillerHit(attackerRef, targetRef);

        Debug.Log($"[MG8Killer] Killer P{attackerRef.PlayerId} hit P{targetRef.PlayerId} for {killerDamage} damage.");
    }

    #endregion

    #region Elimination & Win Condition

    private void HandlePlayerEliminated(PlayerMinigameData data)
    {
        if (!HasStateAuthority || data == null) return;

        PlayerRef eliminatedRef = data.Object.InputAuthority;

        if (!_eliminationOrder.Contains(eliminatedRef))
        {
            _eliminationOrder.Add(eliminatedRef);

            if (eliminatedRef != Killer)
                _runnerKillCount++;

            Debug.Log($"[MG8Killer] P{eliminatedRef.PlayerId} eliminated. Runner kills = {_runnerKillCount}");
        }

        UpdateAlivePlayerCount();
        CheckWinCondition();
    }

    protected override void CheckWinCondition()
    {
        if (!HasStateAuthority || Killer == PlayerRef.None) return;

        var allData = FindObjectsByType<PlayerMinigameData>(FindObjectsSortMode.None);
        if (allData.Length == 0) return;

        int aliveRunnerCount = 0;
        bool killerEliminated = false;

        foreach (var playerData in allData)
        {
            PlayerRef playerRef = playerData.Object.InputAuthority;

            if (playerRef == Killer)
            {
                killerEliminated = playerData.IsEliminated;
                continue;
            }

            if (!playerData.IsEliminated)
                aliveRunnerCount++;
        }

        // Killer đã loại toàn bộ Runner.
        if (aliveRunnerCount == 0)
        {
            FinalizeRanks();
            EndGame(Killer);
            return;
        }

        // Trường hợp Killer bị loại bởi trap/hazard ngoài ý muốn.
        if (killerEliminated)
        {
            FinalizeRanks();
            EndGame(GetHighestRankedPlayer());
        }
    }

    protected override void OnTimeUp()
    {
        if (!HasStateAuthority || IsGameEnded) return;

        Debug.Log("[MG8Killer] Time's up.");

        FinalizeRanks();
        EndGame(GetHighestRankedPlayer());
    }

    #endregion

    #region Ranking

    private void FinalizeRanks()
    {
        var allData = FindObjectsByType<PlayerMinigameData>(FindObjectsSortMode.None);
        var finalOrder = BuildFinalRanking(allData);

        for (int i = 0; i < finalOrder.Count; i++)
        {
            PlayerMinigameData playerData = GetPlayerData(finalOrder[i], allData);
            if (playerData != null)
                playerData.SetFinished(i + 1, 0f);
        }

        ApplyHiddenScores();

        Debug.Log($"[MG8Killer] Final ranking: {FormatRanking(finalOrder)}");
    }

    /// <summary>
    /// Trả về thứ tự rank 1 -> N.
    /// </summary>
    private List<PlayerRef> BuildFinalRanking(PlayerMinigameData[] allData)
    {
        var ranking = new List<PlayerRef>();
        var survivingRunners = new List<PlayerMinigameData>();
        var eliminatedRunners = new List<PlayerRef>();

        int totalRunnerCount = 0;

        foreach (var playerData in allData)
        {
            PlayerRef playerRef = playerData.Object.InputAuthority;
            if (playerRef == Killer) continue;

            totalRunnerCount++;

            if (!playerData.IsEliminated)
                survivingRunners.Add(playerData);
        }

        // Runner sống sót xếp theo HP giảm dần; hòa HP thì PlayerId nhỏ hơn đứng trước.
        survivingRunners.Sort((a, b) =>
        {
            int hpCompare = b.HP.CompareTo(a.HP);
            if (hpCompare != 0) return hpCompare;
            return a.Object.InputAuthority.PlayerId.CompareTo(b.Object.InputAuthority.PlayerId);
        });

        // _eliminationOrder[0] chết sớm nhất.
        // Duyệt ngược để người sống lâu hơn đứng trên.
        for (int i = _eliminationOrder.Count - 1; i >= 0; i--)
        {
            PlayerRef playerRef = _eliminationOrder[i];
            if (playerRef != Killer && !eliminatedRunners.Contains(playerRef))
                eliminatedRunners.Add(playerRef);
        }

        bool killerKilledEveryone = totalRunnerCount > 0 && _runnerKillCount >= totalRunnerCount;
        bool killerKilledNobody = _runnerKillCount == 0;

        if (killerKilledEveryone)
        {
            // Killer top 1; các nạn nhân xếp theo thời gian sống lâu nhất.
            ranking.Add(Killer);
            ranking.AddRange(eliminatedRunners);
        }
        else if (killerKilledNobody)
        {
            // Tất cả Runner sống: Runner xếp theo HP, Killer đứng cuối.
            foreach (var runner in survivingRunners)
                ranking.Add(runner.Object.InputAuthority);

            // An toàn cho trường hợp Runner bị hazard nhưng không do Killer giết.
            ranking.AddRange(eliminatedRunners);
            ranking.Add(Killer);
        }
        else
        {
            // Runner còn sống > Killer > Runner đã bị loại.
            foreach (var runner in survivingRunners)
                ranking.Add(runner.Object.InputAuthority);

            ranking.Add(Killer);
            ranking.AddRange(eliminatedRunners);
        }

        // Fallback: đảm bảo mọi player đều có rank đúng một lần.
        foreach (var playerData in allData)
        {
            PlayerRef playerRef = playerData.Object.InputAuthority;
            if (!ranking.Contains(playerRef))
                ranking.Add(playerRef);
        }

        return ranking;
    }

    private PlayerRef GetHighestRankedPlayer()
    {
        var allData = FindObjectsByType<PlayerMinigameData>(FindObjectsSortMode.None);
        var ranking = BuildFinalRanking(allData);
        return ranking.Count > 0 ? ranking[0] : PlayerRef.None;
    }

    private PlayerMinigameData GetPlayerData(PlayerRef playerRef, PlayerMinigameData[] allData)
    {
        foreach (var playerData in allData)
        {
            if (playerData.Object.InputAuthority == playerRef)
                return playerData;
        }

        return null;
    }

    private string FormatRanking(List<PlayerRef> ranking)
    {
        var parts = new List<string>();
        for (int i = 0; i < ranking.Count; i++)
            parts.Add($"#{i + 1}=P{ranking[i].PlayerId}");
        return string.Join(", ", parts);
    }

    #endregion

    #region Scoreboard

    protected override void BuildScoreboardResults()
    {
        var allData = FindObjectsByType<PlayerMinigameData>(FindObjectsSortMode.None);
        var sorted = new List<PlayerMinigameData>(allData);
        sorted.Sort((a, b) => a.FinishRank.CompareTo(b.FinishRank));

        for (int i = 0; i < ScoreboardResults.Length; i++)
            ScoreboardResults.Set(i, default);

        for (int i = 0; i < sorted.Count && i < ScoreboardResults.Length; i++)
        {
            var playerData = sorted[i];
            ScoreboardResults.Set(i, new MinigameResultData
            {
                Player = playerData.Object.InputAuthority,
                Rank = playerData.FinishRank > 0 ? playerData.FinishRank : i + 1,
                Score = playerData.HP,
                IsValid = true
            });
        }
    }

    protected override void LogScoreboardInfo()
    {
        Debug.Log("========== SCOREBOARD (MG8 Killer Chase) ==========");

        var allData = FindObjectsByType<PlayerMinigameData>(FindObjectsSortMode.None);
        var sorted = new List<PlayerMinigameData>(allData);
        sorted.Sort((a, b) => a.FinishRank.CompareTo(b.FinishRank));

        foreach (var playerData in sorted)
        {
            var networkData = playerData.GetComponent<PlayerNetworkData>();
            string playerName = networkData != null
                ? networkData.PlayerName.ToString()
                : $"P{playerData.Object.InputAuthority.PlayerId}";

            string role = playerData.Object.InputAuthority == Killer ? "Killer" : "Runner";
            Debug.Log($"[Scoreboard] #{playerData.FinishRank}: {playerName} | {role} | {playerData.HP} HP");
        }

        Debug.Log("====================================================");
    }

    #endregion

    #region RPCs

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_AnnounceKiller(PlayerRef killerRef)
    {
        Debug.Log($"[MG8Killer] P{killerRef.PlayerId} is the Killer.");
        // Có thể nối UI, VFX hoặc âm thanh thông báo vai trò tại đây.
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_OnKillerHit(PlayerRef attackerId, PlayerRef targetId)
    {
        Debug.Log($"[MG8Killer] P{attackerId.PlayerId} dealt damage to P{targetId.PlayerId}.");

        if (SFXManager.Instance == null) return;

        var allData = FindObjectsByType<PlayerMinigameData>(FindObjectsSortMode.None);
        foreach (var playerData in allData)
        {
            if (playerData.Object.InputAuthority != targetId) continue;

            AudioClip clip = SFXManager.Instance.AttackSound;
            if (clip != null)
                SFXManager.Instance.PlaySFX3D(clip, playerData.transform.position, 1f);

            break;
        }
    }

    #endregion
}