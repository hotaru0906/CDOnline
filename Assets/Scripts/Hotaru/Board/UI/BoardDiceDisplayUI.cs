using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// Hiện kết quả xúc xắc khi player roll — fade in to màn hình, giữ, rồi fade out.
///
/// SETUP TRONG UNITY EDITOR:
///   1. Tạo GameObject con trong Canvas, đặt tên "DiceDisplayPanel".
///   2. Gắn script này.
///   3. Thêm CanvasGroup component lên cùng GameObject.
///   4. Tạo cấu trúc con:
///        DiceDisplayPanel
///          ├── Background   (Image, màu tối bán trong suốt, RectTransform căn giữa)
///          ├── DiceValue    (TMP_Text, font size lớn ~120, in đậm) ← gán vào diceValueText
///          └── PlayerLabel  (TMP_Text, font size ~28)              ← gán vào playerNameText
///   5. Set CanvasGroup.alpha = 0 trong Inspector (bắt đầu ẩn).
/// </summary>
public class BoardDiceDisplayUI : MonoBehaviour
{
    public static BoardDiceDisplayUI Instance { get; private set; }

    [Header("References")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TMP_Text diceValueText;
    [SerializeField] private TMP_Text playerNameText;

    [Header("Timing")]
    [SerializeField] private float fadeInDuration  = 0.2f;
    [SerializeField] private float holdDuration    = 1.8f;
    [SerializeField] private float fadeOutDuration = 0.4f;

    [Header("Bounce Scale")]
    [SerializeField] private float punchScale = 1.4f;

    private Coroutine _showCoroutine;
    private RectTransform _rect;

    // =====================================================================
    // LIFECYCLE
    // =====================================================================

    private void Awake()
    {
        Instance = this;
        _rect     = GetComponent<RectTransform>();

        // Ẩn ban đầu
        if (canvasGroup != null) canvasGroup.alpha = 0f;
        if (_rect != null)       _rect.localScale   = Vector3.one;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // =====================================================================
    // PUBLIC API
    // =====================================================================

    /// <summary>
    /// Gọi từ BoardHUDController khi nhận dice result.
    /// playerName: tên hiển thị; result: số trên xúc xắc.
    /// </summary>
    public void ShowRoll(string playerName, int result)
    {
        if (diceValueText  != null) diceValueText.text  = result.ToString();
        if (playerNameText != null) playerNameText.text = $"{playerName} rolled!";

        if (_showCoroutine != null) StopCoroutine(_showCoroutine);
        _showCoroutine = StartCoroutine(ShowSequence());
    }

    // =====================================================================
    // ANIMATION
    // =====================================================================

    private IEnumerator ShowSequence()
    {
        // Punch scale bắt đầu
        if (_rect != null) _rect.localScale = Vector3.one * punchScale;

        // Fade in + scale về 1
        float t = 0f;
        while (t < fadeInDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / fadeInDuration);
            if (canvasGroup != null) canvasGroup.alpha = p;
            if (_rect != null)       _rect.localScale  = Vector3.one * Mathf.Lerp(punchScale, 1f, p);
            yield return null;
        }
        if (canvasGroup != null) canvasGroup.alpha = 1f;
        if (_rect != null)       _rect.localScale  = Vector3.one;

        // Hold
        yield return new WaitForSeconds(holdDuration);

        // Fade out
        t = 0f;
        while (t < fadeOutDuration)
        {
            t += Time.deltaTime;
            if (canvasGroup != null) canvasGroup.alpha = 1f - Mathf.Clamp01(t / fadeOutDuration);
            yield return null;
        }
        if (canvasGroup != null) canvasGroup.alpha = 0f;

        _showCoroutine = null;
    }
}
