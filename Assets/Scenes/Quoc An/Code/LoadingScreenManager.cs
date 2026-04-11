using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class LoadingScreenManager : MonoBehaviour
{
    [Header("=== UI Loading References ===")]
    [SerializeField] private Slider loadingBar;
    [SerializeField] private TextMeshProUGUI percentText;
    [SerializeField] private TextMeshProUGUI loadingText;

    [Header("=== Thời gian Loading ===")]
    [SerializeField, Tooltip("Thời gian loading mặc định (giây) - có thể chỉnh trong Inspector")]
    private float loadingDuration = 1.2f;   // ← Bạn thay đổi số này thoải mái

    private Coroutine _currentLoading;

    /// <summary>
    /// Gọi từ bên ngoài nếu muốn loading với thời gian khác (ví dụ CreateRoom)
    /// </summary>
    public void StartFakeLoading(float customDuration = -1f)
    {
        float durationToUse = customDuration > 0 ? customDuration : loadingDuration;

        if (_currentLoading != null)
            StopCoroutine(_currentLoading);

        _currentLoading = StartCoroutine(FakeLoadingCoroutine(durationToUse));
    }

    private void OnEnable()
    {
        if (loadingBar == null || percentText == null || loadingText == null)
        {
            Debug.LogError("[LoadingScreenManager] Chưa gán Slider và TextMeshPro!");
            return;
        }

        // Reset UI
        loadingBar.value = 0f;
        percentText.text = "0%";
        loadingText.text = "Loading...";

        StartFakeLoading(); // Dùng thời gian mặc định
    }

    private void OnDisable()
    {
        if (_currentLoading != null)
        {
            StopCoroutine(_currentLoading);
            _currentLoading = null;
        }
    }

    private IEnumerator FakeLoadingCoroutine(float duration)
    {
        float progress = 0f;
        float elapsed = 0f;

        while (progress < 1f)
        {
            elapsed += Time.deltaTime;
            progress = Mathf.Clamp01(elapsed / duration);

            loadingBar.value = progress;
            percentText.text = $"{Mathf.RoundToInt(progress * 100)}%";

            if (progress >= 0.9f)
                loadingText.text = "You Want To Play Let's Play...";

            yield return null;
        }

        loadingBar.value = 1f;
        percentText.text = "100%";
    }
}