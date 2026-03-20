using Fusion;
using UnityEngine;
using System;

public class VotingManager : NetworkBehaviour
{
    #region Singleton
    public static VotingManager Instance { get; private set; }
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
    #endregion

    #region Local State
    private bool hasVoted = false;
    private int localVoteIndex = -1;
    #endregion

    #region Networked Vote Tracking
    [Networked]
    private int TotalVotes { get; set; }
    
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

    public override void Spawned()
    {
        Debug.Log($"[VotingManager] Spawned. IsHost: {HasStateAuthority}");
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;
        if (!IsVotingActive) return;

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

        Debug.Log("[VotingManager] Starting voting...");

        // Reset votes
        for (int i = 0; i < MinigameCount; i++)
        {
            VoteCounts.Set(i, 0);
        }

        // Đếm số player hiện tại
        var players = FindObjectsByType<PlayerNetworkData>(FindObjectsSortMode.None);
        TotalPlayers = players.Length;
        TotalVotes = 0;

        WinnerIndex = -1;
        RemainingTime = votingDuration;
        IsVotingActive = true;

        Debug.Log($"[VotingManager] Total players: {TotalPlayers}");

        // Notify all clients via RPC
        RPC_OnVotingStarted();
    }

    public void EndVoting()
    {
        if (!HasStateAuthority) return;
        if (!IsVotingActive) return;

        Debug.Log("[VotingManager] Ending voting...");

        IsVotingActive = false;
        WinnerIndex = CalculateWinner();

        Debug.Log($"[VotingManager] Winner: Minigame #{WinnerIndex}");

        // Notify all clients
        RPC_OnVotingEnded(WinnerIndex);

        // Start the winning minigame
        if (GameManager.Instance != null)
        {
            GameManager.Instance.StartMinigame(WinnerIndex);
        }
    }
    #endregion

    #region Vote Submission
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

        Debug.Log($"[VotingManager] Submitting vote for minigame #{minigameIndex}");
        RPC_SubmitVote(minigameIndex);
    }

    public bool HasVoted => hasVoted;
    public int LocalVoteIndex => localVoteIndex;
    #endregion

    #region RPCs
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_SubmitVote(int minigameIndex)
    {
        if (!IsVotingActive) return;

        int currentCount = VoteCounts.Get(minigameIndex);
        VoteCounts.Set(minigameIndex, currentCount + 1);
        TotalVotes++;

        Debug.Log($"[VotingManager] Vote received for #{minigameIndex}. New count: {currentCount + 1}. Total votes: {TotalVotes}/{TotalPlayers}");

        // Notify all clients about the vote update
        RPC_BroadcastVoteUpdate(minigameIndex, currentCount + 1);

        // Check if all players voted
        if (TotalVotes >= TotalPlayers)
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

    public int GetVoteCount(int minigameIndex)
    {
        if (minigameIndex < 0 || minigameIndex >= MinigameCount)
            return 0;

        return VoteCounts.Get(minigameIndex);
    }
    #endregion
}
