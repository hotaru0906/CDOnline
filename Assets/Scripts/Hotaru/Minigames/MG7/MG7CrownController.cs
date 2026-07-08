using Fusion;
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// MG7 — King of the Crown minigame controller.
/// Win condition: Không có elimination — chơi hết GameTimer (từ BaseMinigameController).
/// Rank cuối = tổng điểm tích lũy, cao nhất = rank 1.
///
/// Flow:
///   OnGamePlayingStarted → chọn random holder đầu tiên
///   Mỗi giây, holder hiện tại được +1 điểm (HoldAccumulator dùng chung toàn game, không reset khi đổi holder)
///   Player khác đánh trúng holder → cướp crown (transfer), holder cũ bị stun (xử lý tự động trong PlayerController.CheckAttackHit)
///   Hết giờ (OnTimeUp) → FinalizeRanks theo Score giảm dần → EndGame
/// </summary>
public class MG7CrownController : BaseMinigameController
{
    public new static MG7CrownController Instance =>
        BaseMinigameController.Instance as MG7CrownController;

    // ----------------------------------------------------------------
    //  Networked State
    // ----------------------------------------------------------------

    [Networked, OnChangedRender(nameof(OnCrownHolderChanged))]
    public PlayerRef CrownHolder { get; private set; }

    [Networked]
    public NetworkBool CrownActive { get; private set; }

    /// <summary>
    /// Bộ đếm cộng điểm dùng CHUNG cho toàn game — không reset khi crown đổi chủ.
    /// Mỗi khi đạt mốc 1s, holder hiện tại (bất kể là ai) được +1 điểm.
    /// </summary>
    [Networked]
    private float HoldAccumulator { get; set; }

    // ----------------------------------------------------------------
    //  Setup
    // ----------------------------------------------------------------

    protected override void OnGamePlayingStarted()
    {
        if (!HasStateAuthority) return;

        HoldAccumulator = 0f;

        var allPlayers = GetAllPlayers();
        if (allPlayers.Count == 0) return;

        int randomIndex = UnityEngine.Random.Range(0, allPlayers.Count);
        AssignCrown(allPlayers[randomIndex].Object.InputAuthority);

        MinigameHUDController.Instance?.RefreshPlayers();
        Debug.Log($"[MG7Crown] Game started — initial holder: P{CrownHolder}");
    }

    protected override void OnGameOver()
    {
        CrownActive = false;

        if (MG7Crown.Instance != null)
        {
            MG7Crown.Instance.SetVisible(false);
            MG7Crown.Instance.Detach();
        }
    }

    // ----------------------------------------------------------------
    //  FixedUpdateNetwork — cộng điểm theo thời gian giữ crown
    // ----------------------------------------------------------------

    public override void FixedUpdateNetwork()
    {
        base.FixedUpdateNetwork();

        if (!HasStateAuthority) return;
        if (!IsGameStarted || IsGameEnded) return;
        if (!CrownActive || CrownHolder == PlayerRef.None) return;

        HoldAccumulator += Runner.DeltaTime;

        if (HoldAccumulator >= 1f)
        {
            HoldAccumulator -= 1f;

            var holderData = GetPlayerMinigameData(CrownHolder);
            if (holderData != null)
                holderData.AddScore(1);
        }
    }

    // ----------------------------------------------------------------
    //  Crown Logic
    // ----------------------------------------------------------------

    private void AssignCrown(PlayerRef newHolder)
    {
        if (!HasStateAuthority) return;

        CrownHolder = newHolder;
        CrownActive = true;

        RPC_MoveCrown(newHolder);

        if (MG7Crown.Instance != null)
            MG7Crown.Instance.SetVisible(true);

        Debug.Log($"[MG7Crown] Crown → P{newHolder}");
    }

    /// <summary>
    /// Attacker đánh trúng target đang giữ crown → cướp crown.
    /// Gọi từ MG7CrownPlayer khi attack hit thành công.
    /// Lưu ý: chỉ transfer nếu target ĐANG giữ crown (khác MG5, nơi holder tự đánh để chuyển đi).
    /// </summary>
    public void TryTransferCrown(PlayerRef attacker, PlayerRef target)
    {
        if (!HasStateAuthority) return;
        if (!IsGameStarted || IsGameEnded) return;
        if (CrownHolder != target) return; // target không giữ crown thì không có gì để cướp

        Debug.Log($"[MG7Crown] Crown cướp: P{target} → P{attacker}");
        AssignCrown(attacker); // HoldAccumulator giữ nguyên, không reset
    }

