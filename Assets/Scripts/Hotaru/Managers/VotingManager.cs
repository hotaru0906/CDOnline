using Fusion;
using UnityEngine;
using System;

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
    public const int ROULETTE_OPTION_INDEX = 99; // Index đặc biệt cho option Roulette
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
    #endregion

    #region Events
    public event Action OnVotingStarted;
    public event Action OnVotingEnded;
    public event Action<float> OnTimerUpdated;
    public event Action<int, int> OnVoteCountChanged; // (minigameIndex, newCount)
    public event Action OnAllPlayersVoted; // Khi tất cả đã vote
    public event Action<VotingType> OnVotingTypeChanged_Event;
    #endregion

    #region Local State
    private bool hasVoted = false;
    private int localVoteIndex = -1;
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
        DontDestroyOnLoad(gameObject); // Thêm dòng này
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

        // Reset votes
        for (int i = 0; i < MinigameCount; i++)
        {
            VoteCounts.Set(i, 0);
        }
        RouletteVoteCount = 0;

        // Đếm số player và tính tổng vote weight
        var players = FindObjectsByType<PlayerNetworkData>(FindObjectsSortMode.None);
        TotalPlayers = players.Length;
        TotalVotes = 0;
        TotalVoteWeight = 0;

        // Tính tổng vote weight (người thắng MG gần nhất có 2 vote)
        foreach (var player in players)
        {
            int playerId = player.Object.InputAuthority.PlayerId;
            int weight = RouletteManager.Instance?.GetPlayerVoteWeight(playerId) ?? 1;
            TotalVoteWeight += weight;
        }

        WinnerIndex = -1;
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

        // Xử lý tùy theo loại voting
        if (CurrentVotingType == VotingType.RouletteOrMinigame)
        {
            // So sánh vote Roulette vs tổng vote Minigame
            int totalMinigameVotes = 0;
            int maxMinigameVotes = 0;
            int maxMinigameIndex = 0;

            for (int i = 0; i < MinigameCount; i++)
            {
                int count = VoteCounts.Get(i);
                totalMinigameVotes += count;
                if (count > maxMinigameVotes)
                {
                    maxMinigameVotes = count;
                    maxMinigameIndex = i;
                }
            }

            bool chooseRoulette = RouletteVoteCount > totalMinigameVotes;
            WinnerIndex = chooseRoulette ? ROULETTE_OPTION_INDEX : maxMinigameIndex;

            Debug.Log($"[VotingManager] RouletteOrMinigame result - Roulette: {RouletteVoteCount}, Minigame: {totalMinigameVotes}, Choose Roulette: {chooseRoulette}");

            // Notify all clients
            RPC_OnVotingEnded(WinnerIndex);

            // Notify GameManager
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnVotingComplete(chooseRoulette);

                // Nếu chọn minigame, start minigame
                if (!chooseRoulette)
                {
                    GameManager.Instance.StartMinigame(maxMinigameIndex);
                }
            }
        }
        else // MinigameOnly
        {
            WinnerIndex = CalculateWinner();
            Debug.Log($"[VotingManager] Winner: Minigame #{WinnerIndex}");

            // Notify all clients
            RPC_OnVotingEnded(WinnerIndex);

            // Start the winning minigame
            if (GameManager.Instance != null)
            {
                Debug.Log($"[VotingManager] Calling GameManager.StartMinigame({WinnerIndex})");
                GameManager.Instance.StartMinigame(WinnerIndex);
            }
            else
            {
                Debug.LogError("[VotingManager] GameManager.Instance is NULL!");
            }
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

    /// <summary>
    /// Submit vote cho Roulette (chỉ dùng khi VotingType = RouletteOrMinigame)
    /// </summary>
    public void SubmitRouletteVote()
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

        if (CurrentVotingType != VotingType.RouletteOrMinigame)
        {
            Debug.LogWarning("[VotingManager] Roulette vote only available in RouletteOrMinigame voting");
            return;
        }

        hasVoted = true;
        localVoteIndex = ROULETTE_OPTION_INDEX;

        // Lấy vote weight của player local
        int voteWeight = 1;
        if (RouletteManager.Instance != null && PlayerNetworkData.Local != null)
        {
            int playerId = PlayerNetworkData.Local.Object.InputAuthority.PlayerId;
            voteWeight = RouletteManager.Instance.GetPlayerVoteWeight(playerId);
        }

        Debug.Log($"[VotingManager] Submitting Roulette vote with weight {voteWeight}");
        RPC_SubmitRouletteVote(voteWeight);
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

        // Notify all clients about the vote update
        RPC_BroadcastVoteUpdate(minigameIndex, currentCount + voteWeight);

        // Check if all votes are in (by weight)
        if (TotalVotes >= TotalVoteWeight)
        {
            Debug.Log("[VotingManager] All players have voted!");
            RPC_NotifyAllVoted();

            if (instantEndWhenAllVoted)
            {
                // End voting ngay lập tức
                EndVoting();
            }
            else
            {
                // Giảm thời gian còn lại xuống quickEndTime
                if (RemainingTime > quickEndTime)
                {
                    RemainingTime = quickEndTime;
                    Debug.Log($"[VotingManager] Reducing remaining time to {quickEndTime}s");
                }
            }
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_SubmitRouletteVote(int voteWeight)
    {
        if (!IsVotingActive) return;

        RouletteVoteCount += voteWeight;
        TotalVotes += voteWeight;

        Debug.Log($"[VotingManager] Roulette vote received (weight: {voteWeight}). Roulette count: {RouletteVoteCount}. Total votes: {TotalVotes}/{TotalVoteWeight}");

        // Notify all clients about the roulette vote update
        RPC_BroadcastRouletteVoteUpdate(RouletteVoteCount);

        // Check if all votes are in (by weight)
        if (TotalVotes >= TotalVoteWeight)
        {
            Debug.Log("[VotingManager] All players have voted!");
            RPC_NotifyAllVoted();

            if (instantEndWhenAllVoted)
            {
                EndVoting();
            }
            else
            {
                if (RemainingTime > quickEndTime)
                {
                    RemainingTime = quickEndTime;
                    Debug.Log($"[VotingManager] Reducing remaining time to {quickEndTime}s");
                }
            }
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_BroadcastRouletteVoteUpdate(int newCount)
    {
        // UI có thể subscribe event này để update hiển thị
        OnVoteCountChanged?.Invoke(ROULETTE_OPTION_INDEX, newCount);
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

    /// <summary>
    /// Lấy số vote của một option
    /// </summary>
    /// <param name="index">Index minigame hoặc ROULETTE_OPTION_INDEX</param>
    public int GetVoteCount(int index)
    {
        // Kiểm tra đã spawn chưa
        if (!IsReady) return 0;

        if (index == ROULETTE_OPTION_INDEX)
        {
            return RouletteVoteCount;
        }

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
