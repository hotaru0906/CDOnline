using UnityEngine;
using DG.Tweening;

public class UIVFX : MonoBehaviour
{
    [Header("Canvas Group")]
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Animation Settings")]
    [SerializeField] private float fadeInDuration = 0.8f;
    [SerializeField] private float fadeOutDuration = 0.5f;
    [SerializeField] private Vector3 startScale = Vector3.zero;
    [SerializeField] private Vector3 endScale = Vector3.one;
    
    // Public properties để các Manager có thể truy cập
    public float FadeOutDuration => fadeOutDuration;
    public float FadeInDuration => fadeInDuration;
    public CanvasGroup CanvasGroup => canvasGroup;

    private void Awake()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();
    }

    private void Start()
    {
        InitializeCanvas();
    }

    private void InitializeCanvas()
    {
        if (canvasGroup == null) return;
        canvasGroup.alpha = 0f;
        canvasGroup.transform.localScale = startScale;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    /// <summary>
    /// Hiện UI với animation fade in
    /// </summary>
    public void FadeInUI(System.Action onComplete = null)
    {
        if (canvasGroup == null)
        {
            Debug.LogWarning("UIVFX: canvasGroup is null in FadeInUI!");
            onComplete?.Invoke();
            return;
        }

        canvasGroup.alpha = 0f;
        canvasGroup.transform.localScale = startScale;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;

        var sequence = DOTween.Sequence();
        sequence.Append(canvasGroup.DOFade(1f, fadeInDuration).SetUpdate(true));
        sequence.Join(canvasGroup.transform.DOScale(endScale, fadeInDuration).SetEase(Ease.OutBack).SetUpdate(true));
        sequence.OnComplete(() => onComplete?.Invoke());
        sequence.SetUpdate(true);
    }

    /// <summary>
    /// Ẩn UI với animation fade out
    /// </summary>
    public void FadeOutUI(System.Action onComplete = null)
    {
        if (canvasGroup == null)
        {
            onComplete?.Invoke();
            return;
        }

        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        var sequence = DOTween.Sequence();
        sequence.Append(canvasGroup.DOFade(0f, fadeOutDuration).SetUpdate(true));
        sequence.Join(canvasGroup.transform.DOScale(startScale, fadeOutDuration).SetEase(Ease.InBack).SetUpdate(true));
        sequence.OnComplete(() => onComplete?.Invoke());
        sequence.SetUpdate(true);
    }

    /// <summary>
    /// Hiện UI ngay lập tức không có animation
    /// </summary>
    public void ShowImmediate()
    {
        if (canvasGroup == null) return;
        
        canvasGroup.alpha = 1f;
        canvasGroup.transform.localScale = endScale;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
    }

    /// <summary>
    /// Ẩn UI ngay lập tức không có animation
    /// </summary>
    public void HideImmediate()
    {
        if (canvasGroup == null) return;
        
        canvasGroup.alpha = 0f;
        canvasGroup.transform.localScale = startScale;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    /// <summary>
    /// Set CanvasGroup từ bên ngoài (hữu ích cho DontDestroyOnLoad)
    /// </summary>
    public void SetCanvasGroup(CanvasGroup newCanvasGroup)
    {
        canvasGroup = newCanvasGroup;
    }

    private void OnDestroy()
    {
        if (canvasGroup == null) return;
        canvasGroup.DOKill();
    }
}
