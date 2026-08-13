using Fusion;
using UnityEngine;
using System.Collections.Generic;
using RhythmGame;

/// <summary>
/// Minigame Rhythm — 4 lane, mỗi player một lane, chung một màn hình.
///
/// Điều kiện thắng:
///   - Người cuối cùng còn máu, HOẶC
///   - Hết bài: xếp hạng theo RhythmScore
///
/// ĐỒNG BỘ NHẠC — đọc kỹ phần này trước khi sửa:
///   UnityEngine.AudioSettings.dspTime là đồng hồ card âm thanh của TỪNG MÁY, hoàn toàn
///   không liên quan tới Runner.Tick. Không thể ép hai máy có cùng dspTime.
///   Cách làm đúng: host chốt một TICK bắt đầu, mỗi client quy đổi tick đó sang
///   dspTime cục bộ của chính nó rồi từ đó chạy độc lập.
///   Chấm điểm hoàn toàn cục bộ. Chỉ có ba thứ qua mạng: báo cáo tiến độ,
///   sự kiện fever nổ, và sát thương (host quyết).
/// </summary>
public class MGRhythmController : BaseMinigameController
{
    public new static MGRhythmController Instance =>
        BaseMinigameController.Instance as MGRhythmController;

    [Header("Rhythm Refs")]
    [SerializeField] private Conductor conductor;
    [SerializeField] private MGRhythmDiamondPlayfield playfield;

    [Header("Battle Settings")]
    [SerializeField] private int startHP = 100;
    [SerializeField, Tooltip("Fever nổ trừ bao nhiêu HP của MỖI đối thủ.")]
    private int feverDamage = 20;

    [Header("Song Sync")]
    [SerializeField, Tooltip("Host đặt lịch bắt đầu nhạc sau ngần này giây, đủ để mọi client nhận được state.")]
    private float songStartDelay = 3.5f;

    [SerializeField, Tooltip("Chơi tiếp bao nhiêu giây sau khi hết nhạc rồi mới kết thúc.")]
    private float songTailSeconds = 2f;

    // ----------------------------------------------------------------
    //  Networked
    // ----------------------------------------------------------------

    /// <summary>Tick mà nhạc phải bắt đầu. 0 = chưa chốt.</summary>
    [Networked] public int SongStartTick { get; private set; }

    /// <summary>Tick mà bài kết thúc (đã cộng đuôi).</summary>
    [Networked] public int SongEndTick { get; private set; }

    /// <summary>LanePlayerIds[i] = PlayerId sở hữu lane i. -1 = lane trống.</summary>
    [Networked, Capacity(4)]
    public NetworkArray<int> LanePlayerIds { get; }

    // ----------------------------------------------------------------
    //  Private
    // ----------------------------------------------------------------

    private readonly List<PlayerRef> _eliminationOrder = new();
    private bool _songScheduled;

    // ----------------------------------------------------------------
    //  Setup
    // ----------------------------------------------------------------

