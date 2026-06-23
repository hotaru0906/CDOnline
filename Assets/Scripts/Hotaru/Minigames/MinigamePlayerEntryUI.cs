using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MinigamePlayerEntryUI : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private Sprite[] characterAvatars;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text livesText;
    [SerializeField] private GameObject eliminatedOverlay;
    [SerializeField] private Image backgroundImage;

    // runtime id for lookup
    public int PlayerId { get; private set; } = -1;

    public void SetData(
        int playerId,
        string playerName,
        int lives,
        Color slotColor,
        bool isEliminated)
    {
        PlayerId = playerId;

        if (nameText != null)
            nameText.text = playerName;

        if (livesText != null)
            livesText.text = lives.ToString();

        if (iconImage != null)
            iconImage.color = Color.white;;

        if (backgroundImage != null)
            backgroundImage.color = slotColor;



        SetEliminated(isEliminated);
    }

    public void UpdateLives(int lives)
    {
        if (livesText != null)
            livesText.text = lives.ToString();
    }

    public void SetEliminated(bool eliminated)
    {
        if (eliminatedOverlay != null)
            eliminatedOverlay.SetActive(eliminated);

        if (nameText != null)
            nameText.color = eliminated ? Color.gray : Color.white;
    }

    public void SetCharacterAvatar(int characterIndex)
    {
        if (iconImage == null)
            return;

        if (characterIndex < 0 ||
            characterIndex >= characterAvatars.Length)
            return;

        iconImage.sprite = characterAvatars[characterIndex];
    }
}