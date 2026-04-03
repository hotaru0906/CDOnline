using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

/// <summary>
/// UI quản lý việc vote chọn minigame
/// - Hiển thị các minigame card để vote
/// - Tích hợp với MinigameVotingManager để lọc minigame đã chơi
/// - Roulette voting được xử lý bởi RouletteVotingUI riêng
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
        if (VotingManager.Instance != null)
        {
            SubscribeToEvents();
        }

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
            statusText.text = "Kéo xuống để vote minigame!";
        }

        // Sync with current voting state if voting is already active
        if (VotingManager.Instance != null && VotingManager.Instance.IsVotingActive)
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
            statusText.text = "Đã vote!";
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
        // Bỏ qua roulette vote count - handled by RouletteVotingUI
        if (minigameIndex == VotingManager.ROULETTE_OPTION_INDEX)
        {
            return;
        }

        // Handle minigame vote count
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
            
            // Chỉ hiển thị kết quả nếu là minigame (không phải roulette)
            if (winner != VotingManager.ROULETTE_OPTION_INDEX)
            {
                // Lấy tên minigame nếu có
                string winnerName = $"Minigame #{winner + 1}";
                if (MinigameVotingManager.Instance != null)
                {
                    var minigameData = MinigameVotingManager.Instance.GetMinigameByAvailableIndex(winner);
                    if (minigameData != null)
                    {
                        winnerName = minigameData.minigameName;
                    }
                }
                statusText.text = $"🎮 Bắt đầu: {winnerName}!";
            }
        }

        // Disable all interactions
        DisableAllVoteOptions();
    }
}