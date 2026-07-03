using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// MGUI — Pause menu khi nhấn ESC hoặc Setting Button.
/// Gồm: Resume, Setting, Quit.
/// Gắn vào Setting Button (hoặc object quản lý UI).
/// </summary>
public class MGUISettingToggle : MonoBehaviour
{
    [Header("Pause Panel")]
    [SerializeField] private GameObject pausePanel;    // kéo PausePanel vào
    [SerializeField] private Button settingButton;     // nút gear/ESC trigger

    [Header("Setting Canvas")]
    [SerializeField] private GameObject settingPanel;  // kéo SettingCanvas vào

    [Header("Scene")]
    [SerializeField] private string menuSceneName = "UI menu"; // tên scene menu

    private bool _isPauseOpen = false;

    private void Awake()
    {
        if (settingButton == null)
            settingButton = GetComponent<Button>();

        settingButton.onClick.AddListener(TogglePause);

        // Đảm bảo cả 2 panel ẩn lúc đầu
        if (pausePanel != null)
            pausePanel.SetActive(false);
        if (settingPanel != null)
            settingPanel.SetActive(false);
    }

    private void Update()
    {
        // Đổi KeyCode.Escape thành phím bạn muốn
        if (Input.GetKeyDown(KeyCode.Tab)) // hoặc P, F10, Backspace...
        {
            if (settingPanel != null && settingPanel.activeSelf)
            {
                CloseSettingPanel();
                return;
            }

            TogglePause();
        }
    }

    // ----------------------------------------------------------------
    // Pause Panel
    // ----------------------------------------------------------------

    public void TogglePause()
    {
        _isPauseOpen = !_isPauseOpen;
        ApplyPauseState(_isPauseOpen);
    }

    private void ApplyPauseState(bool isOpen)
    {
        if (pausePanel != null)
            pausePanel.SetActive(isOpen);

        // Khóa input di chuyển
        if (PlayerInputHandler.Instance != null)
            PlayerInputHandler.Instance.InputEnabled = !isOpen;

        // Khóa xoay camera
        if (CameraManager.Instance != null)
            CameraManager.Instance.SetCameraRotationLocked(isOpen);

        // Cursor
        if (CursorManager.Instance != null)
        {
            if (isOpen) CursorManager.Instance.ShowCursor();
            else CursorManager.Instance.HideCursor();
        }
    }

    // ----------------------------------------------------------------
    // 3 nút bên trong Pause Panel
    // ----------------------------------------------------------------

    /// <summary>
    /// Nút Resume — đóng Pause Panel, tiếp tục gameplay.
    /// </summary>
    public void Resume()
    {
        _isPauseOpen = false;
        ApplyPauseState(false);
    }

    /// <summary>
    /// Nút Setting — ẩn Pause Panel, mở Setting Canvas.
    /// </summary>
    public void OpenSetting()
    {
        if (pausePanel != null)
            pausePanel.SetActive(false);

        if (settingPanel != null)
            settingPanel.SetActive(true);
    }

    /// <summary>
    /// Nút Quit — quay về scene Menu.
    /// </summary>
    public void QuitToMenu()
    {
        // Reset trạng thái trước khi rời scene
        if (PlayerInputHandler.Instance != null)
            PlayerInputHandler.Instance.InputEnabled = true;

        if (CameraManager.Instance != null)
            CameraManager.Instance.SetCameraRotationLocked(false);

        SceneManager.LoadScene(menuSceneName);
    }

    // ----------------------------------------------------------------
    // Đóng Setting Canvas (gọi từ nút Back trong SettingCanvas)
    // ----------------------------------------------------------------

    public void CloseSettingPanel()
    {
        if (settingPanel != null)
            settingPanel.SetActive(false);

        // Mở lại Pause Panel khi đóng Setting
        if (pausePanel != null)
            pausePanel.SetActive(true);
    }

    /// <summary>
    /// Gọi từ nút Back/Close bên trong Setting panel.
    /// </summary>
    public void Close()
    {
        if (!_isPauseOpen) return;
        Resume();
    }
}