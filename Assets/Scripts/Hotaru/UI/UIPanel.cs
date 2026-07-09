using UnityEngine;

public enum UIPanelType
{
    Lobby,
    Voting,             // Vote chọn minigame
    Scoreboard,
    Result,
    MinigameTutorial,   // Tutorial trong minigame scene (mỗi scene khác nhau)
    MinigameCountdown,   // Countdown UI chính (dùng chung, không theo scene)
    MinigameSetting,    // MỚI — Setting panel trong minigame
    MinigamePlayerHUD,   // MỚI — Player info HUD dưới màn hình
    MinigameTieBreaker
}

/// <summary>
/// Gắn vào mỗi UI Panel để tự đăng ký với GameManager.
/// Hoạt động ngay cả khi GameObject inactive.
/// </summary>
public class UIPanel : MonoBehaviour
{
    [SerializeField] private UIPanelType panelType;

    public UIPanelType PanelType => panelType;

    private void Awake()
    {
        // Đăng ký với GameManager ngay khi Awake (kể cả khi inactive)
        RegisterWithGameManager();
    }

    private void OnEnable()
    {
        // Đăng ký lại khi enable (phòng trường hợp GameManager spawn sau)
        RegisterWithGameManager();
    }

    private void RegisterWithGameManager()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.RegisterUIPanel(this);
        }
    }
}
