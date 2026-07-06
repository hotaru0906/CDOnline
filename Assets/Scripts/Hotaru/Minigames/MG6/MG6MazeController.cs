using Fusion;
using UnityEngine;
using System.Collections.Generic;

public class MG6MazeController : BaseMinigameController
{
    public new static MG6MazeController Instance => BaseMinigameController.Instance as MG6MazeController;

    private bool _isSpectating = false;
    private int _spectateIndex = 0;
    private readonly List<PlayerController> _activePlayers = new();

    [Header("Path Segments (thứ tự = thứ tự hiện/tắt)")]
    [Tooltip("Gán theo đúng thứ tự bạn muốn hiện lên: index 0 hiện trước, tắt trước.")]
    [SerializeField] private List<MG6PathSegment> _pathSegments = new();
    [SerializeField] private bool _autoRegisterPathSegments = true;
    [SerializeField] private bool _includeInactiveSegments = true;

    [Header("Reveal Cycle Timing")]
    [SerializeField] private float _staggerDelay = 0.3f;   // delay giữa mỗi object khi bắt đầu hiện/tắt
    [SerializeField] private float _fadeDuration = 0.5f;   // thời gian mờ dần từ tối -> sáng (và ngược lại) cho 1 object
    [SerializeField] private float _holdVisibleDuration = 3f;  // giữ hiện toàn bộ trước khi tắt
    [SerializeField] private float _holdHiddenDuration = 2f;   // ẩn hết trước khi lặp lại

    private const float MinFadeDuration = 0.0001f;

    // Server tăng dần, client tự đọc để tính segment nào đang hiện -> không cần RPC per-object.
    [Networked] private float RevealCycleTime { get; set; }

    private float _revealPhaseDuration; // thời gian để hiện hết N object (staggered)
    private float _hidePhaseStart;      // thời điểm bắt đầu tắt (sau khi hold visible xong)
    private float _totalCycleDuration;

    private void Update()
    {
        if (!_isSpectating) return;

        if (Input.GetMouseButtonDown(0))
            CycleSpectateTarget();
    }

    public override void Spawned()
    {
        base.Spawned();

        if (_autoRegisterPathSegments && (_pathSegments.Count == 0 || _pathSegments.Exists(s => s == null)))
            AutoRegisterPathSegments();

        if (_pathSegments.Count == 0)
        {
            Debug.LogWarning("[MG6MazeController] Chưa tìm thấy path segments dưới object controller!");
        }

        RecalculateCycleTimings();
        ApplyFadeToAllSegments(0f);
    }

    [ContextMenu("Auto Register Path Segments")]
    private void AutoRegisterPathSegments()
    {
        _pathSegments.Clear();

        var foundSegments = GetComponentsInChildren<MG6PathSegment>(_includeInactiveSegments);
        foreach (var segment in foundSegments)
        {
            if (segment == null) continue;
            if (segment.gameObject == gameObject) continue;
            _pathSegments.Add(segment);
        }
    }

    private void OnValidate()
    {
        if (_autoRegisterPathSegments)
            AutoRegisterPathSegments();

        RecalculateCycleTimings();
    }

    private void RecalculateCycleTimings()
    {
        int n = _pathSegments.Count;
        float safeFadeDuration = Mathf.Max(_fadeDuration, MinFadeDuration);
        _revealPhaseDuration = n > 0 ? (_staggerDelay * (n - 1)) + safeFadeDuration : 0f;
        _hidePhaseStart = _revealPhaseDuration + _holdVisibleDuration;
        float hidePhaseDuration = n > 0 ? (_staggerDelay * (n - 1)) + safeFadeDuration : 0f;
        _totalCycleDuration = _hidePhaseStart + hidePhaseDuration + _holdHiddenDuration;
    }

    public override void FixedUpdateNetwork()
    {
        base.FixedUpdateNetwork();

        if (!HasStateAuthority) return;
        if (!IsGameStarted) return;
        if (IsGameEnded) return;
        if (_totalCycleDuration <= 0f) return;

        RevealCycleTime += Runner.DeltaTime;
        if (RevealCycleTime >= _totalCycleDuration)
            RevealCycleTime -= _totalCycleDuration;
    }

    public override void Render()
    {
        // Mọi client tự tính visibility từng segment từ cùng 1 networked timer
        // -> đảm bảo đồng bộ tuyệt đối, không cần gửi state riêng cho từng object.
        if (!IsGameStarted || IsGameEnded || _totalCycleDuration <= 0f)
        {
            ApplyFadeToAllSegments(0f);
            return;
        }

        ApplyFadeAtTime(RevealCycleTime);
    }

