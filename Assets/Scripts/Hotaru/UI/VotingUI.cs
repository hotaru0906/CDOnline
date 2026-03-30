using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

public class VotingUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Transform cardContainer;
    [SerializeField] private MinigameCardUI cardPrefab;
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text voteWeightText; // Hiển thị số vote của player

    [Header("Roulette Option")]
    [SerializeField] private GameObject rouletteOptionPanel;
    [SerializeField] private Button rouletteButton;
    [SerializeField] private TMP_Text rouletteVoteCountText;

    [Header("Settings")]
    [SerializeField] private int minigameCount = 3;

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
        vm.OnVotingTypeChanged_Event += OnVotingTypeChanged;
    }

    private void UnsubscribeFromEvents()
    {
        var vm = VotingManager.Instance;
        if (vm == null) return;

        vm.OnTimerUpdated -= UpdateTimer;
        vm.OnVoteCountChanged -= UpdateVoteCount;
        vm.OnVotingStarted -= OnVotingStarted;
        vm.OnVotingEnded -= OnVotingEnded;
        vm.OnVotingTypeChanged_Event -= OnVotingTypeChanged;
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

        // Create new cards
        for (int i = 0; i < minigameCount; i++)
        {
            var card = Instantiate(cardPrefab, cardContainer);
            card.Setup(i, this);
            cards.Add(card);
        }

        // Setup roulette button
        if (rouletteButton != null)
        {
            rouletteButton.onClick.RemoveAllListeners();
            rouletteButton.onClick.AddListener(OnRouletteVote);
        }

        isInitialized = true;
    }

    private void ResetUI()
    {
        // Reset all vote counts to 0
        foreach (var card in cards)
        {
            card.UpdateVoteCount(0);
            card.SetInteractable(true);
        }

        // Update vote weight display
        UpdateVoteWeightDisplay();

        // Handle voting type
        bool isRouletteOrMinigame = VotingManager.Instance?.CurrentVotingType == VotingType.RouletteOrMinigame;
        
        // Show/hide roulette option
        if (rouletteOptionPanel != null)
        {
            rouletteOptionPanel.SetActive(isRouletteOrMinigame);
        }

        if (rouletteVoteCountText != null)
        {
            rouletteVoteCountText.text = "0";
        }

        if (rouletteButton != null)
        {
            rouletteButton.interactable = true;
        }

        if (statusText != null)
        {
            if (isRouletteOrMinigame)
            {
                statusText.text = "Vote: Roulette hoặc Minigame!";
            }
            else
            {
                statusText.text = "Vote chọn minigame!";
            }
        }

        // Sync with current voting state if voting is already active
        if (VotingManager.Instance != null && VotingManager.Instance.IsVotingActive)
        {
            UpdateTimer(VotingManager.Instance.RemainingTime);

            for (int i = 0; i < minigameCount; i++)
            {
                int count = VotingManager.Instance.GetVoteCount(i);
                if (i < cards.Count)
                {
                    cards[i].UpdateVoteCount(count);
                }
            }

            // Update roulette vote count
            if (isRouletteOrMinigame && rouletteVoteCountText != null)
            {
                int rouletteCount = VotingManager.Instance.GetVoteCount(VotingManager.ROULETTE_OPTION_INDEX);
                rouletteVoteCountText.text = rouletteCount.ToString();
            }
        }
    }

    private void UpdateVoteWeightDisplay()
    {
        if (voteWeightText == null) return;

        int weight = VotingManager.Instance?.GetLocalPlayerVoteWeight() ?? 1;
        if (weight > 1)
        {
            voteWeightText.text = $"Bạn có {weight} vote! (Thắng MG gần nhất)";
            voteWeightText.gameObject.SetActive(true);
        }
        else
        {
            voteWeightText.text = "Bạn có 1 vote";
            voteWeightText.gameObject.SetActive(true);
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

        // Disable all cards and roulette button after voting
        DisableAllVoteOptions();

        // Highlight the voted card
        if (minigameIndex < cards.Count)
        {
            cards[minigameIndex].SetSelected(true);
        }

        if (statusText != null)
        {
            statusText.text = "Vote submitted!";
        }
    }

    public void OnRouletteVote()
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

        Debug.Log("[VotingUI] Vote for Roulette");

        VotingManager.Instance.SubmitRouletteVote();

        // Disable all vote options
        DisableAllVoteOptions();

        if (statusText != null)
        {
            statusText.text = "Vote Roulette submitted!";
        }
    }

    private void DisableAllVoteOptions()
    {
        foreach (var card in cards)
        {
            card.SetInteractable(false);
        }

        if (rouletteButton != null)
        {
            rouletteButton.interactable = false;
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
        // Handle Roulette vote count
        if (minigameIndex == VotingManager.ROULETTE_OPTION_INDEX)
        {
            if (rouletteVoteCountText != null)
            {
                rouletteVoteCountText.text = newCount.ToString();
            }
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
        ResetUI();
    }

    private void OnVotingEnded()
    {
        Debug.Log("[VotingUI] Voting ended");

        if (statusText != null)
        {
            int winner = VotingManager.Instance?.WinnerIndex ?? 0;
            
            if (winner == VotingManager.ROULETTE_OPTION_INDEX)
            {
                statusText.text = "🎰 Bắt đầu Russian Roulette!";
            }
            else
            {
                statusText.text = $"Winner: Minigame #{winner + 1}!";
            }
        }

        // Disable all interactions
        DisableAllVoteOptions();
    }

    private void OnVotingTypeChanged(VotingType newType)
    {
        Debug.Log($"[VotingUI] Voting type changed to: {newType}");
        
        // Show/hide roulette option
        bool isRouletteOrMinigame = (newType == VotingType.RouletteOrMinigame);
        
        if (rouletteOptionPanel != null)
        {
            rouletteOptionPanel.SetActive(isRouletteOrMinigame);
        }

        if (statusText != null)
        {
            if (isRouletteOrMinigame)
            {
                statusText.text = "Vote: Roulette hoặc Minigame!";
            }
            else
            {
                statusText.text = "Vote chọn minigame!";
            }
        }
    }
}