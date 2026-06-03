using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

/// <summary>
/// UI quản lý việc vote chọn minigame
/// - Hiển thị các minigame card để vote
/// - Tích hợp với MinigameVotingManager để lọc minigame đã chơi
/// </summary>
public class VotingUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Transform cardContainer;
    [SerializeField] private MinigameCardUI cardPrefab;
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private TMP_Text statusText;

    [Header("Settings")]
    [SerializeField] private int maxMinigameCount = 3;

    private List<MinigameCardUI> cards = new List<MinigameCardUI>();
    private bool isInitialized = false;

    private void OnEnable()
    {
        // Kiểm tra cả VotingManager và MinigameVotingManager trước khi subscribe/setup
        if (VotingManager.Instance != null && VotingManager.Instance.IsReady)
        {
            SubscribeToEvents();
        }

        if (!isInitialized)
        {
            // Delay Setup nếu MinigameVotingManager hoặc VotingManager chưa ready
            bool minigameReady = MinigameVotingManager.Instance != null && MinigameVotingManager.Instance.IsReady;
            bool votingReady = VotingManager.Instance != null && VotingManager.Instance.IsReady;
            
            if (minigameReady && votingReady)
            {
                Setup();
            }
            else
            {
                // Đợi cả hai managers ready rồi mới setup
                StartCoroutine(WaitForManagers());
            }
        }
        else if (VotingManager.Instance != null && VotingManager.Instance.IsReady)
        {
            // Chỉ gọi ResetUI khi VotingManager đã ready
            ResetUI();
        }
    }
    
    private System.Collections.IEnumerator WaitForManagers()
    {
        // Đợi cả MinigameVotingManager và VotingManager ready
        while (MinigameVotingManager.Instance == null || !MinigameVotingManager.Instance.IsReady ||
               VotingManager.Instance == null || !VotingManager.Instance.IsReady)
        {
            yield return null;
        }
        
        // Subscribe events
        SubscribeToEvents();
        
        if (!isInitialized)
        {
            Setup();
        }
        
        ResetUI();
    }

    private void OnDisable()
    {
        UnsubscribeFromEvents();
    }

    private void SubscribeToEvents()
    {
        var vm = VotingManager.Instance;
        if (vm == null) return;

        vm.OnTimerUpdated += UpdateTimer;
        vm.OnVoteCountChanged += UpdateVoteCount;
        vm.OnVotingStarted += OnVotingStarted;
        vm.OnVotingEnded += OnVotingEnded;
    }

    private void UnsubscribeFromEvents()
    {
        var vm = VotingManager.Instance;
        if (vm == null) return;

        vm.OnTimerUpdated -= UpdateTimer;
        vm.OnVoteCountChanged -= UpdateVoteCount;
        vm.OnVotingStarted -= OnVotingStarted;
        vm.OnVotingEnded -= OnVotingEnded;
    }

    private void Setup()
    {
        // Clear existing cards
        foreach (var card in cards)
        {
            if (card != null)
                Destroy(card.gameObject);
        }
        cards.Clear();

        // Lấy số lượng minigame khả dụng từ MinigameVotingManager
        int availableCount = maxMinigameCount;
        if (MinigameVotingManager.Instance != null)
        {
            availableCount = MinigameVotingManager.Instance.GetAvailableMinigameCount();
        }

        // Create new cards based on available minigames
        for (int i = 0; i < availableCount; i++)
        {
            var card = Instantiate(cardPrefab, cardContainer);
            
            // Lấy minigame data từ MinigameVotingManager nếu có
            MinigameData minigameData = null;
            if (MinigameVotingManager.Instance != null)
            {
                minigameData = MinigameVotingManager.Instance.GetMinigameByAvailableIndex(i);
            }
            
            if (minigameData != null)
            {
                card.Setup(i, this, minigameData);
            }
            else
            {
                card.Setup(i, this);
            }
            
            cards.Add(card);
        }

        isInitialized = true;
    }

    private void ResetUI()
    {
        // Reset all cards
        foreach (var card in cards)
        {
            card.ResetCard();
        }

        if (statusText != null)
        {
            statusText.text = "Click or drag down to vote!";
        }

        // Sync with current voting state if voting is already active
        // Kiểm tra IsReady trước khi truy cập Networked properties
        if (VotingManager.Instance != null && VotingManager.Instance.IsReady && VotingManager.Instance.IsVotingActive)
        {
            UpdateTimer(VotingManager.Instance.RemainingTime);

            for (int i = 0; i < cards.Count; i++)
            {
                int count = VotingManager.Instance.GetVoteCount(i);
                cards[i].UpdateVoteCount(count);
            }
        }
    }

    public void OnVote(int minigameIndex)
    {
        if (VotingManager.Instance == null)
        {
            Debug.LogWarning("[VotingUI] VotingManager not found");
            return;
        }

        if (VotingManager.Instance.HasVoted)
        {
            Debug.Log("[VotingUI] Already voted");
            return;
        }

        Debug.Log($"[VotingUI] Vote for minigame #{minigameIndex}");

        VotingManager.Instance.SubmitVote(minigameIndex);

        // Disable all cards after voting
        DisableAllVoteOptions();

        // Highlight the voted card
        if (minigameIndex < cards.Count)
        {
            cards[minigameIndex].SetSelected(true);
        }

        if (statusText != null)
        {
            statusText.text = "Voted!";
        }
    }

    private void DisableAllVoteOptions()
    {
        foreach (var card in cards)
        {
            card.SetInteractable(false);
        }
    }

    private void UpdateTimer(float remainingTime)
    {
        if (timerText != null)
        {
            timerText.text = Mathf.Ceil(remainingTime).ToString();
        }
    }

    private void UpdateVoteCount(int minigameIndex, int newCount)
    {
        if (minigameIndex >= 0 && minigameIndex < cards.Count)
        {
            cards[minigameIndex].UpdateVoteCount(newCount);
        }
    }

    private void OnVotingStarted()
    {
        Debug.Log("[VotingUI] Voting started");
        
        // Re-setup cards khi bắt đầu voting mới
        // (có thể có minigame mới hoặc loại bỏ minigame đã chơi)
        Setup();
        ResetUI();
    }

    private void OnVotingEnded()
    {
        Debug.Log("[VotingUI] Voting ended");

        if (statusText != null)
        {
            int winner = VotingManager.Instance?.WinnerIndex ?? 0;

            string winnerName = $"Minigame #{winner + 1}";
            if (MinigameVotingManager.Instance != null)
            {
                var minigameData = MinigameVotingManager.Instance.GetMinigameByAvailableIndex(winner);
                if (minigameData != null)
                {
                    winnerName = minigameData.minigameName;
                }
            }
            statusText.text = $"Starting: {winnerName}!";
        }

        // Disable all interactions
        DisableAllVoteOptions();
    }
}