using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// UI card cho minigame voting, hỗ trợ cả click và drag-to-select
/// Khi kéo xuống đủ khoảng cách sẽ chọn minigame này
/// Card KHÔNG biến mất khi kéo - chỉ thay đổi visual để hiển thị trạng thái
/// </summary>
public class MinigameCardUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerDownHandler, IPointerUpHandler
{
    [Header("UI Components")]
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text voteCountText;
    [SerializeField] private TMP_Text minigameNameText;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image iconImage;
    
    [Header("Visual Settings")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color selectedColor = Color.green;
    [SerializeField] private Color draggingColor = new Color(0.7f, 0.9f, 1f, 1f); // Màu khi đang kéo
    [SerializeField] private Color confirmZoneColor = new Color(0.5f, 1f, 0.5f, 1f); // Màu khi vào vùng xác nhận
    
    [Header("Drag Settings")]
    [SerializeField] private float dragThreshold = 60f; // Khoảng cách để bắt đầu coi là đang chọn
    [SerializeField] private float confirmThreshold = 160f; // Khoảng cách để xác nhận chọn
    [SerializeField] private float scaleOnDrag = 1.08f; // Scale khi đang kéo
    
    [Header("Scroll Integration")]
    [SerializeField] private bool integrateWithScrollRect = true;
    
    // Private references
    private RectTransform rectTransform;
    private Canvas canvas;
    private ScrollRect scrollRect;
    private CanvasGroup canvasGroup;
    private Vector3 originalScale;
    
    // State
    private int minigameIndex;
    private VotingUI votingUI;
    private Vector2 pointerStartPosition;
    private bool isDraggingForSelection = false;
    private bool isInConfirmZone = false;
    private bool hasVoted = false;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();
        scrollRect = GetComponentInParent<ScrollRect>();
        
        // Add CanvasGroup if not exists (for visual feedback)
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
        
        originalScale = rectTransform.localScale;
    }

