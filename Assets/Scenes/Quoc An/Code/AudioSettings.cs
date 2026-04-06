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

    // ==============================
    //         UNITY EVENTS
    // ==============================
    private void Start()
    {
        LoadAudioSettings(); // Load cài đặt đã lưu
        BindSliderEvents();  // Gắn sự kiện cho Slider
    }

    // ==============================
    //      BIND SLIDER EVENTS
    // ==============================
    private void BindSliderEvents()
    {
        // Gắn sự kiện thay đổi giá trị
        masterSlider.onValueChanged.AddListener(SetMasterVolume);
        musicSlider.onValueChanged.AddListener(SetMusicVolume);
        sfxSlider.onValueChanged.AddListener(SetSFXVolume);
        uiSlider.onValueChanged.AddListener(SetUIVolume);
    }

    // ==============================
    //       SET VOLUME FUNCTIONS
    // ==============================

    // Công thức: dB = log10(value) * 20
    // Vì AudioMixer dùng dB (-80 đến 0)

    public void SetMasterVolume(float value)
    {
        // Tránh log(0) = -infinity
        float dB = value > 0.001f ? Mathf.Log10(value) * 20f : -80f;
        audioMixer.SetFloat("MasterVolume", dB);

        // Cập nhật label %
        masterValueText.text = Mathf.RoundToInt(value * 100) + "%";

        // Lưu vào PlayerPrefs
        PlayerPrefs.SetFloat("MasterVolume", value);
    }

    public void SetMusicVolume(float value)
    {
        float dB = value > 0.001f ? Mathf.Log10(value) * 20f : -80f;
        audioMixer.SetFloat("MusicVolume", dB);
        musicValueText.text = Mathf.RoundToInt(value * 100) + "%";
        PlayerPrefs.SetFloat("MusicVolume", value);
    }

    public void SetSFXVolume(float value)
    {
        float dB = value > 0.001f ? Mathf.Log10(value) * 20f : -80f;
        audioMixer.SetFloat("SFXVolume", dB);
        sfxValueText.text = Mathf.RoundToInt(value * 100) + "%";
        PlayerPrefs.SetFloat("SFXVolume", value);
    }

    public void SetUIVolume(float value)
    {
        float dB = value > 0.001f ? Mathf.Log10(value) * 20f : -80f;
        audioMixer.SetFloat("UIVolume", dB);
        uiValueText.text = Mathf.RoundToInt(value * 100) + "%";
        PlayerPrefs.SetFloat("UIVolume", value);
    }

    // ==============================
    //       SAVE / LOAD
    // ==============================
    private void LoadAudioSettings()
    {
        // Load giá trị đã lưu, mặc định = 1 (100%)
        float master = PlayerPrefs.GetFloat("MasterVolume", 1f);
        float music  = PlayerPrefs.GetFloat("MusicVolume",  1f);
        float sfx    = PlayerPrefs.GetFloat("SFXVolume",    1f);
        float ui     = PlayerPrefs.GetFloat("UIVolume",     1f);

        // Cập nhật Slider (sẽ tự gọi onValueChanged)
        masterSlider.value = master;
        musicSlider.value  = music;
        sfxSlider.value    = sfx;
        uiSlider.value     = ui;
    }

    // Reset về mặc định
    public void ResetAudio()
    {
        masterSlider.value = 1f;
        musicSlider.value  = 1f;
        sfxSlider.value    = 1f;
        uiSlider.value     = 1f;
    }
}