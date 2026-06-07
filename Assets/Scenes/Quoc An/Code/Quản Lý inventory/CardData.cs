using UnityEngine;

/// <summary>
/// ScriptableObject lưu thông tin của 1 loại lá bài.
/// Tạo asset: Right-click > Create > Inventory > Card Data
/// </summary>
[CreateAssetMenu(fileName = "NewCard", menuName = "Inventory/Card Data")]
public class CardData : ScriptableObject
{
    [Header("Hiển thị")]
    public string cardName = "Card Name";
    public Sprite cardImage;

    [TextArea(2, 5)]
    public string description = "Mô tả công dụng lá bài.";

    [Header("Số lượng (Test Local)")]
    [Tooltip("Số lượng lá bài này trong túi. Dùng để test offline.")]
    public int quantity = 1;

    [Header("Network Stub - Chưa kết nối")]
    [Tooltip("ID dùng cho server. Chưa active, chuẩn bị sẵn.")]
    public string networkCardID = "";

    // ─── Network stub ─────────────────────────────────────────────────────────
    // TODO: Thay thế 'quantity' bằng hàm này khi kết nối Photon Fusion
    // public int GetNetworkQuantity() => NetworkInventoryManager.Instance.GetQuantity(networkCardID);
}