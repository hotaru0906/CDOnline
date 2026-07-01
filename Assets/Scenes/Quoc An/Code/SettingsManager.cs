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

    [Header("=== Menu Manager Reference ===")]
    [SerializeField] private MenuManager menuManager; // ✅ Kéo MenuManager vào đây

    [Header("=== Events ===")]
    public UnityEvent OnSettingsOpened;  // ✅ Event khi mở settings
    public UnityEvent OnSettingsClosed;  // ✅ Event khi đóng settings

    private GameObject _currentPanel;

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
    /// ✅ Đóng Settings → Hiện lại UI Menu
    /// </summary>
    public void CloseSettings()
    {
        settingsCanvas.SetActive(false);

        // ✅ Thông báo cho MenuManager hiện lại canvas
        if (menuManager != null)
        {
            menuManager.OnSettingsClosed();
        }

        // ✅ Invoke event nếu có script khác cần lắng nghe
        OnSettingsClosed?.Invoke();
    }

    /// <summary>
    /// ✅ Mở Settings → Ẩn UI Menu
    /// </summary>
    public void OpenSettings()
    {
        settingsCanvas.SetActive(true);
        SwitchTab(audioPanel);

        // ✅ Thông báo cho MenuManager ẩn canvas
        if (menuManager != null)
        {
            menuManager.OnSettingsOpened();
        }

        // ✅ Invoke event nếu có script khác cần lắng nghe
        OnSettingsOpened?.Invoke();
    }
}