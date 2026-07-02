using Fusion;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// MG5 — Bomb Tag minigame controller.
/// Win condition: Last player standing (bị nổ bomb = eliminated).
///
/// Flow:
///   OnGamePlayingStarted → chọn random holder → kích hoạt bomb
///   Player giữ bomb đánh trúng → transfer bomb + stun target
///   Bomb timer = 0 → holder bị eliminated → bomb chuyển sang player random còn lại
///   Còn 1 player → EndGame
/// </summary>
public class MG5BombTagController : BaseMinigameController
{
    public new static MG5BombTagController Instance =>
        BaseMinigameController.Instance as MG5BombTagController;

    [Header("Bomb Settings")]
    [SerializeField] private float bombTimerMin = 15f;
    [SerializeField] private float bombTimerMax = 30f;

    // ----------------------------------------------------------------
    //  Networked State
    // ----------------------------------------------------------------

    [Networked, OnChangedRender(nameof(OnBombHolderChanged))]
    public PlayerRef BombHolder { get; private set; }

    [Networked, OnChangedRender(nameof(OnBombTimerChanged))]
    public float BombTimer { get; private set; }

    [Networked]
    public NetworkBool BombActive { get; private set; }

    // ----------------------------------------------------------------
    //  Private
    // ----------------------------------------------------------------

    private readonly List<PlayerRef> _eliminationOrder = new();

    // ----------------------------------------------------------------
    //  Setup
    // ----------------------------------------------------------------

    protected override void OnGamePlayingStarted()
    {
        if (!HasStateAuthority) return;

        _eliminationOrder.Clear();

        var allData = FindObjectsByType<PlayerMinigameData>(FindObjectsSortMode.None);
        foreach (var p in allData)
        {
            p.OnPlayerEliminated += HandlePlayerEliminated;

            // Khôi phục đầy đủ trạng thái player khi vào MG5 round mới.
            RPC_SetPlayerEliminatedState(p.Object.InputAuthority, false);
        }

        // Chọn random holder đầu tiên
        var allPlayers = GetAlivePlayers();
        if (allPlayers.Count == 0) return;

        int randomIndex = UnityEngine.Random.Range(0, allPlayers.Count);
        AssignBomb(allPlayers[randomIndex].Object.InputAuthority, resetTimer: true);

        MinigameHUDController.Instance?.RefreshPlayers();
        Debug.Log($"[MG5BombTag] Game started — initial holder: P{BombHolder}");
    }

    protected override void OnGameOver()
    {
        BombActive = false;

        if (MG5Bomb.Instance != null)
        {
            MG5Bomb.Instance.SetVisible(false);
            MG5Bomb.Instance.Detach();
        }

        var allData = FindObjectsByType<PlayerMinigameData>(FindObjectsSortMode.None);
        foreach (var p in allData)
        {
            p.OnPlayerEliminated -= HandlePlayerEliminated;

            // Đảm bảo player được hiện lại đầy đủ sau khi kết thúc MG5.
            RPC_SetPlayerEliminatedState(p.Object.InputAuthority, false);
        }
    }

    // ----------------------------------------------------------------
    //  FixedUpdateNetwork — đếm bomb timer
    // ----------------------------------------------------------------

    public override void FixedUpdateNetwork()
    {
        base.FixedUpdateNetwork();

        if (!HasStateAuthority) return;
        if (!IsGameStarted || IsGameEnded) return;
        if (!BombActive) return;

        BombTimer -= Runner.DeltaTime;

        if (BombTimer <= 0f)
        {
            BombTimer = 0f;
            TriggerExplosion();
        }
    }

    // ----------------------------------------------------------------
    //  Bomb Logic
    // ----------------------------------------------------------------

    /// <summary>
    /// Gán bomb cho player. isReset = true khi nổ xong và chuyển sang người mới.
    /// </summary>
    private void AssignBomb(PlayerRef newHolder, bool resetTimer)
    {
        if (!HasStateAuthority) return;

        BombHolder = newHolder;
        BombActive = true;

        if (resetTimer)
            BombTimer = UnityEngine.Random.Range(bombTimerMin, bombTimerMax);

        // RPC chỉ lo attach transform
        RPC_MoveBomb(newHolder);

        // Luôn hiện bomb sau khi gắn holder mới.
        if (MG5Bomb.Instance != null)
            MG5Bomb.Instance.SetVisible(true);

        Debug.Log($"[MG5BombTag] Bomb → P{newHolder} | Timer: {BombTimer:F1}s | ResetTimer: {resetTimer}");
    }

    /// <summary>
    /// Player giữ bomb đánh trúng target → transfer bomb.
    /// Gọi từ PlayerController khi attack hit.
    /// </summary>
    public void TryTransferBomb(PlayerRef attacker, PlayerRef target)
    {
        if (!HasStateAuthority) return;
        if (!IsGameStarted || IsGameEnded) return;
        if (BombHolder != attacker) return;

        var targetData = GetPlayerMinigameData(target);
        if (targetData == null || targetData.IsEliminated) return;

        Debug.Log($"[MG5BombTag] Transfer: P{attacker} → P{target}");
        AssignBomb(target, resetTimer: false); // ← giữ timer
    }

