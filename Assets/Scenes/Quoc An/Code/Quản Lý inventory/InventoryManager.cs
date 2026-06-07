using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// Quản lý toàn bộ fan bài trong Inventory Panel.
/// - Spawn CardUI prefab theo danh sách CardData
/// - Tính toán vị trí fan (xèo bài) tự động
/// - Hỗ trợ scroll ngang khi nhiều bài
/// - Test được qua Inspector (thêm/xóa card trong danh sách)
/// </summary>
public class InventoryManager : MonoBehaviour
{
    // ─── References ──────────────────────────────────────────────────────────
    [Header("Prefab & Container")]
    [Tooltip("Prefab 1 lá bài (có gắn CardUI component)")]
    [SerializeField] private GameObject cardPrefab;

    [Tooltip("ScrollRect chứa tất cả lá bài")]
    [SerializeField] private ScrollRect scrollRect;

    [Tooltip("Content bên trong ScrollRect (RectTransform)")]
    [SerializeField] private RectTransform cardContainer;

    // ─── Dữ liệu bài ─────────────────────────────────────────────────────────
    [Header("Danh sách bài (kéo CardData vào đây để test)")]
    [SerializeField] private List<CardData> cards = new List<CardData>();

    // ─── Fan layout settings ──────────────────────────────────────────────────
    [Header("Fan Layout")]
    [Tooltip("Khoảng cách ngang giữa tâm các lá bài")]
    [SerializeField] private float cardSpacing = 110f;

    [Tooltip("Độ cong fan: mỗi lá nghiêng thêm bao nhiêu độ so với lá liền kề")]
    [SerializeField] private float fanRotationStep = 5f;

    [Tooltip("Chiều cao nổi ở giữa fan (lá giữa cao nhất)")]
    [SerializeField] private float fanHeightCurve = 20f;

    // ─── Internal ─────────────────────────────────────────────────────────────
    private List<CardUI> spawnedCards = new List<CardUI>();

    // ─── Network stub ─────────────────────────────────────────────────────────
    // TODO: Thay cards local bằng data từ server
    // public void LoadFromNetwork(List<NetworkCardEntry> networkCards) { ... }

    // ─────────────────────────────────────────────────────────────────────────

    void OnEnable()
    {
        // Mỗi lần mở inventory, refresh lại để phản ánh data mới nhất
        BuildInventory();
    }

    /// <summary>
    /// Xóa bài cũ và build lại toàn bộ fan từ danh sách cards.
    /// </summary>
    public void BuildInventory()
    {
        ClearCards();

        if (cards == null || cards.Count == 0) return;
        if (cardPrefab == null)
        {
            Debug.LogWarning("[InventoryManager] Chưa gán cardPrefab!");
            return;
        }
        if (cardContainer == null)
        {
            Debug.LogWarning("[InventoryManager] Chưa gán cardContainer!");
            return;
        }

        int count = cards.Count;

        // Tính tổng chiều rộng content để scroll đúng
        float totalWidth = Mathf.Max(cardSpacing * count, 400f);
        cardContainer.sizeDelta = new Vector2(totalWidth, cardContainer.sizeDelta.y);

        for (int i = 0; i < count; i++)
        {
            if (cards[i] == null) continue;

            GameObject go = Instantiate(cardPrefab, cardContainer);
            CardUI cardUI = go.GetComponent<CardUI>();
            if (cardUI == null)
            {
                Debug.LogWarning($"[InventoryManager] Prefab thiếu CardUI component ở index {i}");
                continue;
            }

            // Inject data
            cardUI.Setup(cards[i]);

            // Tính vị trí fan
            Vector2 fanPos = CalculateFanPosition(i, count);
            float fanRot  = CalculateFanRotation(i, count);

            // Đặt rotation
            go.GetComponent<RectTransform>().localRotation = Quaternion.Euler(0, 0, fanRot);

            // Lưu vị trí gốc cho hover
            cardUI.SetDefaultPosition(fanPos);

            spawnedCards.Add(cardUI);
        }
    }

    /// <summary>
    /// Tính vị trí anchoredPosition của lá bài index i trong fan.
    /// Fan được căn giữa theo chiều ngang.
    /// </summary>
    private Vector2 CalculateFanPosition(int index, int total)
    {
        // Căn giữa: lá đầu tiên ở -(total-1)/2 * spacing, lá cuối ở +(total-1)/2 * spacing
        float centerOffset = (total - 1) / 2f;
        float x = (index - centerOffset) * cardSpacing;

        // Đường cong: lá giữa cao nhất, hai bên thấp hơn
        // Dùng hàm parabol đảo: y = -fanHeightCurve * ((i - center)/center)^2 + fanHeightCurve
        float normalizedPos = total > 1 ? (index - centerOffset) / centerOffset : 0f;
        float y = fanHeightCurve * (1f - normalizedPos * normalizedPos);

        return new Vector2(x, y);
    }

    /// <summary>
    /// Tính góc xoay (Z) của lá bài index i trong fan.
    /// Lá giữa thẳng đứng (0 độ), hai bên nghiêng ra.
    /// </summary>
    private float CalculateFanRotation(int index, int total)
    {
        float centerOffset = (total - 1) / 2f;
        // Lá bên trái nghiêng dương (ngả phải), bên phải nghiêng âm (ngả trái)
        return (centerOffset - index) * fanRotationStep;
    }

    /// <summary>
    /// Xóa tất cả card đã spawn.
    /// </summary>
    private void ClearCards()
    {
        foreach (var c in spawnedCards)
        {
            if (c != null) Destroy(c.gameObject);
        }
        spawnedCards.Clear();
    }

    // ─── Inspector Test Helpers ───────────────────────────────────────────────

    /// <summary>
    /// Gọi hàm này từ Inspector (nút test) hoặc code để thêm 1 lá bài runtime.
    /// </summary>
    public void AddCard(CardData newCard)
    {
        if (newCard == null) return;
        cards.Add(newCard);
        BuildInventory(); // Rebuild fan
    }

    /// <summary>
    /// Xóa lá bài khỏi danh sách theo CardData reference.
    /// </summary>
    public void RemoveCard(CardData cardToRemove)
    {
        if (cards.Contains(cardToRemove))
        {
            cards.Remove(cardToRemove);
            BuildInventory();
        }
    }

    // ─── Network stub ─────────────────────────────────────────────────────────
    // TODO: Gọi hàm này khi nhận event từ Photon Fusion (server cập nhật inventory)
    // public void OnNetworkInventoryUpdated(List<CardData> updatedList)
    // {
    //     cards = updatedList;
    //     BuildInventory();
    // }
}