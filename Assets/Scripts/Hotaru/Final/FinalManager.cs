using Fusion;
using UnityEngine;
using UnityEngine.Playables;
using System.Collections;
using System.Collections.Generic;

public enum FinalPhaseState
{
    WaitingPlayers,
    PreCutscene1Teleport,
    Cutscene1,
    Battle,
    PostBattleTeleport,
    Cutscene2,
    Complete
}

public class FinalManager : NetworkBehaviour
{
    #region Singleton
    public static FinalManager Instance { get; private set; }
    #endregion

    #region Inspector References
    [Header("Spawn Points")]
    [SerializeField] private Transform winnerSpawnPoint;
    [SerializeField] private Transform[] loserSpawnPoints; // 3 điểm cho nhóm 3-4 người battle
    [SerializeField] private Transform cageTransform;
    [SerializeField] private Transform thronePosition;
    [SerializeField] private Transform besideThroneP2Position;
    [SerializeField] private Transform behindP2P3Position;

    [Header("Cutscenes")]
    // GameObject cha chua PlayableDirector - bat Play On Awake tren Director, o day chi
    // SetActive(true) de kich hoat Play (thay vi goi Director.Play() truc tiep).
    // Object nay phai duoc de INACTIVE san trong scene tu dau.
    [SerializeField] private GameObject cutscene1Root;
    [SerializeField] private PlayableDirector cutscene1Director; // dung de doc duration
    [SerializeField] private GameObject cutscene2Root;
    [SerializeField] private PlayableDirector cutscene2Director; // dung de doc duration

    [Header("Cameras")]
    [SerializeField] private Camera cutsceneCamera;      // camera riêng cho Timeline, tắt sau khi cutscene xong
    [SerializeField] private Camera playableSharedCamera; // camera chung, 1 góc nhìn cho toàn bộ player

    [Header("Timing")]
    [SerializeField] private float clientReadyTimeout = 8f;

    [Header("Final Scene UI")]
    // UI riêng của Final Scene (vd: result panel, rank display...).
    // Mặc định set Inactive sẵn trong scene; ở đây chỉ đảm bảo tắt đề phòng quên set,
    // và sẽ được bật lại (ShowFinalSceneUI) sau khi cutscene chạy xong.
    [SerializeField] private GameObject[] finalSceneUIRoots;

    [Header("Debug")]
    [SerializeField] private bool showDebugPanel = true;
    #endregion

    #region Networked State
    [Networked, OnChangedRender(nameof(OnFinalPhaseChanged))]
    public FinalPhaseState Phase { get; private set; } = FinalPhaseState.WaitingPlayers;
    #endregion

    #region Local State (host-only bookkeeping)
    private int _expectedPlayerCount = 0;
    private bool _advanceStarted = false;

    // Phase D/E - Battle & Elimination
    // Thu tu bi loai: nguoi thua dau tien (index 0) = rank thap nhat.
    private List<int> _eliminationOrder = new List<int>();
    // Ket qua rank cuoi cung sau battle: index0 = rank1 (winner) ... rank cuoi = rank thap nhat.
    // Duoc FinalizeBattleRanking() dien day du, dung o Phase F (teleport theo rank) va buoc luu ket qua.
    private List<int> _finalRanking = new List<int>();
    #endregion

    #region Lifecycle
    public override void Spawned()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        Debug.Log($"[FinalManager] Spawned. HasStateAuthority: {HasStateAuthority}");

        // Room UI (lobby/voting/scoreboard/...) đã được GameManager.HandleFinalState tự ẩn
        // khi load scene này (LoadScene sẽ dọn sạch UI cũ). Ở đây chỉ cần đảm bảo UI riêng
        // của Final Scene (result panel, rank display...) tắt sẵn phòng khi quên set trong scene.
        HideFinalSceneUI();

        // 2 camera setup: cutscene camera tắt sẵn, playable shared camera cũng tắt sẵn
        // (sẽ bật cutsceneCamera lên ngay trước khi Play Timeline ở Phase C).
        if (cutsceneCamera != null) cutsceneCamera.gameObject.SetActive(false);
        if (playableSharedCamera != null) playableSharedCamera.gameObject.SetActive(false);