    /// <summary>
    /// Bomb nổ — eliminate holder, chuyển bomb sang player random còn lại.
    /// </summary>
    private void TriggerExplosion()
    {
        if (!HasStateAuthority) return;

        PlayerRef victim = BombHolder;
        BombActive = false;

        // Tắt visible trên network trước khi chuyển holder mới để client nhận state đổi rõ ràng.
        if (MG5Bomb.Instance != null)
            MG5Bomb.Instance.SetVisible(false);

        Debug.Log($"[MG5BombTag] BOOM! P{victim} eliminated");

        // Play explosion trước khi eliminate
        if (MG5Bomb.Instance != null)
            MG5Bomb.Instance.PlayExplosion();

        // Eliminate player
        var victimData = GetPlayerMinigameData(victim);
        if (victimData != null)
        {
            victimData.Die(); // Die() → IsEliminated = true → HandlePlayerEliminated() callback
        }

        // Chuyển bomb sang player khác (nếu còn người)
        StartCoroutine(TransferBombAfterExplosion(victim));
    }

    private IEnumerator TransferBombAfterExplosion(PlayerRef previousVictim)
    {
        // Chờ 1 tick để elimination xử lý xong
        yield return new WaitForSeconds(0.5f);

        if (IsGameEnded) yield break;

        var candidates = GetAlivePlayers();

        // Loại bỏ victim vừa nổ khỏi danh sách
        candidates.RemoveAll(p => p.Object.InputAuthority == previousVictim);

        if (candidates.Count == 0)
        {
            Debug.Log("[MG5BombTag] No candidates left after explosion");
            yield break;
        }

        int randomIndex = UnityEngine.Random.Range(0, candidates.Count);
        AssignBomb(candidates[randomIndex].Object.InputAuthority, resetTimer: true);
    }

    // ----------------------------------------------------------------
    //  Elimination
    // ----------------------------------------------------------------

    private void HandlePlayerEliminated(PlayerMinigameData data)
    {
        if (!HasStateAuthority) return;

        var playerRef = data.Object.InputAuthority;

        // Deactivate player + camera switch
        RPC_HandlePlayerEliminated(playerRef);

        if (!_eliminationOrder.Contains(playerRef))
        {
            _eliminationOrder.Add(playerRef);
            Debug.Log($"[MG5BombTag] P{playerRef} eliminated — #{_eliminationOrder.Count} out");
        }

        UpdateAlivePlayerCount();
        CheckWinCondition();
    }

    private void SwitchCameraToActivePlayer()
    {
        var allData = FindObjectsByType<PlayerMinigameData>(FindObjectsSortMode.None);
        var candidates = new List<PlayerController>();

        foreach (var data in allData)
        {
            if (data.IsEliminated) continue;
            var pc = data.GetComponent<PlayerController>();
            if (pc != null && pc.gameObject.activeSelf)
                candidates.Add(pc);
        }

        if (candidates.Count == 0) return;

        var target = candidates[0];
        if (CameraManager.Instance != null)
        {
            CameraManager.Instance.UpdatePlayerTarget(target.transform);
            CameraManager.Instance.SwitchToThirdPersonCamera();
        }

        Debug.Log($"[MG5BombTag] Spectate — Camera → P{target.Object.InputAuthority}");
    }

    // ----------------------------------------------------------------
    //  Win Condition
    // ----------------------------------------------------------------

    protected override void CheckWinCondition()
    {
        var alive = GetAlivePlayers();

        if (alive.Count <= 1)
        {
            PlayerRef lastAlive = alive.Count == 1
                ? alive[0].Object.InputAuthority
                : PlayerRef.None;

            if (lastAlive != PlayerRef.None && !_eliminationOrder.Contains(lastAlive))
                _eliminationOrder.Add(lastAlive);

            FinalizeRanks();
            EndGame(lastAlive);
        }
    }

    protected override void OnTimeUp()
    {
        Debug.Log("[MG5BombTag] Time's up!");

        // Player đang giữ bomb thua (rank cuối trong những người còn sống)
        // Sort người còn sống: holder xuống cuối, còn lại random
        var alive = GetAlivePlayers();

        // Holder vào elimination trước (thua nhất trong những người còn sống)
        foreach (var p in alive)
        {
            var pRef = p.Object.InputAuthority;
            if (pRef == BombHolder && !_eliminationOrder.Contains(pRef))
            {
                _eliminationOrder.Add(pRef);
                break;
            }
        }

        // Còn lại random order
        foreach (var p in alive)
        {
            var pRef = p.Object.InputAuthority;
            if (!_eliminationOrder.Contains(pRef))
                _eliminationOrder.Add(pRef);
        }

        FinalizeRanks();

        PlayerRef winner = PlayerRef.None;
        // Winner = người cuối trong elimination order (sống lâu nhất)
        if (_eliminationOrder.Count > 0)
            winner = _eliminationOrder[_eliminationOrder.Count - 1];

        EndGame(winner);
    }

