using System.Collections.Generic;
using UnityEngine;

namespace RhythmGame
{
    /// <summary>
    /// Script TEST cho sandbox — KHÔNG dùng trong game thật.
    /// Mục đích duy nhất: chứng minh Conductor + NoteView spawn và di chuyển note
    /// đúng nhịp, TRƯỚC KHI đụng tới Fusion hay MGRhythmController.
    ///
    /// Nó tự làm mọi thứ MGRhythmPlayfield làm, nhưng tối giản và một lane:
    ///   - đọc beatmap từ TextAsset
    ///   - bắt đầu nhạc
    ///   - spawn note khi tới lúc, di chuyển mỗi frame, thu hồi khi note đi hết
    ///   - (tuỳ chọn) chấm điểm khi ấn phím, để test luôn cửa sổ ms
    ///
    /// Xoá file này sau khi qua PHA 4.
    /// </summary>
    public class _SandboxNoteTest : MonoBehaviour
    {
        [Header("Kéo vào từ scene")]
        [SerializeField] private Conductor conductor;
        [SerializeField] private TextAsset chartJson;
        [SerializeField] private NoteView notePrefab;
        [Tooltip("NoteContainer của Lane0 — nơi note được sinh ra làm con.")]
        [SerializeField] private RectTransform laneContainer;

        [Header("Thông số vị trí (khớp với Playfield thật)")]
        [SerializeField] private float noteSpeed = 700f;
        [SerializeField] private float hitLineX = 0f;
        [SerializeField] private float spawnX = 1400f;

        [Header("Chấm điểm — bật để test luôn cửa sổ ms")]
        [SerializeField] private bool enableJudging = true;
        [SerializeField] private float perfectMs = 45f;
        [SerializeField] private float goodMs = 95f;
        [SerializeField] private float missMs = 145f;

        private ChartData _chart;
        private int _nextSpawnIndex;
        private readonly List<NoteView> _active = new List<NoteView>(32);
        private readonly Stack<NoteView> _pool = new Stack<NoteView>();
        private float _travelTime;

        // Phím: bất kỳ phím nào trong nhóm này đều tính là một cú đánh.
        private static readonly KeyCode[] HitKeys =
        {
            KeyCode.W, KeyCode.A, KeyCode.S, KeyCode.D,
            KeyCode.UpArrow, KeyCode.LeftArrow, KeyCode.DownArrow, KeyCode.RightArrow
        };

        private void Start()
        {
            if (conductor == null || chartJson == null || notePrefab == null || laneContainer == null)
            {
                Debug.LogError("[SandboxTest] Chưa kéo đủ 4 tham chiếu vào Inspector.");
                enabled = false;
                return;
            }

            _chart = JsonUtility.FromJson<ChartData>(chartJson.text);
            if (_chart == null || _chart.notes == null || _chart.notes.Count == 0)
            {
                Debug.LogError("[SandboxTest] Beatmap rỗng hoặc sai định dạng JSON.");
                enabled = false;
                return;
            }

            _chart.notes.Sort((a, b) => a.time.CompareTo(b.time));
            _travelTime = (spawnX - hitLineX) / Mathf.Max(1f, noteSpeed);

            conductor.StartSong();
            Debug.Log($"[SandboxTest] Bắt đầu — {_chart.notes.Count} note, travelTime {_travelTime:F2}s");
        }

        private void Update()
        {
            if (conductor == null || !conductor.IsPlaying) return;

            double visualPos = conductor.VisualSongPosition;
            double rawPos = conductor.RawSongPosition;

            // 1) Spawn note khi thời điểm của nó chỉ còn cách hiện tại đúng travelTime.
            while (_nextSpawnIndex < _chart.notes.Count &&
                   _chart.notes[_nextSpawnIndex].time - visualPos <= _travelTime)
            {
                float t = _chart.notes[_nextSpawnIndex].time;
                _nextSpawnIndex++;

                NoteView nv = _pool.Count > 0 ? _pool.Pop() : Instantiate(notePrefab);
                nv.Setup(0, t, laneContainer);
                _active.Add(nv);
            }

            // 2) Di chuyển mọi note đang sống. Vị trí tính lại từ thời gian, không cộng dồn.
            for (int i = 0; i < _active.Count; i++)
                _active[i].Redraw(visualPos, noteSpeed, hitLineX);

            // 3) Note đi quá vạch đích quá xa mà chưa ai ấn -> Miss, thu hồi.
            double missWindow = missMs / 1000.0;
            while (_active.Count > 0 && rawPos - _active[0].TargetTime > missWindow)
            {
                Recycle(0);
                if (enableJudging) Debug.Log("<color=red>MISS</color>");
            }

            // 4) Nhận phím (chỉ khi bật chấm điểm).
            if (enableJudging && AnyHitKeyDown() && _active.Count > 0)
            {
                double diffMs = (rawPos - _active[0].TargetTime) * 1000.0;
                double abs = System.Math.Abs(diffMs);

                if (abs <= missMs)
                {
                    string result = abs <= perfectMs ? "<color=lime>PERFECT</color>"
                                  : abs <= goodMs ? "<color=yellow>GOOD</color>"
                                  : "<color=red>MISS</color>";
                    // In cả độ lệch để bạn biết đang sớm hay trễ -> chỉnh userOffset.
                    string dir = diffMs < 0 ? "sớm" : "trễ";
                    Debug.Log($"{result}  (lệch {abs:F0}ms, ấn {dir})");
                    Recycle(0);
                }
                // abs > missMs: ấn quá sớm, bỏ qua, không phạt.
            }
        }

        private static bool AnyHitKeyDown()
        {
            for (int i = 0; i < HitKeys.Length; i++)
                if (Input.GetKeyDown(HitKeys[i])) return true;
            return false;
        }

        private void Recycle(int index)
        {
            NoteView nv = _active[index];
            _active.RemoveAt(index);
            nv.Recycle();
            _pool.Push(nv);
        }
    }
}