    // ----------------------------------------------------------------
    //  Win Condition — không có, chỉ kết thúc theo thời gian
    // ----------------------------------------------------------------

    protected override void CheckWinCondition()
    {
        // MG7 không có elimination — game luôn kết thúc qua OnTimeUp().
    }

    protected override void OnTimeUp()
    {
        Debug.Log("[MG7Crown] Time's up!");

        FinalizeRanks();

        PlayerRef winner = GetTopScorer();
        EndGame(winner);
    }

    // ----------------------------------------------------------------
    //  Rank
    // ----------------------------------------------------------------

    private void FinalizeRanks()
    {
        var allData = new List<PlayerMinigameData>(
            FindObjectsByType<PlayerMinigameData>(FindObjectsSortMode.None));

        // Sort theo Score giảm dần — cao nhất = rank 1
        allData.Sort((a, b) => b.Score.CompareTo(a.Score));

        for (int i = 0; i < allData.Count; i++)
        {
            int rank = i + 1;
            allData[i].SetFinished(rank, 0f);
        }

        // BuildBoardRanking (base) sẽ tự sort theo HiddenScore — cần apply trước khi EndGame gọi tới.
        ApplyHiddenScores();
    }

    private PlayerRef GetTopScorer()
    {
        var allData = FindObjectsByType<PlayerMinigameData>(FindObjectsSortMode.None);
        PlayerMinigameData best = null;

        foreach (var p in allData)
        {
            if (best == null || p.Score > best.Score)
                best = p;
        }

        return best != null ? best.Object.InputAuthority : PlayerRef.None;
    }

    // ----------------------------------------------------------------
    //  Scoreboard
    // ----------------------------------------------------------------

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
                Score = p.Score,
                IsValid = true
            });
        }
    }

    protected override void LogScoreboardInfo()
    {
        Debug.Log("========== SCOREBOARD (MG7 Crown) ==========");
        var allData = FindObjectsByType<PlayerMinigameData>(FindObjectsSortMode.None);
        var sorted = new List<PlayerMinigameData>(allData);
        sorted.Sort((a, b) => a.FinishRank.CompareTo(b.FinishRank));

        foreach (var p in sorted)
        {
            var netData = p.GetComponent<PlayerNetworkData>();
            string name = netData != null
                ? netData.PlayerName.ToString()
                : $"P{p.Object.InputAuthority.PlayerId}";
            Debug.Log($"[Scoreboard] #{p.FinishRank}: {name} — {p.Score} pts");
        }
        Debug.Log("=============================================");
    }

    // ----------------------------------------------------------------
    //  OnChangedRender callbacks
    // ----------------------------------------------------------------

    private void OnCrownHolderChanged()
    {
        Debug.Log($"[MG7Crown] CrownHolder changed → P{CrownHolder}");
    }

    // ----------------------------------------------------------------
    //  RPCs
    // ----------------------------------------------------------------

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_MoveCrown(PlayerRef holderRef)
    {
        var players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        foreach (var p in players)
        {
            if (p.Object.InputAuthority != holderRef) continue;

            if (MG7Crown.Instance != null)
                MG7Crown.Instance.AttachToPlayer(p.transform);

            break;
        }
    }

    // ----------------------------------------------------------------
    //  Helpers
    // ----------------------------------------------------------------

    private List<PlayerMinigameData> GetAllPlayers()
    {
        return new List<PlayerMinigameData>(
            FindObjectsByType<PlayerMinigameData>(FindObjectsSortMode.None));
    }

    private PlayerMinigameData GetPlayerMinigameData(PlayerRef playerRef)
    {
        var all = FindObjectsByType<PlayerMinigameData>(FindObjectsSortMode.None);
        foreach (var p in all)
            if (p.Object.InputAuthority == playerRef) return p;
        return null;
    }
}