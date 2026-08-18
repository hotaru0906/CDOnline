using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

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

    [Tooltip("UISlideAnimator của panel Settings (ButtonGroup). Chỉ được animate (trượt vào/ra) " +
             "khi đang ở scene UI Menu, do MenuManager điều khiển. Ở các scene khác, script này " +
             "sẽ tự ghim vị trí về (0,0) — không animate, không bị kẹt ở vị trí ẩn.")]
    [SerializeField] private UISlideAnimator settingsPanelAnimator;

    [Header("=== Options ===")]
    [SerializeField] private KeyCode closeKey = KeyCode.Escape;

    [Tooltip("Tên scene mà ở đó ESC sẽ KHÔNG tự động bật/tắt Settings " +
             "(vì MenuManager ở scene này đã tự xử lý nút Settings/Back riêng).")]
    [SerializeField] private string excludedEscSceneName = "UI menu";

    [Header("=== Persistence ===")]
    [Tooltip("Giữ SettingsManager tồn tại xuyên suốt các scene (Singleton + DontDestroyOnLoad).")]
    [SerializeField] private bool persistAcrossScenes = true;

    [Header("=== Events ===")]
    public UnityEvent OnSettingsOpened;
    public UnityEvent OnSettingsClosed;

    public static SettingsManager Instance { get; private set; }

    public bool IsOpen => settingsCanvas != null && settingsCanvas.activeSelf;

    private void Awake()
    {
        if (persistAcrossScenes)
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        BindButton(btnAudio,    () => SwitchTab(audioPanel));
        BindButton(btnGraphics, () => SwitchTab(graphicsPanel));
        BindButton(btnGameplay, () => SwitchTab(gameplayPanel));
        BindButton(btnApply,    ApplySettings);
        BindButton(btnReset,    ResetAllSettings);
        BindButton(btnBack,     CloseSettings);

        SwitchTab(audioPanel);
        if (settingsCanvas != null)
            settingsCanvas.SetActive(false);

        // sceneLoaded chỉ bắn cho các scene load SAU thời điểm này,
        // nên cần tự ghim vị trí ngay cho scene hiện tại lúc khởi động.
        PinPositionIfOutsideMenu();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        PinPositionIfOutsideMenu();
    }
    private void PinPositionIfOutsideMenu()
    {
        if (settingsPanelAnimator == null) return;
        if (IsInExcludedEscScene()) return; // ở UI Menu để MenuManager tự animate

        settingsPanelAnimator.SetVisiblePositionImmediate();
    }

    private void BindButton(Button b, UnityEngine.Events.UnityAction action)
    {
        if (b == null) { Debug.LogWarning("[SettingsManager] Thiếu button trong Inspector."); return; }
        b.onClick.RemoveAllListeners();
        b.onClick.AddListener(action);
    }

    private void Update()
    {
        if (!Input.GetKeyDown(closeKey)) return;
        if (IsInExcludedEscScene()) return;

        ToggleSettings();
    }

    private bool IsInExcludedEscScene()
    {
        if (string.IsNullOrEmpty(excludedEscSceneName)) return false;
        return SceneManager.GetActiveScene().name == excludedEscSceneName;
    }

    public void ToggleSettings()
    {
        if (IsOpen)
        {
            CursorManager.Instance.HideCursor();
            CloseSettings();
        }
        else
        {
            CursorManager.Instance.ShowCursor();
            OpenSettings();
        }
         
    }

    public void SwitchTab(GameObject targetPanel)
    {
        if (audioPanel)    audioPanel.SetActive(targetPanel == audioPanel);
        if (graphicsPanel) graphicsPanel.SetActive(targetPanel == graphicsPanel);
        if (gameplayPanel) gameplayPanel.SetActive(targetPanel == gameplayPanel);

        UpdateTabVisual(targetPanel);
    }

    private void UpdateTabVisual(GameObject activePanel)
    {
        if (btnAudio)    btnAudio.interactable    = activePanel != audioPanel;
        if (btnGraphics) btnGraphics.interactable = activePanel != graphicsPanel;
        if (btnGameplay) btnGameplay.interactable = activePanel != gameplayPanel;
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

    public void OpenSettings()
    {
        if (settingsCanvas == null || IsOpen) return;

        PinPositionIfOutsideMenu(); // đảm bảo panel ở đúng (0,0) trước khi hiện, nếu không phải UI Menu

        settingsCanvas.SetActive(true);
        SwitchTab(audioPanel);

        OnSettingsOpened?.Invoke();
    }

    public void CloseSettings()
    {
        if (settingsCanvas == null || !IsOpen) return;

        ApplySettings();
        settingsCanvas.SetActive(false);

        OnSettingsClosed?.Invoke();
    }
}