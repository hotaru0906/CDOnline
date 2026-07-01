using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// MGUI — Toggle Setting panel khi nhấn button hoặc ESC.
/// Khóa input di chuyển + xoay camera khi mở.
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

        if (PlayerInputHandler.Instance != null)
            PlayerInputHandler.Instance.InputEnabled = !_isOpen;

        if (CameraManager.Instance != null)
            CameraManager.Instance.SetCameraRotationLocked(_isOpen);

        if (CursorManager.Instance != null)
        {
            if (_isOpen)
            {
                CursorManager.Instance.ShowCursor(); // luôn hiện cursor khi mở Setting
            }
            else
            {
                CursorManager.Instance.HideCursor(); // ẩn lại khi đóng, về lại gameplay
            }
        }
    }

    /// <summary>
    /// Gọi từ nút Back/Close bên trong Setting panel.
    /// </summary>
    public void Close()
    {
        if (!_isOpen) return; // tránh toggle ngược nếu đã đóng
        Toggle();
    }
}