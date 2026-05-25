using Fusion;
using UnityEngine;
using System.Collections;

public enum BoardPhaseState
{
    Idle,
    WaitingForRoll,
    Rolling,
    Moving,
    ResolvingTile,
    WaitingForTargetSelect,
    NextTurn,
    BoardComplete
}

/// <summary>
/// Quản lý toàn bộ board phase — NetworkBehaviour.
/// Đặt như pre-spawned NetworkObject trong BoardScene (tương tự GameManager).
/// Host điều khiển state machine; clients nhận animation qua RPC.
///
/// Turn order: lấy từ GameManager.GetLastMinigameRanking()
///   slot 0 = rank 1 (người thắng minigame) — đi trước
///   slot 3 = rank 4 — đi sau
///
/// Thứ tự mỗi lượt board:
///   WaitingForRoll -> (client roll) -> Rolling -> Moving -> ResolvingTile -> NextTurn -> ...
///   Khi tất cả players đã đi xong: BoardComplete -> GameManager.ProceedFromBoard()
/// </summary>
public class BoardManager : NetworkBehaviour
{
    public static BoardManager Instance { get; private set; }

    // =====================================================================
    // INSPECTOR
    // =====================================================================

    [Header("References")]
    [SerializeField] private BoardDice dice;
    [Tooltip("ScriptableObject chứa danh sách items cho Item/Jackpot tiles")]
    [SerializeField] private ItemPool itemPool;

    [Header("Tokens — pre-place 4 token trong BoardScene, set playerSlotIndex 0-3")]
    [SerializeField] private BoardPlayerToken[] tokens = new BoardPlayerToken[4];

    [Header("Tile Resolve")]
    [Tooltip("Thời gian hiển thị debug tile message (giây)")]
    [SerializeField] private float tileResolveDuration = 1.5f;

    [Header("Debug")]
    [SerializeField] private bool showDebugPanel = true;

    // =====================================================================
    // NETWORKED STATE
    // host là nguồn sự thật; clients chỉ đọc + nhận RPC
    // =====================================================================

    [Networked, OnChangedRender(nameof(OnBoardStateChanged))]
    public BoardPhaseState BoardState { get; private set; } = BoardPhaseState.Idle;

    [Networked] public int  ActivePlayerCount { get; private set; } = 0;
    [Networked] public int  CurrentTurnIndex  { get; private set; } = 0;
    [Networked] public NetworkBool IsReversed { get; private set; } = false;

    // TurnOrder: PlayerId cho 4 slot cố định (-1 = slot trống)
    [Networked] public int TurnSlot0 { get; private set; } = -1;
    [Networked] public int TurnSlot1 { get; private set; } = -1;
    [Networked] public int TurnSlot2 { get; private set; } = -1;
    [Networked] public int TurnSlot3 { get; private set; } = -1;

    // Vị trí hiện tại (nodeID) của từng slot
    [Networked] public int NodeSlot0 { get; private set; } = 0;
    [Networked] public int NodeSlot1 { get; private set; } = 0;
    [Networked] public int NodeSlot2 { get; private set; } = 0;
    [Networked] public int NodeSlot3 { get; private set; } = 0;

    // Skip flag (true = player này bị bỏ lượt trong board round hiện tại)
    [Networked] public NetworkBool Skip0 { get; private set; } = false;
    [Networked] public NetworkBool Skip1 { get; private set; } = false;
    [Networked] public NetworkBool Skip2 { get; private set; } = false;
    [Networked] public NetworkBool Skip3 { get; private set; } = false;

    // Steal phase — PlayerId của người đang steal (-1 = không ai)
    [Networked] public int StealerPlayerId { get; private set; } = -1;

    // =====================================================================
    // LOCAL STATE (chỉ có nghĩa trên host)
    // =====================================================================

    private int  _completedThisRound  = 0;

    // Chỉ có nghĩa trên local client — true khi đến lượt mình roll
    private bool _waitingForMyRoll    = false;

    // Steal coordination — chỉ có nghĩa trên host
    private int  _stealPendingTargetId = -1;

    // Steal target selection UI — local client (chỉ stealer thấy)
    private bool _waitingForMyStealTarget = false;
    private System.Collections.Generic.List<int> _eligibleStealTargets = new();

