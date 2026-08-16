using UnityEngine;
using System.Collections;

/// <summary>
/// UISlideAnimator - Animate UI elements sliding in/out từ ngoài màn hình
/// Gán script này vào các ButtonGroup hoặc UI panel cần animate
/// </summary>
public class UISlideAnimator : MonoBehaviour
{
    [Header("Animation Settings")]
    [SerializeField] private float animationDuration = 0.5f;
    
    [Header("Positions")]
    [SerializeField] private Vector2 hiddenPosition = new(-1920, 0); // Vị trí ban đầu (ngoài màn hình)
    [SerializeField] private Vector2 visiblePosition = Vector2.zero;  // Vị trí xuất hiện (center)

    private RectTransform _rectTransform;
    private Coroutine _currentAnimation;

    private void OnEnable()
    {
        if (_rectTransform == null)
            _rectTransform = GetComponent<RectTransform>();
    }

    /// <summary>
    /// Animate element từ hidden position đến visible position (mở ra)
    /// </summary>
    public void Show()
    {
        ShowAsync();
    }

    /// <summary>
    /// Animate element từ visible position đến hidden position (đóng lại)
    /// </summary>
    public void Hide()
    {
        HideAsync();
    }

    /// <summary>
    /// Async version - có thể await hoặc track completion
    /// </summary>
    public Coroutine ShowAsync()
    {
        return AnimateSlide(hiddenPosition, visiblePosition);
    }

    /// <summary>
    /// Async version - có thể await hoặc track completion
    /// </summary>
    public Coroutine HideAsync()
    {
        return AnimateSlide(visiblePosition, hiddenPosition);
    }

    /// <summary>
    /// Set vị trí ban đầu mà không animate
    /// </summary>
    public void SetHiddenPositionImmediate()
    {
        if (_rectTransform == null)
            _rectTransform = GetComponent<RectTransform>();

        _rectTransform.anchoredPosition = hiddenPosition;
    }

    /// <summary>
    /// Set vị trí hiển thị mà không animate
    /// </summary>
    public void SetVisiblePositionImmediate()
    {
        if (_rectTransform == null)
            _rectTransform = GetComponent<RectTransform>();

        _rectTransform.anchoredPosition = visiblePosition;
    }

    private Coroutine AnimateSlide(Vector2 startPos, Vector2 endPos)
    {
        // Stop animation hiện tại nếu còn chạy
        if (_currentAnimation != null)
            StopCoroutine(_currentAnimation);

        if (_rectTransform == null)
            _rectTransform = GetComponent<RectTransform>();

        _currentAnimation = StartCoroutine(SlideCoroutine(startPos, endPos));
        return _currentAnimation;
    }

    private IEnumerator SlideCoroutine(Vector2 startPos, Vector2 endPos)
    {
        float elapsedTime = 0f;

        while (elapsedTime < animationDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.Clamp01(elapsedTime / animationDuration);
            
            // Easing: EaseInOutQuad
            t = t < 0.5f ? 2f * t * t : -1f + (4f - 2f * t) * t;

            _rectTransform.anchoredPosition = Vector2.Lerp(startPos, endPos, t);
            yield return null;
        }

        // Đảm bảo vị trí cuối cùng chính xác
        _rectTransform.anchoredPosition = endPos;
        _currentAnimation = null;
    }

    /// <summary>
    /// Visualize hidden position trong Inspector
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        if (_rectTransform == null)
            _rectTransform = GetComponent<RectTransform>();

        if (_rectTransform == null) return;

        // Vẽ vị trí hidden (đỏ)
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(new Vector3(hiddenPosition.x, hiddenPosition.y, 0), Vector3.one * 100);

        // Vẽ vị trí visible (xanh)
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(new Vector3(visiblePosition.x, visiblePosition.y, 0), Vector3.one * 100);
    }
}
