using Fusion;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public enum FinalPhaseState
{
    WaitingPlayers,
    PreCutscene1Teleport,
    Cutscene1,
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
    [SerializeField] private Transform[] cageSpawnPoints; // Tat ca nguoi thua (khong phai top1) vao day, moi nguoi 1 diem rieng de khong dinh collider

    [Header("Cutscene")]
    // Cutscene giờ chạy hoàn toàn bằng code (FinalCutsceneController), không dùng Timeline nữa.
    // Camera trong FinalCutsceneController sẽ ở lại làm gameplay camera luôn sau khi cutscene xong,
    // nên FinalManager không cần quản lý việc bật/tắt camera nữa.
    [SerializeField] private FinalCutsceneController finalCutsceneController;

    [Header("Timing")]
    [SerializeField] private float clientReadyTimeout = 8f;

    [Header("Final Scene UI")]
    // UI riêng của Final Scene (vd: result panel, rank display...).
    // Mặc định set Inactive sẵn trong scene; ở đây chỉ đảm bảo tắt đề phòng quên set,
    // và sẽ được bật lại (ShowFinalSceneUI) sau khi cutscene chạy xong.
    [SerializeField] private GameObject[] finalSceneUIRoots;
    #endregion

    #region Networked State
    [Networked, OnChangedRender(nameof(OnFinalPhaseChanged))]
    public FinalPhaseState Phase { get; private set; } = FinalPhaseState.WaitingPlayers;
    #endregion

    #region Local State (host-only bookkeeping)
    private bool _advanceStarted = false;
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

        if (HasStateAuthority)
        {
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

    // Gọi sau khi cutscene chạy xong.
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

        // Tat ca nguoi con lai (khong phan biet 1, 2 hay 3 nguoi) deu vao cage, moi nguoi 1 diem
        // rieng trong cageSpawnPoints de tranh spawn chong len nhau bi day collider.
        if (cageSpawnPoints == null || cageSpawnPoints.Length < losers.Count)
        {
            Debug.LogError($"[FinalManager] cageSpawnPoints khong du ({(cageSpawnPoints == null ? 0 : cageSpawnPoints.Length)}) cho {losers.Count} nguoi choi.");
        }

        for (int i = 0; i < losers.Count; i++)
        {
            if (cageSpawnPoints == null || i >= cageSpawnPoints.Length || cageSpawnPoints[i] == null)
                continue;

            var loser = losers[i];
            int lid = loser.Object.InputAuthority.PlayerId;
            var sp = cageSpawnPoints[i];

            ApplyTeleport(loser, sp.position, sp.rotation);
            playerIds.Add(lid);
            positions.Add(sp.position);
            rotations.Add(sp.rotation);
        }

        RPC_SyncFinalTeleport(playerIds.ToArray(), positions.ToArray(), rotations.ToArray());

        Debug.Log($"[FinalManager] Phase B teleport hoan tat cho {playerIds.Count}/{players.Length} players.");

        // Teleport xong -> sang Phase C (Cutscene 1).
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
        StartCoroutine(WaitCutscene1ThenComplete());
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayCutscene1()
    {
        // Khoa input toan bo player trong luc cutscene.
        var players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        foreach (var p in players)
            p.SetFrozen(true);

        // Moi client tu chay coroutine cutscene cua rieng minh (camera la local, khong can sync
        // vi tri tung frame qua network - chi can trigger dong loat qua RPC nay la du).
        if (finalCutsceneController != null)
            StartCoroutine(finalCutsceneController.PlayCutscene());
        else
            Debug.LogError("[FinalManager] finalCutsceneController chua duoc gan trong Inspector!");
    }

    private IEnumerator WaitCutscene1ThenComplete()
    {
        float duration = (finalCutsceneController != null) ? finalCutsceneController.TotalDuration : 0f;
        if (duration > 0f)
            yield return new WaitForSeconds(duration);

        RPC_OnCutscene1Finished();

        AdvanceToPhase(FinalPhaseState.Complete);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_OnCutscene1Finished()
    {
        // Camera cua FinalCutsceneController da o san vi tri cuoi (finalCameraPoint) va o lai lam
        // gameplay camera - khong can bat/tat camera nao khac o day nua.

        // Mo lai input cho tat ca player - di chuyen binh thuong.
        var players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        foreach (var p in players)
            p.SetFrozen(false);

        // QUAN TRONG: GameState.Final khong di qua HandlePlayingState/HandleRouletteState nen
        // PlayerInputHandler.InputEnabled van dang giu gia tri false tu state truoc do (Board/
        // Scoreboard...). Neu khong bat lai o day thi player se bi SetFrozen(false) nhung van
        // khong nhan duoc input gi de di chuyen. Bat lai giong pattern cac state khac.
        if (PlayerInputHandler.Instance != null)
            PlayerInputHandler.Instance.InputEnabled = true;

        if (CameraManager.Instance != null)
            CameraManager.Instance.SetCameraRotationLocked(false);

        if (CursorManager.Instance != null)
            CursorManager.Instance.HideCursor();

        // Hien UI rieng cua Final Scene sau khi cutscene chay xong.
        ShowFinalSceneUI();
    }
    #endregion

    #region Phase Complete
    private void OnEnterCompletePhase()
    {
        if (!HasStateAuthority) return;

        int winnerId = GameManager.Instance != null ? GameManager.Instance.FinalWinnerId : -1;
        if (winnerId < 0)
        {
            Debug.LogError("[FinalManager] OnEnterCompletePhase: FinalWinnerId khong hop le - khong the luu ket qua.");
            return;
        }

        if (GameManager.Instance != null)
        {
            // Chi luu top1 (winner). Khong con battle nen khong the xac dinh rank2/3/4.
            GameManager.Instance.SaveFinalRankings(new[] { winnerId });
            Debug.Log($"[FinalManager] Da luu final rankings (chi winner): {winnerId}");
        }
        else
        {
            Debug.LogError("[FinalManager] GameManager.Instance null - khong the SaveFinalRankings.");
        }

        // TODO (buoc sau, ngoai pham vi "nen"): chuyen GameState.Result.
    }
    #endregion
}