using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class DraggableMapCard : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("Thông tin map")]
    public string mapName = "Forest Arena";

    private RectTransform rectTransform;
    private Canvas canvas;
    private ScrollRect scrollRect;

    private Vector2 originalPosition;
    private Transform originalParent;
    private Vector2 pointerStartPosition;   // vị trí chuột khi bắt đầu kéo
    private bool isSelecting = false;       // đang ở chế độ chọn map

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();
        scrollRect = GetComponentInParent<ScrollRect>();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        isSelecting = false;
        pointerStartPosition = eventData.position;

        // Luôn cho Scroll Rect scroll ngang trước
        scrollRect?.OnBeginDrag(eventData);

        // Lưu vị trí gốc để reset sau
        originalPosition = rectTransform.anchoredPosition;
        originalParent = transform.parent;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (isSelecting)
        {
            // Đang kéo card tự do
            rectTransform.anchoredPosition += eventData.delta / canvas.scaleFactor;
            return;
        }

        // Bình thường vẫn scroll ngang
        scrollRect?.OnDrag(eventData);

        // Kiểm tra xem có đang kéo xuống không
        float downwardDistance = pointerStartPosition.y - eventData.position.y;

        if (downwardDistance > 60f) // ← bạn có thể chỉnh số này (50-80 tùy độ nhạy)
        {
            // Chuyển sang chế độ chọn map
            scrollRect?.OnEndDrag(eventData);   // dừng scroll ngang
            isSelecting = true;

            // Lift card ra Canvas để kéo tự do
            transform.SetParent(canvas.transform, true);
            rectTransform.SetAsLastSibling();
            rectTransform.localScale = Vector3.one * 1.12f;
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (isSelecting)
        {
            float downwardDistance = pointerStartPosition.y - eventData.position.y;

            if (downwardDistance > 160f) // ← khoảng cách để xác nhận chọn map (có thể chỉnh)
            {
                Debug.Log($"ĐÃ CHỌN MAP: {mapName}");
                // TODO: thêm animation chọn, highlight, hoặc gọi event chọn map ở đây
            }

            // Reset card về vị trí cũ
            transform.SetParent(originalParent, true);
            rectTransform.anchoredPosition = originalPosition;
            rectTransform.localScale = Vector3.one;

            isSelecting = false;
        }
        else
        {
            scrollRect?.OnEndDrag(eventData);
        }
    }
}