    private void ApplyFadeToAllSegments(float fade)
    {
        for (int i = 0; i < _pathSegments.Count; i++)
            _pathSegments[i]?.SetFade(fade);
    }

    private void ApplyFadeAtTime(float t)
    {
        float safeFadeDuration = Mathf.Max(_fadeDuration, MinFadeDuration);

        for (int i = 0; i < _pathSegments.Count; i++)
        {
            float revealStart = i * _staggerDelay;                 // obj i bắt đầu fade-in tại thời điểm này
            float hideStart = _hidePhaseStart + i * _staggerDelay; // obj i bắt đầu fade-out tại thời điểm này (cùng thứ tự: 1 tắt trước)

            float fade;
            if (t < revealStart)
            {
                fade = 0f; // chưa tới lượt hiện
            }
            else if (t < revealStart + _fadeDuration)
            {
                fade = (t - revealStart) / safeFadeDuration; // đang mờ dần lên sáng
            }
            else if (t < hideStart)
            {
                fade = 1f; // đang hiện đầy đủ
            }
            else if (t < hideStart + _fadeDuration)
            {
                fade = 1f - (t - hideStart) / safeFadeDuration; // đang mờ dần xuống tối
            }
            else
            {
                fade = 0f; // đã tắt hẳn, chờ vòng lặp mới
            }

            _pathSegments[i]?.SetFade(fade);
        }
    }

    // ----------------------------------------------------------------
    //  Checkpoint progress — gọi từ trigger khi player đi qua 1 mốc trong maze
    // ----------------------------------------------------------------

    /// <summary>
    /// TODO: cần tích hợp với PlayerMinigameData sau khi có field checkpoint progress.
    /// </summary>
    public void OnPlayerReachedCheckpoint(PlayerRef player, int checkpointIndex)
    {
        if (!HasStateAuthority) return;

        var allPlayers = FindObjectsByType<PlayerMinigameData>(FindObjectsSortMode.None);
        foreach (var p in allPlayers)
        {
            if (p.Object.InputAuthority != player) continue;

            // Dùng checkpoint index làm progress cơ bản cho tie-break DNF.
            p.SetCheckpoint(checkpointIndex, p.transform.position);
            p.UpdateDistanceProgress(checkpointIndex);
            Debug.Log($"[MG6MazeController] Player {player} reached checkpoint {checkpointIndex}");
            break;
        }
    }

    // ----------------------------------------------------------------
    //  Win Condition — giống MG2, race về đích, DNF theo progress khi hết giờ
    // ----------------------------------------------------------------

    protected override void CheckWinCondition()
    {
        var allPlayers = FindObjectsByType<PlayerMinigameData>(FindObjectsSortMode.None);
        if (allPlayers.Length == 0) return;

        int totalPlayers = allPlayers.Length;
        int doneCount = 0;

        foreach (var p in allPlayers)
        {
            if (p.HasFinished || p.IsEliminated)
                doneCount++;
        }

        if (doneCount >= totalPlayers - 1)
        {
            FinalizeRanks();
            PlayerRef winner = _finishOrder.Count > 0 ? _finishOrder[0] : PlayerRef.None;
            EndGame(winner);
        }
    }

    protected override void OnTimeUp()
    {
        FinalizeRanks();
        PlayerRef winner = _finishOrder.Count > 0 ? _finishOrder[0] : PlayerRef.None;
        EndGame(winner);
    }

    protected override void OnGameOver()
    {
        _isSpectating = false;
    }

    public override void PlayerFinished(PlayerRef playerRef)
    {
        if (!HasStateAuthority) return;
        if (IsGameEnded) return;
        if (_finishOrder.Contains(playerRef)) return;

        _finishOrder.Add(playerRef);
        int rank = _finishOrder.Count;

        var allPlayers = FindObjectsByType<PlayerMinigameData>(FindObjectsSortMode.None);
        foreach (var p in allPlayers)
        {
            if (p.Object.InputAuthority == playerRef)
            {
                float elapsed = (_minigameData != null && _minigameData.timeLimit > 0f)
                    ? _minigameData.timeLimit - GameTimer
                    : 0f;
                p.SetFinished(rank, elapsed);
                break;
            }
        }

        RPC_FreezeFinishedPlayer(playerRef);
        RPC_ShowFinishUI(playerRef);

        OnPlayerFinished(playerRef);
        CheckWinCondition();
    }

