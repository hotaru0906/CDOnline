using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Gắn lên prefab của 1 lá bài trong fan inventory.
/// Xử lý hover (nổi lên), hiển thị ảnh + tên + mô tả + số lượng.
/// </summary>
public class CardUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    // ─── References ──────────────────────────────────────────────────────────
    [Header("Ảnh & Nội dung")]
    [SerializeField] private Image cardImage;
    [SerializeField] private TextMeshProUGUI cardNameText;
    [SerializeField] private TextMeshProUGUI descriptionText;

    [Header("Số lượng (kiểu bài Tây)")]
    [Tooltip("Góc trái trên")]
    [SerializeField] private TextMeshProUGUI quantityTopLeft;
    [Tooltip("Góc phải dưới")]
    [SerializeField] private TextMeshProUGUI quantityBottomRight;

    // ─── Hover animation ─────────────────────────────────────────────────────
    [Header("Hover Settings")]
    [SerializeField] private float hoverRiseAmount = 40f;
    [Tooltip("Tốc độ animation nổi lên/xuống khi hover")]
    [SerializeField] private float hoverSpeed = 8f;

    // ─── Internal ─────────────────────────────────────────────────────────────
    private CardData data;
    private RectTransform rectTransform;
    private Vector2 defaultPosition;
    private Vector2 targetPosition;
    private bool isHovered = false;

    // ─── Sibling index gốc để đưa lá bài lên trên khi hover ─────────────────
    private int defaultSiblingIndex;

    // ─── Network stub ─────────────────────────────────────────────────────────
    // TODO: Thay quantity local bằng network khi online
    // private int networkQuantity;

    // ─────────────────────────────────────────────────────────────────────────

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
    }

    /// <summary>
    /// Gọi từ InventoryManager để inject data vào lá bài này.
    /// </summary>
    public void Setup(CardData cardData)
    {
        data = cardData;

        // Ảnh
        if (cardImage != null && data.cardImage != null)
            cardImage.sprite = data.cardImage;

        // Tên
        if (cardNameText != null)
            cardNameText.text = data.cardName;

        // Mô tả
        if (descriptionText != null)
            descriptionText.text = data.description;

        // Số lượng - lấy từ local, stub sẵn cho network
        int qty = GetQuantity();
        if (quantityTopLeft != null)
            quantityTopLeft.text = qty.ToString();
        if (quantityBottomRight != null)
            quantityBottomRight.text = qty.ToString();
    }

    /// <summary>
    /// Cập nhật lại số lượng hiển thị (gọi khi quantity thay đổi).
    /// </summary>
    public void RefreshQuantity()
    {
        if (data == null) return;
        int qty = GetQuantity();
        if (quantityTopLeft != null)  quantityTopLeft.text  = qty.ToString();
        if (quantityBottomRight != null) quantityBottomRight.text = qty.ToString();
    }

    /// <summary>
    /// Lấy số lượng. Hiện dùng local data.
    /// TODO: Khi online, thay bằng: return NetworkInventoryManager.Instance.GetQuantity(data.networkCardID);
    /// </summary>
    private int GetQuantity()
    {
        return data != null ? data.quantity : 0;
    }

    // ─── Hover ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Gọi từ InventoryManager sau khi card được đặt vào đúng vị trí fan.
    /// Lưu lại vị trí gốc để hover animation hoạt động đúng.
    /// </summary>
    public void SetDefaultPosition(Vector2 pos)
    {
        defaultPosition = pos;
        targetPosition = pos;
        rectTransform.anchoredPosition = pos;
        defaultSiblingIndex = transform.GetSiblingIndex();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovered = true;
        targetPosition = defaultPosition + Vector2.up * hoverRiseAmount;
        // Đưa lá bài này lên trên cùng để không bị che
        transform.SetAsLastSibling();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
        targetPosition = defaultPosition;
        // Trả lại thứ tự cũ
        transform.SetSiblingIndex(defaultSiblingIndex);
    }

    void Update()
    {
        // Lerp mượt lên/xuống
        rectTransform.anchoredPosition = Vector2.Lerp(
            rectTransform.anchoredPosition,
            targetPosition,
            Time.deltaTime * hoverSpeed
        );
    }
}