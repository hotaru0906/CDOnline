using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// MGUI — Toggle Setting panel khi nhấn button hoặc ESC.
/// Gắn vào Setting Button.
/// </summary>
public class MGUISettingToggle : MonoBehaviour
{
    [SerializeField] private GameObject settingPanel;
    [SerializeField] private Button settingButton;

    private bool _isOpen = false;

    private void Awake()
    {
        if (settingButton == null)
            settingButton = GetComponent<Button>();

        settingButton.onClick.AddListener(Toggle);

        // Đảm bảo panel ẩn lúc đầu
        if (settingPanel != null)
            settingPanel.SetActive(false);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            Toggle();
    }

    public void Toggle()
    {
        _isOpen = !_isOpen;
        if (settingPanel != null)
            settingPanel.SetActive(_isOpen);

        // Pause input khi mở setting
        if (PlayerInputHandler.Instance != null)
            PlayerInputHandler.Instance.InputEnabled = !_isOpen;

        // Cursor
        if (CursorManager.Instance != null)
        {
            if (_isOpen) CursorManager.Instance.ShowCursor();
            else CursorManager.Instance.HideCursor();
        }
    }

    /// <summary>
    /// Gọi từ nút Close bên trong Setting panel.
    /// </summary>
    public void Close()
    {
        _isOpen = true;
        Toggle();
    }
}