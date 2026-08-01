using TMPro;
using UnityEngine;

public class MG3HammerTimerText : MonoBehaviour
{
    public static MG3HammerTimerText Instance { get; private set; }

    [SerializeField] private TMP_Text timerText;

    private void Awake()
    {
        Instance = this;
    }

    public void Show()
    {
        timerText.enabled = true;
    }

    public void Hide()
    {
        timerText.enabled = false;
    }

    public void SetTime(float time)
    {
        timerText.text = Mathf.CeilToInt(time).ToString();
    }
}