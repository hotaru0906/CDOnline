using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace RhythmGame
{
    /// <summary>
    /// Chế độ THU BEATMAP cho game HÌNH THOI 2 hướng (trái/phải), hỗ trợ:
    ///   - Tap: ấn nhanh một bên.
    ///   - Hold: giữ một bên (duration = thời gian giữ).
    ///   - Note đôi: giữ CẢ hai bên cùng lúc (type = 1).
    ///
    /// Phím:
    ///   A hoặc ← = bên TRÁI (lane 0)
    ///   D hoặc → = bên PHẢI (lane 1)
    ///
    /// Cách dùng:
    ///  1. Điền bpm và firstBeatOffset. Play.
    ///  2. Nghe nhạc, gõ theo. Ấn nhanh = tap, giữ = hold.
    ///     Giữ cả hai bên trùng thời gian = note đôi (tự gộp khi lưu).
    ///  3. Backspace = xoá note vừa hoàn tất gần nhất.
    ///  4. F5 = lưu ra file JSON.
    /// </summary>
    public class ChartRecorder : MonoBehaviour
    {
        [Header("Tham chiếu")]
        public Conductor conductor;

        [Header("Thông tin bài hát")]
        public string songName = "Flares of the Blazing Sun";
        [Tooltip("BPM của bài. Đo bằng Audacity.")]
        public float bpm = 148f;
        [Tooltip("Thời điểm (giây) của beat ĐẦU TIÊN tính từ đầu file audio.")]
        public float firstBeatOffset = 0f;

        [Header("Làm tròn về lưới nhịp")]
        public bool quantize = true;
        [Tooltip("Chia mỗi beat thành mấy phần. 4 = móc đơn, 8 = móc kép.")]
        public int subdivision = 4;
        [Tooltip("Chỉ snap nếu lệch dưới ngần này ms. Lệch hơn thì giữ nguyên.")]
        public float maxSnapMs = 120f;

        [Header("Ngưỡng phân biệt tap / hold")]
        [Tooltip("Giữ ngắn hơn ngần này (giây) thì tính TAP; dài hơn thì tính HOLD.")]
        public float tapMaxHold = 0.13f;

        [Header("Gộp note đôi")]
        [Tooltip("Hai bên bắt đầu cách nhau dưới ngần này (giây) thì gộp thành note đôi.")]
        public float dualMergeWindow = 0.08f;

        [Header("Tên file xuất")]
        public string outputFileName = "flares_hand.json";

        // Phím mỗi bên
        private static readonly KeyCode[] LeftKeys = { KeyCode.A, KeyCode.LeftArrow };
        private static readonly KeyCode[] RightKeys = { KeyCode.D, KeyCode.RightArrow };

        // Note đã hoàn tất (đã thả). Lưu thời điểm bắt đầu + độ dài giữ + lane.
        private class Recorded
        {
            public int lane;        // 0 trái, 1 phải
            public float start;     // giây (đã snap)
            public float duration;  // giây giữ (0 = tap)
        }
        private readonly List<Recorded> _recorded = new List<Recorded>();

        // Đang giữ: lưu thời điểm nhấn (chưa snap) để tính duration khi thả.
        private bool _leftDown, _rightDown;
        private float _leftStart, _rightStart;

        private void Awake()
        {
            if (conductor == null) conductor = Conductor.Instance;
        }

        private void Start()
        {
            conductor.StartSong();
            Debug.Log("[Recorder] Đang thu. A/← = trái, D/→ = phải. Ấn nhanh=tap, giữ=hold, " +
                      "giữ cả hai=note đôi. Backspace=xoá, F5=lưu.");
        }

        private void Update()
        {
            if (!conductor.IsPlaying) return;
            float pos = (float)conductor.RawSongPosition;
            if (pos < 0) return; // còn trong lead-in

            // --- TRÁI ---
            if (!_leftDown && AnyDown(LeftKeys)) { _leftDown = true; _leftStart = pos; }
            if (_leftDown && AnyUp(LeftKeys)) { _leftDown = false; FinishNote(0, _leftStart, pos); }

            // --- PHẢI ---
            if (!_rightDown && AnyDown(RightKeys)) { _rightDown = true; _rightStart = pos; }
            if (_rightDown && AnyUp(RightKeys)) { _rightDown = false; FinishNote(1, _rightStart, pos); }

            if (Input.GetKeyDown(KeyCode.Backspace) && _recorded.Count > 0)
            {
                _recorded.RemoveAt(_recorded.Count - 1);
                Debug.Log($"[Recorder] Đã xoá. Còn {_recorded.Count} note.");
            }

            if (Input.GetKeyDown(KeyCode.F5)) Save();
        }

        private void FinishNote(int lane, float startRaw, float endRaw)
        {
            float start = Snap(startRaw);
            float held = Mathf.Max(0f, endRaw - startRaw);
            float duration = held <= tapMaxHold ? 0f : Snap(endRaw) - start;
            if (duration < 0f) duration = 0f;

            _recorded.Add(new Recorded { lane = lane, start = start, duration = duration });
            string kind = duration > 0f ? $"hold {duration:F2}s" : "tap";
            Debug.Log($"[Recorder] {(lane == 0 ? "TRÁI" : "PHẢI")} {kind} @ {start:F2}s " +
                      $"(tổng {_recorded.Count})");
        }

        private float Snap(float t)
        {
            if (!quantize || bpm <= 0f || subdivision <= 0) return t;
            float step = 60f / bpm / subdivision;
            float rel = t - firstBeatOffset;
            float snapped = Mathf.Round(rel / step) * step + firstBeatOffset;
            if (Mathf.Abs(snapped - t) * 1000f > maxSnapMs) return t;
            return snapped;
        }

        private void Save()
        {
            // Gộp note đôi: hai note khác lane, bắt đầu gần nhau -> type 1.
            var entries = BuildEntries();

            var chart = new ChartData
            {
                songName = songName,
                bpm = bpm,
                firstBeatOffset = firstBeatOffset,
                notes = entries,
            };
            chart.notes.Sort((a, b) => a.time.CompareTo(b.time));

            string json = JsonUtility.ToJson(chart, true);

#if UNITY_EDITOR
            string dir = Path.Combine(Application.dataPath, "Charts");
#else
            string dir = Application.persistentDataPath;
#endif
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, outputFileName);
            File.WriteAllText(path, json);

            Debug.Log($"[Recorder] Đã lưu {chart.notes.Count} note vào:\n{path}");
#if UNITY_EDITOR
            UnityEditor.AssetDatabase.Refresh();
#endif
        }

        /// <summary>
        /// Chuyển danh sách đã thu thành NoteEntry, gộp cặp trái+phải gần nhau thành note đôi.
        /// </summary>
        private List<NoteEntry> BuildEntries()
        {
            var list = new List<Recorded>(_recorded);
            list.Sort((a, b) => a.start.CompareTo(b.start));

            var used = new bool[list.Count];
            var result = new List<NoteEntry>();

            for (int i = 0; i < list.Count; i++)
            {
                if (used[i]) continue;
                var a = list[i];

                // Tìm một note khác lane, bắt đầu gần nhau -> gộp note đôi.
                int pair = -1;
                for (int j = i + 1; j < list.Count; j++)
                {
                    if (used[j]) continue;
                    var b = list[j];
                    if (b.start - a.start > dualMergeWindow) break; // đã ra khỏi cửa sổ
                    if (b.lane != a.lane)
                    {
                        pair = j;
                        break;
                    }
                }

                if (pair >= 0)
                {
                    var b = list[pair];
                    used[i] = used[pair] = true;
                    // Note đôi: lấy duration dài hơn của hai bên.
                    float dur = Mathf.Max(a.duration, b.duration);
                    result.Add(new NoteEntry { time = a.start, lane = 0, duration = dur, type = 1 });
                }
                else
                {
                    used[i] = true;
                    result.Add(new NoteEntry { time = a.start, lane = a.lane, duration = a.duration, type = 0 });
                }
            }

            return result;
        }

        private static bool AnyDown(KeyCode[] keys)
        {
            for (int i = 0; i < keys.Length; i++)
                if (Input.GetKeyDown(keys[i])) return true;
            return false;
        }

        private static bool AnyUp(KeyCode[] keys)
        {
            // Coi là "thả" khi KHÔNG còn phím nào của bên đó được giữ.
            for (int i = 0; i < keys.Length; i++)
                if (Input.GetKey(keys[i])) return false;
            return true;
        }
    }
}