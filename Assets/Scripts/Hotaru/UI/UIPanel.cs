using UnityEngine;

public enum UIPanelType
{
    Lobby,
    Voting,         // Vote chọn minigame (MinigameOnly)
    RouletteVoting, // Vote Roulette hoặc Minigame (RouletteOrMinigame)
    Scoreboard,
    Result
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