    protected override void OnGamePlayingStarted()
    {
        // Hàm này chạy trên MỌI máy (nó được gọi từ OnPhaseChanged).
        // Phần dưới chỉ host làm.
        if (!HasStateAuthority) return;

        _eliminationOrder.Clear();
        _songScheduled = false;

        AssignLanes();

        // QUAN TRỌNG: ResetCheckpoint() trong TeleportPlayersToSpawnPoints() đã đặt HP = 0.
        // Nếu không set lại ở đây, cú TakeDamage đầu tiên sẽ cho HP = max(0, 0-20) = 0
        // và người đó bị eliminated ngay lập tức.
        var allStates = FindObjectsByType<MGRhythmPlayerState>(FindObjectsSortMode.None);
        foreach (var st in allStates)
        {
            st.ResetForRhythmRound();
            st.MinigameData?.SetHP(startHP);
            st.MinigameData.OnPlayerEliminated += HandlePlayerEliminated;
        }

        // Chốt lịch nhạc. Đặt SAU khi LanePlayerIds đã ghi xong để client nhận
        // cả hai trong cùng một snapshot.
        // Ép kiểu (int) tường minh: Runner.Tick là struct Tick, để implicit sẽ
        // gây lỗi biên dịch mơ hồ ở một số phiên bản Fusion 2.
        int nowTick = (int)Runner.Tick;
        int delayTicks = Mathf.RoundToInt(songStartDelay / Runner.DeltaTime);
        SongStartTick = nowTick + delayTicks;

        float clipLen = conductor != null ? conductor.ClipLength : 0f;
        SongEndTick = SongStartTick + Mathf.RoundToInt((clipLen + songTailSeconds) / Runner.DeltaTime);

        UpdateAlivePlayerCount();
        MinigameHUDController.Instance?.RefreshPlayers();

        Debug.Log($"[MGRhythm] Start tick {SongStartTick}, end tick {SongEndTick}, clip {clipLen:F1}s");
    }

    /// <summary>
    /// Gán lane theo PlayerId tăng dần. Cách này TẤT ĐỊNH — mọi máy tính ra
    /// cùng kết quả, giống hệt cách TeleportPlayersToSpawnPoints() sort spawn point.
    /// </summary>
    private void AssignLanes()
    {
        var players = FindObjectsByType<PlayerNetworkData>(FindObjectsSortMode.None);
        System.Array.Sort(players, (a, b) =>
            a.Object.InputAuthority.PlayerId.CompareTo(b.Object.InputAuthority.PlayerId));

        for (int i = 0; i < LanePlayerIds.Length; i++)
            LanePlayerIds.Set(i, -1);

        for (int i = 0; i < players.Length && i < LanePlayerIds.Length; i++)
            LanePlayerIds.Set(i, players[i].Object.InputAuthority.PlayerId);
    }

    protected override void OnGameOver()
    {
        conductor?.StopSong();
        playfield?.StopPlay();

        var allStates = FindObjectsByType<MGRhythmPlayerState>(FindObjectsSortMode.None);
        foreach (var st in allStates)
        {
            if (st.MinigameData != null)
                st.MinigameData.OnPlayerEliminated -= HandlePlayerEliminated;
        }
    }

    // ----------------------------------------------------------------
    //  Render — mọi máy tự đặt lịch nhạc của mình
    // ----------------------------------------------------------------

    public override void Render()
    {
        base.Render();

        // Đặt lịch nhạc trong Render vì Render chạy TIN CẬY trên mọi máy, kể cả
        // client (proxy). FixedUpdateNetwork chạy không đáng tin trên proxy nên
        // client sẽ không có nhạc/note.
        if (_songScheduled || SongStartTick <= 0 || IsGameEnded) return;

        _songScheduled = true;

        int ticksLeft = SongStartTick - (int)Runner.Tick;
        double secondsUntilStart = ticksLeft * (double)Runner.DeltaTime;

        if (secondsUntilStart >= 0.08)
        {
            // Còn thời gian: đặt lịch phát đúng lúc như bình thường.
            conductor.StartSongAtDsp(UnityEngine.AudioSettings.dspTime + secondsUntilStart);
        }
        else
        {
            // Nhận state TRỄ: đáng lẽ nhạc đã chạy được (-secondsUntilStart) giây.
            // Bù bằng cách phát ngay từ đúng vị trí đó -> không lệch cố định.
            conductor.StartSongAlreadyElapsed(-secondsUntilStart);
        }

        playfield.BeginPlay();
        Debug.Log($"[MGRhythm] Nhạc bắt đầu (còn {secondsUntilStart:F3}s) — lane của tôi: {GetLocalLane()}");
    }

    public override void FixedUpdateNetwork()
    {
        base.FixedUpdateNetwork();
    }

    // ----------------------------------------------------------------
    //  Fever attack — CHỈ host được gọi
    // ----------------------------------------------------------------

