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

        // Khi tất cả đã vote, rút ngắn thời gian còn lại
        if (TotalVoteWeight > 0 && TotalVotes >= TotalVoteWeight)
        {
            if (instantEndWhenAllVoted)
            {
                EndVoting();
                return;
            }
            RemainingTime = quickEndTime;
            Debug.Log($"[VotingManager] All voted - setting remaining time to {quickEndTime}s");
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
            WinnerIndex = topIndices.Count == 1 ? topIndices[0] : 0;
            Debug.Log($"[VotingManager] Winner: Minigame #{WinnerIndex}");
            RPC_OnVotingEnded(WinnerIndex);
            StartWinningMinigame(WinnerIndex);
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

        // TODO: replace this random tie-break fallback with a configurable rule later.
        pendingTieWinnerIndex = tiedIndices[UnityEngine.Random.Range(0, tiedIndices.Count)];

        int candidateCount = Mathf.Min(tiedIndices.Count, 10);
        int[] candidates = new int[candidateCount];
        for (int i = 0; i < candidateCount; i++)
        {
            candidates[i] = tiedIndices[i];
        }

        Debug.Log($"[VotingManager] Tie detected between {candidateCount} options. Opening tie-break UI after {tieBreakUiDelay}s. Winner preselected: #{pendingTieWinnerIndex}");
        RPC_OnTieBreakStarted(candidates, pendingTieWinnerIndex, tieBreakUiDelay);
        StartCoroutine(CompleteTieBreakAfterDelay());
    }

    private IEnumerator CompleteTieBreakAfterDelay()
    {
        yield return new WaitForSeconds(tieBreakUiDelay + tieBreakSpinDuration);

        if (!HasStateAuthority)
            yield break;

        WinnerIndex = pendingTieWinnerIndex >= 0 ? pendingTieWinnerIndex : CalculateWinner();
        Debug.Log($"[VotingManager] Tie-break completed. Winner: Minigame #{WinnerIndex}");

        RPC_OnTieBreakEnded(WinnerIndex);
        RPC_OnVotingEnded(WinnerIndex);
        StartWinningMinigame(WinnerIndex);
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

    private void StartWinningMinigame(int winnerIndex)
    {
        if (GameManager.Instance != null)
        {
            Debug.Log($"[VotingManager] Calling GameManager.StartMinigame({winnerIndex})");
            GameManager.Instance.StartMinigame(winnerIndex);
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
