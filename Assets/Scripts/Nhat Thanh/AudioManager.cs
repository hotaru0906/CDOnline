using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("BGM Audio Source")]
    [SerializeField] private AudioSource bgmSource;

    [Header("BGM Playlist")]
    [SerializeField] private AudioClip[] bgmList;
    private int currentBGMIndex = 0;

    [Header("Minigame BGM")]
    [SerializeField] private AudioClip minigameBGM;

    [Header("Fade Settings")]
    [SerializeField] private float fadeDuration = 1f;

    [Header("Settings")]
    [SerializeField] private bool isBGMOn = true;
    [SerializeField] private bool isSFXOn = true;
    [SerializeField, Range(0f, 1f)] private float bgmVolume = 0.35f;
    [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;

    // Lưu trạng thái lobby BGM
    private float savedLobbyBGMTime = 0f;
    private int savedLobbyBGMIndex = 0;
    private bool isPlayingMinigameBGM = false;
    private Coroutine fadeCoroutine;

    // Public properties
    public bool IsBGMOn => isBGMOn;
    public bool IsSFXOn => isSFXOn;
    //public float BGMVolume => bgmVolume;
    public float SFXVolume => sfxVolume;
    public bool IsPlayingMinigameBGM => isPlayingMinigameBGM;
    public float FadeDuration { get => fadeDuration; set => fadeDuration = value; }

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
        //LoadSettings();
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
        // Auto-play next BGM khi hết bài (chỉ khi không phải minigame BGM)
        if (isBGMOn && !isPlayingMinigameBGM && bgmSource != null && !bgmSource.isPlaying && bgmList != null && bgmList.Length > 0)
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

    #region Minigame BGM Controls

    /// <summary>
    /// Gọi khi bắt đầu loading vào minigame - fade out lobby BGM và lưu vị trí
    /// </summary>
    public void OnEnterMinigameLoading()
    {
        if (!isBGMOn || bgmSource == null) return;

        // Lưu lại vị trí và index của lobby BGM
        savedLobbyBGMTime = bgmSource.time;
        savedLobbyBGMIndex = currentBGMIndex;

        // Đánh dấu đang trong minigame để Update() không tự play next BGM từ bgmList
        isPlayingMinigameBGM = true;

        // Fade out lobby BGM
        FadeOutBGM();
    }

    /// <summary>
    /// Gọi khi minigame đã load xong - bật minigame BGM
    /// </summary>
    public void OnMinigameStart()
    {
        OnMinigameStart(minigameBGM);
    }

    /// <summary>
    /// Gọi khi minigame đã load xong với custom BGM
    /// </summary>
    public void OnMinigameStart(AudioClip customMinigameBGM)
    {
        if (!isBGMOn || bgmSource == null) return;

        isPlayingMinigameBGM = true;

        if (customMinigameBGM != null)
        {
            bgmSource.loop = true; // THÊM: loop minigame BGM
            bgmSource.clip = customMinigameBGM;
            bgmSource.time = 0f;
            FadeInBGM();
        }
    }

    /// <summary>
    /// Gọi khi kết thúc minigame - fade out minigame BGM và khôi phục lobby BGM
    /// </summary>
    public void OnMinigameEnd()
    {
        if (!isBGMOn || bgmSource == null) return;

        // Fade out minigame BGM rồi fade in lobby BGM
        StartCoroutine(TransitionToLobbyBGM());
    }

    private IEnumerator TransitionToLobbyBGM()
    {
        yield return StartCoroutine(FadeOutCoroutine());

        isPlayingMinigameBGM = false;
        bgmSource.loop = false; // THÊM: tắt loop khi về lobby

        if (bgmList != null && bgmList.Length > 0)
        {
            currentBGMIndex = savedLobbyBGMIndex;
            bgmSource.clip = bgmList[currentBGMIndex];
            bgmSource.time = savedLobbyBGMTime;
            FadeInBGM();
        }
    }

    #endregion

    #region Fade Controls

    /// <summary>
    /// Fade out BGM hiện tại
    /// </summary>
    public void FadeOutBGM(float? customDuration = null)
    {
        if (fadeCoroutine != null)
            StopCoroutine(fadeCoroutine);

        fadeCoroutine = StartCoroutine(FadeOutCoroutine(customDuration ?? fadeDuration));
    }

    /// <summary>
    /// Fade in BGM hiện tại
    /// </summary>
    public void FadeInBGM(float? customDuration = null)
    {
        if (fadeCoroutine != null)
            StopCoroutine(fadeCoroutine);

        fadeCoroutine = StartCoroutine(FadeInCoroutine(customDuration ?? fadeDuration));
    }

    private IEnumerator FadeOutCoroutine(float duration = -1f)
    {
        if (duration < 0) duration = fadeDuration;
        if (bgmSource == null) yield break;

        float startVolume = bgmSource.volume;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            bgmSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / duration);
            yield return null;
        }

        bgmSource.volume = 0f;
        bgmSource.Stop();
    }

    private IEnumerator FadeInCoroutine(float duration = -1f)
    {
        if (duration < 0) duration = fadeDuration;
        if (bgmSource == null) yield break;

        bgmSource.volume = 0f;
        bgmSource.Play();

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            bgmSource.volume = Mathf.Lerp(0f, bgmVolume, elapsed / duration);
            yield return null;
        }

        bgmSource.volume = bgmVolume;
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

        //SaveSettings();
    }

    public void ToggleSFX()
    {
        SetSFX(!isSFXOn);
    }

    public void SetSFX(bool isOn)
    {
        isSFXOn = isOn;
        //SaveSettings();
    }

    /// <summary>
    /// Set BGM volume (0-1). Dùng cho slider.
    /// </summary>
    // public void SetBGMVolume(float volume)
    // {
    //     bgmVolume = Mathf.Clamp01(volume);
    //     if (bgmSource != null)
    //         bgmSource.volume = bgmVolume;
    //     SaveSettings();
    // }

    /// <summary>
    /// Set SFX volume (0-1). Dùng cho slider.
    /// </summary>
    public void SetSFXVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);
        //SaveSettings();
    }

    #endregion

    #region Save/Load

    // private void SaveSettings()
    // {
    //     PlayerPrefs.SetInt("BGM_On", isBGMOn ? 1 : 0);
    //     PlayerPrefs.SetInt("SFX_On", isSFXOn ? 1 : 0);
    //     PlayerPrefs.SetFloat("BGM_Volume", bgmVolume);
    //     PlayerPrefs.SetFloat("SFX_Volume", sfxVolume);
    //     PlayerPrefs.Save();
    // }

    // private void LoadSettings()
    // {
    //     isBGMOn = PlayerPrefs.GetInt("BGM_On", 1) == 1;
    //     isSFXOn = PlayerPrefs.GetInt("SFX_On", 1) == 1;
    //     bgmVolume = PlayerPrefs.GetFloat("BGM_Volume", 1f);
    //     sfxVolume = PlayerPrefs.GetFloat("SFX_Volume", 1f);

    //     if (bgmSource != null)
    //         bgmSource.volume = bgmVolume;
    // }

    #endregion
}
