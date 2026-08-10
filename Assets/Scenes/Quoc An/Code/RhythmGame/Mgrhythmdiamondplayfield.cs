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
        [SerializeField] private TextMeshProUGUI comboText;
        [SerializeField] private TextMeshProUGUI judgeText;
        [SerializeField] private TextMeshProUGUI scoreText;
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

        [Header("Note tấn công (đòn né được)")]
        [SerializeField, Tooltip("Prefab note tấn công — màu khác hẳn note thường để dễ nhận ra.")]
        private DiamondNoteView attackNotePrefab;
        [SerializeField, Tooltip("Note tấn công bay về bên nào. 0 = trái, 1 = phải. Để cố định cho dễ né.")]
        private int attackNoteSide = 0;

        [Header("Hold note")]
        [SerializeField, Tooltip("Prefab hold TRÁI (head + body + tail).")]
        private DiamondHoldNoteView holdPrefabLeft;
        [SerializeField, Tooltip("Prefab hold PHẢI. Để trống thì dùng chung prefab trái.")]
        private DiamondHoldNoteView holdPrefabRight;
        [SerializeField, Tooltip("Thanh ngang nối hai note của note ĐÔI (Image chữ nhật). Tuỳ chọn.")]
        private RectTransform dualBarPrefab;
        [SerializeField, Tooltip("Điểm cộng mỗi giây khi giữ đúng.")]
        private int holdScorePerSecond = 200;
        [SerializeField, Tooltip("Fever cộng mỗi giây khi giữ đúng.")]
        private float holdFeverPerSecond = 8f;
        [SerializeField, Tooltip("Được thả sớm hơn đích ngần này giây mà vẫn tính hoàn thành.")]
        private float releaseGraceSeconds = 0.12f;

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

        // Note tấn công đang bay tới người chơi cục bộ (né được).
        private class AttackNote
        {
            public DiamondNoteView view;
            public int side;
            public double targetTime;
            public int attackId;
        }
        private readonly List<AttackNote> _attackNotes = new();

        // Hold note. dual = true nghĩa là note đôi (giữ cả hai hướng).
        private enum HoldState { Approaching, Holding, Done }
        private class HoldNote
        {
            public bool dual;
            public int side;                 // dùng khi không phải dual
            public double headTime, tailTime;
            public DiamondHoldNoteView leftView;   // single-trái hoặc dual
            public DiamondHoldNoteView rightView;  // single-phải hoặc dual
            public RectTransform dualBar;
            public HoldState state = HoldState.Approaching;
            public bool headMissed;
        }
        private readonly List<HoldNote> _holds = new();

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

            for (int i = _attackNotes.Count - 1; i >= 0; i--)
                Destroy(_attackNotes[i].view.gameObject);
            _attackNotes.Clear();

            for (int i = _holds.Count - 1; i >= 0; i--)
                DestroyHold(_holds[i], i);
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
            MoveAttackNotes(visualPos);
            UpdateHolds(visualPos, rawPos);
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

                if (e.type == 1)          // note đôi: giữ cả hai hướng
                    SpawnDualHold(e.time, e.time + e.duration);
                else if (e.duration > 0f) // hold một hướng
                    SpawnSingleHold(e.lane == 1 ? 1 : 0, e.time, e.time + e.duration);
                else                      // note thường
                    SpawnTap(e.lane == 1 ? 1 : 0, e.time);
            }
        }

        private void SpawnTap(int side, float t)
        {
            DiamondNoteView nv = _pool[side].Count > 0
                ? _pool[side].Pop()
                : Instantiate(PrefabForSide(side));
            nv.Setup(side, t, noteContainer);
            _active[side].Add(nv);
        }

        private DiamondHoldNoteView SpawnHoldView(int side, double headTime, double tailTime)
        {
            var prefab = (side == 1 && holdPrefabRight != null) ? holdPrefabRight : holdPrefabLeft;
            var v = Instantiate(prefab);
            v.Setup(side, headTime, tailTime, noteContainer);
            return v;
        }

        private void SpawnSingleHold(int side, double headTime, double tailTime)
        {
            var h = new HoldNote
            {
                dual = false,
                side = side,
                headTime = headTime,
                tailTime = tailTime,
            };
            if (side == 0) h.leftView = SpawnHoldView(0, headTime, tailTime);
            else h.rightView = SpawnHoldView(1, headTime, tailTime);
            _holds.Add(h);
        }

        private void SpawnDualHold(double headTime, double tailTime)
        {
            var h = new HoldNote
            {
                dual = true,
                headTime = headTime,
                tailTime = tailTime,
                leftView = SpawnHoldView(0, headTime, tailTime),
                rightView = SpawnHoldView(1, headTime, tailTime),
            };
            if (dualBarPrefab != null)
            {
                h.dualBar = Instantiate(dualBarPrefab, noteContainer);
                h.dualBar.gameObject.SetActive(true);
            }
            _holds.Add(h);
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

            bool left = Down(LeftKeys);
            bool right = Down(RightKeys);

            // 1) Né note tấn công trước.
            if (left && TryDodge(0, rawPos)) return;
            if (right && TryDodge(1, rawPos)) return;

            // 2) Nếu cú ấn này đang bắt đầu một hold note thì để UpdateHolds xử lý,
            //    không chấm như note tap. Kiểm: có hold nào đang ở cửa sổ đầu ở đúng bên?
            if (left && HoldHeadWaiting(0, rawPos)) left = false;
            if (right && HoldHeadWaiting(1, rawPos)) right = false;

            // 3) Note thường.
            if (left) TryHit(0, rawPos);
            if (right) TryHit(1, rawPos);
        }

        /// <summary>Có hold note nào đang chờ bắt đầu ở bên này, trong cửa sổ đầu note?</summary>
        private bool HoldHeadWaiting(int side, double rawPos)
        {
            double window = missMs / 1000.0;
            for (int i = 0; i < _holds.Count; i++)
            {
                var h = _holds[i];
                if (h.state != HoldState.Approaching) continue;
                if (!h.dual && h.side != side) continue;
                if (System.Math.Abs(rawPos - h.headTime) <= window) return true;
            }
            return false;
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
                TriggerFever();
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
        //  Hold note
        // ----------------------------------------------------------------

        private void UpdateHolds(double visualPos, double rawPos)
        {
            if (_holds.Count == 0) return;

            Vector2 top = topVertex.anchoredPosition;
            Vector2 left = leftVertex.anchoredPosition;
            Vector2 right = rightVertex.anchoredPosition;

            double headWindow = missMs / 1000.0;

            for (int i = _holds.Count - 1; i >= 0; i--)
            {
                var h = _holds[i];

                // Vẽ
                if (h.leftView != null) h.leftView.Redraw(visualPos, travelTime, top, left);
                if (h.rightView != null) h.rightView.Redraw(visualPos, travelTime, top, right);
                if (h.dualBar != null) UpdateDualBar(h, visualPos, top, left, right);

                if (h.state == HoldState.Approaching)
                {
                    // Đầu note đi qua quá xa mà chưa bắt được -> Miss cả note.
                    if (rawPos - h.headTime > headWindow)
                    {
                        FailHold(h, i);
                        continue;
                    }

                    // Bắt đầu giữ khi ấn đúng lúc.
                    if (WithinHead(rawPos, h.headTime, headWindow) && HeadPressed(h))
                    {
                        h.state = HoldState.Holding;
                        if (h.leftView != null) h.leftView.Holding = true;
                        if (h.rightView != null) h.rightView.Holding = true;
                        ShowJudge("HOLD", 0.3f);
                        _combo++;
                        if (_combo > _maxCombo) _maxCombo = _combo;
                        RefreshLocalHud();
                    }
                }
                else if (h.state == HoldState.Holding)
                {
                    // Đang giữ: buông là Miss cả note (theo lựa chọn của bạn).
                    if (!HeldDown(h))
                    {
                        // Cho phép thả sớm trong grace nếu đã gần đích.
                        if (rawPos >= h.tailTime - releaseGraceSeconds)
                            CompleteHold(h, i);
                        else
                            FailHold(h, i);
                        continue;
                    }

                    // Giữ đúng -> cộng điểm + fever dần.
                    _score += Mathf.RoundToInt(holdScorePerSecond * Time.deltaTime);
                    _fever = Mathf.Clamp(_fever + holdFeverPerSecond * Time.deltaTime, 0f, maxFever);
                    RefreshLocalHud();
                    if (_fever >= maxFever) TriggerFever();

                    // Tới đích -> hoàn thành.
                    if (rawPos >= h.tailTime)
                        CompleteHold(h, i);
                }
            }
        }

        private void UpdateDualBar(HoldNote h, double visualPos, Vector2 top, Vector2 left, Vector2 right)
        {
            // Thanh ngang nối đầu trái và đầu phải (cùng độ cao vì đối xứng).
            // Khi đang giữ, đầu ghim ở đích nên thanh bám theo ĐUÔI để cũng ngắn dần.
            double refTime = h.state == HoldState.Holding ? h.tailTime : h.headTime;
            float p = 1f - (float)(refTime - visualPos) / Mathf.Max(0.0001f, travelTime);
            if (p > 1f) p = 1f;

            Vector2 lp = Vector2.LerpUnclamped(top, left, p);
            Vector2 rp = Vector2.LerpUnclamped(top, right, p);
            Vector2 mid = (lp + rp) * 0.5f;
            Vector2 dir = rp - lp;

            h.dualBar.anchoredPosition = mid;
            h.dualBar.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            h.dualBar.sizeDelta = new Vector2(dir.magnitude, h.dualBar.sizeDelta.y);
        }

        private static bool WithinHead(double rawPos, double headTime, double window)
            => System.Math.Abs(rawPos - headTime) <= window;

        /// <summary>Đúng lúc đầu note: có ấn xuống đúng phím không.</summary>
        private bool HeadPressed(HoldNote h)
        {
            if (h.dual)
                // Note đôi: cần cả hai đang được giữ (một trong hai vừa ấn xuống).
                return HeldDown(h) && (Down(LeftKeys) || Down(RightKeys));
            return h.side == 0 ? Down(LeftKeys) : Down(RightKeys);
        }

        /// <summary>Đang giữ đủ phím không (để duy trì hold).</summary>
        private bool HeldDown(HoldNote h)
        {
            if (h.dual) return Held(LeftKeys) && Held(RightKeys);
            return h.side == 0 ? Held(LeftKeys) : Held(RightKeys);
        }

        private void CompleteHold(HoldNote h, int index)
        {
            _perfect++;
            _combo++;
            if (_combo > _maxCombo) _maxCombo = _combo;
            _score += perfectScore;
            _fever = Mathf.Clamp(_fever + perfectFever, 0f, maxFever);
            ShowJudge("PERFECT", 0.35f);
            RefreshLocalHud();
            if (_fever >= maxFever) TriggerFever();
            DestroyHold(h, index);
        }

        private void FailHold(HoldNote h, int index)
        {
            _miss++;
            _combo = 0;
            ShowJudge("MISS", 0.35f);
            RefreshLocalHud();
            DestroyHold(h, index);
        }

        private void DestroyHold(HoldNote h, int index)
        {
            if (h.leftView != null) Destroy(h.leftView.gameObject);
            if (h.rightView != null) Destroy(h.rightView.gameObject);
            if (h.dualBar != null) Destroy(h.dualBar.gameObject);
            _holds.RemoveAt(index);
        }

        private void ShowJudge(string text, float dur)
        {
            if (judgeText == null) return;
            judgeText.text = text;
            _judgeTimer = dur;
        }

        /// <summary>Fever đầy -> nổ. Dùng chung cho cả hold lẫn note thường.</summary>
        private void TriggerFever()
        {
            _fever = 0f;
            if (localFeverBurstVfx != null)
            {
                localFeverBurstVfx.SetActive(false);
                localFeverBurstVfx.SetActive(true);
            }
            _localState?.RPC_ReportFeverFull();
        }

        private static bool Held(KeyCode[] keys)
        {
            for (int i = 0; i < keys.Length; i++)
                if (Input.GetKey(keys[i])) return true;
            return false;
        }

        // ----------------------------------------------------------------
        //  Note tấn công (đòn né được) — gọi từ controller trên máy mục tiêu
        // ----------------------------------------------------------------

        /// <summary>
        /// Spawn một note tấn công bay tới người chơi cục bộ. window = số giây để né,
        /// dùng luôn làm thời gian bay (note tới đích đúng lúc hết hạn né).
        /// </summary>
        public void SpawnAttackNote(int attackId, float window)
        {
            if (!_running) return;

            var prefab = attackNotePrefab != null ? attackNotePrefab : PrefabForSide(attackNoteSide);
            DiamondNoteView nv = Instantiate(prefab);
            double target = conductor.RawSongPosition + window;
            nv.Setup(attackNoteSide, target, noteContainer);

            _attackNotes.Add(new AttackNote
            {
                view = nv,
                side = attackNoteSide,
                targetTime = target,
                attackId = attackId,
            });

            Debug.Log($"[MGRhythmDiamond] Nhận đòn #{attackId}, có {window:F1}s để né.");
        }

        private void MoveAttackNotes(double visualPos)
        {
            if (_attackNotes.Count == 0) return;

            Vector2 top = topVertex.anchoredPosition;
            Vector2 left = leftVertex.anchoredPosition;
            Vector2 right = rightVertex.anchoredPosition;

            for (int i = _attackNotes.Count - 1; i >= 0; i--)
            {
                var a = _attackNotes[i];
                Vector2 side = a.side == 0 ? left : right;
                a.view.Redraw(visualPos, travelTime, top, side);

                // Quá đích quá lâu mà chưa né -> để host xử lý (đã trừ máu theo hạn).
                // Ở đây chỉ dọn hình.
                if (visualPos - a.targetTime > 0.3)
                {
                    Destroy(a.view.gameObject);
                    _attackNotes.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// Thử né: nếu có note tấn công đang gần đích ở đúng bên vừa ấn -> né thành công.
        /// Trả về true nếu đã dùng cú ấn này để né.
        /// </summary>
        private bool TryDodge(int side, double rawPos)
        {
            for (int i = 0; i < _attackNotes.Count; i++)
            {
                var a = _attackNotes[i];
                if (a.side != side) continue;

                double diffMs = System.Math.Abs((rawPos - a.targetTime) * 1000.0);
                if (diffMs <= missMs) // dùng chung cửa sổ với note thường
                {
                    // Báo host đã né qua state của CHÍNH mình (client có input
                    // authority ở đó). Không gọi thẳng RPC trên controller vì client
                    // không có input authority trên MinigameController.
                    _localState?.RPC_ReportDodge(a.attackId);

                    if (judgeText != null)
                    {
                        judgeText.text = "DODGE!";
                        _judgeTimer = 0.5f;
                    }
                    PunchMarker(side);
                    ShowHitEffect(side, Judgement.Perfect);

                    Destroy(a.view.gameObject);
                    _attackNotes.RemoveAt(i);
                    return true;
                }
            }
            return false;
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