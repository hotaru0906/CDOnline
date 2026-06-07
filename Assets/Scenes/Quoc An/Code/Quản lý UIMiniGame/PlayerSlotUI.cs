using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerSlotUI : MonoBehaviour
{
    // ========================================================
    // INSPECTOR REFERENCES
    // ========================================================

    [Header("--- ICON ---")]
    [Tooltip("Image hiển thị icon player")]
    public Image iconImage;

    [Tooltip("Icon mặc định nếu không có icon riêng")]
    public Sprite defaultIcon;

    [Header("--- INFO ---")]
    [Tooltip("Text tên player")]
    public TextMeshProUGUI playerNameText;

    [Header("--- SCORE ---")]
    [Tooltip("Text điểm số")]
    public TextMeshProUGUI scoreText;

    [Tooltip("Background của score panel")]
    public Image scoreBackground;

    // ========================================================
    // RUNTIME
    // ========================================================

    private int currentScore = 0;

    // ========================================================
    // SETUP
    // ========================================================

    public void Setup(string playerName, Sprite icon, int score = 0)
    {
        // Tên player
        if (playerNameText != null)
            playerNameText.text = playerName;

        // Icon
        if (iconImage != null)
        {
            iconImage.sprite = (icon != null) ? icon : defaultIcon;
            iconImage.preserveAspect = true;
        }

        // Score ban đầu
        SetScore(score);

        // Màu background score theo tên
        if (scoreBackground != null)
            scoreBackground.color = GetSlotColor(playerName);
    }

    // ========================================================
    // CẬP NHẬT SCORE
    // ========================================================

    public void SetScore(int score)
    {
        currentScore = score;

        if (scoreText != null)
            scoreText.text = currentScore.ToString();
    }

    public void AddScore(int amount)
    {
        SetScore(currentScore + amount);
    }

    // ========================================================
    // MÀU SLOT THEO TÊN
    // ========================================================

    Color GetSlotColor(string playerName)
    {
        Color[] colors = {
            new Color(59f/255f,  130f/255f, 246f/255f, 0.3f), // xanh dương
            new Color(139f/255f, 92f/255f,  246f/255f, 0.3f), // tím
            new Color(34f/255f,  197f/255f, 94f/255f,  0.3f), // xanh lá
            new Color(249f/255f, 115f/255f, 22f/255f,  0.3f), // cam
        };
        int index = Mathf.Abs(playerName.GetHashCode()) % colors.Length;
        return colors[index];
    }
}
