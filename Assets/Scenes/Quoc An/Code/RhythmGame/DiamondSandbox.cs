using UnityEngine;
using RhythmGame;

/// <summary>
/// Test CỤC BỘ bản hình thoi mà KHÔNG cần lobby / Fusion / GameManager.
/// Gắn script này vào một empty trong scene MG_Rhythm, kéo Conductor + Playfield vào,
/// rồi ấn Play thẳng ở scene này.
///
/// Nó bỏ qua toàn bộ phần mạng: tự phát nhạc và tự gọi BeginPlay().
/// Vì không có MGRhythmController.Instance nên phần "hàng 4 người ở đáy" sẽ trống
/// (bind panel cần controller) — cái đó bình thường, ở đây ta chỉ test note + chấm
/// điểm + fever của người chơi cục bộ.
///
/// XOÁ script này (hoặc tắt object) trước khi chơi thật qua lobby.
/// </summary>
public class _DiamondSandbox : MonoBehaviour
{
    [SerializeField] private Conductor conductor;
    [SerializeField] private MGRhythmDiamondPlayfield playfield;

    [Tooltip("Đợi mấy giây rồi bắt đầu, cho nhạc kịp nạp.")]
    [SerializeField] private float startDelay = 1.5f;

    private bool _started;

    private void Start()
    {
        if (conductor == null || playfield == null)
        {
            Debug.LogError("[DiamondSandbox] Chưa kéo Conductor và Playfield vào Inspector.");
            enabled = false;
            return;
        }

        conductor.StartSong();
        Invoke(nameof(Begin), startDelay);
    }

    private void Begin()
    {
        playfield.BeginPlay();
        _started = true;
        Debug.Log("[DiamondSandbox] Bắt đầu test. Ấn A/← và D/→ theo note.");
    }
}