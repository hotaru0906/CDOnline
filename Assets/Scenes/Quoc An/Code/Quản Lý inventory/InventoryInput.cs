using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Nhận input phím B và nút bấm để toggle Inventory Panel.
/// 
/// ⚠️ QUAN TRỌNG: Script này PHẢI nằm trên 1 GameObject LUÔN ACTIVE.
/// KHÔNG đặt script này lên Panel Inventory (vì panel bị ẩn → SetActive/alpha=0
/// sẽ khiến Update() không chạy và không nhận được phím B).
/// 
/// Gợi ý: Đặt script này lên cùng GameObject với UIManager.
/// </summary>
public class InventoryInput : MonoBehaviour
{
    [Header("Liên kết UIManager")]
    [Tooltip("Kéo CanvasGroup của Panel_Inventory vào đây")]
    [SerializeField] private CanvasGroup inventoryPanel;

    [Header("Nút bật Inventory (tùy chọn)")]
    [Tooltip("Kéo Button bật inventory vào đây nếu có button trên HUD")]
    [SerializeField] private Button toggleButton;

    [Header("Phím tắt")]
    [Tooltip("Phím bàn phím để toggle inventory")]
    [SerializeField] private KeyCode toggleKey = KeyCode.B;

    // ─── Internal ─────────────────────────────────────────────────────────────
    private bool isInventoryOpen = false;

    // ─────────────────────────────────────────────────────────────────────────

    void Start()
    {
        // Đảm bảo inventory ẩn lúc bắt đầu
        if (inventoryPanel != null)
            SetInventoryVisible(false);

        // Gắn sự kiện cho button nếu có
        if (toggleButton != null)
            toggleButton.onClick.AddListener(ToggleInventory);
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleKey))
            ToggleInventory();
    }

    /// <summary>
    /// Toggle mở/đóng inventory.
    /// </summary>
    public void ToggleInventory()
    {
        isInventoryOpen = !isInventoryOpen;
        SetInventoryVisible(isInventoryOpen);
    }

    /// <summary>
    /// Mở inventory (gọi từ code bên ngoài nếu cần).
    /// </summary>
    public void OpenInventory()
    {
        isInventoryOpen = true;
        SetInventoryVisible(true);
    }

    /// <summary>
    /// Đóng inventory (gọi từ code bên ngoài nếu cần).
    /// </summary>
    public void CloseInventory()
    {
        isInventoryOpen = false;
        SetInventoryVisible(false);
    }

    /// <summary>
    /// Hiển thị hoặc ẩn inventory panel thông qua CanvasGroup.
    /// Dùng CanvasGroup thay vì SetActive để tránh kill Update() của các script con.
    /// </summary>
    private void SetInventoryVisible(bool visible)
    {
        if (inventoryPanel == null)
        {
            Debug.LogWarning("[InventoryInput] Chưa gán inventoryPanel!");
            return;
        }

        inventoryPanel.alpha          = visible ? 1f : 0f;
        inventoryPanel.interactable   = visible;
        inventoryPanel.blocksRaycasts = visible;

        // Đồng bộ với UIManager nếu cần
        // TODO: Nếu muốn inventory tham gia navigation history của UIManager:
        // if (visible) UIManager.Instance.NavigateTo(inventoryPanel);
        // else UIManager.Instance.NavigateBack();
    }
}