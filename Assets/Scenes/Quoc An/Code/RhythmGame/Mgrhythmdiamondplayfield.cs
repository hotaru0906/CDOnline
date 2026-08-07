using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace RhythmGame
{
    /// <summary>
    /// Sân chơi kiểu HÌNH THOI, 2 hướng. Chạy hoàn toàn CỤC BỘ trên máy mỗi người —
    /// ai cũng chỉ thấy hình thoi của chính mình, dùng chung một beatmap.
    ///
    /// Note spawn ở ĐỈNH TRÊN, trôi xuống:
    ///   - đỉnh TRÁI  (mũi tên ←, xanh)  — ấn A hoặc ←
    ///   - đỉnh PHẢI (mũi tên →, đỏ)    — ấn D hoặc →
    /// Trong beatmap JSON: lane 0 = trái, lane 1 = phải.
    ///
    /// Phần mạng KHÔNG nằm ở đây. Điểm/combo/fever tính cục bộ, chỉ báo cáo lên host
    /// qua MGRhythmPlayerState (để có điểm cuối xếp hạng + để host xử lý fever nổ).
    /// Hàng 4 người ở đáy đọc HP đã đồng bộ, hiện trên mọi máy.
    /// </summary>
    public class MGRhythmDiamondPlayfield : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private Conductor conductor;
        [SerializeField] private TextAsset chartJson;
        [SerializeField, Tooltip("Prefab mũi tên TRÁI (note bay về đỉnh trái, ấn A/←).")]
        private DiamondNoteView notePrefabLeft;
        [SerializeField, Tooltip("Prefab mũi tên PHẢI (note bay về đỉnh phải, ấn D/→). Để trống thì dùng chung prefab trái.")]
        private DiamondNoteView notePrefabRight;

        [Header("Hình học hình thoi (RectTransform đánh dấu 3 đỉnh)")]
        [SerializeField] private RectTransform noteContainer;
        [SerializeField] private RectTransform topVertex;
        [SerializeField] private RectTransform leftVertex;
        [SerializeField] private RectTransform rightVertex;

        [Header("Đích trái/phải (để nảy khi ấn)")]
        [SerializeField] private RectTransform leftHitMarker;
        [SerializeField] private RectTransform rightHitMarker;
        [SerializeField] private GameObject leftHitEffect;
        [SerializeField] private GameObject rightHitEffect;

        [Header("HUD người chơi cục bộ")]
        [SerializeField] private Image feverFill;      // Filled, Horizontal, Left
        [SerializeField] private TMP_Text comboText;
        [SerializeField] private TMP_Text judgeText;
        [SerializeField] private TMP_Text scoreText;
        [SerializeField] private GameObject localFeverBurstVfx;

        [Header("Hàng 4 người ở đáy")]
        [SerializeField] private MGRhythmPlayerPanel[] bottomPanels = new MGRhythmPlayerPanel[4];

        [Header("Thời gian note đi từ đỉnh trên xuống đích (giây)")]
        [SerializeField] private float travelTime = 1.6f;

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

        [Header("Network")]
        [SerializeField] private float reportsPerSecond = 10f;

        private static readonly KeyCode[] LeftKeys = { KeyCode.A, KeyCode.LeftArrow };
        private static readonly KeyCode[] RightKeys = { KeyCode.D, KeyCode.RightArrow };

        private ChartData _chart;
        private int _nextSpawnIndex;
        // _active[0] = note đang bay về trái, _active[1] = về phải
        private readonly List<DiamondNoteView>[] _active =
            { new List<DiamondNoteView>(32), new List<DiamondNoteView>(32) };
        // Pool riêng mỗi bên: note trái tái sử dụng ra note trái, phải ra phải,
        // nếu chung pool thì hình mũi tên sẽ bị lẫn.
        private readonly Stack<DiamondNoteView>[] _pool =
            { new Stack<DiamondNoteView>(), new Stack<DiamondNoteView>() };

        private int _score, _combo, _maxCombo, _perfect, _good, _miss;
        private float _fever, _reportTimer;
        private bool _running;

        private MGRhythmPlayerState _localState;
        private float _judgeTimer, _leftFxTimer, _rightFxTimer;

        // ----------------------------------------------------------------
        //  Bắt đầu / kết thúc — gọi từ MGRhythmController
        // ----------------------------------------------------------------

        public void BeginPlay()
        {
            LoadChart();
            BindBottomPanels();

            _score = _combo = _maxCombo = 0;
            _perfect = _good = _miss = 0;
            _fever = 0f;
            _nextSpawnIndex = 0;
            _running = true;

            RefreshLocalHud();
        }

        public void StopPlay()
        {
            _running = false;
            for (int s = 0; s < 2; s++)
                for (int i = _active[s].Count - 1; i >= 0; i--)
                    Recycle(s, i);
        }

        private void LoadChart()
        {
            _chart = chartJson != null ? JsonUtility.FromJson<ChartData>(chartJson.text) : null;
            if (_chart == null || _chart.notes == null)
            {
                Debug.LogError("[MGRhythmDiamond] Không đọc được beatmap.");
                _chart = new ChartData();
                return;
            }
            _chart.notes.Sort((a, b) => a.time.CompareTo(b.time));
        }

        private void BindBottomPanels()
        {
            var ctrl = MGRhythmController.Instance;
            if (ctrl == null) return;

            int localLane = ctrl.GetLocalLane();
            for (int i = 0; i < bottomPanels.Length; i++)
            {
                if (bottomPanels[i] == null) continue;
                var state = ctrl.GetStateForLane(i);
                bottomPanels[i].Bind(state, isLocal: i == localLane);
                if (i == localLane) _localState = state;
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
            JudgeMisses(rawPos);
            ReadInput(rawPos);
            ReportProgress();
            TickHudTimers();
        }

        private void SpawnDueNotes(double visualPos)
        {
            while (_nextSpawnIndex < _chart.notes.Count &&
                   _chart.notes[_nextSpawnIndex].time - visualPos <= travelTime)
            {
                var e = _chart.notes[_nextSpawnIndex];
                _nextSpawnIndex++;

                int side = e.lane == 1 ? 1 : 0; // 1 = phải, còn lại = trái

                DiamondNoteView nv = _pool[side].Count > 0
                    ? _pool[side].Pop()
                    : Instantiate(PrefabForSide(side));
                nv.Setup(side, e.time, noteContainer);
                _active[side].Add(nv);
            }
        }

        private void MoveNotes(double visualPos)
        {
            Vector2 top = topVertex.anchoredPosition;
            Vector2 left = leftVertex.anchoredPosition;
            Vector2 right = rightVertex.anchoredPosition;

            for (int i = 0; i < _active[0].Count; i++)
                _active[0][i].Redraw(visualPos, travelTime, top, left);
            for (int i = 0; i < _active[1].Count; i++)
                _active[1][i].Redraw(visualPos, travelTime, top, right);
        }

        private void JudgeMisses(double rawPos)
        {
            double missWindow = missMs / 1000.0;
            for (int s = 0; s < 2; s++)
            {
                var list = _active[s];
                while (list.Count > 0 && rawPos - list[0].TargetTime > missWindow)
                {
                    Recycle(s, 0);
                    ApplyJudgement(Judgement.Miss);
                }
            }
        }

        private void ReadInput(double rawPos)
        {
            if (_localState != null && _localState.MinigameData != null &&
                _localState.MinigameData.IsEliminated) return;

            if (Down(LeftKeys)) TryHit(0, rawPos);
            if (Down(RightKeys)) TryHit(1, rawPos);
        }

        private void TryHit(int side, double rawPos)
        {
            var list = _active[side];
            PunchMarker(side); // luôn nảy đích để có phản hồi kể cả đánh hụt

            if (list.Count == 0) return;

            double diffMs = (rawPos - list[0].TargetTime) * 1000.0;
            double abs = System.Math.Abs(diffMs);
            if (abs > missMs) return; // quá sớm — bỏ qua, không phạt

            Judgement j = abs <= perfectMs ? Judgement.Perfect
                        : abs <= goodMs ? Judgement.Good
                        : Judgement.Miss;

            Recycle(side, 0);
            ShowHitEffect(side, j);
            ApplyJudgement(j);
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
                    _combo = 0; _miss++; break;
            }

            if (_combo > _maxCombo) _maxCombo = _combo;
            _fever = Mathf.Clamp(_fever, 0f, maxFever);

            if (judgeText != null)
            {
                judgeText.text = j.ToString().ToUpper();
                _judgeTimer = 0.35f;
            }
            RefreshLocalHud();

            if (_fever >= maxFever)
            {
                _fever = 0f;
                if (localFeverBurstVfx != null)
                {
                    localFeverBurstVfx.SetActive(false);
                    localFeverBurstVfx.SetActive(true);
                }
                _localState?.RPC_ReportFeverFull(); // host quyết định sát thương
            }
        }

        private void RefreshLocalHud()
        {
            if (feverFill != null) feverFill.fillAmount = _fever / maxFever;
            if (comboText != null) comboText.text = _combo > 1 ? _combo + "x" : "";
            if (scoreText != null) scoreText.text = _score.ToString("N0");
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

        // ----------------------------------------------------------------
        //  Phản hồi hình ảnh
        // ----------------------------------------------------------------

        private void PunchMarker(int side)
        {
            var m = side == 0 ? leftHitMarker : rightHitMarker;
            if (m != null) m.localScale = Vector3.one * 1.2f;
        }

        private void ShowHitEffect(int side, Judgement j)
        {
            if (j == Judgement.Miss) return;
            var fx = side == 0 ? leftHitEffect : rightHitEffect;
            if (fx == null) return;
            fx.SetActive(true);
            if (side == 0) _leftFxTimer = 0.15f; else _rightFxTimer = 0.15f;
        }

        private void TickHudTimers()
        {
            if (_judgeTimer > 0f)
            {
                _judgeTimer -= Time.deltaTime;
                if (_judgeTimer <= 0f && judgeText != null) judgeText.text = "";
            }
            if (_leftFxTimer > 0f)
            {
                _leftFxTimer -= Time.deltaTime;
                if (_leftFxTimer <= 0f && leftHitEffect != null) leftHitEffect.SetActive(false);
            }
            if (_rightFxTimer > 0f)
            {
                _rightFxTimer -= Time.deltaTime;
                if (_rightFxTimer <= 0f && rightHitEffect != null) rightHitEffect.SetActive(false);
            }
            if (leftHitMarker != null && leftHitMarker.localScale.x > 1.001f)
                leftHitMarker.localScale = Vector3.Lerp(leftHitMarker.localScale, Vector3.one, Time.deltaTime * 12f);
            if (rightHitMarker != null && rightHitMarker.localScale.x > 1.001f)
                rightHitMarker.localScale = Vector3.Lerp(rightHitMarker.localScale, Vector3.one, Time.deltaTime * 12f);
        }

        private static bool Down(KeyCode[] keys)
        {
            for (int i = 0; i < keys.Length; i++)
                if (Input.GetKeyDown(keys[i])) return true;
            return false;
        }

        private void Recycle(int side, int index)
        {
            DiamondNoteView nv = _active[side][index];
            _active[side].RemoveAt(index);
            nv.Recycle();
            _pool[side].Push(nv);
        }

        /// <summary>Prefab theo hướng. Nếu chưa gán prefab phải thì dùng chung prefab trái.</summary>
        private DiamondNoteView PrefabForSide(int side)
        {
            if (side == 1 && notePrefabRight != null) return notePrefabRight;
            return notePrefabLeft;
        }

        // ----------------------------------------------------------------
        //  Fever nổ của một người — gọi từ controller (RPC tới mọi máy)
        // ----------------------------------------------------------------

        /// <summary>Nháy VFX trên ô đáy của người vừa nổ fever.</summary>
        public void PlayFeverBurstOnPanel(int lane)
        {
            if (lane < 0 || lane >= bottomPanels.Length) return;
            bottomPanels[lane]?.PlayBurst();
        }
    }
}