using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MinigamePlayerEntryUI : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private Sprite[] characterAvatars;
    [Header("Character Backgrounds")]
    [Tooltip("Index khớp với CharacterIndex (0-3): Panda, Chồn, Thỏ, Ếch")]
    [SerializeField] private Sprite[] characterBackgrounds;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text livesText;
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private Slider hpSlider;  // HP bar slider (0-100)
    [SerializeField] private GameObject eliminatedOverlay;
    [SerializeField] private Image backgroundImage;

    // runtime id for lookup
    public int PlayerId { get; private set; } = -1;

    private void Awake()
    {
        if (backgroundImage == null)
        {
            Transform background = transform.Find("Background");
            if (background != null)
                backgroundImage = background.GetComponent<Image>();
        }
    }

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
            iconImage.color = Color.white;

        if (backgroundImage != null)
            backgroundImage.color = Color.white;

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
        if (characterIndex < 0)
            return;

        if (iconImage != null && characterAvatars != null &&
            characterIndex < characterAvatars.Length &&
            characterAvatars[characterIndex] != null)
        {
            iconImage.sprite = characterAvatars[characterIndex];
        }

        if (backgroundImage != null && characterBackgrounds != null &&
            characterIndex < characterBackgrounds.Length &&
            characterBackgrounds[characterIndex] != null)
        {
            backgroundImage.sprite = characterBackgrounds[characterIndex];
            backgroundImage.enabled = true;
        }
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