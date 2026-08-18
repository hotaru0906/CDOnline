using UnityEngine;
public class CursorManager : MonoBehaviour
{
    public static CursorManager Instance { get; private set; }

    private bool _isVisible;
    private CursorLockMode _lockMode;

    public bool IsVisible => _isVisible;
    public CursorLockMode LockMode => _lockMode;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Show cursor ngay trong Awake để đảm bảo hiển thị trước mọi thứ
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    private void Start()
    {
        // Đảm bảo UI mode khi bắt đầu (Main Menu)
        SetUIMode();
    }
    public void SetGameplayMode()
    {
        SetCursor(false, CursorLockMode.Locked);
    }

    public void SetUIMode()
    {
        SetCursor(true, CursorLockMode.None);
    }

    public void SetMinigameMode(bool showCursor)
    {
        SetCursor(showCursor, showCursor ? CursorLockMode.None : CursorLockMode.Locked);
    }
    public void SetCursor(bool visible, CursorLockMode lockMode)
    {
        _isVisible = visible;
        _lockMode = lockMode;

        Cursor.visible = visible;
        Cursor.lockState = lockMode;
    }
    public void ShowCursor()
    {
        SetCursor(true, CursorLockMode.None);
    }

    public void HideCursor()
    {
        SetCursor(false, CursorLockMode.Locked);
    }

    public void ToggleCursor()
    {
        SetCursor(!_isVisible, _isVisible ? CursorLockMode.Locked : CursorLockMode.None);
    }
}