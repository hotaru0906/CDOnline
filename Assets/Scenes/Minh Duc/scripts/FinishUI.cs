using UnityEngine;

public class FinishUI : MonoBehaviour
{
    public static FinishUI Instance;

    [SerializeField] private GameObject finishPanel;

    private void Awake()
    {
        Instance = this;

        if (finishPanel != null)
            finishPanel.SetActive(false);
    }

    public void ShowFinish()
    {
        if (finishPanel != null)
            finishPanel.SetActive(true);
    }
}