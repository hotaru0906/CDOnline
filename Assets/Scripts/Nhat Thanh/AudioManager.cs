using UnityEngine;
using UnityEngine.Audio; // THÊM
using UnityEngine.SceneManagement;
using System.Collections;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("BGM Audio Source")]
    [SerializeField] private AudioSource bgmSource;

    [Header("Audio Mixer")] // THÊM
    [SerializeField] private AudioMixer audioMixer; // kéo GameAudioMixer vào đây

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

    private float savedLobbyBGMTime = 0f;
    private int savedLobbyBGMIndex = 0;
    private bool isPlayingMinigameBGM = false;
    private Coroutine fadeCoroutine;

    public bool IsBGMOn => isBGMOn;
    public bool IsSFXOn => isSFXOn;
    public float SFXVolume => GetMixerVolume("SFXVolume"); // THÊM
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
    }

    void Start()
    {
        if (isBGMOn && bgmList != null && bgmList.Length > 0)
            PlayCurrentBGM();

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) { }

    void Update()
    {
        if (isBGMOn && !isPlayingMinigameBGM && bgmSource != null
            && !bgmSource.isPlaying && bgmList != null && bgmList.Length > 0)
            PlayNextBGM();
    }

    private void SetupAudioSource()
    {
        if (bgmSource == null)
        {
            bgmSource = gameObject.AddComponent<AudioSource>();
            bgmSource.loop = false;
            bgmSource.playOnAwake = false;
        }

        // THÊM: route BGM qua Music Group trong AudioMixer
        if (audioMixer != null)
        {
            var musicGroup = audioMixer.FindMatchingGroups("Music");
            if (musicGroup.Length > 0)
                bgmSource.outputAudioMixerGroup = musicGroup[0];
        }

        // THÊM: volume AudioSource luôn là 1, để mixer tự quản lý
        bgmSource.volume = 1f;
    }

    #region BGM Controls

    public void PlayCurrentBGM()
    {
        if (bgmSource == null || bgmList == null || bgmList.Length == 0) return;
        bgmSource.clip = bgmList[currentBGMIndex];
        bgmSource.volume = 1f; // SỬA: không set bgmVolume nữa, mixer lo
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
        bgmSource.volume = 1f; // SỬA
        bgmSource.Play();
    }

    public void StopBGM() { if (bgmSource != null) bgmSource.Stop(); }
    public void PauseBGM() { if (bgmSource != null) bgmSource.Pause(); }
    public void ResumeBGM() { if (bgmSource != null && !bgmSource.isPlaying) bgmSource.UnPause(); }

    #endregion

    #region Minigame BGM Controls

    public void OnEnterMinigameLoading()
    {
        if (!isBGMOn || bgmSource == null) return;
        savedLobbyBGMTime = bgmSource.time;
        savedLobbyBGMIndex = currentBGMIndex;
        isPlayingMinigameBGM = true;
        FadeOutBGM();
    }

    public void OnMinigameStart() => OnMinigameStart(minigameBGM);

    public void OnMinigameStart(AudioClip customMinigameBGM)
    {
        if (!isBGMOn || bgmSource == null) return;
        isPlayingMinigameBGM = true;
        if (customMinigameBGM != null)
        {
            bgmSource.loop = true;
            bgmSource.clip = customMinigameBGM;
            bgmSource.time = 0f;
            FadeInBGM();
        }
    }

    public void OnMinigameEnd()
    {
        if (!isBGMOn || bgmSource == null) return;
        StartCoroutine(TransitionToLobbyBGM());
    }

    private IEnumerator TransitionToLobbyBGM()
    {
        yield return StartCoroutine(FadeOutCoroutine());
        isPlayingMinigameBGM = false;
        bgmSource.loop = false;
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

    public void FadeOutBGM(float? customDuration = null)
    {
        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(FadeOutCoroutine(customDuration ?? fadeDuration));
    }

    public void FadeInBGM(float? customDuration = null)
    {
        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(FadeInCoroutine(customDuration ?? fadeDuration));
    }

    /// <summary>
    /// SỬA: Fade qua AudioMixer thay vì bgmSource.volume trực tiếp.
    /// </summary>
    private IEnumerator FadeOutCoroutine(float duration = -1f)
    {
        if (duration < 0) duration = fadeDuration;
        if (audioMixer == null) { bgmSource?.Stop(); yield break; }

        float currentDb;
        audioMixer.GetFloat("MusicVolume", out currentDb);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float db = Mathf.Lerp(currentDb, -80f, elapsed / duration);
            audioMixer.SetFloat("MusicVolume", db);
            yield return null;
        }

        audioMixer.SetFloat("MusicVolume", -80f);
        bgmSource?.Stop();
    }

    private IEnumerator FadeInCoroutine(float duration = -1f)
    {
        if (duration < 0) duration = fadeDuration;
        if (audioMixer == null) { bgmSource?.Play(); yield break; }

        audioMixer.SetFloat("MusicVolume", -80f);
        bgmSource?.Play();
        float elapsed = 0f;

        // Target = 0dB (volume chuẩn, slider sẽ tự set giá trị thật)
        float targetDb = 0f;
        audioMixer.GetFloat("MusicVolume", out float savedDb);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float db = Mathf.Lerp(-80f, targetDb, elapsed / duration);
            audioMixer.SetFloat("MusicVolume", db);
            yield return null;
        }

        audioMixer.SetFloat("MusicVolume", targetDb);
    }

    #endregion

    #region Settings Controls

    public void ToggleBGM() => SetBGM(!isBGMOn);

    public void SetBGM(bool isOn)
    {
        isBGMOn = isOn;
        if (bgmSource != null)
        {
            if (isOn) { if (!bgmSource.isPlaying) PlayCurrentBGM(); }
            else bgmSource.Stop();
        }
    }

    public void ToggleSFX() => SetSFX(!isSFXOn);

    public void SetSFX(bool isOn)
    {
        isSFXOn = isOn;
    }

    public void SetSFXVolume(float volume)
    {
        if (audioMixer != null)
            audioMixer.SetFloat("SFXVolume", SliderToDB(volume));
    }

    #endregion

    #region Helpers

    private float SliderToDB(float value)
    {
        value = Mathf.Clamp(value, 0.0001f, 1f);
        return Mathf.Log10(value) * 20f;
    }

    private float GetMixerVolume(string parameter)
    {
        if (audioMixer == null) return 1f;
        audioMixer.GetFloat(parameter, out float db);
        return Mathf.Pow(10f, db / 20f);
    }

    #endregion
}