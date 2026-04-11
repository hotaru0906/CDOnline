using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Quản lý SFX playback.
/// Singleton độc lập, DontDestroyOnLoad.
/// Hỗ trợ cả local SFX và 3D audio.
/// Tự động thêm sound cho tất cả Button trong scene.
/// </summary>
public class SFXManager : MonoBehaviour
{
    public static SFXManager Instance { get; private set; }

    [Header("Audio Source")]
    [SerializeField] private AudioSource sfxSource;

    [Header("UI SFX Clips")]
    [SerializeField] private AudioClip buttonClickSound;
    [SerializeField] private AudioClip buttonCancelSound;

    [Header("Player Action SFX Clips (Network Sync)")]
    [SerializeField] private AudioClip walkSound;
    [SerializeField] private AudioClip runSound;
    [SerializeField] private AudioClip jumpSound;
    [SerializeField] private AudioClip attackSound;
    [SerializeField] private AudioClip kickSound;

    [Header("Auto Button Sound")]
    [SerializeField] private bool autoAddButtonSound = true;

    [Header("3D Audio Settings")]
    [SerializeField] private float minDistance3D = 1f;
    [SerializeField] private float maxDistance3D = 20f;

    // Public properties for clips (để NetworkSFXController truy cập)
    public AudioClip WalkSound => walkSound;
    public AudioClip RunSound => runSound;
    public AudioClip JumpSound => jumpSound;
    public AudioClip AttackSound => attackSound;
    public AudioClip KickSound => kickSound;

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
        SceneManager.sceneLoaded += OnSceneLoaded;

        if (autoAddButtonSound)
            RegisterAllButtons();
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (autoAddButtonSound)
            RegisterAllButtons();
    }

    /// <summary>
    /// Tự động thêm sound click cho tất cả Button trong scene
    /// </summary>
    private void RegisterAllButtons()
    {
        Button[] allButtons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (Button btn in allButtons)
        {
            // Bỏ qua nếu button đã có UIButtonSound (custom sound)
            if (btn.GetComponent<UIButtonSound>() != null)
                continue;

            btn.onClick.RemoveListener(PlayButtonClick);
            btn.onClick.AddListener(PlayButtonClick);
        }

        Debug.Log($"[SFXManager] Registered {allButtons.Length} buttons in scene {SceneManager.GetActiveScene().name}");
    }

    private void SetupAudioSource()
    {
        if (sfxSource == null)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.loop = false;
            sfxSource.playOnAwake = false;
        }
    }

    #region Local SFX Play Methods

    /// <summary>
    /// Phát SFX clip (local, 2D)
    /// </summary>
    public void PlaySFX(AudioClip clip, float volumeScale = 1f)
    {
        if (!IsSFXEnabled() || clip == null || sfxSource == null) return;
        sfxSource.PlayOneShot(clip, GetSFXVolume() * volumeScale);
    }

    /// <summary>
    /// Phát SFX theo tên
    /// </summary>
    public void PlaySFX(string soundName, float volumeScale = 1f)
    {
        AudioClip clip = GetSFXByName(soundName);
        if (clip != null)
        {
            PlaySFX(clip, volumeScale);
        }
    }

    // UI Sounds
    public void PlayButtonClick() => PlaySFX(buttonClickSound);
    public void PlayButtonCancel() => PlaySFX(buttonCancelSound);

    #endregion

    #region 3D Audio Methods

    /// <summary>
    /// Phát SFX 3D tại vị trí cụ thể (simple version)
    /// </summary>
    public void PlaySFX3D(AudioClip clip, Vector3 position, float volumeScale = 1f)
    {
        if (!IsSFXEnabled() || clip == null) return;
        AudioSource.PlayClipAtPoint(clip, position, GetSFXVolume() * volumeScale);
    }

    /// <summary>
    /// Phát SFX 3D theo tên tại vị trí cụ thể
    /// </summary>
    public void PlaySFX3D(string soundName, Vector3 position, float volumeScale = 1f)
    {
        AudioClip clip = GetSFXByName(soundName);
        PlaySFX3D(clip, position, volumeScale);
    }

    /// <summary>
    /// Phát SFX 3D với kiểm soát distance chi tiết
    /// </summary>
    public void PlaySFX3DAdvanced(AudioClip clip, Vector3 position, float volumeScale = 1f, bool loop = false)
    {
        if (!IsSFXEnabled() || clip == null) return;

        GameObject tempAudio = new GameObject("TempAudio3D");
        tempAudio.transform.position = position;

        AudioSource audioSource = tempAudio.AddComponent<AudioSource>();
        audioSource.clip = clip;
        audioSource.volume = GetSFXVolume() * volumeScale;
        audioSource.spatialBlend = 1f;
        audioSource.minDistance = minDistance3D;
        audioSource.maxDistance = maxDistance3D;
        audioSource.rolloffMode = AudioRolloffMode.Linear;
        audioSource.loop = loop;
        audioSource.Play();

        if (!loop)
        {
            Destroy(tempAudio, clip.length + 0.1f);
        }
    }

    /// <summary>
    /// Tạo AudioSource 3D gắn vào object (cho looping sound như footsteps)
    /// </summary>
    public AudioSource CreateAttached3DAudioSource(Transform parent, AudioClip clip, bool loop = true)
    {
        GameObject audioObj = new GameObject("Attached3DAudio");
        audioObj.transform.SetParent(parent);
        audioObj.transform.localPosition = Vector3.zero;

        AudioSource source = audioObj.AddComponent<AudioSource>();
        source.clip = clip;
        source.volume = GetSFXVolume();
        source.spatialBlend = 1f;
        source.minDistance = minDistance3D;
        source.maxDistance = maxDistance3D;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.loop = loop;
        source.playOnAwake = false;

        return source;
    }

    #endregion

    #region Helper Methods

    private AudioClip GetSFXByName(string name)
    {
        return name.ToLower() switch
        {
            "confirm" or "click" => buttonClickSound,
            "cancel" => buttonCancelSound,
            "walk" => walkSound,
            "run" => runSound,
            "jump" => jumpSound,
            "attack" => attackSound,
            "kick" => kickSound,
            _ => null
        };
    }

    private bool IsSFXEnabled()
    {
        return AudioManager.Instance != null && AudioManager.Instance.IsSFXOn;
    }

    private float GetSFXVolume()
    {
        return AudioManager.Instance != null ? AudioManager.Instance.SFXVolume : 1f;
    }

    #endregion
}
