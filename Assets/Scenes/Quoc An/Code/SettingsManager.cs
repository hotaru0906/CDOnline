using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

public class SettingsManager : MonoBehaviour
{
    [Header("=== Panels ===")]
    [SerializeField] private GameObject audioPanel;
    [SerializeField] private GameObject graphicsPanel;
    [SerializeField] private GameObject gameplayPanel;

    [Header("=== Tab Buttons ===")]
    [SerializeField] private Button btnAudio;
    [SerializeField] private Button btnGraphics;
    [SerializeField] private Button btnGameplay;

    [Header("=== Footer Buttons ===")]
    [SerializeField] private Button btnApply;
    [SerializeField] private Button btnReset;
    [SerializeField] private Button btnBack;

    [Header("=== Sub Scripts ===")]
    [SerializeField] private AudioSettings audioSettings;
    [SerializeField] private GraphicsSettings graphicsSettings;
    [SerializeField] private GameplaySettings gameplaySettings;

    [Header("=== Settings Canvas ===")]
    [SerializeField] private GameObject settingsCanvas;
    [SerializeField] private CanvasGroup settingsCanvasGroup;

    [Header("=== Menu Manager Reference ===")]
    [SerializeField] private MenuManager menuManager;

    [Header("=== Events ===")]
    public UnityEvent OnSettingsOpened;
    public UnityEvent OnSettingsClosed;

    private GameObject _currentPanel;
    private bool _isOpen = false; // THÊM: tránh gọi trùng lặp

    private void Awake()
    {
        // Tạo CanvasGroup nếu chưa có
        if (settingsCanvasGroup == null && settingsCanvas != null)
        {
            settingsCanvasGroup = settingsCanvas.GetComponent<CanvasGroup>();
            if (settingsCanvasGroup == null)
                settingsCanvasGroup = settingsCanvas.AddComponent<CanvasGroup>();
        }

        HideSettingsImmediate();
    }

    private void Start()
    {
        btnAudio.onClick.AddListener(() => SwitchTab(audioPanel));
        btnGraphics.onClick.AddListener(() => SwitchTab(graphicsPanel));
        btnGameplay.onClick.AddListener(() => SwitchTab(gameplayPanel));

        btnApply.onClick.AddListener(ApplySettings);
        btnReset.onClick.AddListener(ResetAllSettings);
        btnBack.onClick.AddListener(CloseSettings);

        SwitchTab(audioPanel);
    }

    public void SwitchTab(GameObject targetPanel)
    {
        audioPanel.SetActive(false);
        graphicsPanel.SetActive(false);
        gameplayPanel.SetActive(false);

        targetPanel.SetActive(true);
        _currentPanel = targetPanel;
    }

    public void ApplySettings()
    {
        PlayerPrefs.Save();
        Debug.Log("✅ Settings Saved!");
    }

    public void ResetAllSettings()
    {
        audioSettings.ResetAudio();
        graphicsSettings.ResetGraphics();
        gameplaySettings.ResetGameplay();
        Debug.Log("🔄 Settings Reset to Default!");
    }

    /// <summary>
    /// Mở Settings — chỉ lo việc hiện CanvasGroup.
    /// MenuManager tự lo việc ẩn menu trước khi gọi hàm này.
    /// </summary>
    public void OpenSettings()
    {
        if (_isOpen) return; // tránh gọi trùng
        _isOpen = true;

        ShowSettingsImmediate();
        SwitchTab(audioPanel);

        // Không gọi menuManager.OnSettingsOpened() ở đây nữa
        // MenuManager.ShowSettings() đã tự gọi OnSettingsOpened() trước rồi

        OnSettingsOpened?.Invoke();
        Debug.Log("[SettingsManager] Settings opened.");
    }

    /// <summary>
    /// Đóng Settings — hiện lại menu.
    /// </summary>
    public void CloseSettings()
    {
        if (!_isOpen) return; // tránh gọi trùng
        _isOpen = false;

        HideSettingsImmediate();

        // Báo MenuManager khôi phục màn hình trước đó
        if (menuManager != null)
            menuManager.OnSettingsClosed();

        OnSettingsClosed?.Invoke();
        Debug.Log("[SettingsManager] Settings closed.");
    }

    private void ShowSettingsImmediate()
    {
        if (settingsCanvasGroup != null)
        {
            settingsCanvasGroup.alpha = 1f;
            settingsCanvasGroup.interactable = true;
            settingsCanvasGroup.blocksRaycasts = true;
        }
        else
        {
            settingsCanvas?.SetActive(true);
        }
    }

    private void HideSettingsImmediate()
    {
        if (settingsCanvasGroup != null)
        {
            settingsCanvasGroup.alpha = 0f;
            settingsCanvasGroup.interactable = false;
            settingsCanvasGroup.blocksRaycasts = false;
        }
        else
        {
            settingsCanvas?.SetActive(false);
        }
    }
}