using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Audio;
using TMPro;

public class AudioSettings : MonoBehaviour
{
    [Header("=== Mixer ===")]
    [SerializeField] private AudioMixer mixer;

    [Header("=== Sliders (Min 0 / Max 1) ===")]
    [SerializeField] private Slider masterSlider;
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider sfxSlider;

    [Header("=== Label % (optional) ===")]
    [SerializeField] private TMP_Text masterLabel;
    [SerializeField] private TMP_Text musicLabel;
    [SerializeField] private TMP_Text sfxLabel;

    // Tên exposed parameter trong AudioMixer
    private const string P_MASTER = "MasterVolume";
    private const string P_MUSIC = "MusicVolume";
    private const string P_SFX = "SFXVolume";

    // Key PlayerPrefs
    private const string K_MASTER = "audio_master";
    private const string K_MUSIC = "audio_music";
    private const string K_SFX = "audio_sfx";
    private const float DEFAULT_VOL = 0.8f;
    private const float MIN_VOL = 0.0001f; // tránh log10(0) = -Infinity

    private void Awake()
    {
        LoadAudio();
    }

    private void OnEnable()
    {
        if (masterSlider) masterSlider.onValueChanged.AddListener(SetMaster);
        if (musicSlider) musicSlider.onValueChanged.AddListener(SetMusic);
        if (sfxSlider) sfxSlider.onValueChanged.AddListener(SetSFX);
    }

    private void OnDisable()
    {
        if (masterSlider) masterSlider.onValueChanged.RemoveListener(SetMaster);
        if (musicSlider) musicSlider.onValueChanged.RemoveListener(SetMusic);
        if (sfxSlider) sfxSlider.onValueChanged.RemoveListener(SetSFX);
    }

    #region Set
    public void SetMaster(float v) => Apply(P_MASTER, K_MASTER, v, masterLabel);
    public void SetMusic(float v)
    {
        Apply(P_MUSIC, K_MUSIC, v, musicLabel);

        // NEW — báo cho AudioManager biết target dB mới, để lần fade tiếp theo dùng đúng giá trị
        float db = Mathf.Log10(Mathf.Max(Mathf.Clamp01(v), MIN_VOL)) * 20f;
        if (Mathf.Clamp01(v) <= MIN_VOL) db = -80f;
        AudioManager.Instance?.SetMusicTargetDb(db);
    }
    public void SetSFX(float v) => Apply(P_SFX, K_SFX, v, sfxLabel);

    private void Apply(string param, string key, float linear, TMP_Text label)
    {
        linear = Mathf.Clamp01(linear);

        // Chuyển tuyến tính (0..1) sang decibel cho tai người nghe mượt
        float db = Mathf.Log10(Mathf.Max(linear, MIN_VOL)) * 20f;
        if (linear <= MIN_VOL) db = -80f; // tắt hẳn

        if (mixer != null) mixer.SetFloat(param, db);

        PlayerPrefs.SetFloat(key, linear); // ghi ngay khi kéo
        if (label != null) label.text = Mathf.RoundToInt(linear * 100f) + "%";
    }
    #endregion

    #region Load / Reset
    public void LoadAudio()
    {
        float m = PlayerPrefs.GetFloat(K_MASTER, DEFAULT_VOL);
        float mu = PlayerPrefs.GetFloat(K_MUSIC, DEFAULT_VOL);
        float s = PlayerPrefs.GetFloat(K_SFX, DEFAULT_VOL);

        // SetValueWithoutNotify để không bắn onValueChanged khi đang load
        if (masterSlider) masterSlider.SetValueWithoutNotify(m);
        if (musicSlider) musicSlider.SetValueWithoutNotify(mu);
        if (sfxSlider) sfxSlider.SetValueWithoutNotify(s);

        Apply(P_MASTER, K_MASTER, m, masterLabel);
        Apply(P_MUSIC, K_MUSIC, mu, musicLabel);
        Apply(P_SFX, K_SFX, s, sfxLabel);
    }

    public void ResetAudio()
    {
        if (masterSlider) masterSlider.SetValueWithoutNotify(DEFAULT_VOL);
        if (musicSlider) musicSlider.SetValueWithoutNotify(DEFAULT_VOL);
        if (sfxSlider) sfxSlider.SetValueWithoutNotify(DEFAULT_VOL);

        Apply(P_MASTER, K_MASTER, DEFAULT_VOL, masterLabel);
        Apply(P_MUSIC, K_MUSIC, DEFAULT_VOL, musicLabel);
        Apply(P_SFX, K_SFX, DEFAULT_VOL, sfxLabel);
    }
    #endregion
}