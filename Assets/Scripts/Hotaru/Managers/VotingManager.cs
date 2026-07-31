using Fusion;
using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Quản lý voting system
/// - Hỗ trợ 2 loại voting: MinigameOnly và RouletteOrMinigame
/// - Weighted voting: người thắng MG gần nhất có 2 vote, còn lại 1 vote
/// </summary>
public class VotingManager : NetworkBehaviour
{
    #region Singleton
    public static VotingManager Instance { get; private set; }
    #endregion

    #region Constants
    #endregion

    #region Networked Properties
    [Networked, Capacity(10)]
    private NetworkArray<int> VoteCounts => default;

    [Networked, OnChangedRender(nameof(OnTimerChanged))]
    public float RemainingTime { get; private set; }

    [Networked, OnChangedRender(nameof(OnVotingStateChanged))]
    public NetworkBool IsVotingActive { get; private set; }

    [Networked]
    public int WinnerIndex { get; private set; } = -1;

    [Networked]
    public int MinigameCount { get; private set; } = 3;

    /// <summary>
    /// Loại voting hiện tại (sync từ GameManager)
    /// </summary>
    [Networked, OnChangedRender(nameof(OnVotingTypeChanged))]
    public VotingType CurrentVotingType { get; private set; } = VotingType.MinigameOnly;

    /// <summary>
    /// Số vote cho Roulette (khi voting RouletteOrMinigame)
    /// </summary>
    [Networked]
    public int RouletteVoteCount { get; private set; }
    #endregion

    #region Settings
    [SerializeField] private float votingDuration = 10f;
    [SerializeField] private float quickEndTime = 3f; // Thời gian còn lại khi tất cả đã vote
    [SerializeField] private bool instantEndWhenAllVoted = false; // End ngay khi tất cả vote
    [SerializeField] private float tieBreakUiDelay = 2f; // Chờ 2s sau khi hiện tổng vote rồi mới mở wheel
    [SerializeField] private float tieBreakSpinDuration = 2.2f; // Thời gian wheel quay
    [SerializeField] private float tieBreakResultHoldDuration = 1.5f; // Giữ màn hình sau khi wheel dừng
    #endregion

    #region Events
    public event Action OnVotingStarted;
    public event Action OnVotingEnded;
    public event Action<float> OnTimerUpdated;
    public event Action<int, int> OnVoteCountChanged; // (minigameIndex, newCount)
    public event Action OnAllPlayersVoted; // Khi tất cả đã vote
    public event Action<VotingType> OnVotingTypeChanged_Event;
    public event Action<int[], int, float> OnTieBreakStarted; // (availableIndices, winnerAvailableIndex, duration)
    public event Action<int> OnTieBreakEnded; // winnerAvailableIndex
    #endregion

    #region Local State
    private bool hasVoted = false;
    private int localVoteIndex = -1;
    private int pendingTieWinnerIndex = -1;
    private int confirmedTieWinnerIndex = -1;
    private bool hasReachedAllVotedQuickEnd = false;
    #endregion

    /// <summary>
    /// Kiểm tra xem manager đã spawn và sẵn sàng chưa
    /// Phải kiểm tra trước khi truy cập Networked properties
    /// </summary>
    public bool IsReady { get; private set; } = false;

    #region Networked Vote Tracking
    [Networked]
    private int TotalVotes { get; set; }

    /// <summary>
    /// Tổng số vote có thể (tính theo weight)
    /// </summary>
    [Networked]
    private int TotalVoteWeight { get; set; }

    [Networked]
    private int TotalPlayers { get; set; }
    #endregion

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public override void Spawned()
    {
        IsReady = true;
        Debug.Log($"[VotingManager] Spawned. IsHost: {HasStateAuthority}");
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;
        if (!IsReady || !IsVotingActive) return;

        // Khi tất cả đã vote, rút ngắn thời gian còn lại một lần rồi tiếp tục đếm xuống
        if (TotalVoteWeight > 0 && TotalVotes >= TotalVoteWeight)
        {
            if (!hasReachedAllVotedQuickEnd)
            {
                hasReachedAllVotedQuickEnd = true;

                if (instantEndWhenAllVoted)
                {
                    EndVoting();
                    return;
                }

                RemainingTime = quickEndTime;
                Debug.Log($"[VotingManager] All voted - setting remaining time to {quickEndTime}s");
            }
        }
        else
        {
            hasReachedAllVotedQuickEnd = false;
        }

        // Update timer (host only)
        RemainingTime -= Runner.DeltaTime;

        if (RemainingTime <= 0)
        {
            EndVoting();
        }
    }