    #region Setup
    public void Setup(int index, VotingUI ui)
    {
        minigameIndex = index;
        votingUI = ui;

        // Button click still works as fallback
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnVoteClicked);
        }

        UpdateVoteCount(0);
        SetSelected(false);
        ResetVisual();
    }

    public void Setup(int index, VotingUI ui, MinigameData minigameData)
    {
        Setup(index, ui);
        
        if (minigameData != null)
        {
            if (minigameNameText != null)
                minigameNameText.text = minigameData.minigameName;
            
            if (iconImage != null && minigameData.icon != null)
                iconImage.sprite = minigameData.icon;
        }
    }
    #endregion

    #region Pointer Events
    public void OnPointerDown(PointerEventData eventData)
    {
        pointerStartPosition = eventData.position;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        // Reset nếu không phải drag
        if (!isDraggingForSelection)
        {
            ResetVisual();
        }
    }
    #endregion

    #region Drag Events
    public void OnBeginDrag(PointerEventData eventData)
    {
        isDraggingForSelection = false;
        isInConfirmZone = false;
        pointerStartPosition = eventData.position;

        // Forward to ScrollRect for horizontal scrolling
        if (integrateWithScrollRect && scrollRect != null)
        {
            scrollRect.OnBeginDrag(eventData);
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (hasVoted) return;

        // Tính khoảng cách kéo xuống
        float downwardDistance = pointerStartPosition.y - eventData.position.y;

        if (isDraggingForSelection)
        {
            // Đã vào chế độ chọn - cập nhật visual dựa trên khoảng cách
            UpdateDragVisual(downwardDistance);
        }
        else
        {
            // Kiểm tra có nên chuyển sang chế độ chọn không
            if (downwardDistance > dragThreshold)
            {
                // Chuyển sang chế độ chọn
                isDraggingForSelection = true;
                
                // Dừng scroll ngang
                if (integrateWithScrollRect && scrollRect != null)
                {
                    scrollRect.OnEndDrag(eventData);
                }
                
                // Visual feedback - card vẫn ở vị trí cũ nhưng thay đổi màu/scale
                OnEnterSelectionMode();
            }
            else
            {
                // Vẫn đang scroll ngang bình thường
                if (integrateWithScrollRect && scrollRect != null)
                {
                    scrollRect.OnDrag(eventData);
                }
            }
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (isDraggingForSelection)
        {
            float downwardDistance = pointerStartPosition.y - eventData.position.y;

            if (downwardDistance > confirmThreshold && !hasVoted)
            {
                // Xác nhận chọn minigame này
                OnConfirmSelection();
            }
            else
            {
                // Hủy chọn - reset visual
                OnCancelSelection();
            }

            isDraggingForSelection = false;
            isInConfirmZone = false;
        }
        else
        {
            // Forward to ScrollRect
            if (integrateWithScrollRect && scrollRect != null)
            {
                scrollRect.OnEndDrag(eventData);
            }
        }
        
        ResetVisual();
    }
    #endregion

    #region Selection Methods
    private void OnEnterSelectionMode()
    {
        Debug.Log($"[MinigameCardUI] Enter selection mode for minigame #{minigameIndex}");
        
        // Scale up nhẹ để cho thấy đang được chọn
        rectTransform.localScale = originalScale * scaleOnDrag;
        
        // Đổi màu
        if (backgroundImage != null)
        {
            backgroundImage.color = draggingColor;
        }
    }

    private void UpdateDragVisual(float downwardDistance)
    {
        bool wasInConfirmZone = isInConfirmZone;
        isInConfirmZone = downwardDistance > confirmThreshold;

        if (isInConfirmZone != wasInConfirmZone)
        {
            // Đổi visual khi vào/ra vùng xác nhận
            if (backgroundImage != null)
            {
                backgroundImage.color = isInConfirmZone ? confirmZoneColor : draggingColor;
            }
            
            // Scale feedback
            float targetScale = isInConfirmZone ? scaleOnDrag * 1.05f : scaleOnDrag;
            rectTransform.localScale = originalScale * targetScale;
        }
    }

    private void OnConfirmSelection()
    {
        Debug.Log($"[MinigameCardUI] Confirmed selection for minigame #{minigameIndex}");
        
        // Vote cho minigame này
        if (votingUI != null)
        {
            votingUI.OnVote(minigameIndex);
            hasVoted = true;
        }
        
        // Visual feedback
        SetSelected(true);
    }

    private void OnCancelSelection()
    {
        Debug.Log($"[MinigameCardUI] Cancelled selection for minigame #{minigameIndex}");
        ResetVisual();
    }

    private void ResetVisual()
    {
        rectTransform.localScale = originalScale;
        
        if (!hasVoted && backgroundImage != null)
        {
            backgroundImage.color = normalColor;
        }
    }
    #endregion

    #region Button Click (Fallback)
    private void OnVoteClicked()
    {
        if (hasVoted) return;
        
        if (votingUI != null)
        {
            votingUI.OnVote(minigameIndex);
            hasVoted = true;
        }
    }
    #endregion

    #region Public Methods
    public void UpdateVoteCount(int count)
    {
        if (voteCountText != null)
        {
            voteCountText.text = count.ToString();
        }
    }

    public void SetInteractable(bool interactable)
    {
        if (button != null)
        {
            button.interactable = interactable;
        }
        
        // Cập nhật alpha khi không tương tác được
        if (canvasGroup != null)
        {
            canvasGroup.alpha = interactable ? 1f : 0.6f;
        }
    }

    public void SetSelected(bool selected)
    {
        hasVoted = selected;
        
        if (backgroundImage != null)
        {
            backgroundImage.color = selected ? selectedColor : normalColor;
        }
    }

    /// <summary>
    /// Reset card về trạng thái ban đầu
    /// </summary>
    public void ResetCard()
    {
        hasVoted = false;
        isDraggingForSelection = false;
        isInConfirmZone = false;
        UpdateVoteCount(0);
        SetSelected(false);
        SetInteractable(true);
        ResetVisual();
    }

    /// <summary>
    /// Lấy index của minigame
    /// </summary>
    public int MinigameIndex => minigameIndex;
    #endregion
}