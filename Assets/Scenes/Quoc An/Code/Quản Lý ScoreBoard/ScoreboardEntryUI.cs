using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ScoreboardEntryUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TextMeshProUGUI rankText;
    [SerializeField] private Image characterIconImage;
    [SerializeField] private TextMeshProUGUI playerNameText;
    [SerializeField] private TextMeshProUGUI scoreText;

    [Header("Rank Colors")]
    [SerializeField] private Color colorRank1 = new Color(1f, 0.85f, 0.1f);
    [SerializeField] private Color colorRank2 = new Color(0.75f, 0.75f, 0.75f);
    [SerializeField] private Color colorRank3 = new Color(0.8f, 0.5f, 0.2f);
    [SerializeField] private Color colorDefault = Color.white;

    public void Setup(int rank, ScoreboardEntry entry)
    {
        rankText.text = rank.ToString();
        playerNameText.text = entry.playerName;
        scoreText.text = entry.score.ToString("N0") + " pts";

        if (entry.characterIcon != null)
            characterIconImage.sprite = entry.characterIcon;

        rankText.color = rank switch
        {
            1 => colorRank1,
            2 => colorRank2,
            3 => colorRank3,
            _ => colorDefault
        };
    }
}