    #region Public Methods (Host Only)
    public void StartVoting()
    {
        if (!HasStateAuthority)
        {
            Debug.LogWarning("[VotingManager] Only Host can start voting");
            return;
        }

        // Sync voting type từ GameManager
        if (GameManager.Instance != null)
        {
            CurrentVotingType = GameManager.Instance.CurrentVotingType;
        }

        // Sync MinigameCount từ MinigameVotingManager
        if (MinigameVotingManager.Instance != null && MinigameVotingManager.Instance.IsReady)
        {
            MinigameCount = MinigameVotingManager.Instance.GetAvailableMinigameCount();
            Debug.Log($"[VotingManager] Synced MinigameCount: {MinigameCount}");
        }
        else
        {
            Debug.LogWarning("[VotingManager] MinigameVotingManager not ready, using default MinigameCount");
        }

        Debug.Log($"[VotingManager] Starting voting... Type: {CurrentVotingType}, MinigameCount: {MinigameCount}");

        // Reset ALL votes
        for (int i = 0; i < VoteCounts.Length; i++)
        {
            VoteCounts.Set(i, 0);
        }

        // Reset local vote state trên Host
        hasVoted = false;
        localVoteIndex = -1;
        hasReachedAllVotedQuickEnd = false;

        // Đếm số player và tính tổng vote weight
        var players = FindObjectsByType<PlayerNetworkData>(FindObjectsSortMode.None);
        TotalPlayers = players.Length;
        TotalVotes = 0;
        TotalVoteWeight = 0;


        // Tính tổng vote weight (người thắng MG gần nhất có 2 vote, còn lại 1 vote)
        foreach (var player in players)
        {
            int weight = 1;
            if (RouletteManager.Instance != null)
            {
                weight = RouletteManager.Instance.GetPlayerVoteWeightByRef(player.Object.InputAuthority);
            }
            TotalVoteWeight += weight;
        }

        WinnerIndex = -1;
        pendingTieWinnerIndex = -1;
        RemainingTime = votingDuration;
        IsVotingActive = true;

        Debug.Log($"[VotingManager] Total players: {TotalPlayers}, Total vote weight: {TotalVoteWeight}");

        // Notify all clients via RPC
        RPC_OnVotingStarted();
    }

    public void EndVoting()
    {
        if (!HasStateAuthority) return;
        if (!IsVotingActive) return;

        Debug.Log("[VotingManager] Ending voting...");

        IsVotingActive = false;

        // Only reveal the final tally if at least one vote was cast.
        if (TotalVotes > 0)
        {
            RevealVoteCounts();
        }

        List<int> topIndices = GetTopVotedIndices();
        if (topIndices.Count <= 1)
        {
            int selectedAvailableWinner = topIndices.Count == 1 ? topIndices[0] : 0;
            int actualWinner = selectedAvailableWinner;

            if (MinigameVotingManager.Instance != null && MinigameVotingManager.Instance.IsReady)
            {
                actualWinner = MinigameVotingManager.Instance.GetActualIndexByAvailableIndex(selectedAvailableWinner);
                if (actualWinner < 0)
                {
                    actualWinner = selectedAvailableWinner;
                }
            }

            WinnerIndex = actualWinner;
            Debug.Log($"[VotingManager] Winner availableIndex={selectedAvailableWinner} actualIndex={WinnerIndex}");
            RPC_OnVotingEnded(WinnerIndex);
            StartWinningMinigame(WinnerIndex, true);
        }
        else
        {
            StartTieBreak(topIndices);
        }
    }

    #endregion

    #region Vote Submission
    /// <summary>
    /// Submit vote cho minigame
    /// </summary>
    public void SubmitVote(int minigameIndex)
    {
        if (!IsVotingActive)
        {
            Debug.LogWarning("[VotingManager] Voting is not active");
            return;
        }

        if (hasVoted)
        {
            Debug.LogWarning("[VotingManager] Already voted");
            return;
        }

        if (minigameIndex < 0 || minigameIndex >= MinigameCount)
        {
            Debug.LogWarning($"[VotingManager] Invalid minigame index: {minigameIndex}");
            return;
        }

        hasVoted = true;
        localVoteIndex = minigameIndex;

        // Lấy vote weight của player local
        int voteWeight = 1;
        if (RouletteManager.Instance != null && PlayerNetworkData.Local != null)
        {
            int playerId = PlayerNetworkData.Local.Object.InputAuthority.PlayerId;
            voteWeight = RouletteManager.Instance.GetPlayerVoteWeight(playerId);
        }

        Debug.Log($"[VotingManager] Submitting vote for minigame #{minigameIndex} with weight {voteWeight}");
        RPC_SubmitVote(minigameIndex, voteWeight);
    }

    public bool HasVoted => hasVoted;
    public int LocalVoteIndex => localVoteIndex;
    #endregion

