using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace RhythmGame
{
    /// <summary>
    /// Chế độ THU BEATMAP. Bật scene này, nghe nhạc và gõ WASD/mũi tên theo bài.
    /// Mỗi lần gõ sẽ ghi lại thời điểm (lấy từ dspTime, chính xác tuyệt đối),
    /// sau đó làm tròn về lưới nhịp nên tay bạn lệch vài chục ms vẫn ra note đúng beat.
    ///
    /// Cách dùng:
    ///  1. Điền bpm và firstBeatOffset của bài (xem hướng dẫn đo trong SETUP.md).
    ///  2. Play. Gõ theo nhạc.
    ///  3. Ấn F5 để lưu. Đường dẫn file sẽ hiện trong Console.
    ///  4. Nếu một đoạn gõ hỏng: ấn Backspace để xoá note vừa gõ gần nhất.
    /// </summary>
    public class ChartRecorder : MonoBehaviour
    {
        [Header("Tham chiếu")]
        public Conductor conductor;

        [Header("Thông tin bài hát")]
        public string songName = "Flares of the Blazing Sun";
        [Tooltip("BPM của bài. Đo bằng công cụ tap-BPM hoặc Audacity.")]
        public float bpm = 120f;
        [Tooltip("Thời điểm (giây) của beat ĐẦU TIÊN tính từ đầu file audio.")]
        public float firstBeatOffset = 0f;

        [Header("Làm tròn về lưới nhịp")]
        public bool quantize = true;
        [Tooltip("Chia mỗi beat thành mấy phần. 4 = nốt móc đơn, 8 = móc kép. Bài nhanh thì để 8.")]
        public int subdivision = 4;
        [Tooltip("Chỉ làm tròn nếu lệch dưới ngần này ms. Lệch hơn thì giữ nguyên thời điểm gõ.")]
        public float maxSnapMs = 120f;

        [Header("Tên file xuất")]
        public string outputFileName = "chart.json";

        private static readonly KeyCode[][] LaneKeys =
        {
            new[] { KeyCode.W, KeyCode.UpArrow },
            new[] { KeyCode.A, KeyCode.LeftArrow },
            new[] { KeyCode.S, KeyCode.DownArrow },
            new[] { KeyCode.D, KeyCode.RightArrow },
        };

        private readonly List<NoteEntry> recorded = new List<NoteEntry>();

        private void Awake()
        {
            if (conductor == null) conductor = Conductor.Instance;
        }

        private void Start()
        {
            conductor.StartSong();
            Debug.Log("[Recorder] Đang thu. Gõ WASD hoặc mũi tên theo nhạc. F5 = lưu, Backspace = xoá note gần nhất.");
        }

        private void Update()
        {
            if (!conductor.IsPlaying) return;

            double pos = conductor.RawSongPosition;
            if (pos < 0) return; // vẫn đang trong lead-in, nhạc chưa kêu

            for (int lane = 0; lane < 4; lane++)
            {
                var keys = LaneKeys[lane];
                for (int k = 0; k < keys.Length; k++)
                {
                    if (!Input.GetKeyDown(keys[k])) continue;
                    recorded.Add(new NoteEntry { lane = lane, time = Snap((float)pos) });
                    break;
                }
            }

            if (Input.GetKeyDown(KeyCode.Backspace) && recorded.Count > 0)
            {
                recorded.RemoveAt(recorded.Count - 1);
                Debug.Log($"[Recorder] Đã xoá. Còn {recorded.Count} note.");
            }

            if (Input.GetKeyDown(KeyCode.F5)) Save();
        }

        /// <summary>Làm tròn thời điểm về ô lưới nhịp gần nhất.</summary>
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
            var chart = new ChartData
            {
                songName = songName,
                bpm = bpm,
                firstBeatOffset = firstBeatOffset,
                notes = new List<NoteEntry>(recorded)
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
    }
}
