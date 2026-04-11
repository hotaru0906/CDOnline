using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// Simple loading screen manager using prefab from Resources.
/// Place "LoadingScreen" prefab in Resources folder.
/// </summary>
public class LoadingScreen : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Canvas canvas;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TextMeshProUGUI loadingText;
    [SerializeField] private Slider loadingBar;
    [SerializeField] private TextMeshProUGUI percentText;

    [Header("Settings")]
    [SerializeField] private float fadeSpeed = 3f;
    [SerializeField] private float barFillSpeed = 0.5f;
    [SerializeField] private float barFillSpeedFast = 2f; // Tốc độ nhanh khi hoàn thành
    [SerializeField] private float delayBeforeHide = 3f;
    [SerializeField] private int sortingOrder = 32767; // Giá trị cao nhất để luôn ở trên

    private static LoadingScreen _instance;
    private bool _isShowing;
    private float _targetAlpha;
    
    // Loading progress
    private float _currentProgress;
    private float _targetProgress;
    private bool _isComplete;
    private Coroutine _hideCoroutine;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);

        // Tìm Canvas nếu chưa assign
        if (canvas == null)
        {
            canvas = GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = GetComponentInChildren<Canvas>();
            }
        }

        // Đảm bảo Canvas luôn ở trên cùng
        SetupCanvasOverlay();

        // Start hidden
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
        }
        gameObject.SetActive(false);
    }

    /// <summary>
    /// Setup Canvas để luôn render ở trên tất cả UI khác
    /// </summary>
    private void SetupCanvasOverlay()
    {
        if (canvas != null)
        {
            // Dùng ScreenSpaceOverlay để không phụ thuộc vào camera
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Sort order cao nhất để che tất cả canvas khác
            canvas.sortingOrder = sortingOrder;
            
            Debug.Log($"[LoadingScreen] Canvas setup - RenderMode: {canvas.renderMode}, SortingOrder: {canvas.sortingOrder}");
        }
        else
        {
            Debug.LogWarning("[LoadingScreen] Canvas component not found!");
        }
    }

    private void Update()
    {
        // Fade animation
        if (canvasGroup != null && Mathf.Abs(canvasGroup.alpha - _targetAlpha) > 0.01f)
        {
            canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, _targetAlpha, fadeSpeed * Time.unscaledDeltaTime);
            
            // Hide gameObject when fully faded out
            if (_targetAlpha == 0f && canvasGroup.alpha < 0.01f)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.blocksRaycasts = false;
                gameObject.SetActive(false);
            }
        }

        // Update loading bar progress
        if (_isShowing)
        {
            UpdateProgress();
        }
    }

    private void UpdateProgress()
    {
        // Smoothly move current progress towards target
        // Dùng tốc độ nhanh khi đã complete
        float speed = _isComplete ? barFillSpeedFast : barFillSpeed;
        
        if (_currentProgress < _targetProgress)
        {
            _currentProgress = Mathf.MoveTowards(_currentProgress, _targetProgress, speed * Time.unscaledDeltaTime);
        }

        // Update UI
        if (loadingBar != null)
        {
            loadingBar.value = _currentProgress;
        }

        if (percentText != null)
        {
            int percent = Mathf.RoundToInt(_currentProgress * 100f);
            percentText.text = $"{percent}%";
        }
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }

    #region Static Methods

    /// <summary>
    /// Show loading screen with optional message.
    /// </summary>
    public static void Show(string message = "Loading...")
    {
        EnsureInstance();
        if (_instance != null)
        {
            _instance.ShowInternal(message);
        }
    }

    /// <summary>
    /// Hide loading screen with delay after reaching 100%.
    /// </summary>
    public static void Hide()
    {
        if (_instance != null)
        {
            _instance.CompleteAndHide();
        }
    }

    /// <summary>
    /// Update loading progress (0-1). Sẽ bị giới hạn ở 80% cho đến khi gọi Hide().
    /// </summary>
    public static void SetProgress(float progress)
    {
        if (_instance != null)
        {
            // Giới hạn progress ở 80% cho đến khi complete
            float clampedProgress = Mathf.Clamp01(progress) * 0.8f;
            _instance._targetProgress = Mathf.Max(_instance._targetProgress, clampedProgress);
        }
    }

    /// <summary>
    /// Update loading text.
    /// </summary>
    public static void SetText(string message)
    {
        if (_instance != null && _instance.loadingText != null)
        {
            _instance.loadingText.text = message;
        }
    }

    /// <summary>
    /// Check if loading screen is visible.
    /// </summary>
    public static bool IsShowing => _instance != null && _instance._isShowing;

    #endregion

    #region Internal Methods

    private void ShowInternal(string message)
    {
        // Cancel any pending hide
        if (_hideCoroutine != null)
        {
            StopCoroutine(_hideCoroutine);
            _hideCoroutine = null;
        }

        _isShowing = true;
        _isComplete = false;
        _currentProgress = 0f;
        _targetProgress = 0f;
        
        gameObject.SetActive(true);

        // Đảm bảo Canvas luôn ở trên cùng mỗi khi show
        SetupCanvasOverlay();

        if (loadingText != null)
        {
            loadingText.text = message;
        }

        // Reset loading bar
        if (loadingBar != null)
        {
            loadingBar.value = 0f;
        }

        if (percentText != null)
        {
            percentText.text = "0%";
        }

        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = true;
            _targetAlpha = 1f;
        }

        // Auto progress đến 50-60% trong khi chờ
        StartCoroutine(AutoProgressCoroutine());

        Debug.Log($"[LoadingScreen] Show: {message}");
    }

    private IEnumerator AutoProgressCoroutine()
    {
        // Tự động tăng progress đến 50-60% trong khi chờ loading
        float autoTarget = Random.Range(0.5f, 0.6f);
        
        while (_isShowing && !_isComplete && _targetProgress < autoTarget)
        {
            _targetProgress += 0.02f;
            yield return new WaitForSecondsRealtime(0.1f);
        }
    }

    private void CompleteAndHide()
    {
        if (!_isShowing) return;
        
        _isComplete = true;
        _targetProgress = 1f; // Set target về 100%
        
        // Start coroutine để chờ bar đầy rồi mới hide
        if (_hideCoroutine != null)
        {
            StopCoroutine(_hideCoroutine);
        }
        _hideCoroutine = StartCoroutine(DelayedHideCoroutine());
    }

    private IEnumerator DelayedHideCoroutine()
    {
        // Chờ progress bar đạt 100%
        while (_currentProgress < 0.99f)
        {
            yield return null;
        }

        // Đảm bảo hiển thị 100%
        _currentProgress = 1f;
        if (loadingBar != null) loadingBar.value = 1f;
        if (percentText != null) percentText.text = "100%";

        // Delay thêm 3-5s
        float delay = Random.Range(delayBeforeHide, delayBeforeHide + 2f);
        yield return new WaitForSecondsRealtime(delay);

        // Hide
        HideInternal();
    }

    private void HideInternal()
    {
        _isShowing = false;
        _targetAlpha = 0f;

        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = false;
        }

        Debug.Log("[LoadingScreen] Hide");
    }

    private static void EnsureInstance()
    {
        if (_instance == null)
        {
            // Try to load from Resources
            var prefab = Resources.Load<LoadingScreen>("LoadingScreen");
            if (prefab != null)
            {
                _instance = Instantiate(prefab);
                Debug.Log("[LoadingScreen] Instantiated from Resources");
            }
            else
            {
                Debug.LogWarning("[LoadingScreen] Prefab not found in Resources folder! Create a LoadingScreen prefab.");
            }
        }
    }

    #endregion
}
