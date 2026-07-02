using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Toggle Panel khi nhấn Button hoặc Tab
/// Tự động ẩn/hiện button cùng panel
/// </summary>
public class SimplePanelToggle : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject panel;           // Panel muốn toggle
    [SerializeField] private GameObject toggleButton;    // Button sẽ ẩn khi panel hiện

    [Header("Settings")]
    [SerializeField] private KeyCode toggleKey = KeyCode.Tab;  // Phím toggle

    private bool _isPanelOpen = false;

    private void Start()
    {
        // Khởi tạo: Panel ẩn, Button hiện
        if (panel != null)
            panel.SetActive(false);
        
        if (toggleButton != null)
            toggleButton.SetActive(true);

        _isPanelOpen = false;
    }

    private void Update()
    {
        // Nhấn phím để toggle
        if (Input.GetKeyDown(toggleKey))
        {
            TogglePanel();
        }
    }

    /// <summary>
    /// Gọi hàm này từ Button.onClick
    /// </summary>
    public void TogglePanel()
    {
        _isPanelOpen = !_isPanelOpen;
        ApplyState();
    }

    private void ApplyState()
    {
        if (panel != null)
            panel.SetActive(_isPanelOpen);

        if (toggleButton != null)
            toggleButton.SetActive(!_isPanelOpen);  // Ngược lại với panel

        // Hiện cursor khi panel mở
        Cursor.visible = _isPanelOpen;
        Cursor.lockState = _isPanelOpen ? CursorLockMode.None : CursorLockMode.Locked;
    }

    /// <summary>
    /// Đóng panel (gọi từ nút Close trong Panel)
    /// </summary>
    public void ClosePanel()
    {
        _isPanelOpen = false;
        ApplyState();
    }
}