    // Debug tile message (tất cả clients)
    private string _lastTileMessage      = "";
    private float  _lastTileMessageTimer = 0f;

    // Reaction FX — local client
    private string _reactionLine  = "";
    private float  _reactionTimer = 0f;

    // =====================================================================
    // EVENTS
    // =====================================================================

    public System.Action<int> OnTurnStarted;    // PlayerId đến lượt
    public System.Action       OnBoardPhaseComplete;

    // =====================================================================
    // HELPERS — slot accessor
    // =====================================================================

    public int GetPlayerIDAtSlot(int slot) => slot switch
    {
        0 => TurnSlot0, 1 => TurnSlot1, 2 => TurnSlot2, 3 => TurnSlot3, _ => -1
    };

    private void SetPlayerIDAtSlot(int slot, int id)
    {
        switch (slot)
        {
            case 0: TurnSlot0 = id; break;
            case 1: TurnSlot1 = id; break;
            case 2: TurnSlot2 = id; break;
            case 3: TurnSlot3 = id; break;
        }
    }

    public int GetNodeIDAtSlot(int slot) => slot switch
    {
        0 => NodeSlot0, 1 => NodeSlot1, 2 => NodeSlot2, 3 => NodeSlot3, _ => 0
    };

    private void SetNodeIDAtSlot(int slot, int nodeID)
    {
        switch (slot)
        {
            case 0: NodeSlot0 = nodeID; break;
            case 1: NodeSlot1 = nodeID; break;
            case 2: NodeSlot2 = nodeID; break;
            case 3: NodeSlot3 = nodeID; break;
        }
    }

    private bool GetSkipAtSlot(int slot) => slot switch
    {
        0 => Skip0, 1 => Skip1, 2 => Skip2, 3 => Skip3, _ => false
    };

    private void SetSkipAtSlot(int slot, bool v)
    {
        switch (slot)
        {
            case 0: Skip0 = v; break;
            case 1: Skip1 = v; break;
            case 2: Skip2 = v; break;
            case 3: Skip3 = v; break;
        }
    }

    /// <summary>PlayerId của player đang có lượt.</summary>
    public int CurrentPlayerID
    {
        get
        {
            if (ActivePlayerCount <= 0) return -1;
            return GetPlayerIDAtSlot(CurrentSlot);
        }
    }

    public int CurrentSlot =>
        ActivePlayerCount > 0 ? CurrentTurnIndex % ActivePlayerCount : 0;

    // =====================================================================
    // LIFECYCLE
    // =====================================================================

    public override void Spawned()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (dice == null)
            dice = FindFirstObjectByType<BoardDice>();

