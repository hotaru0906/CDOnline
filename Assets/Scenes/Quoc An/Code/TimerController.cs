using UnityEngine;
using TMPro;
using System.Collections;

public class TimerController : MonoBehaviour
{
    [Header("=== UI References ===")]
    [SerializeField] private GameObject timerPanel;
    [SerializeField] private TextMeshProUGUI timerText;

    [Header("=== Timer Settings ===")]
    [SerializeField] private float totalTime = 90f;   // ← Chỉnh trong Inspector (giây)
    [SerializeField] private bool countDown  = true;  // true = đếm xuống, false = đếm lên

    [Header("=== Warning Settings ===")]
    [SerializeField] private float warningTime  = 10f;          // Bắt đầu cảnh báo khi còn X giây
    [SerializeField] private Color normalColor  = Color.white;
    [SerializeField] private Color warningColor = Color.red;
    [SerializeField] private float flashSpeed   = 2f;           // Tốc độ nhấp nháy

    // Event
    public System.Action OnTimeUp;

    // Private
    private float   _currentTime;
    private bool    _isRunning;
    private bool    _isWarning;

    // ==============================
    //         UNITY EVENTS
    // ==============================
    private void Start()
    {
        // Ẩn timer, chờ countdown xong
        timerPanel.SetActive(false);
        _currentTime = countDown ? totalTime : 0f;
        UpdateTimerUI(_currentTime);
    }

    private void Update()
    {
        if (!_isRunning) return;

        // Cập nhật thời gian
        if (countDown)
        {
            _currentTime -= Time.deltaTime;
            _currentTime  = Mathf.Max(_currentTime, 0f);
        }
        else
        {
            _currentTime += Time.deltaTime;
            _currentTime  = Mathf.Min(_currentTime, totalTime);
        }

        // Cập nhật UI
        UpdateTimerUI(_currentTime);

        // Kiểm tra cảnh báo
        HandleWarning();

        // Kiểm tra hết giờ
        if (IsTimeUp())
        {
            TimeUp();
        }
    }

    // ==============================
    //         PUBLIC API
    // ==============================

    // Bắt đầu đếm (gọi sau countdown)
    public void StartTimer()
    {
        timerPanel.SetActive(true);
        _isRunning    = true;
        _currentTime  = countDown ? totalTime : 0f;
    }

    // Dừng timer
    public void StopTimer()
    {
        _isRunning = false;
    }

    // Tiếp tục timer
    public void ResumeTimer()
    {
        _isRunning = true;
    }

    // Reset timer
    public void ResetTimer()
    {
        _isRunning   = false;
        _currentTime = countDown ? totalTime : 0f;
        UpdateTimerUI(_currentTime);
    }

    // ==============================
    //         TIMER LOGIC
    // ==============================
    private bool IsTimeUp()
    {
        if (countDown) return _currentTime <= 0f;
        else           return _currentTime >= totalTime;
    }

    private void TimeUp()
    {
        _isRunning = false;
        timerText.text  = countDown ? "00:00" : FormatTime(totalTime);
        timerText.color = warningColor;
        Debug.Log("⏰ Hết giờ!");

        // Gọi event hết giờ
        OnTimeUp?.Invoke();
    }

    // ==============================
    //        WARNING FLASH
    // ==============================
    private void HandleWarning()
    {
        bool shouldWarn = countDown
            ? _currentTime <= warningTime
            : _currentTime >= totalTime - warningTime;

        if (shouldWarn)
        {
            _isWarning = true;

            // Nhấp nháy đỏ trắng
            float lerp = Mathf.PingPong(Time.time * flashSpeed, 1f);
            timerText.color = Color.Lerp(normalColor, warningColor, lerp);
        }
        else
        {
            _isWarning      = false;
            timerText.color = normalColor;
        }
    }

    // ==============================
    //         UPDATE UI
    // ==============================
    private void UpdateTimerUI(float time)
    {
        timerText.text = FormatTime(time);
    }

    // Định dạng giây → mm:ss
    private string FormatTime(float time)
    {
        int minutes = Mathf.FloorToInt(time / 60f);
        int seconds = Mathf.FloorToInt(time % 60f);
        return string.Format("{0:00}:{1:00}", minutes, seconds);
    }

    // ==============================
    //      GETTERS (Cho script khác)
    // ==============================
    public float GetCurrentTime() => _currentTime;
    public bool  IsRunning()      => _isRunning;
    public bool  IsWarning()      => _isWarning;
}