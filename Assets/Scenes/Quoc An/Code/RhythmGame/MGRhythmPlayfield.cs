using System.Collections.Generic;
using UnityEngine;
using RhythmGame;

/// <summary>
/// Sân chơi rhythm. Chạy hoàn toàn CỤC BỘ trên mỗi máy.
///
/// Cùng một beatmap được spawn vào CẢ 4 LANE — mọi người chơi đúng cùng một
/// chuỗi note. Đây là lựa chọn cố ý: nếu mỗi lane có pattern khác nhau thì
/// người bốc phải đoạn khó sẽ thiệt, không còn là cuộc đua công bằng nữa.
/// Trường "lane" trong file JSON vì vậy bị bỏ qua ở chế độ này.
///
/// Chỉ lane của người chơi cục bộ mới nhận phím và được chấm điểm.
/// Lane của người khác chỉ chạy note cho đẹp; số liệu của họ lấy từ
/// MGRhythmPlayerState đã replicate về.
/// </summary>
public class MGRhythmPlayfield : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Conductor conductor;
    [SerializeField] private TextAsset chartJson;
    [SerializeField] private NoteView notePrefab;

    [Tooltip("4 lane UI, thứ tự TỪ TRÊN XUỐNG. Lane i tương ứng LanePlayerIds[i].")]
    [SerializeField] private MGRhythmLaneUI[] lanes = new MGRhythmLaneUI[4];

    [Header("Gameplay")]
    [SerializeField] private float noteSpeed = 700f;
    [SerializeField] private float hitLineX = 0f;
    [SerializeField] private float spawnX = 1400f;

    [Header("Cửa sổ chấm (ms)")]
    [SerializeField] private float perfectMs = 45f;
    [SerializeField] private float goodMs = 95f;
    [SerializeField] private float missMs = 145f;

    [Header("Điểm")]
    [SerializeField] private int perfectScore = 300;
    [SerializeField] private int goodScore = 100;

    [Header("Fever")]
    [SerializeField] private float maxFever = 1000f;
    [SerializeField] private float perfectFever = 10f;
    [SerializeField] private float goodFever = 5f;
    [SerializeField] private float missFever = 0f;

    [Header("Network")]
    [Tooltip("Báo cáo tiến độ lên host mấy lần mỗi giây. 10 là quá đủ cho UI.")]
    [SerializeField] private float reportsPerSecond = 10f;

    /// <summary>
    /// Người chơi cục bộ chỉ có MỘT lane, nên bất kỳ phím nào trong danh sách này
    /// cũng tính là một cú đánh vào lane của họ. Nhờ vậy dùng WASD hay mũi tên
    /// hay cả hai tay đều được.
    /// </summary>
    private static readonly KeyCode[] HitKeys =
    {
        KeyCode.W, KeyCode.A, KeyCode.S, KeyCode.D,
        KeyCode.UpArrow, KeyCode.LeftArrow, KeyCode.DownArrow, KeyCode.RightArrow
    };

    private ChartData _chart;
    private int _nextSpawnIndex;
    private readonly List<NoteView>[] _active = new List<NoteView>[4];
    private readonly Stack<NoteView> _pool = new Stack<NoteView>();

    private float _travelTime;
    private int _localLane = -1;
    private bool _running;

    // state cục bộ của người chơi này
    private int _score, _combo, _maxCombo;
    private int _perfect, _good, _miss;
    private float _fever;
    private float _reportTimer;

    private MGRhythmPlayerState _localState;

    private void Awake()
    {
        for (int i = 0; i < 4; i++) _active[i] = new List<NoteView>(32);
    }

    // ----------------------------------------------------------------
    //  Bắt đầu / kết thúc
    // ----------------------------------------------------------------

    public void BeginPlay(int localLane)
    {
        _localLane = localLane;
        _travelTime = (spawnX - hitLineX) / Mathf.Max(1f, noteSpeed);

        LoadChart();
        BindLanes();

        _score = _combo = _maxCombo = 0;
        _perfect = _good = _miss = 0;
        _fever = 0f;
        _nextSpawnIndex = 0;
        _running = true;

        if (_localLane < 0)
            Debug.LogWarning("[MGRhythm] Không tìm thấy lane cho player cục bộ — vào chế độ xem.");
    }

    public void StopPlay()
    {
        _running = false;
        for (int lane = 0; lane < 4; lane++)
        {
            for (int i = _active[lane].Count - 1; i >= 0; i--)
                Recycle(lane, i);
        }
    }

    private void LoadChart()
    {
        _chart = chartJson != null ? JsonUtility.FromJson<ChartData>(chartJson.text) : null;
        if (_chart == null || _chart.notes == null)
        {
            Debug.LogError("[MGRhythm] Không đọc được beatmap.");
            _chart = new ChartData();
            return;
        }
        _chart.notes.Sort((a, b) => a.time.CompareTo(b.time));
    }

    /// <summary>Gán từng lane UI với người chơi tương ứng và bật/tắt lane trống.</summary>
    private void BindLanes()
    {
        var ctrl = MGRhythmController.Instance;
        if (ctrl == null) return;

        for (int i = 0; i < lanes.Length; i++)
        {
            if (lanes[i] == null) continue;

            var state = ctrl.GetStateForLane(i);
            lanes[i].Bind(state, isLocal: i == _localLane);

            if (i == _localLane) _localState = state;
        }
    }

    // ----------------------------------------------------------------
    //  Vòng lặp
    // ----------------------------------------------------------------

    private void Update()
    {
        if (!_running || conductor == null || !conductor.IsPlaying) return;

        double visualPos = conductor.VisualSongPosition;
        double rawPos = conductor.RawSongPosition;

        SpawnDueNotes(visualPos);
        MoveNotes(visualPos);
        CleanupAndJudgeMisses(rawPos);
        ReadLocalInput(rawPos);
        ReportProgress();
    }

    private void SpawnDueNotes(double visualPos)
    {
        while (_nextSpawnIndex < _chart.notes.Count &&
               _chart.notes[_nextSpawnIndex].time - visualPos <= _travelTime)
        {
            float t = _chart.notes[_nextSpawnIndex].time;
            _nextSpawnIndex++;

            // Cùng một note vào cả 4 lane đang có người.
            for (int lane = 0; lane < 4; lane++)
            {
                if (lanes[lane] == null || !lanes[lane].IsOccupied) continue;

                NoteView nv = _pool.Count > 0 ? _pool.Pop() : Instantiate(notePrefab);
                nv.Setup(lane, t, lanes[lane].NoteContainer);
                _active[lane].Add(nv);
            }
        }
    }

    private void MoveNotes(double visualPos)
    {
        for (int lane = 0; lane < 4; lane++)
        {
            var list = _active[lane];
            for (int i = 0; i < list.Count; i++)
                list[i].Redraw(visualPos, noteSpeed, hitLineX);
        }
    }

    private void CleanupAndJudgeMisses(double rawPos)
    {
        double missWindow = missMs / 1000.0;

        for (int lane = 0; lane < 4; lane++)
        {
            var list = _active[lane];
            while (list.Count > 0 && rawPos - list[0].TargetTime > missWindow)
            {
                Recycle(lane, 0);

                // Chỉ lane cục bộ mới bị tính Miss. Lane người khác chỉ dọn note.
                if (lane == _localLane) ApplyJudgement(Judgement.Miss);
            }
        }
    }

    private void ReadLocalInput(double rawPos)
    {
        if (_localLane < 0) return;
        if (_localState != null && _localState.MinigameData != null &&
            _localState.MinigameData.IsEliminated) return;

        if (!AnyHitKeyDown()) return;

        var list = _active[_localLane];
        if (list.Count == 0) return;

        double diffMs = (rawPos - list[0].TargetTime) * 1000.0;
        double abs = System.Math.Abs(diffMs);

        if (abs > missMs) return; // ấn quá sớm — bỏ qua, không phạt

        Judgement j = abs <= perfectMs ? Judgement.Perfect
                    : abs <= goodMs ? Judgement.Good
                    : Judgement.Miss;

        Recycle(_localLane, 0);
        ApplyJudgement(j);
    }

    private static bool AnyHitKeyDown()
    {
        for (int i = 0; i < HitKeys.Length; i++)
            if (Input.GetKeyDown(HitKeys[i])) return true;
        return false;
    }

    private void ApplyJudgement(Judgement j)
    {
        switch (j)
        {
            case Judgement.Perfect:
                _score += perfectScore; _combo++; _perfect++; _fever += perfectFever; break;
            case Judgement.Good:
                _score += goodScore; _combo++; _good++; _fever += goodFever; break;
            case Judgement.Miss:
                _combo = 0; _miss++; _fever += missFever; break;
        }

        if (_combo > _maxCombo) _maxCombo = _combo;
        _fever = Mathf.Clamp(_fever, 0f, maxFever);

        if (_localLane >= 0 && lanes[_localLane] != null)
            lanes[_localLane].ShowLocalJudgement(j, _combo, _fever / maxFever);

        if (_fever >= maxFever)
        {
            _fever = 0f;
            // Host là bên quyết định sát thương. Client chỉ báo cáo.
            _localState?.RPC_ReportFeverFull();
        }
    }

    private void ReportProgress()
    {
        if (_localState == null) return;

        _reportTimer -= Time.deltaTime;
        if (_reportTimer > 0f) return;
        _reportTimer = 1f / Mathf.Max(1f, reportsPerSecond);

        _localState.RPC_ReportProgress(_score, _combo, _maxCombo,
                                       _fever / maxFever, _perfect, _good, _miss);
    }

    private void Recycle(int lane, int index)
    {
        NoteView nv = _active[lane][index];
        _active[lane].RemoveAt(index);
        nv.Recycle();
        _pool.Push(nv);
    }

    // ----------------------------------------------------------------
    //  VFX
    // ----------------------------------------------------------------

    public void PlayFeverBurstVfx(int lane)
    {
        if (lane < 0 || lane >= lanes.Length) return;
        lanes[lane]?.PlayFeverBurst();
    }
}