    #region RPCs
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_SubmitVote(int minigameIndex, int voteWeight)
    {
        if (!IsVotingActive) return;

        int currentCount = VoteCounts.Get(minigameIndex);
        VoteCounts.Set(minigameIndex, currentCount + voteWeight);
        TotalVotes += voteWeight;

        Debug.Log($"[VotingManager] Vote received for #{minigameIndex} (weight: {voteWeight}). New count: {currentCount + voteWeight}. Total votes: {TotalVotes}/{TotalVoteWeight}");

        // Timer reduction + instant end handled in FixedUpdateNetwork
        if (TotalVotes >= TotalVoteWeight)
        {
            Debug.Log("[VotingManager] All players have voted!");
            RevealVoteCounts();
            RPC_NotifyAllVoted();
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_NotifyAllVoted()
    {
        OnAllPlayersVoted?.Invoke();
        Debug.Log("[VotingManager] All players have voted - notified");
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_BroadcastVoteUpdate(int minigameIndex, int newCount)
    {
        OnVoteCountChanged?.Invoke(minigameIndex, newCount);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_OnVotingStarted()
    {
        hasVoted = false;
        localVoteIndex = -1;
        OnVotingStarted?.Invoke();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_OnVotingEnded(int winnerIndex)
    {
        OnVotingEnded?.Invoke();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_OnTieBreakStarted(int[] candidateIndices, int winnerIndex, float duration)
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ShowMinigameTieBreakerPanel();
        }

        OnTieBreakStarted?.Invoke(candidateIndices, winnerIndex, duration);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_OnTieBreakEnded(int winnerIndex)
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.HideMinigameTieBreakerPanel();
        }

        OnTieBreakEnded?.Invoke(winnerIndex);
    }
    #endregion

    #region Callbacks
    private void OnTimerChanged()
    {
        OnTimerUpdated?.Invoke(RemainingTime);
    }

    private void OnVotingStateChanged()
    {
        if (IsVotingActive)
        {
            Debug.Log("[VotingManager] Voting is now active");
        }
        else
        {
            Debug.Log("[VotingManager] Voting is now inactive");
        }
    }

    private void OnVotingTypeChanged()
    {
        Debug.Log($"[VotingManager] Voting type changed to: {CurrentVotingType}");
        OnVotingTypeChanged_Event?.Invoke(CurrentVotingType);
    }
    #endregion

    #region Helpers
    private int CalculateWinner()
    {
        int maxVotes = -1;
        int winnerIndex = 0;

        for (int i = 0; i < MinigameCount; i++)
        {
            int count = VoteCounts.Get(i);
            if (count > maxVotes)
            {
                maxVotes = count;
                winnerIndex = i;
            }
        }

        return winnerIndex;
    }

    private List<int> GetTopVotedIndices()
    {
        List<int> top = new List<int>();
        int maxVotes = int.MinValue;

        for (int i = 0; i < MinigameCount; i++)
        {
            int count = VoteCounts.Get(i);

            if (count > maxVotes)
            {
                maxVotes = count;
                top.Clear();
                top.Add(i);
            }
            else if (count == maxVotes)
            {
                top.Add(i);
            }
        }

        return top;
    }

    private void StartTieBreak(List<int> tiedIndices)
    {
        if (!HasStateAuthority || tiedIndices == null || tiedIndices.Count == 0)
            return;

        if (MinigameVotingManager.Instance == null || !MinigameVotingManager.Instance.IsReady)
        {
            Debug.LogWarning("[VotingManager] Cannot start tie-break because MinigameVotingManager is not ready");
            return;
        }

        List<int> actualCandidates = new List<int>();
        for (int i = 0; i < tiedIndices.Count; i++)
        {
            int actualIndex = MinigameVotingManager.Instance.GetActualIndexByAvailableIndex(tiedIndices[i]);
            if (actualIndex >= 0 && !actualCandidates.Contains(actualIndex))
            {
                actualCandidates.Add(actualIndex);
            }
        }

        const int wheelSlotCount = 6;
        HashSet<int> excludeActual = new HashSet<int>(actualCandidates);
        while (actualCandidates.Count < wheelSlotCount)
        {
            int extraActual = MinigameVotingManager.Instance.GetRandomEligibleActualMinigameIndexExcluding(excludeActual);
            if (extraActual < 0)
                break;

            actualCandidates.Add(extraActual);
            excludeActual.Add(extraActual);
        }

        if (actualCandidates.Count == 0)
        {
            Debug.LogWarning("[VotingManager] No actual candidates available for tie-break");
            return;
        }

        confirmedTieWinnerIndex = -1;
        pendingTieWinnerIndex = actualCandidates[UnityEngine.Random.Range(0, actualCandidates.Count)];

        int candidateCount = Mathf.Min(actualCandidates.Count, 10);
        int[] candidates = new int[candidateCount];
        for (int i = 0; i < candidateCount; i++)
        {
            candidates[i] = actualCandidates[i];
        }

        Debug.Log($"[VotingManager] Tie detected between {candidateCount} options. Opening tie-break UI after {tieBreakUiDelay}s. Winner preselected: #{pendingTieWinnerIndex}");
        RPC_OnTieBreakStarted(candidates, pendingTieWinnerIndex, tieBreakUiDelay);
        StartCoroutine(CompleteTieBreakAfterDelay());
    }

    private IEnumerator CompleteTieBreakAfterDelay()
    {
        yield return new WaitForSeconds(tieBreakUiDelay + tieBreakSpinDuration + tieBreakResultHoldDuration);

        if (!HasStateAuthority)
            yield break;

        int finalWinner = confirmedTieWinnerIndex >= 0 ? confirmedTieWinnerIndex : pendingTieWinnerIndex;
        WinnerIndex = finalWinner >= 0 ? finalWinner : CalculateWinner();
        string winnerName = "(unknown)";
        if (MinigameVotingManager.Instance != null && MinigameVotingManager.Instance.IsReady)
        {
            var md = MinigameVotingManager.Instance.GetMinigameByActualIndex(WinnerIndex);
            if (md != null) winnerName = md.minigameName;
        }
        Debug.Log($"[VotingManager] Tie-break completed. Winner actualIndex: {WinnerIndex} name: {winnerName}");

        RPC_OnTieBreakEnded(WinnerIndex);
        RPC_OnVotingEnded(WinnerIndex);
        StartWinningMinigame(WinnerIndex, true);
    }

    private void RevealVoteCounts()
    {
        if (MinigameCount <= 0)
            return;

        for (int i = 0; i < MinigameCount; i++)
        {
            int count = VoteCounts.Get(i);
            RPC_BroadcastVoteUpdate(i, count);
        }
    }

    public void ConfirmTieBreakResult(int winnerIndex)
    {
        if (MinigameVotingManager.Instance == null || !MinigameVotingManager.Instance.IsReady)
            return;

        if (winnerIndex < 0 || winnerIndex >= MinigameVotingManager.Instance.TotalMinigameCount)
        {
            Debug.LogWarning($"[VotingManager] ConfirmTieBreakResult received invalid index: {winnerIndex}");
            return;
        }

        if (HasStateAuthority)
        {
            SetConfirmedTieWinner(winnerIndex);
        }
        else
        {
            RPC_ConfirmTieBreakResult(winnerIndex);
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_ConfirmTieBreakResult(int winnerIndex)
    {
        SetConfirmedTieWinner(winnerIndex);
    }

    private void SetConfirmedTieWinner(int winnerIndex)
    {
        confirmedTieWinnerIndex = winnerIndex;
        string name = "(unknown)";
        if (MinigameVotingManager.Instance != null && MinigameVotingManager.Instance.IsReady)
        {
            var md = MinigameVotingManager.Instance.GetMinigameByActualIndex(winnerIndex);
            if (md != null) name = md.minigameName;
        }
        Debug.Log($"[VotingManager] Tie-break result confirmed by wheel: #{winnerIndex} name: {name}");
    }

    private void StartWinningMinigame(int winnerIndex, bool isActualIndex = false)
    {
        if (GameManager.Instance != null)
        {
            string name = "(unknown)";
            if (MinigameVotingManager.Instance != null && MinigameVotingManager.Instance.IsReady)
            {
                var md = MinigameVotingManager.Instance.GetMinigameByActualIndex(winnerIndex);
                if (md != null) name = md.minigameName;
            }

            if (isActualIndex)
            {
                Debug.Log($"[VotingManager] Calling GameManager.StartMinigameActual({winnerIndex}) name:{name}");
                GameManager.Instance.StartMinigameActual(winnerIndex);
            }
            else
            {
                Debug.Log($"[VotingManager] Calling GameManager.StartMinigame({winnerIndex}) name:{name}");
                GameManager.Instance.StartMinigame(winnerIndex);
            }
        }
        else
        {
            Debug.LogError("[VotingManager] GameManager.Instance is NULL!");
        }
    }

    /// <summary>
    /// Lấy số vote của một option
    /// </summary>
    /// <param name="index">Index minigame (0..MinigameCount-1)</param>
    public int GetVoteCount(int index)
    {
        // Kiểm tra đã spawn chưa
        if (!IsReady) return 0;

        if (index < 0 || index >= MinigameCount)
            return 0;

        return VoteCounts.Get(index);
    }

    /// <summary>
    /// Lấy vote weight của player local
    /// </summary>
    public int GetLocalPlayerVoteWeight()
    {
        if (RouletteManager.Instance != null && PlayerNetworkData.Local != null)
        {
            int playerId = PlayerNetworkData.Local.Object.InputAuthority.PlayerId;
            return RouletteManager.Instance.GetPlayerVoteWeight(playerId);
        }
        return 1;
    }
    #endregion
}