        // Cutscene root phai tat san trong scene (Play On Awake se tu chay khi active len).
        // O day chi de phong hoa neu quen tat trong Editor.
        if (cutscene1Root != null) cutscene1Root.SetActive(false);
        if (cutscene2Root != null) cutscene2Root.SetActive(false);

        if (HasStateAuthority)
        {
            _expectedPlayerCount = CountActivePlayers();
            StartCoroutine(WaitForAllClientsReadyThenAdvance());
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private int CountActivePlayers()
    {
        int count = 0;
        foreach (var p in Runner.ActivePlayers) count++;
        return count;
    }
    #endregion

    #region Final Scene UI Helpers
    private void HideFinalSceneUI()
    {
        if (finalSceneUIRoots == null) return;
        foreach (var ui in finalSceneUIRoots)
        {
            if (ui != null) ui.SetActive(false);
        }
    }

    // Gọi sau khi cutscene (1 hoặc 2, tuỳ case) chạy xong — sẽ wire vào ở Phase C/F.
    private void ShowFinalSceneUI()
    {
        if (finalSceneUIRoots == null) return;
        foreach (var ui in finalSceneUIRoots)
        {
            if (ui != null) ui.SetActive(true);
        }
    }
    #endregion

    #region Phase A - Waiting Players
    // Gia lap doi player ket noi: doi han 8 giay roi bat dau, khong check ready/spawn nua
    // (logic check ready RPC + PlayerController count truoc day chua on dinh - se quay lai sau).
    private IEnumerator WaitForAllClientsReadyThenAdvance()
    {
        if (_advanceStarted) yield break;
        _advanceStarted = true;

        Debug.Log($"[FinalManager] Gia lap doi player ket noi trong {clientReadyTimeout}s...");
        yield return new WaitForSeconds(clientReadyTimeout);

        Debug.Log("[FinalManager] Het thoi gian cho - bat dau Phase B.");

        AdvanceToPhase(FinalPhaseState.PreCutscene1Teleport);
    }
    #endregion

    #region Phase Transition Helper
    private void AdvanceToPhase(FinalPhaseState newPhase)
    {
        if (!HasStateAuthority) return;

        Debug.Log($"[FinalManager] Phase: {Phase} -> {newPhase}");
        Phase = newPhase;
    }

    private void OnFinalPhaseChanged()
    {
        Debug.Log($"[FinalManager] Phase changed (render) -> {Phase}");

        switch (Phase)
        {
            case FinalPhaseState.PreCutscene1Teleport:
                if (HasStateAuthority) TeleportBeforeCutscene1();
                break;
            case FinalPhaseState.Cutscene1:
                if (HasStateAuthority) PlayCutscene1();
                break;
            case FinalPhaseState.Battle:
                if (HasStateAuthority) OnEnterBattlePhase();
                break;
            case FinalPhaseState.PostBattleTeleport:
                if (HasStateAuthority) TeleportPostBattle();
                break;
            case FinalPhaseState.Cutscene2:
                // TODO: Phase F
                break;
            case FinalPhaseState.Complete:
                if (HasStateAuthority) OnEnterCompletePhase();
                break;
        }
    }
    #endregion

    #region Phase B - Teleport truoc Cutscene 1
    private void TeleportBeforeCutscene1()
    {
        int winnerId = GameManager.Instance != null ? GameManager.Instance.FinalWinnerId : -1;
        if (winnerId < 0)
        {
            Debug.LogError("[FinalManager] FinalWinnerId khong hop le (-1) - khong the teleport.");
            return;
        }

        var players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);

        // Sort theo playerId de thu tu deterministic giua host/client (giong pattern BaseMinigameController).
        System.Array.Sort(players, (a, b) =>
            a.Object.InputAuthority.PlayerId.CompareTo(b.Object.InputAuthority.PlayerId));

        PlayerController winnerController = null;
        var losers = new List<PlayerController>();

        foreach (var p in players)
        {
            int pid = p.Object.InputAuthority.PlayerId;
            if (pid == winnerId) winnerController = p;
            else losers.Add(p);
        }

        if (winnerController == null)
        {
            Debug.LogError($"[FinalManager] Khong tim thay PlayerController cho winnerId={winnerId}.");
            return;
        }

        int totalPlayers = players.Length;

        var playerIds = new List<int>();
        var positions = new List<Vector3>();
        var rotations = new List<Quaternion>();

        // Winner luon vao winnerSpawnPoint.
        if (winnerSpawnPoint != null)
        {
            ApplyTeleport(winnerController, winnerSpawnPoint.position, winnerSpawnPoint.rotation);
            playerIds.Add(winnerId);
            positions.Add(winnerSpawnPoint.position);
            rotations.Add(winnerSpawnPoint.rotation);
        }
        else
        {
            Debug.LogError("[FinalManager] winnerSpawnPoint chua duoc gan trong Inspector!");
        }

        if (totalPlayers == 2)
        {
            // 2 nguoi: nguoi con lai vao thang cage.
            if (losers.Count > 0 && cageTransform != null)
            {
                var loser = losers[0];
                int lid = loser.Object.InputAuthority.PlayerId;

                ApplyTeleport(loser, cageTransform.position, cageTransform.rotation);
                playerIds.Add(lid);
                positions.Add(cageTransform.position);
                rotations.Add(cageTransform.rotation);
            }
            else if (cageTransform == null)
            {
                Debug.LogError("[FinalManager] cageTransform chua duoc gan trong Inspector!");
            }
        }
        else
        {
            // 3-4 nguoi: nhung nguoi con lai vao loserSpawnPoints[0..n], khong ai vao long.
            if (loserSpawnPoints == null || loserSpawnPoints.Length < losers.Count)
            {
                Debug.LogError($"[FinalManager] loserSpawnPoints khong du ({(loserSpawnPoints == null ? 0 : loserSpawnPoints.Length)}) cho {losers.Count} nguoi choi.");
            }

            for (int i = 0; i < losers.Count; i++)
            {
                if (loserSpawnPoints == null || i >= loserSpawnPoints.Length || loserSpawnPoints[i] == null)
                    continue;

                var loser = losers[i];
                int lid = loser.Object.InputAuthority.PlayerId;
                var sp = loserSpawnPoints[i];

                ApplyTeleport(loser, sp.position, sp.rotation);
                playerIds.Add(lid);
                positions.Add(sp.position);
                rotations.Add(sp.rotation);
            }
        }

        RPC_SyncFinalTeleport(playerIds.ToArray(), positions.ToArray(), rotations.ToArray());

        Debug.Log($"[FinalManager] Phase B teleport hoan tat cho {playerIds.Count}/{totalPlayers} players.");

        // Teleport xong -> sang Phase C (Cutscene 1). Se code cu the o buoc sau.
        AdvanceToPhase(FinalPhaseState.Cutscene1);
    }

