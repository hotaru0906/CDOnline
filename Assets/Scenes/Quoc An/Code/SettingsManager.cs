using UnityEngine;
using UnityEngine.UI;

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

    private GameObject _currentPanel;

    private void Start()
    {
        btnAudio.onClick.AddListener(()    => SwitchTab(audioPanel));
        btnGraphics.onClick.AddListener(() => SwitchTab(graphicsPanel));
        btnGameplay.onClick.AddListener(() => SwitchTab(gameplayPanel));

        btnApply.onClick.AddListener(ApplySettings);
        btnReset.onClick.AddListener(ResetAllSettings);
        btnBack.onClick.AddListener(CloseSettings);

        SwitchTab(audioPanel);
    }

    // ✅ Đổi thành public để thấy trong Inspector
    public void SwitchTab(GameObject targetPanel)
    {
        audioPanel.SetActive(false);
        graphicsPanel.SetActive(false);
        gameplayPanel.SetActive(false);

        targetPanel.SetActive(true);
        _currentPanel = targetPanel;
    }

    // ✅ Đổi thành public
    public void ApplySettings()
    {
        PlayerPrefs.Save();
        Debug.Log("✅ Settings Saved!");
    }

    // ✅ Đổi thành public
    public void ResetAllSettings()
    {
        audioSettings.ResetAudio();
        graphicsSettings.ResetGraphics();
        gameplaySettings.ResetGameplay();
        Debug.Log("🔄 Settings Reset to Default!");
    }

    // ✅ Đổi thành public
    public void CloseSettings()
    {
        settingsCanvas.SetActive(false);
    }

    // ✅ Đã public sẵn
    public void OpenSettings()
    {
        settingsCanvas.SetActive(true);
        SwitchTab(audioPanel);
    }
}