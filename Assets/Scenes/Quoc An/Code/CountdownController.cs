using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class CountdownController : MonoBehaviour
{
    [Header("=== UI References ===")]
    [SerializeField] private GameObject countdownPanel;
    [SerializeField] private TextMeshProUGUI countdownText;

    [Header("=== Countdown Settings ===")]
    [SerializeField] private float countdownFrom = 3f;   // Đếm từ số mấy
    [SerializeField] private float delayPerNumber = 1f;  // Thời gian mỗi số
    [SerializeField] private string goText = "GO!";      // Chữ cuối

    [Header("=== Animation Settings ===")]
    [SerializeField] private float scaleFrom  = 1.5f;   // Scale bắt đầu
    [SerializeField] private float scaleTo    = 0.8f;   // Scale kết thúc
    [SerializeField] private float goScaleMax = 2f;     // Scale chữ GO

    [Header("=== Colors ===")]
    [SerializeField] private Color numberColor = Color.white;
    [SerializeField] private Color goColor     = Color.yellow;

    // Event gọi khi countdown xong
    public System.Action OnCountdownComplete;

    // ==============================
    //         UNITY EVENTS
    // ==============================
    private void Start()
    {
        // Ẩn panel lúc đầu
        countdownPanel.SetActive(false);
    }

    // ==============================
    //         PUBLIC API
    // ==============================

    // Gọi hàm này để bắt đầu đếm ngược
    public void StartCountdown()
    {
        StartCoroutine(CountdownRoutine());
    }

    // ==============================
    //         COROUTINE
    // ==============================
    private IEnumerator CountdownRoutine()
    {
        // Hiện panel
        countdownPanel.SetActive(true);

        // Đếm từ countdownFrom về 1
        for (int i = (int)countdownFrom; i >= 1; i--)
        {
            // Cập nhật text và màu
            countdownText.text  = i.ToString();
            countdownText.color = numberColor;

            // Animation Scale phình to → thu nhỏ
            yield return StartCoroutine(AnimateScale(
                countdownText.transform,
                scaleFrom,
                scaleTo,
                delayPerNumber
            ));
        }

        // Hiện chữ GO!
        countdownText.text  = goText;
        countdownText.color = goColor;

        yield return StartCoroutine(AnimateScale(
            countdownText.transform,
            goScaleMax,     // GO to lớn hơn
            scaleTo,
            delayPerNumber
        ));

        // Ẩn panel sau khi xong
        countdownPanel.SetActive(false);

        // Gọi event báo countdown hoàn tất
        OnCountdownComplete?.Invoke();
    }

    // ==============================
    //       ANIMATION SCALE
    // ==============================
    private IEnumerator AnimateScale(
        Transform target,
        float from,
        float to,
        float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // Easing: ease out (mượt cuối)
            float eased = 1f - Mathf.Pow(1f - t, 3f);

            // Áp dụng scale
            float scale = Mathf.Lerp(from, to, eased);
            target.localScale = Vector3.one * scale;

            // Fade out dần
            if (countdownText != null)
            {
                Color c = countdownText.color;
                c.a = Mathf.Lerp(1f, 0.3f, t);
                countdownText.color = c;
            }

            yield return null;
        }

        target.localScale = Vector3.one * to;
    }
}