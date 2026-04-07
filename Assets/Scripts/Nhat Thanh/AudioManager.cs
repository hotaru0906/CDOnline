using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Quản lý BGM và Audio Settings.
/// Singleton độc lập, DontDestroyOnLoad.
/// Chạy LOCAL - không sync qua network.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("BGM Audio Source")]
    [SerializeField] private AudioSource bgmSource;

    [Header("BGM Playlist")]
    [SerializeField] private AudioClip[] bgmList;
    private int currentBGMIndex = 0;

    [Header("Settings")]
    [SerializeField] private bool isBGMOn = true;
    [SerializeField] private bool isSFXOn = true;
    [SerializeField, Range(0f, 1f)] private float bgmVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;

    // Public properties
    public bool IsBGMOn => isBGMOn;
    public bool IsSFXOn => isSFXOn;
    public float BGMVolume => bgmVolume;
    public float SFXVolume => sfxVolume;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        SetupAudioSource();
        LoadSettings();
    }

    void Start()
    {
        if (isBGMOn && bgmList != null && bgmList.Length > 0)
        {
            PlayCurrentBGM();
        }

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Có thể thêm logic đổi BGM theo scene ở đây
    }

    void Update()
    {
        // Auto-play next BGM khi hết bài
        if (isBGMOn && bgmSource != null && !bgmSource.isPlaying && bgmList != null && bgmList.Length > 0)
        {
            PlayNextBGM();
        }
    }

    private void SetupAudioSource()
    {
        if (bgmSource == null)
        {
            bgmSource = gameObject.AddComponent<AudioSource>();
            bgmSource.loop = false;
            bgmSource.playOnAwake = false;
        }
    }

    #region BGM Controls

    public void PlayCurrentBGM()
    {
        if (bgmSource == null || bgmList == null || bgmList.Length == 0) return;

        bgmSource.clip = bgmList[currentBGMIndex];
        bgmSource.volume = bgmVolume;
        bgmSource.Play();
    }

    public void PlayNextBGM()
    {
        currentBGMIndex = (currentBGMIndex + 1) % bgmList.Length;
        PlayCurrentBGM();
    }

    public void PlayPreviousBGM()
    {
        currentBGMIndex = (currentBGMIndex - 1 + bgmList.Length) % bgmList.Length;
        PlayCurrentBGM();
    }

    public void PlayBGM(int index)
    {
        if (bgmList == null || bgmList.Length == 0) return;
        currentBGMIndex = Mathf.Clamp(index, 0, bgmList.Length - 1);
        PlayCurrentBGM();
    }

    public void PlayBGM(AudioClip clip)
    {
        if (clip == null || bgmSource == null) return;
        
        bgmSource.clip = clip;
        bgmSource.volume = bgmVolume;
        bgmSource.Play();
    }

    public void StopBGM()
    {
        if (bgmSource != null)
            bgmSource.Stop();
    }

    public void PauseBGM()
    {
        if (bgmSource != null)
            bgmSource.Pause();
    }

    public void ResumeBGM()
    {
        if (bgmSource != null && !bgmSource.isPlaying)
            bgmSource.UnPause();
    }

    #endregion

    #region Settings Controls

    public void ToggleBGM()
    {
        SetBGM(!isBGMOn);
    }

    public void SetBGM(bool isOn)
    {
        isBGMOn = isOn;

        if (bgmSource != null)
        {
            if (isOn)
            {
                if (!bgmSource.isPlaying)
                    PlayCurrentBGM();
            }
            else
            {
                bgmSource.Stop();
            }
        }

        SaveSettings();
    }

    public void ToggleSFX()
    {
        SetSFX(!isSFXOn);
    }

    public void SetSFX(bool isOn)
    {
        isSFXOn = isOn;
        SaveSettings();
    }

    /// <summary>
    /// Set BGM volume (0-1). Dùng cho slider.
    /// </summary>
    public void SetBGMVolume(float volume)
    {
        bgmVolume = Mathf.Clamp01(volume);
        if (bgmSource != null)
            bgmSource.volume = bgmVolume;
        SaveSettings();
    }

    /// <summary>
    /// Set SFX volume (0-1). Dùng cho slider.
    /// </summary>
    public void SetSFXVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);
        SaveSettings();
    }

    #endregion

    #region Save/Load

    private void SaveSettings()
    {
        PlayerPrefs.SetInt("BGM_On", isBGMOn ? 1 : 0);
        PlayerPrefs.SetInt("SFX_On", isSFXOn ? 1 : 0);
        PlayerPrefs.SetFloat("BGM_Volume", bgmVolume);
        PlayerPrefs.SetFloat("SFX_Volume", sfxVolume);
        PlayerPrefs.Save();
    }

    private void LoadSettings()
    {
        isBGMOn = PlayerPrefs.GetInt("BGM_On", 1) == 1;
        isSFXOn = PlayerPrefs.GetInt("SFX_On", 1) == 1;
        bgmVolume = PlayerPrefs.GetFloat("BGM_Volume", 1f);
        sfxVolume = PlayerPrefs.GetFloat("SFX_Volume", 1f);

        if (bgmSource != null)
            bgmSource.volume = bgmVolume;
    }

    #endregion
}
