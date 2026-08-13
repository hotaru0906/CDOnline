using UnityEngine;
using UnityEngine.UI;

namespace RhythmGame
{
    /// <summary>
    /// Làm nền TĨNH "sống" theo nhạc, không cần animation hay sprite sheet.
    ///   - Phình nhẹ mỗi NHỊP (tính từ Conductor + BPM).
    ///   - Sáng lên theo nhịp.
    ///   - Giật + phình mạnh khi gọi FeverPunch() (lúc fever nổ).
    ///
    /// Gắn vào object nền (Image hoặc RawImage phủ màn hình). Thuần cục bộ,
    /// không liên quan mạng.
    ///
    /// Lưu ý tempo: bài của bạn ĐỔI TEMPO, nên nhịp có thể trôi nhẹ ở các đoạn
    /// đổi tempo. Với nền thì trôi vài chục ms không sao. Điền BPM của đoạn chính
    /// (148) là hợp nhất; muốn chuẩn tuyệt đối theo tempo map thì báo mình thêm.
    /// </summary>
    public class BackgroundPulse : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private Conductor conductor;
        [Tooltip("Transform của ảnh nền để phình/giật. Để trống thì dùng chính object này.")]
        [SerializeField] private RectTransform target;
        [Tooltip("Image nền để đổi độ sáng. Có thể để trống nếu không muốn hiệu ứng sáng.")]
        [SerializeField] private Graphic tintGraphic;

        [Header("Nhịp")]
        [SerializeField] private float bpm = 148f;
        [SerializeField] private float firstBeatOffset = 0f;
        [Tooltip("Chia mỗi nhịp thành mấy lần đập. 1 = mỗi phách. 2 = đập gấp đôi.")]
        [SerializeField] private int subdivision = 1;

        [Header("Phình theo nhịp")]
        [SerializeField] private bool pulseEnabled = true;
        [Tooltip("Phình thêm bao nhiêu mỗi nhịp. 0.03 = to thêm 3%.")]
        [SerializeField] private float pulseAmount = 0.03f;
        [Tooltip("Phình xẹp nhanh cỡ nào. Lớn hơn = xẹp nhanh hơn.")]
        [SerializeField] private float pulseDecay = 6f;

        [Header("Sáng theo nhịp")]
        [SerializeField] private bool brightnessEnabled = true;
        [SerializeField] private Color baseColor = Color.white;
        [Tooltip("Màu lúc sáng nhất mỗi nhịp.")]
        [SerializeField] private Color beatColor = new Color(1.15f, 1.15f, 1.15f, 1f);

        [Header("Giật khi fever")]
        [Tooltip("Phình thêm bao nhiêu khi fever nổ.")]
        [SerializeField] private float feverScale = 0.12f;
        [Tooltip("Biên độ rung (pixel).")]
        [SerializeField] private float feverShake = 22f;
        [Tooltip("Rung trong bao lâu (giây).")]
        [SerializeField] private float feverDuration = 0.4f;

        private Vector3 _baseScale;
        private Vector2 _basePos;
        private float _pulse;      // 0..1, giá trị đập của nhịp hiện tại
        private float _feverT;     // đếm ngược thời gian giật fever
        private int _lastBeat = int.MinValue;

        private void Awake()
        {
            if (target == null) target = (RectTransform)transform;
            if (conductor == null) conductor = Conductor.Instance;
            _baseScale = target.localScale;
            _basePos = target.anchoredPosition;
            if (tintGraphic != null) tintGraphic.color = baseColor;
        }

        private void Update()
        {
            // Phát hiện sang nhịp mới.
            if (pulseEnabled && conductor != null && conductor.IsPlaying)
            {
                double pos = conductor.RawSongPosition - firstBeatOffset;
                if (pos >= 0 && bpm > 0f)
                {
                    double beatsPerSecond = bpm / 60.0 * subdivision;
                    int beat = (int)(pos * beatsPerSecond);
                    if (beat != _lastBeat)
                    {
                        _lastBeat = beat;
                        _pulse = 1f; // sang nhịp -> đập
                    }
                }
            }

            // Xẹp dần.
            if (_pulse > 0f)
                _pulse = Mathf.MoveTowards(_pulse, 0f, pulseDecay * Time.deltaTime);

            // Giật fever đếm ngược.
            if (_feverT > 0f)
                _feverT -= Time.deltaTime;

            ApplyTransform();
            ApplyColor();
        }

        private void ApplyTransform()
        {
            float feverN = feverDuration > 0f ? Mathf.Clamp01(_feverT / feverDuration) : 0f;

            // Scale = nền + đập nhịp + phình fever.
            float scale = 1f + pulseAmount * _pulse + feverScale * feverN;
            target.localScale = _baseScale * scale;

            // Rung fever: lệch vị trí ngẫu nhiên, giảm dần.
            Vector2 offset = Vector2.zero;
            if (feverN > 0f)
            {
                float mag = feverShake * feverN;
                offset = new Vector2(Random.Range(-mag, mag), Random.Range(-mag, mag));
            }
            target.anchoredPosition = _basePos + offset;
        }

        private void ApplyColor()
        {
            if (!brightnessEnabled || tintGraphic == null) return;
            // Sáng theo đập nhịp, cộng thêm khi fever.
            float feverN = feverDuration > 0f ? Mathf.Clamp01(_feverT / feverDuration) : 0f;
            float t = Mathf.Clamp01(_pulse + feverN);
            tintGraphic.color = Color.Lerp(baseColor, beatColor, t);
        }

        /// <summary>Gọi khi fever nổ để nền giật mạnh + phình.</summary>
        public void FeverPunch()
        {
            _feverT = feverDuration;
        }
    }
}