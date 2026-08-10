using UnityEngine;

/// <summary>
/// Banner "Your Turn" — chỉ SetActive true/false, không cần đổi text.
/// Chỉ hiện cho local player đang tới lượt, không đồng bộ qua network.
/// </summary>
public class BoardYourTurnUI : MonoBehaviour
{
    public static BoardYourTurnUI Instance { get; private set; }

    [SerializeField] private GameObject banner;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (banner != null)
            banner.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void Show()
    {
        if (banner != null) banner.SetActive(true);
    }

    public void Hide()
    {
        if (banner != null) banner.SetActive(false);
    }
}