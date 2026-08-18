using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class GraphicsSettings : MonoBehaviour
{
    [Header("=== Dropdowns ===")]
    [Tooltip("3 option (Fullscreen / Windowed / Borderless) được thiết kế SẴN trong Editor. " +
             "Code KHÔNG sinh options cho dropdown này, chỉ đọc/ghi value. " +
             "Thứ tự option trong Editor phải khớp với SetDisplayMode(index).")]
    [SerializeField] private TMP_Dropdown displayModeDropdown;

    [Tooltip("Danh sách được sinh động lúc runtime theo Screen.resolutions của máy người chơi. " +
             "Item hiển thị dùng đúng Template mà bạn thiết kế trong Editor ở dropdown này.")]
    [SerializeField] private TMP_Dropdown resolutionDropdown;

    [Tooltip("Danh sách được sinh động lúc runtime theo QualitySettings.names của project.")]
    [SerializeField] private TMP_Dropdown qualityDropdown;

    [Header("=== Toggles ===")]
    [SerializeField] private Toggle vsyncToggle;
    [SerializeField] private Toggle fpsToggle;

    // Private
    private Resolution[] _availableResolutions;

    // ==============================
    //         UNITY EVENTS
    // ==============================
    private void Start()
    {
        SetupResolutionDropdown();
        SetupQualityDropdown();

        // ✅ Đăng ký listener rồi mới load. Nhưng để chắc chắn setting ĐƯỢC ÁP DỤNG THẬT
        // (Toggle/Dropdown chỉ bắn onValueChanged khi giá trị thật sự thay đổi),
        // LoadGraphicsSettings() bên dưới gọi tường minh từng hàm Set... luôn,
        // không phụ thuộc vào việc sự kiện có tự bắn hay không.
        BindEvents();
        LoadGraphicsSettings();
    }

    // ==============================
    //          SETUP UI
    // ==============================
    // ⚠️ KHÔNG có Setup cho displayModeDropdown nữa.
    // 3 option (Fullscreen / Windowed / Borderless) đã được thiết kế sẵn trong Editor,
    // code chỉ set/đọc .value ở LoadGraphicsSettings() và SetDisplayMode().

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
    // Thứ tự này phải khớp với thứ tự 3 option bạn thiết kế trong Editor.
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
        // FPS Counter giờ nằm ở object riêng (FPSCounterDisplay), độc lập với Settings Panel
        // nên vẫn hiển thị được khi đóng Settings.
        if (FPSCounterDisplay.Instance != null)
            FPSCounterDisplay.Instance.SetVisible(isOn);

        PlayerPrefs.SetInt("ShowFPS", isOn ? 1 : 0);
    }

    // ==============================
    //       SAVE / LOAD
    // ==============================
    private void LoadGraphicsSettings()
    {
        // Load Display Mode (chỉ set value, KHÔNG đụng vào options đã thiết kế sẵn)
        int displayMode = PlayerPrefs.GetInt("DisplayMode", 0);
        displayModeDropdown.value = displayMode;
        SetDisplayMode(displayMode); // gọi tường minh, không phụ thuộc sự kiện

        // Load Resolution
        int resIndex = PlayerPrefs.GetInt("ResolutionIndex",
                        _availableResolutions.Length - 1);
        resolutionDropdown.value = resIndex;
        SetResolution(resIndex); // gọi tường minh

        // Load Quality
        int quality = PlayerPrefs.GetInt("QualityLevel",
                        QualitySettings.GetQualityLevel());
        qualityDropdown.value = quality;
        SetQuality(quality); // gọi tường minh

        // Load VSync
        bool vsync = PlayerPrefs.GetInt("VSync", 1) == 1;
        vsyncToggle.isOn = vsync;
        SetVSync(vsync); // gọi tường minh — fix chính cho lỗi VSync không được áp dụng thật

        // Load FPS Counter
        bool fps = PlayerPrefs.GetInt("ShowFPS", 0) == 1;
        fpsToggle.isOn = fps;
        SetFPSCounter(fps); // gọi tường minh
    }

    // Reset về mặc định
    public void ResetGraphics()
    {
        displayModeDropdown.value  = 0;
        resolutionDropdown.value   = _availableResolutions.Length - 1;
        qualityDropdown.value      = QualitySettings.names.Length - 1;
        vsyncToggle.isOn           = true;
        fpsToggle.isOn             = false;

        // Áp dụng thật sự, không chỉ đổi UI
        SetDisplayMode(0);
        SetResolution(_availableResolutions.Length - 1);
        SetQuality(QualitySettings.names.Length - 1);
        SetVSync(true);
        SetFPSCounter(false);
    }
}