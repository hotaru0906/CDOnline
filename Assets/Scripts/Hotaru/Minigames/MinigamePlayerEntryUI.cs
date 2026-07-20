using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MinigamePlayerEntryUI : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private Sprite[] characterAvatars;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text livesText;
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private Slider hpSlider;  // HP bar slider (0-100)
    [SerializeField] private GameObject eliminatedOverlay;
    [SerializeField] private Image backgroundImage;

    // runtime id for lookup
    public int PlayerId { get; private set; } = -1;

    public void SetData(
        int playerId,
        string playerName,
        int lives,
        Color slotColor,
        bool isEliminated,
        int score = 0)
    {
        PlayerId = playerId;

        if (nameText != null)
            nameText.text = playerName;

        if (livesText != null)
            livesText.text = lives.ToString();

        if (scoreText != null)
            scoreText.text = score.ToString();

        if (iconImage != null)
            iconImage.color = Color.white;;

        if (backgroundImage != null)
            backgroundImage.color = slotColor;

        SetHP(lives);
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

    public void UpdateScore(int score)
    {
        if (scoreText != null)
            scoreText.text = score.ToString();
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
    public void SetHP(int hp)
    {
        if (hpSlider != null)
        {
            hpSlider.minValue = 0;
            hpSlider.maxValue = 100;
            hpSlider.value = hp;
        }
    }

    public void UpdateHP(int hp)
    {
        if (livesText != null)
            livesText.text = hp.ToString();
        SetHP(hp);
    }
}