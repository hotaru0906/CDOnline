using TMPro;
using UnityEngine;

public class MG3HammerTimerUI : MonoBehaviour
{
    public static MG3HammerTimerUI Instance { get; private set; }

    [SerializeField] private TMP_Text timerText;

    private void Awake()
    {
        Instance = this;
    }

    public void Show()
    {
        timerText.gameObject.SetActive(true);
    }

    public void Hide()
    {
        timerText.gameObject.SetActive(false);
    }
}