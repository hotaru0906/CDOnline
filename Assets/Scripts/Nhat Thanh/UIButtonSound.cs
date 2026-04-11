using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Component UI cho phép custom sound riêng cho từng Button.
/// Gắn vào Button để override sound mặc định của SFXManager.
/// Chạy LOCAL - không sync qua network.
/// </summary>
[RequireComponent(typeof(Button))]
public class UIButtonSound : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
{
    [Header("Sound Settings")]
    [Tooltip("Tên sound trong SFXManager (ví dụ: 'confirm', 'cancel', etc.)")]
    [SerializeField] private string clickSoundName = "click";

    [Tooltip("Hoặc dùng AudioClip custom")]
    [SerializeField] private AudioClip customClickSound;

    [Tooltip("Sound khi hover (optional)")]
    [SerializeField] private AudioClip hoverSound;

    [Header("Volume")]
    [SerializeField, Range(0f, 1f)] private float volumeScale = 1f;

    [Header("Options")]
    [SerializeField] private bool playOnClick = true;
    [SerializeField] private bool playOnHover = false;

    private Button _button;

    private void Awake()
    {
        _button = GetComponent<Button>();
    }

    private void OnEnable()
    {
        if (_button != null && playOnClick)
        {
            _button.onClick.AddListener(OnButtonClick);
        }
    }

    private void OnDisable()
    {
        if (_button != null)
        {
            _button.onClick.RemoveListener(OnButtonClick);
        }
    }

    private void OnButtonClick()
    {
        PlayClickSound();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // Backup nếu Button.onClick không được gọi
        // (thường không cần vì đã đăng ký onClick)
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (playOnHover)
        {
            PlayHoverSound();
        }
    }

    /// <summary>
    /// Phát sound click
    /// </summary>
    public void PlayClickSound()
    {
        if (SFXManager.Instance == null) return;

        // Ưu tiên custom clip
        if (customClickSound != null)
        {
            SFXManager.Instance.PlaySFX(customClickSound, volumeScale);
        }
        // Fallback theo tên
        else if (!string.IsNullOrEmpty(clickSoundName))
        {
            SFXManager.Instance.PlaySFX(clickSoundName, volumeScale);
        }
    }

    /// <summary>
    /// Phát sound hover
    /// </summary>
    public void PlayHoverSound()
    {
        if (hoverSound != null && SFXManager.Instance != null)
        {
            SFXManager.Instance.PlaySFX(hoverSound, volumeScale * 0.7f);
        }
    }

    #region Editor Helpers

    /// <summary>
    /// Set sound bằng tên (dùng trong code)
    /// </summary>
    public void SetClickSound(string soundName)
    {
        clickSoundName = soundName;
        customClickSound = null;
    }

    /// <summary>
    /// Set sound bằng clip (dùng trong code)
    /// </summary>
    public void SetClickSound(AudioClip clip)
    {
        customClickSound = clip;
        clickSoundName = null;
    }

    #endregion
}