    // Host tu di chuyen truoc (giong pattern TeleportPlayersToSpawnPoints trong BaseMinigameController).
    private void ApplyTeleport(PlayerController player, Vector3 position, Quaternion rotation)
    {
        if (player == null) return;

        player.Teleport(position);
        player.transform.rotation = rotation;
    }

    // Dong bo teleport xuong tat ca client, cung pattern voi RPC_SyncSpawnPositions.
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_SyncFinalTeleport(int[] playerIds, Vector3[] positions, Quaternion[] rotations)
    {
        var players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);

        for (int i = 0; i < playerIds.Length; i++)
        {
            foreach (var player in players)
            {
                if (player.Object.InputAuthority.PlayerId != playerIds[i]) continue;

                var cc = player.GetComponent<CharacterController>();
                if (cc != null)
                {
                    cc.enabled = false;
                    player.transform.position = positions[i];
                    player.transform.rotation = rotations[i];
                    cc.enabled = true;
                }
                else
                {
                    player.transform.position = positions[i];
                    player.transform.rotation = rotations[i];
                }
                break;
            }
        }
    }
    #endregion

    #region Phase C - Cutscene 1
    private void PlayCutscene1()
    {
        RPC_PlayCutscene1();
        StartCoroutine(WaitCutscene1ThenBranch());
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayCutscene1()
    {
        // Camera: bat cutscene camera, tat shared camera (dung 1 goc nhin duy nhat trong luc Play).
        if (cutsceneCamera != null) cutsceneCamera.gameObject.SetActive(true);
        if (playableSharedCamera != null) playableSharedCamera.gameObject.SetActive(false);

        // Khoa input toan bo player trong luc cutscene.
        var players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        foreach (var p in players)
            p.SetFrozen(true);

        if (cutscene1Root != null)
            cutscene1Root.SetActive(true); // Play On Awake tren Director se tu Play khi object active len
        else
            Debug.LogError("[FinalManager] cutscene1Root chua duoc gan trong Inspector!");
    }

    private IEnumerator WaitCutscene1ThenBranch()
    {
        float duration = (cutscene1Director != null) ? (float)cutscene1Director.duration : 0f;
        if (duration > 0f)
            yield return new WaitForSeconds(duration);

        RPC_OnCutscene1Finished();

        int totalPlayers = CountActivePlayers();

        if (totalPlayers <= 2)
        {
            // 2 nguoi: ket thuc Final flow ngay sau cutscene 1.
            AdvanceToPhase(FinalPhaseState.Complete);
        }
        else
        {
            // 3-4 nguoi: mo input cho nhom con lai bat dau battle.
            AdvanceToPhase(FinalPhaseState.Battle);
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_OnCutscene1Finished()
    {
        // Cutscene camera tat lien sau khi chay xong -> chuyen qua shared playable camera.
        if (cutsceneCamera != null) cutsceneCamera.gameObject.SetActive(false);
        if (playableSharedCamera != null) playableSharedCamera.gameObject.SetActive(true);
    }
    #endregion

    #region Phase D - Enter Battle (mo input cho nhom battle)
    private void OnEnterBattlePhase()
    {
        int winnerId = GameManager.Instance != null ? GameManager.Instance.FinalWinnerId : -1;

        var players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        var battlerIds = new List<int>();
        foreach (var p in players)
        {
            int pid = p.Object.InputAuthority.PlayerId;
            if (pid != winnerId) battlerIds.Add(pid);
        }

        RPC_UnlockBattleInput(battlerIds.ToArray());
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_UnlockBattleInput(int[] battlerIds)
    {
        var players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        foreach (var p in players)
        {
            int pid = p.Object.InputAuthority.PlayerId;
            bool isBattler = System.Array.IndexOf(battlerIds, pid) >= 0;
            // Battler duoc mo input; winner (dang o thronePosition) giu nguyen frozen.
            p.SetFrozen(!isBattler);
        }

        // Cutscene xong, battle bat dau -> hien UI rieng cua Final Scene (neu co, vd HUD battle).
        ShowFinalSceneUI();
    }
    #endregion

    #region Phase D/E - Battle & Elimination API
    // ============================================================
    // TODO (chua co logic battle thuc te - se lam sau):
    // Battle system that tries de xac dinh top2-3-4 CHUA duoc thiet ke.
    // O day chi chuan bi san API de sau nay code battle goi vao:
    //   - FinalManager.Instance.ReportPlayerEliminated(playerId) moi khi
    //     1 nguoi trong nhom battle bi loai.
    //   - Khi da du so nguoi bi loai (= tong battler - 1), tu dong finalize
    //     ranking va chuyen sang Phase PostBattleTeleport.
    // Battle co the la minigame rieng, PvP, hay bat cu co che nao -
    // chi can goi dung ham nay theo dung thu tu loai la du.
    // ============================================================

    /// <summary>
    /// Goi khi 1 player trong nhom battle bi loai. Host-only.
    /// Thu tu goi ham nay quyet dinh rank: goi som nhat = rank thap nhat.
    /// </summary>
    public void ReportPlayerEliminated(int playerId)
    {
        if (!HasStateAuthority) return;
        if (Phase != FinalPhaseState.Battle)
        {
            Debug.LogWarning($"[FinalManager] ReportPlayerEliminated goi sai phase ({Phase}) - bo qua.");
            return;
        }
        if (_eliminationOrder.Contains(playerId))
        {
            Debug.LogWarning($"[FinalManager] Player {playerId} da duoc bao eliminated truoc do - bo qua.");
            return;
        }

        _eliminationOrder.Add(playerId);
        Debug.Log($"[FinalManager] Player {playerId} eliminated. Thu tu hien tai: [{string.Join(", ", _eliminationOrder)}]");

        CheckBattleCompletion();
    }

    private void CheckBattleCompletion()
    {
        int totalPlayers = CountActivePlayers();
        int totalBattlers = totalPlayers - 1; // tru winner
        int neededEliminations = totalBattlers - 1; // con lai 1 nguoi song sot cuoi = top2

        if (_eliminationOrder.Count >= neededEliminations)
        {
            FinalizeBattleRanking();
        }
    }

    /// <summary>
    /// Tinh ranking cuoi cung tu _eliminationOrder va chuyen sang Phase PostBattleTeleport.
    /// Case 4 nguoi (3 battler): elim[0]=top4, elim[1]=top3, nguoi con song=top2.
    /// Case 3 nguoi (2 battler): elim[0]=top3 (vao thang cage), nguoi con song=top2.
    /// Cong thuc chung: rank = [winner, survivor, ...elimination order dao nguoc].
    /// </summary>
    private void FinalizeBattleRanking()
    {
        int winnerId = GameManager.Instance != null ? GameManager.Instance.FinalWinnerId : -1;

        var players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        System.Array.Sort(players, (a, b) =>
            a.Object.InputAuthority.PlayerId.CompareTo(b.Object.InputAuthority.PlayerId));

        int survivorId = -1;
        foreach (var p in players)
        {
            int pid = p.Object.InputAuthority.PlayerId;
            if (pid == winnerId) continue;
            if (!_eliminationOrder.Contains(pid))
            {
                survivorId = pid;
                break;
            }
        }

        if (survivorId < 0)
        {
            Debug.LogError("[FinalManager] Khong xac dinh duoc survivor (top2) - kiem tra lai so lan ReportPlayerEliminated.");
            return;
        }

        _finalRanking.Clear();
        _finalRanking.Add(winnerId);   // rank1
        _finalRanking.Add(survivorId); // rank2

        // Nguoi bi loai cuoi cung = rank3, nguoi bi loai dau tien = rank4 (neu co).
        for (int i = _eliminationOrder.Count - 1; i >= 0; i--)
            _finalRanking.Add(_eliminationOrder[i]);

        Debug.Log($"[FinalManager] Battle ranking finalized: [{string.Join(", ", _finalRanking)}] (rank1..rank{_finalRanking.Count})");

        AdvanceToPhase(FinalPhaseState.PostBattleTeleport);
    }
    #endregion

    #region Debug (chi de test rank battle thu cong, chua co battle logic thuc)
    private void OnGUI()
    {
        if (!showDebugPanel || !HasStateAuthority) return;
        if (Phase != FinalPhaseState.Battle) return;

        GUILayout.BeginArea(new Rect(10, 10, 260, 300));
        GUILayout.Label("[DEBUG] Final Battle - Force Eliminate:");

        var players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        System.Array.Sort(players, (a, b) =>
            a.Object.InputAuthority.PlayerId.CompareTo(b.Object.InputAuthority.PlayerId));

        int winnerId = GameManager.Instance != null ? GameManager.Instance.FinalWinnerId : -1;

        foreach (var p in players)
        {
            int pid = p.Object.InputAuthority.PlayerId;
            if (pid == winnerId) continue; // winner khong battle
            if (_eliminationOrder.Contains(pid)) continue; // da bi loai roi

            if (GUILayout.Button($"Eliminate P{pid}"))
            {
                ReportPlayerEliminated(pid);
            }
        }

        GUILayout.Space(6);
        GUILayout.Label($"Elimination order: [{string.Join(", ", _eliminationOrder)}]");

        GUILayout.EndArea();
    }
    #endregion

    #region Phase F - Teleport theo rank + Cutscene 2
    // Ca 2 case 3 nguoi va 4 nguoi deu chay cutscene 2 sau khi teleport xong.
    // Chi case 2 nguoi la khong vao day (da Complete ngay sau cutscene 1).
    private void TeleportPostBattle()
    {
        int totalPlayers = CountActivePlayers();

        var playerIds = new List<int>();
        var positions = new List<Vector3>();
        var rotations = new List<Quaternion>();

        // rank2 (index 1) luon dung canh top1.
        if (_finalRanking.Count >= 2)
        {
            if (besideThroneP2Position != null)
                ApplyTeleportToController(_finalRanking[1], besideThroneP2Position.position, besideThroneP2Position.rotation, playerIds, positions, rotations);
            else
                Debug.LogError("[FinalManager] besideThroneP2Position chua duoc gan trong Inspector!");
        }

        if (totalPlayers == 4)
        {
            // rank3 -> behind P2/P3, rank4 -> cage.
            if (_finalRanking.Count >= 3)
            {
                if (behindP2P3Position != null)
                    ApplyTeleportToController(_finalRanking[2], behindP2P3Position.position, behindP2P3Position.rotation, playerIds, positions, rotations);
                else
                    Debug.LogError("[FinalManager] behindP2P3Position chua duoc gan trong Inspector!");
            }

            if (_finalRanking.Count >= 4)
            {
                if (cageTransform != null)
                    ApplyTeleportToController(_finalRanking[3], cageTransform.position, cageTransform.rotation, playerIds, positions, rotations);
                else
                    Debug.LogError("[FinalManager] cageTransform chua duoc gan trong Inspector!");
            }
        }
        else // totalPlayers == 3
        {
            // rank3 -> vao cage.
            if (_finalRanking.Count >= 3)
            {
                if (cageTransform != null)
                    ApplyTeleportToController(_finalRanking[2], cageTransform.position, cageTransform.rotation, playerIds, positions, rotations);
                else
                    Debug.LogError("[FinalManager] cageTransform chua duoc gan trong Inspector!");
            }
        }

        RPC_SyncFinalTeleport(playerIds.ToArray(), positions.ToArray(), rotations.ToArray());

        Debug.Log($"[FinalManager] Phase F teleport hoan tat theo ranking: [{string.Join(", ", _finalRanking)}]. Chuan bi Play cutscene 2.");

        StartCoroutine(PlayCutscene2ThenComplete());
    }

    // Host tim PlayerController theo playerId, teleport ngay tren host va gom vao list de sync sau.
    private void ApplyTeleportToController(int playerId, Vector3 position, Quaternion rotation,
        List<int> outIds, List<Vector3> outPositions, List<Quaternion> outRotations)
    {
        var players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        foreach (var p in players)
        {
            if (p.Object.InputAuthority.PlayerId != playerId) continue;

            ApplyTeleport(p, position, rotation);
            outIds.Add(playerId);
            outPositions.Add(position);
            outRotations.Add(rotation);
            return;
        }

        Debug.LogWarning($"[FinalManager] Khong tim thay PlayerController cho playerId={playerId} khi teleport Phase F.");
    }

    private IEnumerator PlayCutscene2ThenComplete()
    {
        RPC_PlayCutscene2();

        float duration = (cutscene2Director != null) ? (float)cutscene2Director.duration : 0f;
        if (duration > 0f)
            yield return new WaitForSeconds(duration);

        RPC_OnCutscene2Finished();

        AdvanceToPhase(FinalPhaseState.Complete);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayCutscene2()
    {
        if (cutsceneCamera != null) cutsceneCamera.gameObject.SetActive(true);
        if (playableSharedCamera != null) playableSharedCamera.gameObject.SetActive(false);

        var players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        foreach (var p in players)
            p.SetFrozen(true);

        if (cutscene2Root != null)
            cutscene2Root.SetActive(true);
        else
            Debug.LogError("[FinalManager] cutscene2Root chua duoc gan trong Inspector!");
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_OnCutscene2Finished()
    {
        if (cutsceneCamera != null) cutsceneCamera.gameObject.SetActive(false);
        if (playableSharedCamera != null) playableSharedCamera.gameObject.SetActive(true);
    }
    #endregion

    #region Phase Complete (tam thoi - se hoan thien o buoc luu ket qua)
    private void OnEnterCompletePhase()
    {
        // TODO (buoc sau): luu FinalRank qua GameManager.SaveFinalRankings() roi chuyen GameState.Result.
        Debug.Log("[FinalManager] Phase Complete - se hoan thien logic luu ket qua + chuyen Result o buoc sau.");
    }
    #endregion
}