    private void FinalizeRanks()
    {
        var allPlayers = FindObjectsByType<PlayerMinigameData>(FindObjectsSortMode.None);

        var unfinished = new List<PlayerMinigameData>();
        foreach (var p in allPlayers)
            if (!p.HasFinished) unfinished.Add(p);

        if (unfinished.Count > 0)
        {
            unfinished.Sort((a, b) =>
            {
                int cpCompare = b.CurrentCheckpointIndex.CompareTo(a.CurrentCheckpointIndex);
                if (cpCompare != 0) return cpCompare;
                return b.DistanceProgress.CompareTo(a.DistanceProgress);
            });

            int nextRank = _finishOrder.Count + 1;
            foreach (var p in unfinished)
            {
                p.SetFinished(nextRank, 0f);
                _finishOrder.Add(p.Object.InputAuthority);
                nextRank++;
            }
        }

        ApplyHiddenScores();
    }

    private void CycleSpectateTarget()
    {
        RebuildActiveSpectateTargets();
        if (_activePlayers.Count == 0) return;

        _spectateIndex = (_spectateIndex + 1) % _activePlayers.Count;
        var target = _activePlayers[_spectateIndex];
        FocusSpectateTarget(target);
    }

    private void SwitchCameraToActivePlayer()
    {
        RebuildActiveSpectateTargets();

        if (_activePlayers.Count == 0)
        {
            _isSpectating = false;
            return;
        }

        _isSpectating = true;
        _spectateIndex = 0;
        FocusSpectateTarget(_activePlayers[_spectateIndex]);
    }

    private void RebuildActiveSpectateTargets()
    {
        _activePlayers.Clear();

        var allData = FindObjectsByType<PlayerMinigameData>(FindObjectsSortMode.None);
        foreach (var data in allData)
        {
            if (data.HasFinished || data.IsEliminated) continue;
            if (data.Object.InputAuthority == Runner.LocalPlayer) continue;

            var pc = data.GetComponent<PlayerController>();
            if (pc != null) _activePlayers.Add(pc);
        }

        if (_spectateIndex >= _activePlayers.Count)
            _spectateIndex = 0;
    }

    private static void FocusSpectateTarget(PlayerController target)
    {
        if (target == null) return;
        if (CameraManager.Instance == null) return;

        CameraManager.Instance.UpdatePlayerTarget(target.transform);
        CameraManager.Instance.SwitchToThirdPersonCamera();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_FreezeFinishedPlayer(PlayerRef playerRef)
    {
        var players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        foreach (var p in players)
        {
            if (p.Object.InputAuthority != playerRef) continue;
            p.SetFrozen(true);

            // Local player về đích thì chuyển camera sang người còn đang chạy.
            if (Runner.LocalPlayer == playerRef)
                SwitchCameraToActivePlayer();

            break;
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ShowFinishUI(PlayerRef playerRef)
    {
        if (Runner.LocalPlayer == playerRef)
            FinishUI.Instance?.ShowFinish();
    }

    protected override void BuildScoreboardResults()
    {
        var players = FindObjectsByType<PlayerMinigameData>(FindObjectsSortMode.None);
        var sorted = new List<PlayerMinigameData>(players);

        sorted.Sort((a, b) =>
        {
            if (a.FinishRank == 0 && b.FinishRank == 0)
                return b.DistanceProgress.CompareTo(a.DistanceProgress);
            if (a.FinishRank == 0) return 1;
            if (b.FinishRank == 0) return -1;
            return a.FinishRank.CompareTo(b.FinishRank);
        });

        for (int i = 0; i < ScoreboardResults.Length; i++)
            ScoreboardResults.Set(i, default);

        for (int i = 0; i < sorted.Count && i < ScoreboardResults.Length; i++)
        {
            var p = sorted[i];
            ScoreboardResults.Set(i, new MinigameResultData
            {
                Player = p.Object.InputAuthority,
                Rank = p.FinishRank > 0 ? p.FinishRank : (i + 1),
                FinishTime = p.FinishTime,
                Score = p.Score,
                IsValid = true
            });
        }
    }

    protected override void LogScoreboardInfo()
    {
        Debug.Log("========== SCOREBOARD (MG6 Maze) ==========");
        var players = FindObjectsByType<PlayerMinigameData>(FindObjectsSortMode.None);
        var sorted = new List<PlayerMinigameData>(players);
        sorted.Sort((a, b) => a.FinishRank.CompareTo(b.FinishRank));

        foreach (var p in sorted)
        {
            string timeStr = p.FinishTime > 0f ? $"{p.FinishTime:F2}s" : "DNF";
            Debug.Log($"[Scoreboard] #{p.FinishRank}: Player {p.Object.InputAuthority} — {timeStr}");
        }
        Debug.Log("============================================");
    }
}