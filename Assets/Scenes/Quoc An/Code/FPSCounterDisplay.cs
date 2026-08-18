using UnityEngine;
using TMPro;

/// <summary>
/// Hiển thị FPS Counter độc lập, KHÔNG nằm trong Settings Panel,
/// nên luôn hiển thị xuyên suốt lúc chơi game kể cả khi Settings đang đóng.
/// Đặt object này làm con của SettingsManager (persist qua scene nhờ DontDestroyOnLoad ở root).
/// </summary>
public class FPSCounterDisplay : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI fpsCounterText;

    [Tooltip("Tần suất cập nhật số FPS hiển thị (giây).")]
    [SerializeField] private float updateInterval = 0.5f;

    public static FPSCounterDisplay Instance { get; private set; }

    private float _timer;
    private bool _visible;

    private void Awake()
    {
        // Không cần singleton phức tạp riêng vì object này đã nằm dưới
        // root DontDestroyOnLoad của SettingsManager, nhưng vẫn set Instance
        // để GraphicsSettings gọi tới dễ dàng.
        Instance = this;
    }

    private void Start()
    {
        // Tự đọc trạng thái đã lưu, không phụ thuộc GraphicsSettings load trước hay sau.
        bool shown = PlayerPrefs.GetInt("ShowFPS", 0) == 1;
        SetVisible(shown);
    }

    private void Update()
    {
        if (!_visible) return;

        _timer += Time.unscaledDeltaTime;
        if (_timer >= updateInterval)
        {
            int fps = Mathf.RoundToInt(1f / Time.unscaledDeltaTime);
            if (fpsCounterText != null)
                fpsCounterText.text = "FPS: " + fps;
            _timer = 0f;
        }
    }

    public void SetVisible(bool visible)
    {
        _visible = visible;
        _timer = 0f;

        if (fpsCounterText != null)
            fpsCounterText.gameObject.SetActive(visible);
    }
}