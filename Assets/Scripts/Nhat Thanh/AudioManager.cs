using UnityEngine;
using UnityEngine.Audio;
using System.Collections;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("BGM Audio Source")]
    [SerializeField] private AudioSource bgmSource;

    [Header("Audio Mixer")]
    [SerializeField] private AudioMixer audioMixer;

    [Header("Main BGM (Menu / Lobby / Board - dùng chung, liên tục)")]
    [SerializeField] private AudioClip mainBGM;

    [Header("Fade Settings")]
    [SerializeField] private float fadeDuration = 1f;

    [Header("Settings")]
    [SerializeField] private bool isBGMOn = true;
    [SerializeField] private bool isSFXOn = true;

    // Lưu vị trí phát của MainBGM khi chuyển sang Minigame
    private float savedMainBGMTime = 0f;
    private bool isPlayingMinigameBGM = false;
    private Coroutine fadeCoroutine;

    public bool IsBGMOn => isBGMOn;
    public bool IsSFXOn => isSFXOn;
    public float SFXVolume => GetMixerVolume("SFXVolume");
    public bool IsPlayingMinigameBGM => isPlayingMinigameBGM;
    public float FadeDuration { get => fadeDuration; set => fadeDuration = value; }
    private const string K_MUSIC = "audio_music";       // NEW — khớp key AudioSettings đang dùng
    private const float DEFAULT_MUSIC_VOL = 0.8f;
    private float musicTargetDb = 0f;

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

        // NEW — đọc sẵn volume đã lưu, không phụ thuộc AudioSettings chạy trước
        float savedMusic = PlayerPrefs.GetFloat(K_MUSIC, DEFAULT_MUSIC_VOL);
        musicTargetDb = LinearToDb(savedMusic);
        if (audioMixer != null)
            audioMixer.SetFloat("MusicVolume", musicTargetDb);
    }
    private float LinearToDb(float linear)
    {
        linear = Mathf.Clamp01(linear);
        return linear <= 0.0001f ? -80f : Mathf.Log10(linear) * 20f;
    }

    public void SetMusicTargetDb(float db)   // NEW
    {
        musicTargetDb = db;
    }

    void Start()
    {
        if (isBGMOn && mainBGM != null)
        {
            bgmSource.clip = mainBGM;
            bgmSource.loop = true;
            bgmSource.time = 0f;
            bgmSource.Play();
        }
    }

    private void SetupAudioSource()
    {
        if (bgmSource == null)
        {
            bgmSource = gameObject.AddComponent<AudioSource>();
            bgmSource.loop = true;
            bgmSource.playOnAwake = false;
        }

        if (audioMixer != null)
        {
            var musicGroup = audioMixer.FindMatchingGroups("Music");
            if (musicGroup.Length > 0)
                bgmSource.outputAudioMixerGroup = musicGroup[0];
        }

        bgmSource.volume = 1f;
    }

    #region Main BGM (Menu / Lobby / Board)
    public void EnterMainBGM()
    {
        if (!isBGMOn || bgmSource == null || mainBGM == null) return;

        // Đã đang phát MainBGM rồi (đang ở Lobby<->Board) -> để yên, không restart
        if (!isPlayingMinigameBGM && bgmSource.clip == mainBGM && bgmSource.isPlaying)
            return;

        StartCoroutine(TransitionToMainBGM());
    }

    private IEnumerator TransitionToMainBGM()
    {
        // Nếu đang phát minigame BGM thì fade out trước
        if (bgmSource.isPlaying)
            yield return StartCoroutine(FadeOutCoroutine());

        isPlayingMinigameBGM = false;
        bgmSource.loop = true;
        bgmSource.clip = mainBGM;
        bgmSource.time = Mathf.Clamp(savedMainBGMTime, 0f, mainBGM.length - 0.01f);

        FadeInBGM();
    }

    #endregion

    #region Minigame BGM
    public void EnterMinigameBGM(AudioClip minigameClip)
    {
        if (!isBGMOn || bgmSource == null) return;

        // Lưu lại vị trí MainBGM đang phát dở trước khi rời đi
        if (!isPlayingMinigameBGM && bgmSource.clip == mainBGM)
            savedMainBGMTime = bgmSource.time;

        if (minigameClip == null)
        {
            // Minigame này không có BGM riêng -> im lặng (fade out MainBGM)
            isPlayingMinigameBGM = true;
            FadeOutBGM();
            return;
        }

        StartCoroutine(TransitionToMinigameBGM(minigameClip));
    }

    private IEnumerator TransitionToMinigameBGM(AudioClip clip)
    {
        if (bgmSource.isPlaying)
            yield return StartCoroutine(FadeOutCoroutine());

        isPlayingMinigameBGM = true;
        bgmSource.loop = true;
        bgmSource.clip = clip;
        bgmSource.time = 0f;

        FadeInBGM();
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

    private IEnumerator FadeOutCoroutine(float duration = -1f)
    {
        if (duration < 0) duration = fadeDuration;
        if (audioMixer == null) { bgmSource?.Stop(); yield break; }

        audioMixer.GetFloat("MusicVolume", out float currentDb);
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
        float targetDb = musicTargetDb;   // SỬA — thay vì hardcode 0f

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
            if (isOn) { if (!bgmSource.isPlaying) bgmSource.Play(); }
            else bgmSource.Stop();
        }
    }

    public void ToggleSFX() => SetSFX(!isSFXOn);
    public void SetSFX(bool isOn) => isSFXOn = isOn;

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