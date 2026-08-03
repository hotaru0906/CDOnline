using UnityEngine;

// LƯU Ý: project này có class AudioSettings : MonoBehaviour ở global namespace,
// trùng tên với UnityEngine.AudioSettings. C# ưu tiên type ở global namespace hơn
// type nhập bằng "using UnityEngine;", nên bắt buộc phải ghi đầy đủ
// UnityEngine.AudioSettings.dspTime ở mọi chỗ trong file này.

namespace RhythmGame
{
    /// <summary>
    /// Đồng hồ của bài hát. Đây là script quan trọng nhất trong toàn bộ game.
    ///
    /// Nguyên tắc: KHÔNG BAO GIỜ dùng Time.time hay audioSource.time để tính vị trí bài hát.
    /// - Time.time phụ thuộc framerate, sẽ trôi lệch dần và sau 2 phút có thể lệch cả trăm ms.
    /// - audioSource.time chỉ cập nhật mỗi khi audio buffer đổi, giá trị bị giật cấp.
    /// UnityEngine.AudioSettings.dspTime chạy trên đồng hồ của card âm thanh, cùng nguồn với việc phát nhạc,
    /// nên nó không bao giờ lệch khỏi bài hát.
    /// </summary>
    public class Conductor : MonoBehaviour
    {
        public static Conductor Instance { get; private set; }

        [SerializeField] private AudioSource source;

        [Tooltip("Đợi bao nhiêu giây trước khi nhạc thật sự phát. Cần > 0 để PlayScheduled kịp đưa dữ liệu vào buffer.")]
        [SerializeField] private double leadInSeconds = 2.0;

        [Tooltip("Bù trễ thiết bị, tính bằng GIÂY. Dương = note tới sớm hơn. " +
                 "Nếu bạn thấy phải ấn TRỄ hơn nhạc mới ăn Perfect thì tăng giá trị này lên.")]
        public float userOffset = 0f;

        private double dspSongStart;
        private double smoothedDsp;
        private double lastDsp;

        public bool IsPlaying { get; private set; }
        public AudioSource Source => source;

        /// <summary>
        /// Vị trí bài hát CHÍNH XÁC (giây). Dùng cho việc CHẤM ĐIỂM.
        /// Giá trị này chỉ nhảy mỗi khi audio buffer đổi (~mỗi 10-20ms) nhưng luôn đúng tuyệt đối.
        /// </summary>
        public double RawSongPosition => UnityEngine.AudioSettings.dspTime - dspSongStart + userOffset;

        /// <summary>
        /// Vị trí bài hát ĐÃ LÀM MƯỢT (giây). Chỉ dùng để DI CHUYỂN NOTE trên màn hình.
        /// Nếu dùng RawSongPosition để vẽ, note sẽ bị giật vì dspTime không đổi mỗi frame.
        /// </summary>
        public double VisualSongPosition => smoothedDsp - dspSongStart + userOffset;

        public float ClipLength => source != null && source.clip != null ? source.clip.length : 0f;

        private void Awake()
        {
            Instance = this;
            if (source == null) source = GetComponent<AudioSource>();
            if (source != null) source.playOnAwake = false;
        }

        /// <summary>
        /// Bản offline: tự chọn thời điểm bắt đầu.
        /// </summary>
        public void StartSong()
        {
            StartSongAtDsp(UnityEngine.AudioSettings.dspTime + leadInSeconds);
        }

        /// <summary>
        /// Bản multiplayer: thời điểm bắt đầu do bên ngoài quyết định (quy đổi từ tick của Fusion).
        /// dspStart PHẢI là một mốc trong tương lai, cách hiện tại ít nhất ~0.1 giây,
        /// nếu không PlayScheduled sẽ không kịp nạp buffer và nhạc sẽ vào trễ.
        /// </summary>
        public void StartSongAtDsp(double dspStart)
        {
            double now = UnityEngine.AudioSettings.dspTime;
            if (dspStart < now + 0.05)
            {
                Debug.LogWarning($"[Conductor] dspStart quá sát hiện tại ({dspStart - now:F3}s), đẩy lùi 0.1s.");
                dspStart = now + 0.1;
            }

            dspSongStart = dspStart;
            lastDsp = now;
            smoothedDsp = now;
            source.PlayScheduled(dspSongStart);
            IsPlaying = true;
        }

        public void StopSong()
        {
            if (source != null) source.Stop();
            IsPlaying = false;
        }

        private void Update()
        {
            if (!IsPlaying) return;

            // dspTime chỉ đổi giá trị mỗi lần audio thread xử lý xong một buffer.
            // Giữa hai lần đó ta cộng dồn deltaTime để note trôi mượt,
            // và reset về giá trị thật ngay khi buffer mới tới -> không bao giờ trôi lệch.
            double dsp = UnityEngine.AudioSettings.dspTime;
            if (dsp != lastDsp)
            {
                smoothedDsp = dsp;
                lastDsp = dsp;
            }
            else
            {
                smoothedDsp += Time.unscaledDeltaTime;
            }
        }
    }
}