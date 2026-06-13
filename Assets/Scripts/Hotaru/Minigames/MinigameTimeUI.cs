using UnityEngine;
using TMPro;

public class MinigameTimeUI : MonoBehaviour
{
    [SerializeField] private TMP_Text timeText;

    public void UpdateTime(float seconds)
    {
        int minutes = Mathf.FloorToInt(seconds / 60f);
        int secs = Mathf.FloorToInt(seconds % 60f);
        timeText.text = $"{minutes:00}:{secs:00}";
    }
}
