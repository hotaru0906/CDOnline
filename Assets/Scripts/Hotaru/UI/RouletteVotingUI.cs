using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// UI quản lý việc vote giữa Roulette và Minigame
/// Hiện khi voting type là RouletteOrMinigame
/// </summary>
public class RouletteVotingUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject rootPanel;
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private TMP_Text statusText;
    
    [Header("Roulette Option")]
    [SerializeField] private Button rouletteButton;
    [SerializeField] private TMP_Text rouletteVoteCountText;
    [SerializeField] private Image rouletteButtonBackground;
    
    [Header("Continue Minigame Option")]  
    [SerializeField] private Button continueMinigameButton;
    [SerializeField] private TMP_Text minigameVoteCountText;
    [SerializeField] private Image minigameButtonBackground;
    
    [Header("Visual Settings")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color selectedColor = Color.green;

    private bool hasVoted = false;

    private void OnEnable()
    {
        // Subscribe events
        if (VotingManager.Instance != null)
        {
            SubscribeToEvents();
        }
        
        // Reset UI khi enable (GameManager đã kiểm tra VotingType trước khi enable panel này)
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

    private void Start()
    {
        // Setup button listeners
        if (rouletteButton != null)
        {
            rouletteButton.onClick.RemoveAllListeners();
            rouletteButton.onClick.AddListener(OnRouletteVote);
        }
        
        if (continueMinigameButton != null)
        {
            continueMinigameButton.onClick.RemoveAllListeners();
            continueMinigameButton.onClick.AddListener(OnContinueMinigameVote);
        }
    }

    private void ResetUI()
    {
        hasVoted = false;
        
        // Reset vote counts
        if (rouletteVoteCountText != null)
            rouletteVoteCountText.text = "0";
        
        if (minigameVoteCountText != null)
            minigameVoteCountText.text = "0";
        
        // Reset button states
        SetButtonsInteractable(true);
        SetButtonSelected(rouletteButtonBackground, false);
        SetButtonSelected(minigameButtonBackground, false);
        
        if (statusText != null)
        {
            statusText.text = "Vote: Return to Roulette or Continue the Minigame?";
        }
        
        // Sync with current voting state if active
        if (VotingManager.Instance != null && VotingManager.Instance.IsVotingActive)
        {
            UpdateTimer(VotingManager.Instance.RemainingTime);
            
            // (RouletteOrMinigame voting removed — this panel is no longer active)
            if (rouletteVoteCountText != null)
                rouletteVoteCountText.text = "0";
        }
    }

    #region Vote Handlers
    private void OnRouletteVote()
    {
        if (VotingManager.Instance == null)
        {
            Debug.LogWarning("[RouletteVotingUI] VotingManager not found");
            return;
        }

        if (hasVoted || VotingManager.Instance.HasVoted)
        {
            Debug.Log("[RouletteVotingUI] Already voted");
            return;
        }

        Debug.Log("[RouletteVotingUI] Vote for Roulette (no-op: RouletteOrMinigame voting removed)");
        hasVoted = true;
        SetButtonsInteractable(false);
        SetButtonSelected(rouletteButtonBackground, true);

        if (statusText != null)
        {
            statusText.text = "Voted: Return to Roulette!";
        }
    }

    private void OnContinueMinigameVote()
    {
        if (VotingManager.Instance == null)
        {
            Debug.LogWarning("[RouletteVotingUI] VotingManager not found");
            return;
        }

        if (hasVoted || VotingManager.Instance.HasVoted)
        {
            Debug.Log("[RouletteVotingUI] Already voted");
            return;
        }

        Debug.Log("[RouletteVotingUI] Vote for Continue Minigame");
        hasVoted = true;

        // Vote for "continue minigame" - use a special index or first available minigame
        // This means NOT choosing roulette - will go to next minigame voting
        VotingManager.Instance.SubmitVote(0); // Vote for minigame option

        // Update UI
        SetButtonsInteractable(false);
        SetButtonSelected(minigameButtonBackground, true);

        if (statusText != null)
        {
            statusText.text = "Voted: Continue the Minigame!";
        }
    }
    #endregion

    #region UI Update Methods
    private void SetButtonsInteractable(bool interactable)
    {
        if (rouletteButton != null)
            rouletteButton.interactable = interactable;
        
        if (continueMinigameButton != null)
            continueMinigameButton.interactable = interactable;
    }

    private void SetButtonSelected(Image background, bool selected)
    {
        if (background != null)
        {
            background.color = selected ? selectedColor : normalColor;
        }
    }

    private void UpdateTimer(float remainingTime)
    {
        if (timerText != null)
        {
            timerText.text = Mathf.Ceil(remainingTime).ToString();
        }
    }

    private void UpdateVoteCount(int optionIndex, int newCount)
    {
        if (optionIndex >= 0)
        {
            if (rouletteVoteCountText != null)
                rouletteVoteCountText.text = newCount.ToString();
        }
        else
        {
            // Count all minigame votes for "continue" option
            if (minigameVoteCountText != null)
            {
                int totalMinigameVotes = 0;
                if (VotingManager.Instance != null)
                {
                    for (int i = 0; i < VotingManager.Instance.MinigameCount; i++)
                    {
                        totalMinigameVotes += VotingManager.Instance.GetVoteCount(i);
                    }
                }
                minigameVoteCountText.text = totalMinigameVotes.ToString();
            }
        }
    }

    private void OnVotingStarted()
    {
        Debug.Log("[RouletteVotingUI] Voting started");
        hasVoted = false;
        
        // GameManager đã kiểm tra VotingType và chỉ enable panel này khi RouletteOrMinigame
        // Nên ở đây chỉ cần reset UI
        ResetUI();
    }

    private void OnVotingEnded()
    {
        Debug.Log("[RouletteVotingUI] Voting ended");

        if (statusText != null)
        {
            statusText.text = "Continue the Minigame!";
        }

        SetButtonsInteractable(false);
    }

    private void OnVotingTypeChanged(VotingType newType)
    {
        Debug.Log($"[RouletteVotingUI] Voting type changed to: {newType}");
        // GameManager sẽ xử lý việc hiện/ẩn panel dựa vào VotingType
        // Ở đây chỉ cần reset UI nếu cần
        if (gameObject.activeInHierarchy)
        {
            ResetUI();
        }
    }
    #endregion
}