        Debug.Log("[BoardManager] Spawned");
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (_lastTileMessageTimer > 0f)
            _lastTileMessageTimer -= Time.deltaTime;
        if (_reactionTimer > 0f)
            _reactionTimer -= Time.deltaTime;
    }

    // =====================================================================
    // START BOARD PHASE
    // =====================================================================

    /// <summary>
    /// Gọi bởi BoardSceneController sau khi scene load hoàn tất.
    /// rankOrder: PlayerId theo rank 1 → N (đọc từ GameManager.GetLastMinigameRanking()).
    /// </summary>
    public void StartBoardPhase(int[] rankOrder)
    {
        if (!HasStateAuthority) return;

        int count = Mathf.Min(rankOrder.Length, 4);

        if (count == 0)
        {
            Debug.LogError("[BoardManager] StartBoardPhase called with 0 players! Aborting — check GameManager.OnBoardSceneReady().");
            return;
        }

        ActivePlayerCount  = count;
        CurrentTurnIndex   = 0;
        IsReversed         = false;
        _completedThisRound = 0;

        for (int i = 0; i < 4; i++)
        {
            SetPlayerIDAtSlot(i, i < count ? rankOrder[i] : -1);
            SetNodeIDAtSlot(i, 0);
            SetSkipAtSlot(i, false);
        }

        // Gửi RPC init tokens cho tất cả clients
        RPC_InitializeTokens(rankOrder, count);

        Debug.Log($"[BoardManager] Board phase started — players: {string.Join(", ", rankOrder)}");
        StartTurn();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_InitializeTokens(int[] playerIds, int count)
    {
        // Auto-find nếu tokens chưa được assign trong Inspector
        if (tokens == null || tokens.Length < 4 || tokens[0] == null)
        {
            var found = FindObjectsByType<BoardPlayerToken>(FindObjectsSortMode.None);
            tokens = new BoardPlayerToken[4];
            foreach (var t in found)
            {
                int s = t.playerSlotIndex;
                if (s >= 0 && s < 4) tokens[s] = t;
            }
        }

        for (int i = 0; i < count; i++)
        {
            if (tokens[i] != null)
                tokens[i].Initialize(playerIds[i], i, 0);
        }
    }

    // =====================================================================
    // TURN FLOW
    // =====================================================================

    private void StartTurn()
    {
        if (!HasStateAuthority) return;

        // Safety guard — không nên xảy ra, nhưng phòng ngừa
        if (_completedThisRound >= ActivePlayerCount)
        {
            CompleteBoardPhase();
            return;
        }

        int slot     = CurrentSlot;
        int playerId = GetPlayerIDAtSlot(slot);

        if (playerId < 0)
        {
            // Slot trống — advance luôn
            _completedThisRound++;
            AdvanceTurn();
            return;
        }

        // Xử lý skip
        if (GetSkipAtSlot(slot))
        {
            SetSkipAtSlot(slot, false);
            Debug.Log($"[BoardManager] Player {playerId} SKIPPED this turn");
            _completedThisRound++;

            if (_completedThisRound >= ActivePlayerCount)
                CompleteBoardPhase();
            else
                AdvanceTurn();
            return;
        }

        Debug.Log($"[BoardManager] StartTurn — slot={slot}, playerId={playerId}");
        BoardState = BoardPhaseState.WaitingForRoll;
        RPC_TurnStarted(playerId);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_TurnStarted(int playerId)
    {
        Debug.Log($"[BoardManager] >>> Lượt của Player {playerId}");
        OnTurnStarted?.Invoke(playerId);

        // Xác định có phải lượt của local player không
        // Dùng Runner.LocalPlayer thay vì PlayerNetworkData.Local vì object đó
        // có thể bị destroy khi BoardScene load (nằm ở scene khác)
        _waitingForMyRoll = false;
        if (Runner != null && Runner.LocalPlayer.PlayerId == playerId)
        {
            _waitingForMyRoll = true;
            Debug.Log("[BoardManager] IT'S MY TURN — Roll button enabled");
        }
    }

    // =====================================================================
    // DICE ROLL
    // =====================================================================

    /// <summary>
    /// Local player nhấn Roll.
    /// Gọi từ UI button hoặc debug OnGUI.
    /// </summary>
    public void RequestRoll()
    {
        if (!_waitingForMyRoll)                          return;
        if (BoardState != BoardPhaseState.WaitingForRoll) return;

        _waitingForMyRoll = false;

        int myId = PlayerNetworkData.Local != null
            ? PlayerNetworkData.Local.Object.InputAuthority.PlayerId
            : -1;

        RPC_SubmitRollRequest(myId);
    }

    // RpcSources.All: bất kỳ client nào cũng có thể gọi
    // RpcTargets.StateAuthority: chỉ host nhận và xử lý
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_SubmitRollRequest(int requestingPlayerId)
    {
        if (!HasStateAuthority) return;
        if (BoardState != BoardPhaseState.WaitingForRoll) return;

        if (requestingPlayerId != CurrentPlayerID)
        {
            Debug.LogWarning($"[BoardManager] Invalid roll from {requestingPlayerId}, expected {CurrentPlayerID}");
            return;
        }

        BoardState = BoardPhaseState.Rolling;
        int result = dice != null ? dice.Roll() : Random.Range(1, 7);

        Debug.Log($"[BoardManager] Player {requestingPlayerId} rolled {result}");
        RPC_ShowDiceResult(requestingPlayerId, result);

        StartCoroutine(ExecuteMovement(CurrentSlot, requestingPlayerId, result));
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ShowDiceResult(int playerId, int result)
    {
        // Phase 0: chỉ log. Phase 7 sẽ có animation xúc xắc.
        Debug.Log($"[BoardManager] Player {playerId} tung được: {result}");
    }

    // =====================================================================
    // MOVEMENT
    // =====================================================================

    private IEnumerator ExecuteMovement(int slot, int playerId, int steps)
    {
        BoardState = BoardPhaseState.Moving;

        var path = BoardNodePath.Instance;
        var currentNode = path?.GetNodeByID(GetNodeIDAtSlot(slot));

        if (currentNode == null || path == null)
        {
            Debug.LogWarning($"[BoardManager] Node not found for slot {slot}, skipping movement");
            yield return FinishTurn(slot, playerId, GetNodeIDAtSlot(slot), TileType.Empty);
            yield break;
        }

        var destination = path.GetNodeAfterSteps(currentNode, steps, out int[] pathIDs);

        if (pathIDs.Length == 0)
        {
            yield return FinishTurn(slot, playerId, GetNodeIDAtSlot(slot), currentNode.tileType);
            yield break;
        }

        int finalNodeID = pathIDs[pathIDs.Length - 1];

        // Cập nhật networked position
        SetNodeIDAtSlot(slot, finalNodeID);

        // Gửi animation tới tất cả clients
        RPC_AnimateMovement(slot, pathIDs);

        // Chờ animation xong (ước tính: số ô × thời gian mỗi ô + buffer)
        float waitTime = pathIDs.Length * (1f / 4f) + 0.6f; // moveSpeed=4
        yield return new WaitForSeconds(waitTime);

        yield return FinishTurn(slot, playerId, finalNodeID, destination.tileType);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_AnimateMovement(int slot, int[] pathNodeIDs)
    {
        if (tokens != null && slot < tokens.Length && tokens[slot] != null)
            tokens[slot].AnimateMovement(pathNodeIDs);
    }

    private IEnumerator FinishTurn(int slot, int playerId, int finalNodeID, TileType tileType)
    {
        BoardState = BoardPhaseState.ResolvingTile;

        if (tileType == TileType.Steal)
        {
            yield return HandleStealTile(playerId, finalNodeID);
        }
        else if (tileType == TileType.Toss)
        {
            ResolveToss(playerId);
            yield return new WaitForSeconds(tileResolveDuration);
        }
        else if (tileType == TileType.Shuffle)
        {
            ResolveShuffle(playerId);
            yield return new WaitForSeconds(tileResolveDuration);
        }
        else
        {
            // Item, Jackpot, Gamble, Empty — generic effect via BoardNode
            var node = BoardNodePath.Instance?.GetNodeByID(finalNodeID);
            if (node != null)
                node.CreateEffect(itemPool).Resolve(playerId);
            RPC_PlayerLanded(playerId, finalNodeID, tileType);
            yield return new WaitForSeconds(tileResolveDuration);
        }

        _completedThisRound++;

        if (_completedThisRound >= ActivePlayerCount)
            CompleteBoardPhase();
        else
            AdvanceTurn();
    }

    // =====================================================================
    // PHASE 3 — SOCIAL TILE HANDLERS (host only)
    // =====================================================================

    private IEnumerator HandleStealTile(int stealerId, int nodeID)
    {
        // Tìm targets eligible: có item, không phải stealer
        var eligibles = new System.Collections.Generic.List<int>();
        for (int i = 0; i < ActivePlayerCount; i++)
        {
            int pid = GetPlayerIDAtSlot(i);
            if (pid < 0 || pid == stealerId) continue;
            var inv = PlayerItemInventory.GetForPlayer(pid);
            if (inv != null && inv.GetItemCount() > 0)
                eligibles.Add(pid);
        }

        if (eligibles.Count == 0)
        {
            Debug.Log("[BoardManager] Steal: không có target nào có item.");
            RPC_PlayerLanded(stealerId, nodeID, TileType.Steal);
            yield return new WaitForSeconds(tileResolveDuration);
            yield break;
        }

        // Enter target selection state — chờ stealer chọn
        BoardState            = BoardPhaseState.WaitingForTargetSelect;
        StealerPlayerId       = stealerId;
        _stealPendingTargetId = -1;
        RPC_BeginTargetSelect(stealerId, eligibles.ToArray());

        float elapsed = 0f;
        while (_stealPendingTargetId == -1 && elapsed < 10f)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (_stealPendingTargetId == -1)
        {
            _stealPendingTargetId = eligibles[Random.Range(0, eligibles.Count)];
            Debug.Log($"[BoardManager] Steal timeout — auto chọn P{_stealPendingTargetId}");
        }

        // Thực hiện steal
        int targetId   = _stealPendingTargetId;
        var stealerInv = PlayerItemInventory.GetForPlayer(stealerId);
        var targetInv  = PlayerItemInventory.GetForPlayer(targetId);
        int stolenEffect = -1;

        if (stealerInv != null && targetInv != null && targetInv.GetItemCount() > 0)
        {
            var items  = targetInv.GetItems();
            var chosen = items[Random.Range(0, items.Count)];
            stolenEffect = (int)chosen;

            for (int s = 0; s < 8; s++)
            {
                if (targetInv.HeldItems.Get(s) == stolenEffect)
                {
                    targetInv.RemoveItem(s);
                    break;
                }
            }
            stealerInv.AddItem(chosen);
        }

        BoardState      = BoardPhaseState.ResolvingTile;
        StealerPlayerId = -1;
        RPC_StealResult(stealerId, targetId, stolenEffect);
        yield return new WaitForSeconds(tileResolveDuration);
    }

    private void ResolveToss(int playerId)
    {
        var inv = PlayerItemInventory.GetForPlayer(playerId);
        int lostEffect = -1;

        if (inv != null && inv.GetItemCount() > 0)
        {
            var items  = inv.GetItems();
            var chosen = items[Random.Range(0, items.Count)];
            lostEffect = (int)chosen;

            for (int s = 0; s < 8; s++)
            {
                if (inv.HeldItems.Get(s) == lostEffect)
                {
                    inv.RemoveItem(s);
                    break;
                }
            }
        }

        RPC_TossResult(playerId, lostEffect);
    }

    private void ResolveShuffle(int triggerPlayerId)
    {
        int victims = 0;
        for (int i = 0; i < ActivePlayerCount; i++)
        {
            int pid = GetPlayerIDAtSlot(i);
            if (pid < 0) continue;
            var inv = PlayerItemInventory.GetForPlayer(pid);
            if (inv == null || inv.GetItemCount() == 0) continue;

            var items  = inv.GetItems();
            var chosen = items[Random.Range(0, items.Count)];

            for (int s = 0; s < 8; s++)
            {
                if (inv.HeldItems.Get(s) == (int)chosen)
                {
                    inv.RemoveItem(s);
                    break;
                }
            }
            victims++;
        }

        RPC_ShuffleResult(triggerPlayerId, victims);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayerLanded(int playerId, int nodeID, TileType tileType)
    {
        string msg = GetTileDisplayMessage(tileType);
        Debug.Log($"[BoardManager] Player {playerId} đứng tại node {nodeID} [{tileType}]{(msg.Length > 0 ? ": " + msg : "")}");
        _lastTileMessage      = msg;
        _lastTileMessageTimer = tileResolveDuration;
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_BeginTargetSelect(int stealerId, int[] eligibles)
    {
        // Chỉ stealer mới thấy UI chọn target
        if (Runner != null && Runner.LocalPlayer.PlayerId == stealerId)
        {
            _waitingForMyStealTarget = true;
            _eligibleStealTargets    = new System.Collections.Generic.List<int>(eligibles);
        }
        Debug.Log($"[BoardManager] P{stealerId} đang chọn target để steal...");
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_SubmitTargetSelect(int stealerId, int targetId)
    {
        if (!HasStateAuthority) return;
        if (BoardState != BoardPhaseState.WaitingForTargetSelect) return;
        if (stealerId != StealerPlayerId) return;
        _stealPendingTargetId = targetId;
        Debug.Log($"[BoardManager] P{stealerId} chọn P{targetId}");
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_StealResult(int stealerId, int targetId, int itemEffect)
    {
        string itemName = itemEffect >= 0
            ? (ItemPool.Current?.GetByEffect((ItemEffect)itemEffect)?.itemName ?? ((ItemEffect)itemEffect).ToString())
            : "???";

        _lastTileMessage = itemEffect >= 0
            ? $"P{stealerId} STOLE {itemName} from P{targetId}!"
            : "STEAL: failed";
        _lastTileMessageTimer = tileResolveDuration;

        if (Runner != null)
        {
            int myId = Runner.LocalPlayer.PlayerId;
            if      (myId == stealerId) _reactionLine = "(>:D) STOLE IT!";
            else if (myId == targetId)  _reactionLine = "(T_T) MY ITEM...";
        }
        _reactionTimer = 2f;
        Debug.Log($"[BoardManager] {_lastTileMessage}");
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_TossResult(int playerId, int itemEffect)
    {
        string itemName = itemEffect >= 0
            ? (ItemPool.Current?.GetByEffect((ItemEffect)itemEffect)?.itemName ?? ((ItemEffect)itemEffect).ToString())
            : "nothing";

        _lastTileMessage = itemEffect >= 0
            ? $"P{playerId} lost {itemName}!"
            : $"P{playerId} TOSS: nothing to lose";
        _lastTileMessageTimer = tileResolveDuration;

        if (Runner != null && Runner.LocalPlayer.PlayerId == playerId)
            _reactionLine = "(T_T) DROPPED IT!";
        _reactionTimer = 2f;
        Debug.Log($"[BoardManager] {_lastTileMessage}");
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ShuffleResult(int triggerPlayerId, int victimCount)
    {
        _lastTileMessage = victimCount > 0
            ? $"SHUFFLE! {victimCount} player(s) lost items!"
            : "SHUFFLE! (no items to lose)";
        _lastTileMessageTimer = tileResolveDuration;
        _reactionLine  = "(*_*) CHAOS!";
        _reactionTimer = 2f;
        Debug.Log($"[BoardManager] {_lastTileMessage}");
    }

    private static string GetTileDisplayMessage(TileType type) => type switch
    {
        TileType.Empty   => "",
        TileType.Item    => "GOT ITEM!",
        TileType.Steal   => "STEAL!",
        TileType.Toss    => "TOSS ITEM!",
        TileType.Shuffle => "SHUFFLE!",
        TileType.Jackpot => "JACKPOT!",
        TileType.Gamble  => "GAMBLE!",
        _                => ""
    };

    private void AdvanceTurn()
    {
        if (!HasStateAuthority) return;

        BoardState = BoardPhaseState.NextTurn;

        if (IsReversed)
            CurrentTurnIndex = ((CurrentTurnIndex - 1) % ActivePlayerCount + ActivePlayerCount) % ActivePlayerCount;
        else
            CurrentTurnIndex = (CurrentTurnIndex + 1) % ActivePlayerCount;

        if (_completedThisRound < ActivePlayerCount)
            StartTurn();
        else
            CompleteBoardPhase();
    }

    // =====================================================================
    // BOARD COMPLETE
    // =====================================================================

    private void CompleteBoardPhase()
    {
        if (!HasStateAuthority) return;

        Debug.Log("[BoardManager] Board phase complete!");
        BoardState = BoardPhaseState.BoardComplete;

        RPC_BoardComplete();

        // Thông báo GameManager để chuyển sang state tiếp theo
        if (GameManager.Instance != null)
            GameManager.Instance.ProceedFromBoard();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_BoardComplete()
    {
        Debug.Log("[BoardManager] RPC: Board phase complete — mọi player đã đi xong");
        OnBoardPhaseComplete?.Invoke();
    }

    // =====================================================================
    // PUBLIC API — Phase 3+
    // =====================================================================

    /// <summary>Đánh dấu player bị skip lượt trong board round này.</summary>
    public void SkipPlayer(int playerId)
    {
        if (!HasStateAuthority) return;

        for (int i = 0; i < ActivePlayerCount; i++)
        {
            if (GetPlayerIDAtSlot(i) == playerId)
            {
                SetSkipAtSlot(i, true);
                Debug.Log($"[BoardManager] Player {playerId} sẽ bị skip lượt tiếp theo");
                return;
            }
        }
        Debug.LogWarning($"[BoardManager] Player {playerId} không tìm thấy trong TurnOrder để skip");
    }

    /// <summary>Đảo chiều vòng (Uno Reverse).</summary>
    public void ReverseOrder()
    {
        if (!HasStateAuthority) return;

        IsReversed = !IsReversed;
        Debug.Log($"[BoardManager] Chiều vòng: {(IsReversed ? "NGƯỢC" : "XUÔI")}");
    }

    // =====================================================================
    // CALLBACKS
    // =====================================================================

    private void OnBoardStateChanged()
    {
        Debug.Log($"[BoardManager] BoardState → {BoardState}");
    }

    // =====================================================================
    // DEBUG UI
    // =====================================================================

    private void OnGUI()
    {
        if (!showDebugPanel) return;

        float panelX = Screen.width - 235f;
        GUILayout.BeginArea(new Rect(panelX, 10, 225, 520));

        GUILayout.Label($"BoardState: {BoardState}");
        GUILayout.Label($"Player lượt: {CurrentPlayerID} (slot {CurrentSlot})");
        GUILayout.Label($"Completed: {_completedThisRound}/{ActivePlayerCount}");
        GUILayout.Label($"Reversed: {(bool)IsReversed}");
        GUILayout.Space(4);
        GUILayout.Label("TurnOrder:");
        for (int i = 0; i < ActivePlayerCount; i++)
        {
            int pid     = GetPlayerIDAtSlot(i);
            int nid     = GetNodeIDAtSlot(i);
            bool skip   = GetSkipAtSlot(i);
            string mark = (i == CurrentSlot) ? " ◄" : "";
            GUILayout.Label($"  [{i}] P{pid} @ N{nid}{(skip ? " [SKIP]" : "")}{mark}");
        }

        // Inventory của từng player
        GUILayout.Space(4);
        GUILayout.Label("Inventory:");
        for (int i = 0; i < ActivePlayerCount; i++)
        {
            int pid = GetPlayerIDAtSlot(i);
            if (pid < 0) continue;
            var inv = PlayerItemInventory.GetForPlayer(pid);
            if (inv == null)
            {
                GUILayout.Label($"  P{pid}: (no inventory)");
                continue;
            }
            var items = inv.GetItems();
            string invStr = items.Count > 0
                ? string.Join(", ", items)
                : "(empty)";
            GUILayout.Label($"  P{pid} [{items.Count}/8]: {invStr}");
        }

        // Tile message
        if (_lastTileMessageTimer > 0f && _lastTileMessage.Length > 0)
        {
            var msgColor = GUI.color;
            GUI.color = Color.yellow;
            GUILayout.Label($"▶ {_lastTileMessage}");
            GUI.color = msgColor;
        }

        // Reaction FX
        if (_reactionTimer > 0f)
        {
            var rc = GUI.color;
            GUI.color = Color.cyan;
            GUILayout.Label($"  {_reactionLine}");
            GUI.color = rc;
        }

        // Steal target selection (chỉ stealer thấy)
        if (_waitingForMyStealTarget && BoardState == BoardPhaseState.WaitingForTargetSelect)
        {
            GUILayout.Space(4);
            var sc = GUI.color;
            GUI.color = Color.red;
            GUILayout.Label("STEAL — Chon target:");
            GUI.color = sc;
            foreach (int tid in _eligibleStealTargets)
            {
                if (GUILayout.Button($"> Steal from P{tid}"))
                {
                    _waitingForMyStealTarget = false;
                    _eligibleStealTargets.Clear();
                    RPC_SubmitTargetSelect(StealerPlayerId, tid);
                }
            }
        }

        // Debug: fill inventories for testing
        if (HasStateAuthority && itemPool != null)
        {
            GUILayout.Space(4);
            if (GUILayout.Button("[DEBUG] Give All 3 Items"))
            {
                for (int i = 0; i < ActivePlayerCount; i++)
                {
                    int pid = GetPlayerIDAtSlot(i);
                    if (pid < 0) continue;
                    var inv = PlayerItemInventory.GetForPlayer(pid);
                    if (inv == null) continue;
                    for (int k = 0; k < 3; k++)
                    {
                        var item = itemPool.GetRandom();
                        if (item != null) inv.AddItem(item.effectType);
                    }
                }
            }
        }

        GUILayout.Space(6);
        if (_waitingForMyRoll && BoardState == BoardPhaseState.WaitingForRoll)
        {
            var oldColor = GUI.color;
            GUI.color = Color.yellow;
            if (GUILayout.Button("►  ROLL DICE  ◄"))
                RequestRoll();
            GUI.color = oldColor;
        }

        GUILayout.EndArea();
    }
}
