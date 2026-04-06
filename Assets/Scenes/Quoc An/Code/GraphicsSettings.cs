using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class GraphicsSettings : MonoBehaviour
{
    [Header("=== Dropdowns ===")]
    [SerializeField] private TMP_Dropdown displayModeDropdown;
    [SerializeField] private TMP_Dropdown resolutionDropdown;
    [SerializeField] private TMP_Dropdown qualityDropdown;

    [Header("=== Toggles ===")]
    [SerializeField] private Toggle vsyncToggle;
    [SerializeField] private Toggle fpsToggle;

    [Header("=== FPS Counter UI ===")]
    [SerializeField] private TextMeshProUGUI fpsCounterText;

    // Private
    private Resolution[] _availableResolutions;
    private float _fpsTimer;
    private bool _showFPS;

    // ==============================
    //         UNITY EVENTS
    // ==============================
    private void Start()
    {
        SetupResolutionDropdown();
        SetupQualityDropdown();
        LoadGraphicsSettings();
        BindEvents();
    }

    private void Update()
    {
        // Hiển thị FPS Counter
        if (_showFPS)
        {
            _fpsTimer += Time.deltaTime;
            if (_fpsTimer >= 0.5f) // Cập nhật mỗi 0.5 giây
            {
                int fps = Mathf.RoundToInt(1f / Time.deltaTime);
                fpsCounterText.text = "FPS: " + fps;
                _fpsTimer = 0f;
            }
        }
    }

    // ==============================
    //          SETUP UI
    // ==============================
    private void SetupResolutionDropdown()
    {
        _availableResolutions = Screen.resolutions;
        resolutionDropdown.ClearOptions();

        List<string> options = new List<string>();
        int currentIndex = 0;

        for (int i = 0; i < _availableResolutions.Length; i++)
        {
            var res = _availableResolutions[i];
            string option = $"{res.width} x {res.height} @ {res.refreshRateRatio.value:F0}Hz";
            options.Add(option);

            // Tìm resolution hiện tại
            if (res.width  == Screen.currentResolution.width &&
                res.height == Screen.currentResolution.height)
            {
                currentIndex = i;
            }
        }

        resolutionDropdown.AddOptions(options);
        resolutionDropdown.value = currentIndex;
        resolutionDropdown.RefreshShownValue();
    }

    private void SetupQualityDropdown()
    {
        qualityDropdown.ClearOptions();

        // Lấy tên Quality từ Unity Project Settings
        List<string> qualityNames = new List<string>(QualitySettings.names);
        qualityDropdown.AddOptions(qualityNames);
        qualityDropdown.value = QualitySettings.GetQualityLevel();
        qualityDropdown.RefreshShownValue();
    }

    private void BindEvents()
    {
        displayModeDropdown.onValueChanged.AddListener(SetDisplayMode);
        resolutionDropdown.onValueChanged.AddListener(SetResolution);
        qualityDropdown.onValueChanged.AddListener(SetQuality);
        vsyncToggle.onValueChanged.AddListener(SetVSync);
        fpsToggle.onValueChanged.AddListener(SetFPSCounter);
    }

    // ==============================
    //       SET FUNCTIONS
    // ==============================

    // 0: Fullscreen | 1: Windowed | 2: Borderless
    public void SetDisplayMode(int index)
    {
        switch (index)
        {
            case 0:
                Screen.fullScreenMode = FullScreenMode.ExclusiveFullScreen;
                break;
            case 1:
                Screen.fullScreenMode = FullScreenMode.Windowed;
                break;
            case 2:
                Screen.fullScreenMode = FullScreenMode.FullScreenWindow;
                break;
        }
        PlayerPrefs.SetInt("DisplayMode", index);
    }

    public void SetResolution(int index)
    {
        Resolution res = _availableResolutions[index];
        Screen.SetResolution(res.width, res.height, Screen.fullScreenMode);
        PlayerPrefs.SetInt("ResolutionIndex", index);
    }

    public void SetQuality(int index)
    {
        QualitySettings.SetQualityLevel(index);
        PlayerPrefs.SetInt("QualityLevel", index);
    }

    public void SetVSync(bool isOn)
    {
        // 0 = Tắt VSync | 1 = Bật VSync
        QualitySettings.vSyncCount = isOn ? 1 : 0;
        PlayerPrefs.SetInt("VSync", isOn ? 1 : 0);
    }

    public void SetFPSCounter(bool isOn)
    {
        _showFPS = isOn;
        fpsCounterText.gameObject.SetActive(isOn);
        PlayerPrefs.SetInt("ShowFPS", isOn ? 1 : 0);
    }

    // ==============================
    //       SAVE / LOAD
    // ==============================
    private void LoadGraphicsSettings()
    {
        // Load Display Mode
        int displayMode = PlayerPrefs.GetInt("DisplayMode", 0);
        displayModeDropdown.value = displayMode;
        SetDisplayMode(displayMode);

        // Load Resolution
        int resIndex = PlayerPrefs.GetInt("ResolutionIndex",
                        _availableResolutions.Length - 1);
        resolutionDropdown.value = resIndex;

        // Load Quality
        int quality = PlayerPrefs.GetInt("QualityLevel",
                        QualitySettings.GetQualityLevel());
        qualityDropdown.value = quality;

        // Load VSync
        bool vsync = PlayerPrefs.GetInt("VSync", 1) == 1;
        vsyncToggle.isOn = vsync;

        // Load FPS Counter
        bool fps = PlayerPrefs.GetInt("ShowFPS", 0) == 1;
        fpsToggle.isOn = fps;
    }

    // Reset về mặc định
    public void ResetGraphics()
    {
        displayModeDropdown.value  = 0;
        resolutionDropdown.value   = _availableResolutions.Length - 1;
        qualityDropdown.value      = QualitySettings.names.Length - 1;
        vsyncToggle.isOn           = true;
        fpsToggle.isOn             = false;
    }
}