using TMPro;
using UnityEngine;

public class MG3PlayerRow : MonoBehaviour
{
    [SerializeField] private TMP_Text rankText;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text scoreText;

    public void Setup(int rank, string playerName, int score)
    {
        rankText.text = rank + "st";
        nameText.text = playerName;
        scoreText.text = score.ToString();
    }
}