    // ----------------------------------------------------------------
    //  Đòn tấn công né được
    // ----------------------------------------------------------------
    //  Fever đầy -> mọi đối thủ nhận MỘT note tấn công chen vào màn của họ.
    //  Ấn trúng trong hạn -> né sạch, không mất máu.
    //  Hết hạn chưa né -> host trừ feverDamage.
    //  Chỉ host quyết damage; client chỉ báo "tôi né được".

    [SerializeField, Tooltip("Đối thủ có bao nhiêu giây để ấn trúng note tấn công mà né.")]
    private float dodgeWindowSeconds = 2.0f;

    // Các đòn đang chờ xử lý trên host: id -> (mục tiêu, hạn chót, đã né chưa)
    private struct PendingAttack
    {
        public PlayerRef target;
        public TickTimer deadline;
        public bool dodged;
    }
    private readonly Dictionary<int, PendingAttack> _pendingAttacks = new();
    private int _attackSeq;

    /// <summary>
    /// Gọi từ MGRhythmPlayerState.RPC_ReportFeverFull (đã chạy trên host).
    /// </summary>
    public void ApplyFeverAttack(PlayerRef attacker)
    {
        if (!HasStateAuthority) return;
        if (!IsGameStarted || IsGameEnded) return;

        var attackerData = GetMinigameData(attacker);
        if (attackerData == null || attackerData.IsEliminated) return;

        int hit = 0;
        var all = FindObjectsByType<PlayerMinigameData>(FindObjectsSortMode.None);
        foreach (var target in all)
        {
            if (target.Object.InputAuthority == attacker) continue;
            if (target.IsEliminated) continue;

            int attackId = ++_attackSeq;
            _pendingAttacks[attackId] = new PendingAttack
            {
                target = target.Object.InputAuthority,
                deadline = TickTimer.CreateFromSeconds(Runner, dodgeWindowSeconds),
                dodged = false,
            };

            // Báo cho máy mục tiêu spawn note tấn công. Bên nào là mục tiêu thì
            // playfield của bên đó mới thật sự sinh note (kiểm trong RPC).
            RPC_SpawnAttackNote(target.Object.InputAuthority, attackId, dodgeWindowSeconds);
            hit++;
        }

        Debug.Log($"[MGRhythm] FEVER! P{attacker} → {hit} đối thủ (đòn né được)");
        RPC_PlayFeverVfx(attacker);
    }

    /// <summary>
    /// Host kiểm hạn né mỗi tick. Đòn nào hết hạn mà chưa né -> trừ máu.
    /// </summary>
    private void TickPendingAttacks()
    {
        if (!HasStateAuthority || _pendingAttacks.Count == 0) return;

        // Gom id đã xử lý xong để xoá sau vòng lặp.
        List<int> done = null;
        foreach (var kv in _pendingAttacks)
        {
            var atk = kv.Value;
            if (atk.dodged)
            {
                (done ??= new List<int>()).Add(kv.Key);
                continue;
            }
            if (atk.deadline.Expired(Runner))
            {
                var td = GetMinigameData(atk.target);
                if (td != null && !td.IsEliminated)
                    td.TakeDamage(feverDamage);
                (done ??= new List<int>()).Add(kv.Key);
            }
        }
        if (done != null)
            foreach (int id in done) _pendingAttacks.Remove(id);
    }

