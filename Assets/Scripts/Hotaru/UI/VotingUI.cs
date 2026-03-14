using UnityEngine;
using System.Collections.Generic;
using TMPro;

public class VotingUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Transform cardContainer;
    [SerializeField] private MinigameCardUI cardPrefab;
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private TMP_Text statusText;

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

        // Create new cards
        for (int i = 0; i < minigameCount; i++)
        {
            var card = Instantiate(cardPrefab, cardContainer);
            card.Setup(i, this);
            cards.Add(card);
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

        if (statusText != null)
        {
            statusText.text = "Vote for a minigame!";
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
        foreach (var card in cards)
        {
            card.SetInteractable(false);
        }

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
        ResetUI();
    }

    private void OnVotingEnded()
    {
        Debug.Log("[VotingUI] Voting ended");

        if (statusText != null)
        {
            int winner = VotingManager.Instance?.WinnerIndex ?? 0;
            statusText.text = $"Winner: Minigame #{winner + 1}!";
        }

        // Disable all interactions
        foreach (var card in cards)
        {
            card.SetInteractable(false);
        }
    }
}