    // ----------------------------------------------------------------
    //  Rank
    // ----------------------------------------------------------------

    private void FinalizeRanks()
    {
        // eliminationOrder[0] = chết đầu = rank cao nhất số (rank 4)
        // eliminationOrder[last] = sống lâu = rank 1
        int total = _eliminationOrder.Count;
        var allData = FindObjectsByType<PlayerMinigameData>(FindObjectsSortMode.None);

        for (int i = 0; i < _eliminationOrder.Count; i++)
        {
            int rank = total - i; // index 0 → rank total, index last → rank 1
            var pRef = _eliminationOrder[i];

            foreach (var p in allData)
            {
                if (p.Object.InputAuthority == pRef)
                {
                    p.SetFinished(rank, 0f);
                    break;
                }
            }
        }
    }

    protected override int[] BuildBoardRanking(PlayerRef winner)
    {
        // Rank 1 = sống lâu nhất = cuối _eliminationOrder → đảo ngược
        var ranking = new List<int>();
        for (int i = _eliminationOrder.Count - 1; i >= 0; i--)
            ranking.Add(_eliminationOrder[i].PlayerId);
        return ranking.ToArray();
    }

    // ----------------------------------------------------------------
    //  Scoreboard
    // ----------------------------------------------------------------

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
                Score = p.Score,
                IsValid = true
            });
        }
    }

    protected override void LogScoreboardInfo()
    {
        Debug.Log("========== SCOREBOARD (MG5 Bomb Tag) ==========");
        var allData = FindObjectsByType<PlayerMinigameData>(FindObjectsSortMode.None);
        var sorted = new List<PlayerMinigameData>(allData);
        sorted.Sort((a, b) => a.FinishRank.CompareTo(b.FinishRank));

        foreach (var p in sorted)
        {
            var netData = p.GetComponent<PlayerNetworkData>();
            string name = netData != null
                ? netData.PlayerName.ToString()
                : $"P{p.Object.InputAuthority.PlayerId}";
            Debug.Log($"[Scoreboard] #{p.FinishRank}: {name}");
        }
        Debug.Log("================================================");
    }

    // ----------------------------------------------------------------
    //  OnChangedRender callbacks — chạy trên tất cả clients
    // ----------------------------------------------------------------

    private void OnBombHolderChanged()
    {
        Debug.Log($"[MG5BombTag] BombHolder changed → P{BombHolder}");
        // UI highlight player đang giữ bomb nếu cần
    }

    private void OnBombTimerChanged()
    {
        // Feed timer vào bomb visual để tính blink
        if (MG5Bomb.Instance != null)
            MG5Bomb.Instance.SetDisplayTimer(BombTimer);
    }

    // ----------------------------------------------------------------
    //  RPCs
    // ----------------------------------------------------------------

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_MoveBomb(PlayerRef holderRef)
    {
        var players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        foreach (var p in players)
        {
            if (p.Object.InputAuthority != holderRef) continue;

            if (MG5Bomb.Instance != null)
                MG5Bomb.Instance.AttachToPlayer(p.transform);

            break;
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_HandlePlayerEliminated(PlayerRef eliminatedRef)
    {
        RPC_SetPlayerEliminatedState(eliminatedRef, true);

        // Nếu là local player → chuyển camera sang player khác
        if (Runner.LocalPlayer == eliminatedRef)
            SwitchCameraToActivePlayer();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_SetPlayerEliminatedState(PlayerRef playerRef, bool eliminated)
    {
        var players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        foreach (var p in players)
        {
            if (p.Object.InputAuthority != playerRef) continue;

            var colliders = p.GetComponentsInChildren<Collider>(true);
            foreach (var col in colliders)
            {
                if (col == null) continue;
                col.enabled = !eliminated;
            }

            var modelSwitcher = p.GetComponent<PlayerModelSwitcher>();
            if (modelSwitcher != null)
            {
                if (eliminated) modelSwitcher.HideCharacter();
                else modelSwitcher.ShowCharacter();
            }

            if (!eliminated)
                p.SetFrozen(false);

            break;
        }
    }

    // ----------------------------------------------------------------
    //  Helpers
    // ----------------------------------------------------------------

    private List<PlayerMinigameData> GetAlivePlayers()
    {
        var all = FindObjectsByType<PlayerMinigameData>(FindObjectsSortMode.None);
        var alive = new List<PlayerMinigameData>();
        foreach (var p in all)
            if (!p.IsEliminated) alive.Add(p);
        return alive;
    }

    private PlayerMinigameData GetPlayerMinigameData(PlayerRef playerRef)
    {
        var all = FindObjectsByType<PlayerMinigameData>(FindObjectsSortMode.None);
        foreach (var p in all)
            if (p.Object.InputAuthority == playerRef) return p;
        return null;
    }
}