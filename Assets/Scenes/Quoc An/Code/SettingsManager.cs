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

    [Header("=== Options ===")]
    [SerializeField] private KeyCode closeKey = KeyCode.Escape;

    [Header("=== Events ===")]
    public UnityEvent OnSettingsOpened;
    public UnityEvent OnSettingsClosed;

    private GameObject _currentPanel;
    private bool _isOpen = false;

    public bool IsOpen => _isOpen;

    private void Awake()
    {
        if (settingsCanvasGroup == null && settingsCanvas != null)
        {
            settingsCanvasGroup = settingsCanvas.GetComponent<CanvasGroup>();
            if (settingsCanvasGroup == null)
                settingsCanvasGroup = settingsCanvas.AddComponent<CanvasGroup>();
        }

        // Đăng ký listener ở Awake: an toàn hơn Start
        BindButton(btnAudio,    () => SwitchTab(audioPanel));
        BindButton(btnGraphics, () => SwitchTab(graphicsPanel));
        BindButton(btnGameplay, () => SwitchTab(gameplayPanel));
        BindButton(btnApply,    ApplySettings);
        BindButton(btnReset,    ResetAllSettings);
        BindButton(btnBack,     CloseSettings);

        SwitchTab(audioPanel);
        HideSettingsImmediate();
    }

    private void BindButton(Button b, UnityEngine.Events.UnityAction action)
    {
        if (b == null) { Debug.LogWarning("[SettingsManager] Thiếu button trong Inspector."); return; }
        b.onClick.RemoveAllListeners(); // tránh đăng ký chồng
        b.onClick.AddListener(action);
    }

    private void Update()
    {
        if (_isOpen && Input.GetKeyDown(closeKey))
            CloseSettings();
    }

    public void SwitchTab(GameObject targetPanel)
    {
        if (targetPanel == null || targetPanel == _currentPanel) return;

        if (audioPanel)    audioPanel.SetActive(false);
        if (graphicsPanel) graphicsPanel.SetActive(false);
        if (gameplayPanel) gameplayPanel.SetActive(false);

        targetPanel.SetActive(true);
        _currentPanel = targetPanel;

        UpdateTabVisual();
    }

    private void UpdateTabVisual()
    {
        SetTabSelected(btnAudio,    _currentPanel == audioPanel);
        SetTabSelected(btnGraphics, _currentPanel == graphicsPanel);
        SetTabSelected(btnGameplay, _currentPanel == gameplayPanel);
    }

    private void SetTabSelected(Button b, bool selected)
    {
        if (b == null) return;
        b.interactable = !selected; // tab đang mở thì disable, nhìn là biết ngay
    }

    public void ApplySettings()
    {
        PlayerPrefs.Save();
        Debug.Log("[SettingsManager] Settings saved.");
    }

    public void ResetAllSettings()
    {
        if (audioSettings)    audioSettings.ResetAudio();
        if (graphicsSettings) graphicsSettings.ResetGraphics();
        if (gameplaySettings) gameplaySettings.ResetGameplay();
        PlayerPrefs.Save();
        Debug.Log("[SettingsManager] Settings reset to default.");
    }

    /// <returns>true nếu thật sự mở, false nếu đang mở sẵn</returns>
    public bool OpenSettings()
    {
        if (_isOpen) return false;
        _isOpen = true;

        ShowSettingsImmediate();
        SwitchTab(audioPanel);

        OnSettingsOpened?.Invoke();
        return true;
    }

    public void CloseSettings()
    {
        if (!_isOpen) return;
        _isOpen = false;

        ApplySettings();          // lưu luôn khi thoát
        HideSettingsImmediate();

        if (menuManager != null)
            menuManager.OnSettingsClosed();

        OnSettingsClosed?.Invoke();
    }

    private void ShowSettingsImmediate()  => SetCanvasVisible(true);
    private void HideSettingsImmediate()  => SetCanvasVisible(false);

    private void SetCanvasVisible(bool visible)
    {
        if (settingsCanvasGroup != null)
        {
            settingsCanvasGroup.alpha          = visible ? 1f : 0f;
            settingsCanvasGroup.interactable   = visible;
            settingsCanvasGroup.blocksRaycasts = visible;
        }
        else if (settingsCanvas != null)
        {
            settingsCanvas.SetActive(visible);
        }
    }
}