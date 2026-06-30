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
    WaitingForItemTarget,
    NextTurn,
    BoardComplete
}

public class BoardManager : NetworkBehaviour
{
    public static BoardManager Instance { get; private set; }

    #region Inspector
    [Header("References")]
    [SerializeField] private BoardDice dice;
    [SerializeField] private BoardItemPool boardItemPool;
    [SerializeField] private ItemPool rouletteItemPool;

    [Header("Tokens")]
    [SerializeField] private BoardPlayerToken[] tokens = new BoardPlayerToken[4];

    [Header("Tile Resolve")]
    [SerializeField] private float tileResolveDuration = 1.5f;

    [Header("Debug")]
    [SerializeField] private bool showDebugPanel = true;
    [SerializeField] private bool useDebugRoll = false;

    [SerializeField]
    [Range(1, 12)]
    private int debugRollValue = 1;
    #endregion

    #region Networked State
    [Networked, OnChangedRender(nameof(OnBoardStateChanged))]
    public BoardPhaseState BoardState { get; private set; } = BoardPhaseState.Idle;

    [Networked] public int ActivePlayerCount { get; private set; } = 0;
    [Networked] public int CurrentTurnIndex { get; private set; } = 0;
    [Networked] public NetworkBool IsReversed { get; private set; } = false;

    [Networked] public int TurnSlot0 { get; private set; } = -1;
    [Networked] public int TurnSlot1 { get; private set; } = -1;
    [Networked] public int TurnSlot2 { get; private set; } = -1;
    [Networked] public int TurnSlot3 { get; private set; } = -1;

    [Networked] public int NodeSlot0 { get; private set; } = 0;
    [Networked] public int NodeSlot1 { get; private set; } = 0;
    [Networked] public int NodeSlot2 { get; private set; } = 0;
    [Networked] public int NodeSlot3 { get; private set; } = 0;

    [Networked] public NetworkBool Skip0 { get; private set; } = false;
    [Networked] public NetworkBool Skip1 { get; private set; } = false;
    [Networked] public NetworkBool Skip2 { get; private set; } = false;
    [Networked] public NetworkBool Skip3 { get; private set; } = false;

    [Networked] public int StealerPlayerId { get; private set; } = -1;
    [Networked] public int ItemUserPlayerId { get; private set; } = -1;
    #endregion

    #region Local State
    private int _completedThisRound = 0;


    private bool _waitingForMyRoll = false;

    // Per-slot host state
    private bool[] _hasShield = new bool[4];
    private int[] _bonusSteps = new int[4];

    // Item target coordination — host
    private int _itemTargetPendingId = -1;
    private BoardItemEffect _pendingItemEffect = BoardItemEffect.None;

    // Steal coordination — host
    private int _stealPendingTargetId = -1;

    // Local client UI state
    private bool _itemUsedThisTurn = false;
    private bool _waitingForMyItemTarget = false;
    private bool _waitingForMyStealTarget = false;
    private System.Collections.Generic.List<int> _eligibleItemTargets = new();
    private System.Collections.Generic.List<int> _eligibleStealTargets = new();

    // Debug display
    private string _lastTileMessage = "";
    private float _lastTileMessageTimer = 0f;
    private string _reactionLine = "";
    private float _reactionTimer = 0f;

    private int _targetSelectIndex = 0;
    private bool _isSelectingTarget = false;
    #endregion

    #region Events
    public System.Action<int> OnTurnStarted;
    public System.Action OnBoardPhaseComplete;
    #endregion

    #region Slot Accessors
    public int GetPlayerIDAtSlot(int slot) => slot switch
    {
        0 => TurnSlot0,
        1 => TurnSlot1,
        2 => TurnSlot2,
        3 => TurnSlot3,
        _ => -1
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
        0 => NodeSlot0,
        1 => NodeSlot1,
        2 => NodeSlot2,
        3 => NodeSlot3,
        _ => 0
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
        0 => Skip0,
        1 => Skip1,
        2 => Skip2,
        3 => Skip3,
        _ => false
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

    public int CurrentPlayerID =>
        ActivePlayerCount > 0 ? GetPlayerIDAtSlot(CurrentSlot) : -1;

    public int CurrentSlot =>
        ActivePlayerCount > 0 ? CurrentTurnIndex % ActivePlayerCount : 0;
    #endregion

    #region Lifecycle
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
        if (_lastTileMessageTimer > 0f) _lastTileMessageTimer -= Time.deltaTime;
        if (_reactionTimer > 0f) _reactionTimer -= Time.deltaTime;

        // A-D để chọn target item
        if (_isSelectingTarget && _eligibleItemTargets.Count > 0)
        {
            if (Input.GetKeyDown(KeyCode.A))
            {
                _targetSelectIndex = (_targetSelectIndex - 1 + _eligibleItemTargets.Count) % _eligibleItemTargets.Count;
                RPC_HighlightTarget(_eligibleItemTargets[_targetSelectIndex]);
            }
            else if (Input.GetKeyDown(KeyCode.D))
            {
                _targetSelectIndex = (_targetSelectIndex + 1) % _eligibleItemTargets.Count;
                RPC_HighlightTarget(_eligibleItemTargets[_targetSelectIndex]);
            }
            else if (Input.GetKeyDown(KeyCode.Space))
            {
                _isSelectingTarget = false;
                _waitingForMyItemTarget = false;
                RPC_SubmitItemTargetSelect(ItemUserPlayerId, _eligibleItemTargets[_targetSelectIndex]);
            }
            return; // không xử lý roll khi đang chọn target
        }

        // Space to roll
        if (_waitingForMyRoll
            && BoardState == BoardPhaseState.WaitingForRoll
            && Input.GetKeyDown(KeyCode.Space))
        {
            RequestRoll();
        }
    }
    #endregion

