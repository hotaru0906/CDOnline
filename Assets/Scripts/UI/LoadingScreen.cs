using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Simple loading screen manager using prefab from Resources.
/// Place "LoadingScreen" prefab in Resources folder.
/// </summary>
public class LoadingScreen : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TextMeshProUGUI loadingText;
    //[SerializeField] private Image loadingIcon;

    [Header("Settings")]
    [SerializeField] private float fadeSpeed = 3f;
    //[SerializeField] private bool rotateIcon = true;
    //[SerializeField] private float rotateSpeed = 200f;

    private static LoadingScreen _instance;
    private bool _isShowing;
    private float _targetAlpha;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);

        // Start hidden
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
        }
        gameObject.SetActive(false);
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

        // Rotate loading icon
        // if (rotateIcon && loadingIcon != null && _isShowing)
        // {
        //     loadingIcon.transform.Rotate(0f, 0f, -rotateSpeed * Time.unscaledDeltaTime);
        // }
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
    /// Hide loading screen.
    /// </summary>
    public static void Hide()
    {
        if (_instance != null)
        {
            _instance.HideInternal();
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
        _isShowing = true;
        gameObject.SetActive(true);

        if (loadingText != null)
        {
            loadingText.text = message;
        }

        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = true;
            _targetAlpha = 1f;
        }

        Debug.Log($"[LoadingScreen] Show: {message}");
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
