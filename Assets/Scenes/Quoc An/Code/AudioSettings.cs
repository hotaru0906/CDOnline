using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Audio;
using TMPro;

public class AudioSettings : MonoBehaviour
{
    [Header("=== Audio Mixer ===")]
    [SerializeField] private AudioMixer audioMixer;

    [Header("=== Sliders ===")]
    [SerializeField] private Slider masterSlider;
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider sfxSlider;
    [SerializeField] private Slider uiSlider;

    [Header("=== Value Labels ===")]
    [SerializeField] private TextMeshProUGUI masterValueText;
    [SerializeField] private TextMeshProUGUI musicValueText;
    [SerializeField] private TextMeshProUGUI sfxValueText;
    [SerializeField] private TextMeshProUGUI uiValueText;

    [Header("=== Toggles ===")]
    [SerializeField] private Toggle bgmToggle;
    [SerializeField] private Toggle sfxToggle;

    // ----------------------------------------------------------------
    // Awake — set Mixer TRƯỚC khi AudioManager.Start() play nhạc
    // ----------------------------------------------------------------
    private void Awake()
    {
        ApplyMixerFromPrefs();
    }

    private void Start()
    {
        LoadSliderValues();
        BindSliderEvents();
    }

    // ----------------------------------------------------------------
    // Set mixer ngay từ PlayerPrefs — không cần slider
    // Gọi trong Awake để đảm bảo âm thanh đúng ngay từ đầu
    // ----------------------------------------------------------------
    private void ApplyMixerFromPrefs()
    {
        if (audioMixer == null) return;

        audioMixer.SetFloat("MasterVolume", SliderToDB(PlayerPrefs.GetFloat("MasterVolume", 1f)));
        audioMixer.SetFloat("MusicVolume",  SliderToDB(PlayerPrefs.GetFloat("MusicVolume",  1f)));
        audioMixer.SetFloat("SFXVolume",    SliderToDB(PlayerPrefs.GetFloat("SFXVolume",    1f)));
        audioMixer.SetFloat("UIVolume",     SliderToDB(PlayerPrefs.GetFloat("UIVolume",     1f)));
    }

    // ----------------------------------------------------------------
    // Load giá trị vào slider — gọi trong Start()
    // Slider.onValueChanged sẽ tự gọi SetXVolume() khi set value
    // ----------------------------------------------------------------
    private void LoadSliderValues()
    {
        // Tắt event trước khi set slider tránh trigger 2 lần
        masterSlider.onValueChanged.RemoveAllListeners();
        musicSlider.onValueChanged.RemoveAllListeners();
        sfxSlider.onValueChanged.RemoveAllListeners();
        uiSlider.onValueChanged.RemoveAllListeners();

        masterSlider.value = PlayerPrefs.GetFloat("MasterVolume", 1f);
        musicSlider.value  = PlayerPrefs.GetFloat("MusicVolume",  1f);
        sfxSlider.value    = PlayerPrefs.GetFloat("SFXVolume",    1f);
        uiSlider.value     = PlayerPrefs.GetFloat("UIVolume",     1f);

        // Cập nhật label %
        UpdateLabel(masterValueText, masterSlider.value);
        UpdateLabel(musicValueText,  musicSlider.value);
        UpdateLabel(sfxValueText,    sfxSlider.value);
        UpdateLabel(uiValueText,     uiSlider.value);

        // Load toggle
        if (AudioManager.Instance != null)
        {
            if (bgmToggle != null) bgmToggle.isOn = AudioManager.Instance.IsBGMOn;
            if (sfxToggle != null) sfxToggle.isOn = AudioManager.Instance.IsSFXOn;
        }
    }

    private void BindSliderEvents()
    {
        masterSlider.onValueChanged.AddListener(SetMasterVolume);
        musicSlider.onValueChanged.AddListener(SetMusicVolume);
        sfxSlider.onValueChanged.AddListener(SetSFXVolume);
        uiSlider.onValueChanged.AddListener(SetUIVolume);

        if (bgmToggle != null) bgmToggle.onValueChanged.AddListener(SetBGMEnabled);
        if (sfxToggle != null) sfxToggle.onValueChanged.AddListener(SetSFXEnabled);
    }

    // ----------------------------------------------------------------
    // Set Volume — gọi khi kéo slider
    // ----------------------------------------------------------------

    public void SetMasterVolume(float value)
    {
        audioMixer.SetFloat("MasterVolume", SliderToDB(value));
        UpdateLabel(masterValueText, value);
        PlayerPrefs.SetFloat("MasterVolume", value);
    }

    public void SetMusicVolume(float value)
    {
        audioMixer.SetFloat("MusicVolume", SliderToDB(value));
        UpdateLabel(musicValueText, value);
        PlayerPrefs.SetFloat("MusicVolume", value);
    }

    public void SetSFXVolume(float value)
    {
        audioMixer.SetFloat("SFXVolume", SliderToDB(value));
        UpdateLabel(sfxValueText, value);
        PlayerPrefs.SetFloat("SFXVolume", value);
    }

    public void SetUIVolume(float value)
    {
        audioMixer.SetFloat("UIVolume", SliderToDB(value));
        UpdateLabel(uiValueText, value);
        PlayerPrefs.SetFloat("UIVolume", value);
    }

    // ----------------------------------------------------------------
    // Toggle BGM / SFX
    // ----------------------------------------------------------------

    public void SetBGMEnabled(bool isOn)
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.SetBGM(isOn);
        PlayerPrefs.SetInt("BGM_On", isOn ? 1 : 0);
    }

    public void SetSFXEnabled(bool isOn)
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.SetSFX(isOn);
        PlayerPrefs.SetInt("SFX_On", isOn ? 1 : 0);
    }

    // ----------------------------------------------------------------
    // Apply / Reset
    // ----------------------------------------------------------------

    public void ApplySettings()
    {
        PlayerPrefs.Save();
        Debug.Log("[AudioSettings] Settings saved.");
    }

    public void ResetAudio()
    {
        masterSlider.value = 1f;
        musicSlider.value  = 1f;
        sfxSlider.value    = 1f;
        uiSlider.value     = 1f;

        if (bgmToggle != null) bgmToggle.isOn = true;
        if (sfxToggle != null) sfxToggle.isOn = true;

        PlayerPrefs.Save();
    }

    // ----------------------------------------------------------------
    // Helpers
    // ----------------------------------------------------------------

    private float SliderToDB(float value)
    {
        value = Mathf.Clamp(value, 0.0001f, 1f);
        return Mathf.Log10(value) * 20f;
    }

    private void UpdateLabel(TextMeshProUGUI label, float value)
    {
        if (label != null)
            label.text = Mathf.RoundToInt(value * 100) + "%";
    }
}