    #region Start Board Phase


    public void StartBoardPhase(int[] rankOrder)
    {
        if (!HasStateAuthority) return;

        int count = Mathf.Min(rankOrder.Length, 4);
        if (count == 0)
        {
            Debug.LogError("[BoardManager] StartBoardPhase called with 0 players!");
            return;
        }

        ActivePlayerCount = count;
        CurrentTurnIndex = 0;
        IsReversed = false;
        _completedThisRound = 0;

        for (int i = 0; i < 4; i++)
        {
            _hasShield[i] = false;
            _bonusSteps[i] = 0;
        }

        for (int i = 0; i < 4; i++)
        {
            SetPlayerIDAtSlot(i, i < count ? rankOrder[i] : -1);
            SetNodeIDAtSlot(i, 0);
            SetSkipAtSlot(i, false);
        }

        RPC_InitializeTokens(rankOrder, count);
        Debug.Log($"[BoardManager] Board phase started — {string.Join(", ", rankOrder)}");
        StartTurn();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_InitializeTokens(int[] playerIds, int count)
    {
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
            if (tokens[i] == null)
                continue;

            tokens[i].Initialize(playerIds[i], i, 0);
        }
        
    }
    #endregion

    #region Turn Flow
    private void StartTurn()
    {
        if (!HasStateAuthority) return;

        if (_completedThisRound >= ActivePlayerCount)
        {
            CompleteBoardPhase();
            return;
        }

        int slot = CurrentSlot;
        int playerId = GetPlayerIDAtSlot(slot);

        if (playerId < 0)
        {
            _completedThisRound++;
            AdvanceTurn();
            return;
        }

        if (GetSkipAtSlot(slot))
        {
            SetSkipAtSlot(slot, false);
            Debug.Log($"[BoardManager] Player {playerId} SKIPPED");
            _completedThisRound++;
            if (_completedThisRound >= ActivePlayerCount) CompleteBoardPhase();
            else AdvanceTurn();
            return;
        }

        BoardState = BoardPhaseState.WaitingForRoll;
        RPC_TurnStarted(playerId);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_TurnStarted(int playerId)
    {
        OnTurnStarted?.Invoke(playerId);

        _waitingForMyRoll = false;
        if (Runner != null && Runner.LocalPlayer.PlayerId == playerId)
        {
            _waitingForMyRoll = true;
            _itemUsedThisTurn = false;
            _waitingForMyItemTarget = false;
        }

        BoardCameraController.Instance?.FocusOnPlayer(playerId);

        var token = GetTokenByPlayerId(playerId);

        Debug.Log($"[DICE] Player={playerId} Token={(token != null ? token.name : "NULL")}");

        if (token != null)
        {
            BoardDiceVisual.Instance?.ShowAt(token.DiceAnchor);
        }

        Debug.Log($"[BoardManager] >>> Lượt của Player {playerId}");
    }

    private void AdvanceTurn()
    {
        if (!HasStateAuthority) return;

        BoardState = BoardPhaseState.NextTurn;

        CurrentTurnIndex = IsReversed
            ? ((CurrentTurnIndex - 1) % ActivePlayerCount + ActivePlayerCount) % ActivePlayerCount
            : (CurrentTurnIndex + 1) % ActivePlayerCount;

        if (_completedThisRound < ActivePlayerCount) StartTurn();
        else CompleteBoardPhase();
    }
    #endregion

    #region Dice Roll
    public void RequestRoll()
    {
        if (!_waitingForMyRoll) return;
        if (BoardState != BoardPhaseState.WaitingForRoll) return;

        _waitingForMyRoll = false;

        // Dùng Runner.LocalPlayer thay vì PlayerNetworkData.Local — luôn có giá trị đúng
        int myId = Runner != null ? Runner.LocalPlayer.PlayerId : -1;
        if (myId < 0) return;

        RPC_SubmitRollRequest(myId);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_SubmitRollRequest(int requestingPlayerId)
    {
        if (!HasStateAuthority) return;
        if (BoardState != BoardPhaseState.WaitingForRoll) return;
        if (requestingPlayerId != CurrentPlayerID)
        {
            Debug.LogWarning($"[BoardManager] Invalid roll from {requestingPlayerId}");
            return;
        }

        BoardState = BoardPhaseState.Rolling;
        int slot = CurrentSlot;
        int result;

        if (useDebugRoll)
        {
            result = debugRollValue;
        }
        else
        {
            result = dice != null ? dice.Roll() : Random.Range(2, 13);
        }

        result += _bonusSteps[slot];
        _bonusSteps[slot] = 0;

        StartCoroutine(RollSequence(slot, requestingPlayerId, result));
    }

    private IEnumerator RollSequence(int slot, int playerId, int result)
    {
        // 1. Jump
        RPC_TriggerTokenJump(slot);
        yield return new WaitForSeconds(0.4f);

        // 2. Dice bắt đầu quay
        RPC_StartDiceSpin();

        // Quay khoảng 0.8 giây
        yield return new WaitForSeconds(0.8f);

        // Giữ thêm 1 giây
        yield return new WaitForSeconds(1.0f);

        // 4. Dice dừng quay
        RPC_StopDiceSpin();

        // 3. Hiện số
        RPC_ShowDiceResult(playerId, result);

        // 5. Ẩn xúc xắc
        RPC_HideDice();

        // 6. Di chuyển
        yield return StartCoroutine(ExecuteMovement(slot, playerId, result));
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_TriggerTokenJump(int slot)
    {
        if (tokens != null && slot < tokens.Length && tokens[slot] != null)
            tokens[slot].PlayJumpAnimation();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ShowDiceResult(int playerId, int result)
    {
        BoardHUDController.Instance?.OnDiceResult(playerId, result);
        Debug.Log($"[BoardManager] Player {playerId} rolled {result}");
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_HideDice()
    {
        BoardDiceVisual.Instance?.Hide();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_StartDiceSpin()
    {
        BoardDiceVisual.Instance?.StartSpin();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_StopDiceSpin()
    {
        BoardDiceVisual.Instance?.StopSpin();
    }
    #endregion

    #region Item Use
    public void RequestUseItem(int itemSlot, BoardItemEffect effect)
    {
        if (!_waitingForMyRoll) return;
        if (_itemUsedThisTurn) return;
        if (BoardState != BoardPhaseState.WaitingForRoll) return;

        int myId = Runner?.LocalPlayer.PlayerId ?? -1;
        if (myId < 0) return;

        RPC_SubmitItemUseRequest(myId, itemSlot, (int)effect);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_SubmitItemUseRequest(int userId, int itemSlot, int effectId)
    {
        if (!HasStateAuthority) return;
        if (BoardState != BoardPhaseState.WaitingForRoll) return;
        if (userId != CurrentPlayerID) return;

        var inv = PlayerItemInventory.GetForPlayer(userId);
        if (inv == null) return;
        if (itemSlot < 0 || inv.BoardItems.Get(itemSlot) != effectId) return;

        inv.RemoveBoardItem(itemSlot);

        var effect = (BoardItemEffect)effectId;
        int slot = CurrentSlot;

        switch (effect)
        {
            case BoardItemEffect.RushForward:
                _bonusSteps[slot] = 2;
                RPC_ItemUsed(userId, effectId);
                break;

            case BoardItemEffect.Shield:
                _hasShield[slot] = true;
                RPC_ItemUsed(userId, effectId);
                break;

            case BoardItemEffect.PushBack:
            case BoardItemEffect.PositionSwap:
                BoardState = BoardPhaseState.WaitingForItemTarget;
                ItemUserPlayerId = userId;
                _itemTargetPendingId = -1;
                _pendingItemEffect = effect;

                var eligibles = new System.Collections.Generic.List<int>();
                for (int i = 0; i < ActivePlayerCount; i++)
                {
                    int pid = GetPlayerIDAtSlot(i);
                    if (pid >= 0 && pid != userId) eligibles.Add(pid);
                }

                RPC_BeginItemTargetSelect(userId, eligibles.ToArray(), effectId);
                int userSlotVerified = CurrentSlot; // lấy đúng slot của user
                StartCoroutine(WaitForItemTargetSelect(userSlotVerified, userId));
                break;
        }
    }

    private IEnumerator WaitForItemTargetSelect(int userSlot, int userId)
    {
        // Verify lại userSlot từ userId ngay lập tức — tránh nhầm sau khi chờ
        int verifiedUserSlot = userSlot;
        for (int i = 0; i < ActivePlayerCount; i++)
            if (GetPlayerIDAtSlot(i) == userId) { verifiedUserSlot = i; break; }

        float elapsed = 0f;
        while (_itemTargetPendingId == -1 && elapsed < 10f)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (_itemTargetPendingId == -1)
        {
            for (int i = 0; i < ActivePlayerCount; i++)
            {
                int pid = GetPlayerIDAtSlot(i);
                if (pid >= 0 && pid != userId) { _itemTargetPendingId = pid; break; }
            }
        }

        if (_itemTargetPendingId == -1)
        {
            var usedEff = _pendingItemEffect;
            BoardState = BoardPhaseState.WaitingForRoll;
            ItemUserPlayerId = -1;
            _pendingItemEffect = BoardItemEffect.None;
            RPC_ItemUsed(userId, (int)usedEff);
            yield break;
        }

        int targetId = _itemTargetPendingId;
        int targetSlot = -1;
        for (int i = 0; i < ActivePlayerCount; i++)
            if (GetPlayerIDAtSlot(i) == targetId) { targetSlot = i; break; }

        if (_pendingItemEffect == BoardItemEffect.PushBack)
            yield return ExecutePushBack(targetSlot, userId);
        else if (_pendingItemEffect == BoardItemEffect.PositionSwap)
            yield return ExecutePositionSwap(verifiedUserSlot, targetSlot, userId, targetId); // dùng verifiedUserSlot

        var doneEffect = _pendingItemEffect;
        BoardState = BoardPhaseState.WaitingForRoll;
        ItemUserPlayerId = -1;
        _pendingItemEffect = BoardItemEffect.None;
        RPC_ItemUsed(userId, (int)doneEffect);
    }

    private IEnumerator ExecutePushBack(int targetSlot, int userId)
    {
        if (targetSlot >= 0 && _hasShield[targetSlot])
        {
            _hasShield[targetSlot] = false;
            RPC_ShieldBlocked(GetPlayerIDAtSlot(targetSlot), userId);
            yield break;
        }

        if (targetSlot >= 0)
        {
            var pathObj = BoardNodePath.Instance;
            var currentNode = pathObj?.GetNodeByID(GetNodeIDAtSlot(targetSlot));
            if (pathObj != null && currentNode != null)
            {
                var dest = pathObj.GetNodeBeforeSteps(currentNode, 2, out int[] pathIDs);
                SetNodeIDAtSlot(targetSlot, dest.nodeID);
                if (pathIDs.Length > 0)
                {
                    RPC_AnimateMovement(targetSlot, pathIDs);
                    yield return new WaitForSeconds(pathIDs.Length * (1f / 4f) + 0.4f);
                }
            }
        }

        RPC_FocusOnTargetForPushBack(GetPlayerIDAtSlot(targetSlot));
    }

    private IEnumerator ExecutePositionSwap(int userSlot, int targetSlot, int userId, int targetId)
    {
        if (targetSlot >= 0 && _hasShield[targetSlot])
        {
            _hasShield[targetSlot] = false;
            RPC_ShieldBlocked(targetId, userId);
            yield break;
        }

        int userNode = GetNodeIDAtSlot(userSlot);
        int targetNode = GetNodeIDAtSlot(targetSlot);

        SetNodeIDAtSlot(userSlot, targetNode);
        SetNodeIDAtSlot(targetSlot, userNode);

        RPC_SnapTokensForSwap(userSlot, targetNode, targetSlot, userNode);
        yield return new WaitForSeconds(0.8f);

        RPC_PositionSwapResult(userId, targetId);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_BeginItemTargetSelect(int userId, int[] eligibles, int effectId)
    {
        if (Runner != null && Runner.LocalPlayer.PlayerId == userId)
        {
            _waitingForMyItemTarget = true;
            _eligibleItemTargets = new System.Collections.Generic.List<int>(eligibles);
            _targetSelectIndex = 0;
            _isSelectingTarget = true;

            // Gọi trực tiếp thay vì RPC
            BoardCameraController.Instance?.FocusOnPlayer(eligibles[0]);
        }
        Debug.Log($"[BoardManager] P{userId} chọn target cho {(BoardItemEffect)effectId}");
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_SubmitItemTargetSelect(int userId, int targetId)
    {
        if (!HasStateAuthority) return;
        if (BoardState != BoardPhaseState.WaitingForItemTarget) return;
        if (userId != ItemUserPlayerId) return;
        _itemTargetPendingId = targetId;
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ItemUsed(int userId, int effectId)
    {
        var effect = (BoardItemEffect)effectId;
        string effName = BoardItemPool.Current?.GetByEffect(effect)?.itemName ?? effect.ToString();

        _lastTileMessage = $"P{userId} dùng: {effName}!";
        _lastTileMessageTimer = tileResolveDuration;

        if (Runner != null && Runner.LocalPlayer.PlayerId == userId)
        {
            _itemUsedThisTurn = true;
            _waitingForMyItemTarget = false;
            _isSelectingTarget = false;
        }

        // Camera về player vừa dùng item — tất cả clients đều thấy
        BoardCameraController.Instance?.FocusOnPlayer(userId);

        BoardHUDController.Instance?.OnItemUsed(userId, effect);
        Debug.Log($"[BoardManager] {_lastTileMessage}");
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ShieldBlocked(int defenderId, int attackerId)
    {
        _lastTileMessage = $"P{defenderId} SHIELD blocked P{attackerId}!";
        _lastTileMessageTimer = tileResolveDuration;

        if (Runner != null)
        {
            int myId = Runner.LocalPlayer.PlayerId;
            if (myId == defenderId) _reactionLine = "(🛡) BLOCKED!";
            else if (myId == attackerId) _reactionLine = "(T_T) BLOCKED BY SHIELD!";
        }
        _reactionTimer = 2f;
        Debug.Log($"[BoardManager] {_lastTileMessage}");
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_FocusOnTargetForPushBack(int targetId)
    {
        BoardCameraController.Instance?.FocusOnPlayer(targetId);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_SnapTokensForSwap(int slot1, int node1, int slot2, int node2)
    {
        if (tokens == null) return;
        if (slot1 < tokens.Length && tokens[slot1] != null) tokens[slot1].SnapToNode(node1);
        if (slot2 < tokens.Length && tokens[slot2] != null) tokens[slot2].SnapToNode(node2);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PositionSwapResult(int userId, int targetId)
    {
        _lastTileMessage = $"P{userId} SWAPPED with P{targetId}!";
        _lastTileMessageTimer = tileResolveDuration;

        if (Runner != null && (Runner.LocalPlayer.PlayerId == userId || Runner.LocalPlayer.PlayerId == targetId))
            _reactionLine = "(*_*) SWAPPED!";
        _reactionTimer = 2f;

        // Chỉ focus targetId, RPC_ItemUsed sẽ tự về userId
        BoardCameraController.Instance?.FocusOnPlayer(targetId);

        Debug.Log($"[BoardManager] {_lastTileMessage}");
    }
    #endregion

    #region Movement
    private IEnumerator ExecuteMovement(int slot, int playerId, int steps)
    {
        BoardState = BoardPhaseState.Moving;

        var path = BoardNodePath.Instance;
        var currentNode = path?.GetNodeByID(GetNodeIDAtSlot(slot));

        if (currentNode == null || path == null)
        {
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
        SetNodeIDAtSlot(slot, finalNodeID);
        RPC_AnimateMovement(slot, pathIDs);

        float waitTime = pathIDs.Length * (1f / 4f) + 0.6f;
        yield return new WaitForSeconds(waitTime);

        yield return FinishTurn(slot, playerId, finalNodeID, destination.tileType);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_AnimateMovement(int slot, int[] pathNodeIDs)
    {
        if (tokens != null && slot < tokens.Length && tokens[slot] != null)
            tokens[slot].AnimateMovement(pathNodeIDs);
    }
    #endregion

    #region Tile Resolve
    private IEnumerator FinishTurn(int slot, int playerId, int finalNodeID, TileType tileType)
    {
        BoardState = BoardPhaseState.ResolvingTile;

        BoardCollectableManager.Instance?.TryCollectKey(playerId, finalNodeID);

        switch (tileType)
        {
            case TileType.Steal:
                yield return HandleStealTile(playerId, finalNodeID);
                break;
            case TileType.Toss:
                ResolveToss(playerId);
                yield return new WaitForSeconds(tileResolveDuration);
                break;
            case TileType.Item:
                ResolveItem(playerId);
                RPC_PlayerLanded(playerId, finalNodeID, tileType);
                yield return new WaitForSeconds(tileResolveDuration);
                break;
            case TileType.Jackpot:
                ResolveJackpot(playerId);
                RPC_PlayerLanded(playerId, finalNodeID, tileType);
                yield return new WaitForSeconds(tileResolveDuration);
                break;
            case TileType.Gamble:
                ResolveGamble(playerId);
                RPC_PlayerLanded(playerId, finalNodeID, tileType);
                yield return new WaitForSeconds(tileResolveDuration);
                break;
            default:
                RPC_PlayerLanded(playerId, finalNodeID, tileType);
                yield return new WaitForSeconds(tileResolveDuration);
                break;
        }

        _completedThisRound++;

        if (_completedThisRound >= ActivePlayerCount) CompleteBoardPhase();
        else AdvanceTurn();
    }

    private void ResolveItem(int playerId)
    {
        if (boardItemPool == null) return;
        var inv = PlayerItemInventory.GetForPlayer(playerId);

        Debug.Log($"ResolveItem -> playerId = {playerId}");
        Debug.Log($"Inventory NULL = {inv == null}");
        var item = boardItemPool.GetRandom();
        if (inv == null || item == null) return;

        bool ok = inv.AddBoardItem(item.effectType);

        Debug.Log($"AddBoardItem returned = {ok}");

        if (ok)
        {
            var ui = FindFirstObjectByType<BoardInventoryUI>();
            if (ui != null)
            {
                ui.RefreshAfterRestore();
            }
        }

        if (!ok)
            RPC_TileMessage(playerId, "[BOARD ITEMS FULL]");
        else
            RPC_TileMessage(playerId, $"GOT: {item.itemName} [{item.rarity}]");
    }

    private void ResolveJackpot(int playerId)
    {
        if (boardItemPool == null) return;
        var inv = PlayerItemInventory.GetForPlayer(playerId);
        if (inv == null) return;

        int granted = 0;
        for (int i = 0; i < 2; i++)
        {
            var item = i == 0 ? boardItemPool.GetRandom(ItemRarity.Rare) : boardItemPool.GetRandom();
            if (item == null) continue;
            if (!inv.AddBoardItem(item.effectType)) { RPC_TileMessage(playerId, $"JACKPOT! +{granted} [FULL]"); return; }
            granted++;
        }
        RPC_TileMessage(playerId, $"JACKPOT! +{granted}");
    }

    private void ResolveGamble(int playerId)
    {
        var inv = PlayerItemInventory.GetForPlayer(playerId);
        if (inv == null) return;

        bool win = Random.value >= 0.5f;
        if (win)
        {
            if (boardItemPool == null) return;
            var item = boardItemPool.GetRandom();
            if (item == null) return;
            if (!inv.AddBoardItem(item.effectType)) { RPC_TileMessage(playerId, "GAMBLE WIN — FULL!"); return; }
            RPC_GambleResult(playerId, true, item.itemName);
        }
        else
        {
            int lostEffect = -1;
            if (inv.GetBoardItemCount() > 0)
            {
                var items = inv.GetBoardItemsWithSlots();
                var chosen = items[Random.Range(0, items.Count)];
                lostEffect = (int)chosen.effect;
                inv.RemoveBoardItem(chosen.slot);
            }
            RPC_GambleResult(playerId, false, lostEffect >= 0
                ? (boardItemPool?.GetByEffect((BoardItemEffect)lostEffect)?.itemName ?? lostEffect.ToString())
                : "nothing");
        }
    }

    private IEnumerator HandleStealTile(int stealerId, int nodeID)
    {
        var eligibles = new System.Collections.Generic.List<int>();
        for (int i = 0; i < ActivePlayerCount; i++)
        {
            int pid = GetPlayerIDAtSlot(i);
            if (pid < 0 || pid == stealerId) continue;
            var inv = PlayerItemInventory.GetForPlayer(pid);
            if (inv != null && inv.GetBoardItemCount() > 0) eligibles.Add(pid);
        }

        if (eligibles.Count == 0)
        {
            RPC_PlayerLanded(stealerId, nodeID, TileType.Steal);
            yield return new WaitForSeconds(tileResolveDuration);
            yield break;
        }

        BoardState = BoardPhaseState.WaitingForTargetSelect;
        StealerPlayerId = stealerId;
        _stealPendingTargetId = -1;
        RPC_BeginTargetSelect(stealerId, eligibles.ToArray());

        float elapsed = 0f;
        while (_stealPendingTargetId == -1 && elapsed < 10f)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (_stealPendingTargetId == -1)
            _stealPendingTargetId = eligibles[Random.Range(0, eligibles.Count)];

        int targetId = _stealPendingTargetId;

        // Check shield
        int targetSlotForShield = -1;
        for (int i = 0; i < ActivePlayerCount; i++)
            if (GetPlayerIDAtSlot(i) == targetId) { targetSlotForShield = i; break; }

        if (targetSlotForShield >= 0 && _hasShield[targetSlotForShield])
        {
            _hasShield[targetSlotForShield] = false;
            BoardState = BoardPhaseState.ResolvingTile;
            StealerPlayerId = -1;
            RPC_ShieldBlocked(targetId, stealerId);
            yield return new WaitForSeconds(tileResolveDuration);
            yield break;
        }

        // Thực hiện steal
        var stealerInv = PlayerItemInventory.GetForPlayer(stealerId);
        var targetInv = PlayerItemInventory.GetForPlayer(targetId);
        int stolenEffect = -1;

        if (stealerInv != null && targetInv != null && targetInv.GetBoardItemCount() > 0)
        {
            var items = targetInv.GetBoardItemsWithSlots();
            var chosen = items[Random.Range(0, items.Count)];
            stolenEffect = (int)chosen.effect;
            targetInv.RemoveBoardItem(chosen.slot);
            stealerInv.AddBoardItem(chosen.effect);
        }

        BoardState = BoardPhaseState.ResolvingTile;
        StealerPlayerId = -1;
        RPC_StealResult(stealerId, targetId, stolenEffect);
        yield return new WaitForSeconds(1f);
        RPC_FocusBackToStealer(stealerId);

        yield return new WaitForSeconds(tileResolveDuration - 1f);
    }

    private void ResolveToss(int playerId)
    {
        var inv = PlayerItemInventory.GetForPlayer(playerId);
        int lostEffect = -1;

        if (inv != null && inv.GetBoardItemCount() > 0)
        {
            var items = inv.GetBoardItemsWithSlots();
            var chosen = items[Random.Range(0, items.Count)];
            lostEffect = (int)chosen.effect;
            inv.RemoveBoardItem(chosen.slot);
        }
        RPC_TossResult(playerId, lostEffect);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayerLanded(int playerId, int nodeID, TileType tileType)
    {
        string msg = tileType switch
        {
            TileType.Item => "GOT ITEM!",
            TileType.Steal => "STEAL!",
            TileType.Toss => "TOSS ITEM!",
            TileType.Jackpot => "JACKPOT!",
            TileType.Gamble => "GAMBLE!",
            _ => ""
        };
        _lastTileMessage = msg;
        _lastTileMessageTimer = tileResolveDuration;
        Debug.Log($"[BoardManager] P{playerId} @ N{nodeID} [{tileType}]");
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_BeginTargetSelect(int stealerId, int[] eligibles)
    {
        if (Runner != null && Runner.LocalPlayer.PlayerId == stealerId)
        {
            _waitingForMyStealTarget = true;
            _eligibleStealTargets = new System.Collections.Generic.List<int>(eligibles);
        }
        Debug.Log($"[BoardManager] P{stealerId} chọn target để steal...");
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_SubmitTargetSelect(int stealerId, int targetId)
    {
        if (!HasStateAuthority) return;
        if (BoardState != BoardPhaseState.WaitingForTargetSelect) return;
        if (stealerId != StealerPlayerId) return;
        _stealPendingTargetId = targetId;
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_StealResult(int stealerId, int targetId, int itemEffect)
    {
        // Chỉ focus targetId
        BoardCameraController.Instance?.FocusOnPlayer(targetId);

        string itemName = itemEffect >= 0
            ? (BoardItemPool.Current?.GetByEffect((BoardItemEffect)itemEffect)?.itemName ?? ((BoardItemEffect)itemEffect).ToString())
            : "???";

        _lastTileMessage = itemEffect >= 0 ? $"P{stealerId} STOLE {itemName} from P{targetId}!" : "STEAL: failed";
        _lastTileMessageTimer = tileResolveDuration;

        if (Runner != null)
        {
            int myId = Runner.LocalPlayer.PlayerId;
            if (myId == stealerId) _reactionLine = "(>:D) STOLE IT!";
            else if (myId == targetId) _reactionLine = "(T_T) MY ITEM...";
        }
        _reactionTimer = 2f;
        Debug.Log($"[BoardManager] {_lastTileMessage}");
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_TossResult(int playerId, int itemEffect)
    {
        string itemName = itemEffect >= 0
            ? (BoardItemPool.Current?.GetByEffect((BoardItemEffect)itemEffect)?.itemName ?? ((BoardItemEffect)itemEffect).ToString())
            : "nothing";

        _lastTileMessage = itemEffect >= 0 ? $"P{playerId} lost {itemName}!" : $"P{playerId} TOSS: nothing";
        _lastTileMessageTimer = tileResolveDuration;

        if (Runner != null && Runner.LocalPlayer.PlayerId == playerId)
            _reactionLine = "(T_T) DROPPED IT!";
        _reactionTimer = 2f;
        Debug.Log($"[BoardManager] {_lastTileMessage}");
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_TileMessage(int playerId, string message)
    {
        _lastTileMessage = message;
        _lastTileMessageTimer = tileResolveDuration;
        Debug.Log($"[BoardManager] P{playerId}: {message}");
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_GambleResult(int playerId, bool won, string itemName)
    {
        _lastTileMessage = won ? $"P{playerId} GAMBLE WIN! Got: {itemName}" : $"P{playerId} GAMBLE LOSE! Lost: {itemName}";
        _lastTileMessageTimer = tileResolveDuration;

        if (Runner != null && Runner.LocalPlayer.PlayerId == playerId)
            _reactionLine = won ? "(*_*) LUCKY!" : "(T_T) UNLUCKY!";
        _reactionTimer = 2f;
        Debug.Log($"[BoardManager] {_lastTileMessage}");
    }
    #endregion

    #region Board Complete
    private void CompleteBoardPhase()
    {
        if (!HasStateAuthority) return;

        GameManager.Instance?.SaveBoardPositions(NodeSlot0, NodeSlot1, NodeSlot2, NodeSlot3);

        // Lưu Board items của từng slot
        for (int i = 0; i < ActivePlayerCount; i++)
        {
            int pid = GetPlayerIDAtSlot(i);
            if (pid < 0) continue;

            var inv = PlayerItemInventory.GetForPlayer(pid);
            if (inv == null) continue;

            Debug.Log($"SAVE SLOT {i}: [{inv.BoardItems.Get(0)}, {inv.BoardItems.Get(1)}, {inv.BoardItems.Get(2)}, {inv.BoardItems.Get(3)}]");

            Debug.Log(
            $"Before Save P{i}: " +
            $"[{inv.BoardItems.Get(0)}, {inv.BoardItems.Get(1)}, {inv.BoardItems.Get(2)}, {inv.BoardItems.Get(3)}]");

            GameManager.Instance?.SaveBoardItems(i,
                inv.BoardItems.Get(0),
                inv.BoardItems.Get(1),
                inv.BoardItems.Get(2),
                inv.BoardItems.Get(3));
        }

        DistributeRouletteRewards();
        BoardState = BoardPhaseState.BoardComplete;
        RPC_BoardComplete();
        GameManager.Instance?.ProceedFromBoard();
    }

    private void DistributeRouletteRewards()
    {
        if (rouletteItemPool == null)
        {
            Debug.LogWarning("[BoardManager] RouletteItemPool chưa assign.");
            return;
        }

        var ranking = new System.Collections.Generic.List<(int playerId, int nodeID)>();
        for (int i = 0; i < ActivePlayerCount; i++)
        {
            int pid = GetPlayerIDAtSlot(i);
            if (pid >= 0) ranking.Add((pid, GetNodeIDAtSlot(i)));
        }
        ranking.Sort((a, b) => b.nodeID.CompareTo(a.nodeID));

        int count = ranking.Count;
        int[] pidArr = new int[count];
        int[] rewArr = new int[count];

        for (int i = 0; i < count; i++)
        {
            int rewardCount = count - i;
            pidArr[i] = ranking[i].playerId;
            rewArr[i] = rewardCount;

            var inv = PlayerItemInventory.GetForPlayer(ranking[i].playerId);
            if (inv == null) continue;
            for (int k = 0; k < rewardCount; k++)
            {
                var item = rouletteItemPool.GetRandom();
                if (item != null) inv.AddRouletteItem(item.effectType);
            }
        }

        RPC_RouletteRewardsDistributed(pidArr, rewArr);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_RouletteRewardsDistributed(int[] playerIds, int[] rewardCounts)
    {
        var sb = new System.Text.StringBuilder("BOARD RACE REWARD: ");
        for (int i = 0; i < playerIds.Length; i++)
            sb.Append($"P{playerIds[i]}+{rewardCounts[i]}rlt ");
        _lastTileMessage = sb.ToString().TrimEnd();
        _lastTileMessageTimer = 4f;
        Debug.Log($"[BoardManager] {_lastTileMessage}");
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_BoardComplete()
    {
        OnBoardPhaseComplete?.Invoke();
        Debug.Log("[BoardManager] Board phase complete");
    }

    [Rpc(RpcSources.All, RpcTargets.All)]
    private void RPC_HighlightTarget(int targetId)
    {
        BoardCameraController.Instance?.FocusOnPlayer(targetId);
    }
    #endregion

    #region Public API
    public void SkipPlayer(int playerId)
    {
        if (!HasStateAuthority) return;
        for (int i = 0; i < ActivePlayerCount; i++)
        {
            if (GetPlayerIDAtSlot(i) == playerId)
            {
                SetSkipAtSlot(i, true);
                Debug.Log($"[BoardManager] Player {playerId} sẽ bị skip");
                return;
            }
        }
    }

    public void ReverseOrder()
    {
        if (!HasStateAuthority) return;
        IsReversed = !IsReversed;
        Debug.Log($"[BoardManager] Chiều vòng: {(IsReversed ? "NGƯỢC" : "XUÔI")}");
    }

    public void RestoreBoardPositions(int[] nodeSlots)
    {
        if (!HasStateAuthority) return;
        if (nodeSlots == null || nodeSlots.Length < 4) return;

        for (int i = 0; i < 4; i++)
            SetNodeIDAtSlot(i, nodeSlots[i]);

        RPC_SnapTokensToSavedPositions(nodeSlots[0], nodeSlots[1], nodeSlots[2], nodeSlots[3]);
        Debug.Log($"[BoardManager] Restored: [{string.Join(", ", nodeSlots)}]");
    }

    public BoardPlayerToken GetTokenByPlayerId(int playerId)
    {
        if (tokens == null)
            return null;

        foreach (var token in tokens)
        {
            if (token == null)
            continue;

            if (token.ownerPlayerId == playerId)
                return token;
        }

        return null;
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_SnapTokensToSavedPositions(int n0, int n1, int n2, int n3)
    {
        int[] slots = { n0, n1, n2, n3 };
        for (int i = 0; i < 4; i++)
            if (tokens != null && i < tokens.Length && tokens[i] != null)
                tokens[i].SnapToNode(slots[i]);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_FocusBackToStealer(int stealerId)
    {
        BoardCameraController.Instance?.FocusOnPlayer(stealerId);
    }
    #endregion

    public int GetPlayerCountOnNode(int nodeID)
    {
        int count = 0;

        if (tokens == null)
            return 0;

        foreach (var token in tokens)
        {
            if (token == null)
                continue;

            if (token.CurrentNodeID == nodeID)
                count++;
        }

        return count;
    }

    #region Callbacks
    private void OnBoardStateChanged()
    {
        Debug.Log($"[BoardManager] BoardState → {BoardState}");
    }
    #endregion

    #region Debug UI
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
            int pid = GetPlayerIDAtSlot(i);
            int nid = GetNodeIDAtSlot(i);
            bool skip = GetSkipAtSlot(i);
            bool shld = _hasShield[i];
            string mark = (i == CurrentSlot) ? " ◄" : "";
            GUILayout.Label($"  [{i}] P{pid} @ N{nid}{(skip ? " [SKIP]" : "")}{(shld ? " [🛡]" : "")}{mark}");
        }

        GUILayout.Space(4);
        GUILayout.Label("Inventory:");
        for (int i = 0; i < ActivePlayerCount; i++)
        {
            int pid = GetPlayerIDAtSlot(i);
            if (pid < 0) continue;
            var inv = PlayerItemInventory.GetForPlayer(pid);
            if (inv == null) { GUILayout.Label($"  P{pid}: (no inventory)"); continue; }
            var bItems = inv.GetBoardItems();
            var rItems = inv.GetRouletteItems();
            GUILayout.Label($"  P{pid} Board[{bItems.Count}/4]:{(bItems.Count > 0 ? string.Join(",", bItems) : "-")}");
            GUILayout.Label($"       Rlt[{rItems.Count}/8]:{(rItems.Count > 0 ? string.Join(",", rItems) : "-")}");
            GUILayout.Label($"       Keys: {inv.GetKeyCount()}");
        }

        if (_lastTileMessageTimer > 0f && _lastTileMessage.Length > 0)
        {
            GUI.color = Color.yellow;
            GUILayout.Label($"▶ {_lastTileMessage}");
            GUI.color = Color.white;
        }

        if (_reactionTimer > 0f)
        {
            GUI.color = Color.cyan;
            GUILayout.Label($"  {_reactionLine}");
            GUI.color = Color.white;
        }

        // Item target selection
        if (_waitingForMyItemTarget && BoardState == BoardPhaseState.WaitingForItemTarget)
        {
            GUILayout.Space(4);
            GUI.color = Color.magenta;
            GUILayout.Label("CHỌN TARGET:");
            GUI.color = Color.white;
            foreach (int tid in _eligibleItemTargets)
            {
                if (GUILayout.Button($"> P{tid}"))
                {
                    _waitingForMyItemTarget = false;
                    _eligibleItemTargets.Clear();
                    RPC_SubmitItemTargetSelect(ItemUserPlayerId, tid);
                }
            }
        }

        // Steal target selection
        if (_waitingForMyStealTarget && BoardState == BoardPhaseState.WaitingForTargetSelect)
        {
            GUILayout.Space(4);
            GUI.color = Color.red;
            GUILayout.Label("STEAL — Chọn target:");
            GUI.color = Color.white;
            foreach (int tid in _eligibleStealTargets)
            {
                if (GUILayout.Button($"> Steal P{tid}"))
                {
                    _waitingForMyStealTarget = false;
                    _eligibleStealTargets.Clear();
                    RPC_SubmitTargetSelect(StealerPlayerId, tid);
                }
            }
        }

        // Debug give items
        if (HasStateAuthority && boardItemPool != null)
        {
            GUILayout.Space(4);
            GUILayout.Label("[DEBUG] Give Board Items:");
            BoardItemEffect[] testEffects = { BoardItemEffect.PushBack, BoardItemEffect.RushForward, BoardItemEffect.Shield, BoardItemEffect.PositionSwap };
            foreach (var eff in testEffects)
            {
                GUI.color = new Color(0.4f, 1f, 0.6f);
                if (GUILayout.Button($"▶ Give all: {eff}"))
                {
                    for (int i = 0; i < ActivePlayerCount; i++)
                    {
                        int pid = GetPlayerIDAtSlot(i);
                        if (pid < 0) continue;
                        PlayerItemInventory.GetForPlayer(pid)?.AddBoardItem(eff);
                    }
                }
                GUI.color = Color.white;
            }

            GUILayout.Space(5);

            GUI.color = Color.yellow;

            if (GUILayout.Button("Give 1 Key To Current Player"))
            {
                var inv = PlayerItemInventory.GetForPlayer(CurrentPlayerID);

                if (inv != null)
                {
                    inv.AddKey();
                }
            }

            GUI.color = Color.white;
        }

        GUILayout.EndArea();
    }
    #endregion
}