    /// <summary>Mục tiêu đã né được. Gọi trên host từ MGRhythmPlayerState.RPC_ReportDodge.</summary>
    public void NotifyDodge(int attackId)
    {
        if (!HasStateAuthority) return;
        if (!_pendingAttacks.TryGetValue(attackId, out var atk)) return;
        atk.dodged = true;
        _pendingAttacks[attackId] = atk;
        Debug.Log($"[MGRhythm] P{atk.target} né được đòn #{attackId}");
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_SpawnAttackNote(PlayerRef target, int attackId, float window)
    {
        // Chỉ máy sở hữu mục tiêu mới spawn note (để đúng người bị đánh xử lý).
        if (Runner.LocalPlayer != target) return;
        playfield?.SpawnAttackNote(attackId, window);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayFeverVfx(PlayerRef attacker)
    {
        playfield?.PlayFeverBurstOnPanel(GetLaneOfPlayerId(attacker.PlayerId));
    }

    // ----------------------------------------------------------------
    //  Elimination + Win condition
    // ----------------------------------------------------------------

    private void HandlePlayerEliminated(PlayerMinigameData data)
    {
        if (!HasStateAuthority) return;

        var pRef = data.Object.InputAuthority;
        if (!_eliminationOrder.Contains(pRef))
        {
            _eliminationOrder.Add(pRef);
            Debug.Log($"[MGRhythm] P{pRef} hết máu — #{_eliminationOrder.Count} out");
        }

        UpdateAlivePlayerCount();
        CheckWinCondition();
    }

    protected override void CheckWinCondition()
    {
        // Chạy mỗi tick trên host (base gọi từ FixedUpdateNetwork).
        // Xử lý hạn né của các đòn tấn công đang chờ.
        TickPendingAttacks();

        var alive = GetAlivePlayers();

        // Điều kiện 1: chỉ còn một người còn máu
        if (alive.Count <= 1)
        {
            PlayerRef last = alive.Count == 1 ? alive[0].Object.InputAuthority : PlayerRef.None;
            FinishMatch(last);
            return;
        }

        // Điều kiện 2: hết bài
        if (SongEndTick > 0 && (int)Runner.Tick >= SongEndTick)
        {
            FinishMatch(HighestScorer(alive));
        }
    }

    protected override void OnTimeUp()
    {
        // timeLimit trong MinigameData là lưới an toàn. Nên đặt nó DÀI HƠN bài hát
        // để nhánh SongEndTick chạy trước.
        Debug.Log("[MGRhythm] Hết giờ (timeLimit) — kết thúc theo điểm.");
        FinishMatch(HighestScorer(GetAlivePlayers()));
    }

    private void FinishMatch(PlayerRef winner)
    {
        if (IsGameEnded) return;

        // Người còn sống xếp sau người đã chết, và trong nhóm còn sống thì
        // điểm cao hơn đứng trên.
        var alive = GetAlivePlayers();
        alive.Sort((a, b) =>
        {
            int sa = GetRhythmScore(a.Object.InputAuthority);
            int sb = GetRhythmScore(b.Object.InputAuthority);
            return sa.CompareTo(sb); // tăng dần, vì _eliminationOrder là "chết trước đứng đầu"
        });

        foreach (var p in alive)
        {
            var pRef = p.Object.InputAuthority;
            if (!_eliminationOrder.Contains(pRef))
                _eliminationOrder.Add(pRef);
        }

        FinalizeRanks();
        EndGame(winner);
    }

    private PlayerRef HighestScorer(List<PlayerMinigameData> pool)
    {
        PlayerRef best = PlayerRef.None;
        int bestScore = int.MinValue;

        foreach (var p in pool)
        {
            int s = GetRhythmScore(p.Object.InputAuthority);
            if (s > bestScore)
            {
                bestScore = s;
                best = p.Object.InputAuthority;
            }
        }
        return best;
    }

    // ----------------------------------------------------------------
    //  Rank
    // ----------------------------------------------------------------

    private void FinalizeRanks()
    {
        // _eliminationOrder[0] = ra sớm nhất = rank thấp nhất
        int total = _eliminationOrder.Count;
        var allData = FindObjectsByType<PlayerMinigameData>(FindObjectsSortMode.None);

        for (int i = 0; i < _eliminationOrder.Count; i++)
        {
            int rank = total - i;
            var pRef = _eliminationOrder[i];

            foreach (var p in allData)
            {
                if (p.Object.InputAuthority != pRef) continue;
                p.SetFinished(rank, 0f);
                break;
            }
        }

        // Base class tự lo BuildBoardRanking dựa trên HiddenScore.
        ApplyHiddenScores();
    }

    protected override void BuildScoreboardResults()
    {
        var allData = FindObjectsByType<PlayerMinigameData>(FindObjectsSortMode.None);
        var sorted = new List<PlayerMinigameData>(allData);
        sorted.Sort((a, b) => a.FinishRank.CompareTo(b.FinishRank));

        for (int i = 0; i < ScoreboardResults.Length; i++)
            ScoreboardResults.Set(i, default);

        for (int i = 0; i < sorted.Count && i < ScoreboardResults.Length; i++)
        {
            var p = sorted[i];
            ScoreboardResults.Set(i, new MinigameResultData
            {
                Player = p.Object.InputAuthority,
                Rank = p.FinishRank > 0 ? p.FinishRank : (i + 1),
                Score = GetRhythmScore(p.Object.InputAuthority),
                IsValid = true
            });
        }
    }

    protected override void LogScoreboardInfo()
    {
        Debug.Log("========== SCOREBOARD (MG Rhythm) ==========");
        var allData = FindObjectsByType<PlayerMinigameData>(FindObjectsSortMode.None);
        var sorted = new List<PlayerMinigameData>(allData);
        sorted.Sort((a, b) => a.FinishRank.CompareTo(b.FinishRank));

        foreach (var p in sorted)
        {
            var st = p.GetComponent<MGRhythmPlayerState>();
            var net = p.GetComponent<PlayerNetworkData>();
            string name = net != null ? net.PlayerName.ToString() : $"P{p.Object.InputAuthority.PlayerId}";
            string detail = st != null
                ? $"{st.RhythmScore} pts | P{st.PerfectCount} G{st.GoodCount} M{st.MissCount} | max combo {st.MaxCombo}"
                : "-";
            Debug.Log($"[Scoreboard] #{p.FinishRank}: {name} — {detail}");
        }
        Debug.Log("============================================");
    }

    // ----------------------------------------------------------------
    //  Helpers — dùng được từ mọi máy
    // ----------------------------------------------------------------

    public int GetLocalLane() => GetLaneOfPlayerId(Runner.LocalPlayer.PlayerId);

    public int GetLaneOfPlayerId(int playerId)
    {
        for (int i = 0; i < LanePlayerIds.Length; i++)
            if (LanePlayerIds[i] == playerId) return i;
        return -1;
    }

    public MGRhythmPlayerState GetStateForLane(int lane)
    {
        if (lane < 0 || lane >= LanePlayerIds.Length) return null;
        int id = LanePlayerIds[lane];
        if (id < 0) return null;

        var all = FindObjectsByType<MGRhythmPlayerState>(FindObjectsSortMode.None);
        foreach (var st in all)
            if (st.Object.InputAuthority.PlayerId == id) return st;
        return null;
    }

    private int GetRhythmScore(PlayerRef pRef)
    {
        var all = FindObjectsByType<MGRhythmPlayerState>(FindObjectsSortMode.None);
        foreach (var st in all)
            if (st.Object.InputAuthority == pRef) return st.RhythmScore;
        return 0;
    }

    private PlayerMinigameData GetMinigameData(PlayerRef pRef)
    {
        var all = FindObjectsByType<PlayerMinigameData>(FindObjectsSortMode.None);
        foreach (var p in all)
            if (p.Object.InputAuthority == pRef) return p;
        return null;
    }

    private List<PlayerMinigameData> GetAlivePlayers()
    {
        var all = FindObjectsByType<PlayerMinigameData>(FindObjectsSortMode.None);
        var alive = new List<PlayerMinigameData>();
        foreach (var p in all)
            if (!p.IsEliminated) alive.Add(p);
        return alive;
    }
}