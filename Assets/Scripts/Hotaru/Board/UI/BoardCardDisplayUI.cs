using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// Hiện card giữa màn hình khi player dùng item — tất cả player đều thấy.
/// Gọi từ BoardHUDController khi nhận RPC_ItemUsed.
/// SETUP:
///   1. Tạo GameObject "CardDisplay" ở center Canvas
///   2. Attach script này
///   3. Cấu trúc con:
///        CardDisplay
///          ├── CardImage   (Image)     ← cardImage
///          └── DescText    (TMP_Text)  ← descText
/// </summary>
public class BoardCardDisplayUI : MonoBehaviour
{
    public static BoardCardDisplayUI Instance { get; private set; }

    [Header("References")]
    [SerializeField] private Image    cardImage;
    [SerializeField] private TMP_Text descText;

    [Header("Timing")]
    [SerializeField] private float holdDuration    = 2f;
    [SerializeField] private float fadeDuration    = 0.4f;

    private CanvasGroup  _canvasGroup;
    private Coroutine    _routine;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();

        _canvasGroup.alpha = 0f;
        gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // =====================================================================
    // PUBLIC API
    // =====================================================================

    /// <summary>
    /// Hiện card giữa màn hình, giữ holdDuration giây rồi fade out.
    /// Gọi từ BoardHUDController.OnItemUsed().
    /// </summary>
    public void Show(BoardItemEffect effect)
    {
        var data = BoardItemPool.Current?.GetByEffect(effect);

        if (cardImage != null) cardImage.sprite = data?.icon;
        if (descText  != null) descText.text    = data?.description ?? effect.ToString();

        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(ShowRoutine());
    }

    // =====================================================================
    // ANIMATION
    // =====================================================================

    private IEnumerator ShowRoutine()
    {
        gameObject.SetActive(true);
        _canvasGroup.alpha = 0f;

        // Fade in
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed            += Time.deltaTime;
            _canvasGroup.alpha  = Mathf.Lerp(0f, 1f, elapsed / fadeDuration);
            yield return null;
        }
        _canvasGroup.alpha = 1f;

        // Hold
        yield return new WaitForSeconds(holdDuration);

        // Fade out
        elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed            += Time.deltaTime;
            _canvasGroup.alpha  = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
            yield return null;
        }

        _canvasGroup.alpha = 0f;
        gameObject.SetActive(false);
        _routine = null;
    }
}