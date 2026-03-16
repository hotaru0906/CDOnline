using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MinigameCardUI : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text voteCountText;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color selectedColor = Color.green;

    private int minigameIndex;
    private VotingUI votingUI;

    public void Setup(int index, VotingUI ui)
    {
        minigameIndex = index;
        votingUI = ui;

        button.onClick.AddListener(OnVoteClicked);

        UpdateVoteCount(0);
        SetSelected(false);
    }

    private void OnVoteClicked()
    {
        Debug.Log($"[MinigameCardUI] Vote clicked for minigame index: {minigameIndex}");
        votingUI.OnVote(minigameIndex);
    }

    public void UpdateVoteCount(int count)
    {
        if (voteCountText != null)
        {
            voteCountText.text = count.ToString();
        }
    }

    public void SetInteractable(bool interactable)
    {
        if (button != null)
        {
            button.interactable = interactable;
        }
    }

    public void SetSelected(bool selected)
    {
        if (backgroundImage != null)
        {
            backgroundImage.color = selected ? selectedColor : normalColor;
        }
    }
}