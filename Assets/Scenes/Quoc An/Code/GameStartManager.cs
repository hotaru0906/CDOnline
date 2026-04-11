using UnityEngine;

public class GameStartManager : MonoBehaviour
{
    [Header("=== Controllers ===")]
    [SerializeField] private CountdownController countdown;
    [SerializeField] private TimerController timer;

    private void Start()
    {
        // Lắng nghe sự kiện countdown xong → bật timer
        countdown.OnCountdownComplete += OnCountdownFinished;

        // Bắt đầu countdown
        countdown.StartCountdown();
    }

    private void OnCountdownFinished()
    {
        Debug.Log("🎮 Game bắt đầu!");

        // Bật timer sau khi countdown xong
        timer.StartTimer();

        // TODO: Bật các hệ thống game khác ở đây
        // playerController.enabled = true;
        // enemySpawner.StartSpawning();
    }

    private void OnDestroy()
    {
        // Gỡ event tránh memory leak
        if (countdown != null)
            countdown.OnCountdownComplete -= OnCountdownFinished;
    }
}