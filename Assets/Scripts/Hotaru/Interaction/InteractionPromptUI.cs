using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// UI hiển thị prompt khi player nhìn vào Interactable object
/// Là World Space Canvas, luôn hướng về camera
/// </summary>
public class InteractionPromptUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Canvas canvas;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TMP_Text promptText;
    [SerializeField] private TMP_Text keyText;
    
    [Header("Position Settings")]
    [Tooltip("Offset từ target position")]
    [SerializeField] private Vector3 positionOffset = new Vector3(0f, 1.5f, 0f);
    
    [Header("Animation")]
    [SerializeField] private float fadeSpeed = 8f;
    [SerializeField] private float scaleSpeed = 10f;
    [SerializeField] private Vector3 hiddenScale = new Vector3(0.8f, 0.8f, 0.8f);
    
    private Transform _targetTransform;
    private Transform _cameraTransform;
    private bool _isVisible;
    private float _targetAlpha;
    
    private void Awake()
    {
        // Auto-find components
        if (canvas == null)
            canvas = GetComponent<Canvas>();
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();
        
        // Start hidden
        if (canvasGroup != null)
            canvasGroup.alpha = 0f;
        transform.localScale = hiddenScale;
    }

    private void Start()
    {
        _cameraTransform = Camera.main?.transform;
    }

    private void LateUpdate()
    {
        // Fade animation
        if (canvasGroup != null)
        {
            canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, _targetAlpha, fadeSpeed * Time.deltaTime);
        }
        
        // Scale animation
        Vector3 targetScale = _isVisible ? Vector3.one : hiddenScale;
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, scaleSpeed * Time.deltaTime);
        
        // Follow target
        if (_isVisible && _targetTransform != null)
        {
            transform.position = _targetTransform.position + positionOffset;
            
            // Billboard - hướng về camera
            if (_cameraTransform != null)
            {
                Vector3 lookDir = _cameraTransform.position - transform.position;
                lookDir.y = 0; // Only Y rotation
                if (lookDir != Vector3.zero)
                {
                    transform.rotation = Quaternion.LookRotation(-lookDir);
                }
            }
        }
        
        // Disable when fully hidden
        if (!_isVisible && canvasGroup != null && canvasGroup.alpha < 0.01f)
        {
            gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Hiển thị prompt cho interactable object
    /// </summary>
    public void Show(InteractableObject interactable)
    {
        if (interactable == null) return;
        
        gameObject.SetActive(true);
        _isVisible = true;
        _targetAlpha = 1f;
        _targetTransform = interactable.transform;
        
        // Update text
        if (promptText != null)
            promptText.text = interactable.PromptText;
        
        if (keyText != null)
        {
            keyText.gameObject.SetActive(interactable.ShowKeyHint);
            keyText.text = interactable.InteractionKey.ToString();
        }
        
        // Update camera reference
        if (_cameraTransform == null)
            _cameraTransform = Camera.main?.transform;
    }

    /// <summary>
    /// Ẩn prompt
    /// </summary>
    public void Hide()
    {
        _isVisible = false;
        _targetAlpha = 0f;
        _targetTransform = null;
    }
    /// <summary>
    /// Set position offset
    /// </summary>
    public void SetOffset(Vector3 offset)
    {
        positionOffset = offset;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (canvas == null)
            canvas = GetComponent<Canvas>();
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();
    }
#endif
}
