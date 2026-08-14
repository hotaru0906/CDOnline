using Fusion;
using UnityEngine;
using System.Collections;

public enum BoardPhaseState
{
    Idle,
    WaitingForRoll,
    Rolling,
    Moving,
    WaitingForDirection,
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
    //[SerializeField] private ItemPool rouletteItemPool;
    [SerializeField] private TrapTile trapTile;
    [SerializeField] private AudioClip itemRewardAudioClip;
    [SerializeField] private AudioClip jackpotRewardAudioClip;

    [Header("Tokens")]
    [SerializeField] private BoardPlayerToken[] tokens = new BoardPlayerToken[4];

    [Header("Tile Resolve")]
    [SerializeField] private float tileResolveDuration = 1.5f;

    [Header("VFX")]
    [SerializeField] private bool useShieldVfx = true;
    [SerializeField] private float rushForwardFlyTotalDuration = 2f;

    [Header("Gamble Wheel")]
    [SerializeField] private BoardGambleWheelUI gambleWheelUI;
    [SerializeField] private GameObject gambleWheelRoot;

    [Header("Debug")]
    [SerializeField] private bool showDebugPanel = true;
    [SerializeField] private bool useDebugRoll = false;

    [SerializeField]
    [Range(1, 12)]
    private int debugRollValue = 1;
    [SerializeField] private int debugTeleportNodeID = 0;
    [Header("End Game")]
    [SerializeField] private SceneRef endGameSceneRef;
    [Header("Item")]
    [SerializeField] private float itemResolveDuration = 3.5f;
    [Header("Jackpot")]
    [SerializeField] private float jackpotResolveDuration = 5f;
    [Header("Steal")]
    [SerializeField] private float stealHandTravelWait = 0.8f;
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
    [Networked] public int WinnerPlayerId { get; private set; } = -1;
    #endregion

    #region Local State
    private int _completedThisRound = 0;

    // ===== Branch Movement =====
    private int _remainingSteps = 0;
    private BoardNode _currentMoveNode;
    private int _selectedBranchIndex = 0;
    private int _selectedTargetNodeId = -1;

    private bool _directionSelected = false;


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
    private bool _gameEnded = false;
    private BoardItemEffect _localSelectingEffect = BoardItemEffect.None;
    private bool[] _introReady = new bool[4];
    private int _introReadyCount = 0;
    #endregion

    #region Events
    public System.Action<int> OnTurnStarted;
    public System.Action OnBoardPhaseComplete;
    public System.Action<BoardNode> OnDirectionSelectionRequested;
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

        if (trapTile == null)
        {
            Debug.LogError("TrapTile reference is missing!");
        }

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

        if (_waitingForMyStealTarget && _eligibleStealTargets.Count > 0)
        {
            if (Input.GetKeyDown(KeyCode.A))
            {
                int prevTarget = _eligibleStealTargets[_targetSelectIndex];
                _targetSelectIndex = (_targetSelectIndex - 1 + _eligibleStealTargets.Count) % _eligibleStealTargets.Count;
                int newTarget = _eligibleStealTargets[_targetSelectIndex];

                BoardHUDController.Instance?.UpdateStealSelectionPrompt(_eligibleStealTargets, _targetSelectIndex);
                RPC_HighlightTarget(newTarget);
                RPC_SwitchStealHandPreview(GetSlotByPlayerId(prevTarget), GetSlotByPlayerId(newTarget));
            }
            else if (Input.GetKeyDown(KeyCode.D))
            {
                int prevTarget = _eligibleStealTargets[_targetSelectIndex];
                _targetSelectIndex = (_targetSelectIndex + 1) % _eligibleStealTargets.Count;
                int newTarget = _eligibleStealTargets[_targetSelectIndex];

                BoardHUDController.Instance?.UpdateStealSelectionPrompt(_eligibleStealTargets, _targetSelectIndex);
                RPC_HighlightTarget(newTarget);
                RPC_SwitchStealHandPreview(GetSlotByPlayerId(prevTarget), GetSlotByPlayerId(newTarget));
            }
            else if (Input.GetKeyDown(KeyCode.Space))
            {
                _waitingForMyStealTarget = false;
                BoardHUDController.Instance?.HideStealSelectionPrompt();
                RPC_SubmitTargetSelect(StealerPlayerId, _eligibleStealTargets[_targetSelectIndex]);
            }
            return;
        }

        if (_isSelectingTarget && _eligibleItemTargets.Count > 0)
        {
            bool isSwap = _localSelectingEffect == BoardItemEffect.PositionSwap;

            if (Input.GetKeyDown(KeyCode.A))
            {
                int prevTarget = _eligibleItemTargets[_targetSelectIndex];
                _targetSelectIndex = (_targetSelectIndex - 1 + _eligibleItemTargets.Count) % _eligibleItemTargets.Count;
                int newTarget = _eligibleItemTargets[_targetSelectIndex];

                RPC_HighlightTarget(newTarget);

                if (isSwap)
                    RPC_SwitchTPPreview(GetSlotByPlayerId(prevTarget), GetSlotByPlayerId(newTarget));
                else
                    RPC_SwitchGlovePreview(GetSlotByPlayerId(prevTarget), GetSlotByPlayerId(newTarget));
            }
            else if (Input.GetKeyDown(KeyCode.D))
            {
                int prevTarget = _eligibleItemTargets[_targetSelectIndex];
                _targetSelectIndex = (_targetSelectIndex + 1) % _eligibleItemTargets.Count;
                int newTarget = _eligibleItemTargets[_targetSelectIndex];

                RPC_HighlightTarget(newTarget);

                if (isSwap)
                    RPC_SwitchTPPreview(GetSlotByPlayerId(prevTarget), GetSlotByPlayerId(newTarget));
                else
                    RPC_SwitchGlovePreview(GetSlotByPlayerId(prevTarget), GetSlotByPlayerId(newTarget));
            }
            else if (Input.GetKeyDown(KeyCode.Space))
            {
                _isSelectingTarget = false;
                _waitingForMyItemTarget = false;

                int chosenTarget = _eligibleItemTargets[_targetSelectIndex];
                int chosenSlot = GetSlotByPlayerId(chosenTarget);

                if (isSwap)
                    RPC_SetTPSelectionPreview(chosenSlot, false);
                else
                    RPC_SetGlovePreview(chosenSlot, false);

                _localSelectingEffect = BoardItemEffect.None;
                RPC_SubmitItemTargetSelect(ItemUserPlayerId, chosenTarget);
            }
            return;
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
            _introReady[i] = false;
        _introReadyCount = 0;

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
        StartCoroutine(RestoreShieldVisualsAfterInit());

        Debug.Log($"[BoardManager] Board phase started — {string.Join(", ", rankOrder)}");

        StartCoroutine(BeginBoardIntro());
    }

    private IEnumerator RestoreShieldVisualsAfterInit()
    {
        yield return null;

        for (int i = 0; i < ActivePlayerCount; i++)
        {
            int pid = GetPlayerIDAtSlot(i);
            if (pid < 0) continue;

            bool active = false;
            if (GameManager.Instance != null && GameManager.Instance.TryGetShieldState(pid, out bool savedShield))
                active = savedShield;

            SetShieldStateForPlayer(pid, active, saveToGameManager: false);
        }
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

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayBoardIntro()
    {
        if (BoardIntroController.Instance != null)
        {
            StartCoroutine(BoardIntroController.Instance.PlayIntro());
        }
    }

    private IEnumerator BeginBoardIntro()
    {
        yield return null;
        yield return null;

        if (!GameManager.Instance.HasPlayedBoardIntro)
        {
            GameManager.Instance.HasPlayedBoardIntro = true;

            // Đợi tất cả client báo đã sẵn sàng, có timeout tránh treo vĩnh viễn nếu 1 client bị đứng loading
            yield return WaitForAllClientsReadyForIntro();

            RPC_PlayBoardIntro();

            float introDuration = BoardIntroController.Instance != null
                ? BoardIntroController.Instance.TotalDuration
                : 12f;

            yield return new WaitForSeconds(introDuration);
        }

        StartTurn();
    }

    private IEnumerator WaitForAllClientsReadyForIntro()
    {
        float elapsed = 0f;
        float timeout = 8f; // fallback — nếu 1 client bị kẹt loading quá lâu, vẫn cho intro chạy để không treo cả phòng

        while (_introReadyCount < ActivePlayerCount && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (_introReadyCount < ActivePlayerCount)
            Debug.LogWarning($"[BoardManager] Bắt đầu intro dù chỉ {_introReadyCount}/{ActivePlayerCount} client sẵn sàng (timeout {timeout}s).");
    }


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
            BoardYourTurnUI.Instance?.Show();
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
        BoardYourTurnUI.Instance?.Hide();

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

        // 3. Quay
        yield return new WaitForSeconds(1.2f);

        // 4. Dice dừng quay đúng mặt kết quả
        RPC_StopDiceSpin(result);

        // 5. Cho người chơi thấy xúc xắc đã dừng
        yield return new WaitForSeconds(0.5f);

        // 6. Hiện UI kết quả
        RPC_ShowDiceResult(playerId, result);

        // 7. Giữ UI một chút để người chơi nhìn
        yield return new WaitForSeconds(1.2f);

        // 8. Ẩn xúc xắc
        RPC_HideDice();

        // 9. Di chuyển
        yield return StartCoroutine(
            ExecuteMovementStepByStep(slot, playerId, result));
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
    private void RPC_StopDiceSpin(int result)
    {
        BoardDiceVisual.Instance?.StopSpin(result);
    }
    #endregion

    #region Item Use
    public void RequestUseItem(int itemSlot, BoardItemEffect effect)
    {
        if (_itemUsedThisTurn) return;
        if (BoardState != BoardPhaseState.WaitingForRoll) return;

        int myId = Runner?.LocalPlayer.PlayerId ?? -1;
        if (myId < 0) return;

        if (myId != CurrentPlayerID)
        {
            Debug.LogWarning($"[BoardManager] RequestUseItem ignored: not current player (local={myId}, current={CurrentPlayerID})");
            return;
        }

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

        if (effect != BoardItemEffect.Shield && slot >= 0 && _hasShield[slot])
        {
            SetShieldStateForPlayer(userId, false);
        }

        switch (effect)
        {
            case BoardItemEffect.RushForward:
                RPC_ItemUsed(userId, effectId);
                StartCoroutine(ExecuteRushForward(slot, userId, 3));
                break;

            case BoardItemEffect.Shield:
                SetShieldStateForPlayer(userId, true);
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

                if (effect == BoardItemEffect.PositionSwap)
                {
                    RPC_ResetAllTPPreviews();
                    if (eligibles.Count > 0)
                        RPC_SetTPSelectionPreview(GetSlotByPlayerId(eligibles[0]), true);
                }
                else // PushBack
                {
                    RPC_ResetAllGlovePreviews();
                    if (eligibles.Count > 0)
                        RPC_SetGlovePreview(GetSlotByPlayerId(eligibles[0]), true);
                }

                RPC_BeginItemTargetSelect(userId, eligibles.ToArray(), effectId);
                int userSlotVerified = CurrentSlot;
                StartCoroutine(WaitForItemTargetSelect(userSlotVerified, userId));
                break;
        }
    }

    private IEnumerator WaitForItemTargetSelect(int userSlot, int userId)
    {
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
            if (_pendingItemEffect == BoardItemEffect.PositionSwap)
                RPC_ResetAllTPPreviews();
            else
                RPC_ResetAllGlovePreviews();

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

        if (_pendingItemEffect == BoardItemEffect.PositionSwap)
            RPC_SetTPSelectionPreview(targetSlot, false);
        else
            RPC_SetGlovePreview(targetSlot, false);

        if (_pendingItemEffect == BoardItemEffect.PushBack)
            yield return ExecutePushBack(targetSlot, userId);
        else if (_pendingItemEffect == BoardItemEffect.PositionSwap)
            yield return ExecutePositionSwap(verifiedUserSlot, targetSlot, userId, targetId);

        var doneEffect = _pendingItemEffect;
        BoardState = BoardPhaseState.WaitingForRoll;
        ItemUserPlayerId = -1;
        _pendingItemEffect = BoardItemEffect.None;
        RPC_ItemUsed(userId, (int)doneEffect);
    }
    private IEnumerator ExecuteRushForward(int slot, int playerId, int steps)
    {
        if (slot < 0 || playerId < 0)
        {
            BoardState = BoardPhaseState.WaitingForRoll;
            RPC_TurnStarted(playerId);
            yield break;
        }

        var path = BoardNodePath.Instance;
        if (path == null)
        {
            BoardState = BoardPhaseState.WaitingForRoll;
            RPC_TurnStarted(playerId);
            yield break;
        }

        var currentNode = path.GetNodeByID(GetNodeIDAtSlot(slot));
        if (currentNode == null)
        {
            BoardState = BoardPhaseState.WaitingForRoll;
            RPC_TurnStarted(playerId);
            yield break;
        }

        BoardState = BoardPhaseState.Moving;

        // Tính trước node đích, dừng sớm nếu gặp Finish Node giữa đường
        bool hitFinish = false;
        for (int i = 0; i < steps; i++)
        {
            var nextNode = path.GetNextNode(currentNode, 0);
            if (nextNode == null) break;

            currentNode = nextNode;

            if (currentNode.isFinishNode)
            {
                hitFinish = true;
                break;
            }
        }

        SetNodeIDAtSlot(slot, currentNode.nodeID);
        RPC_PlayRushForwardFly(slot, currentNode.nodeID);

        yield return new WaitForSeconds(rushForwardFlyTotalDuration);

        if (hitFinish)
        {
            EndGame(playerId);
            yield break;
        }

        BoardState = BoardPhaseState.WaitingForRoll;
        RPC_TurnStarted(playerId);
    }

    private IEnumerator ExecutePushBack(int targetSlot, int userId)
    {
        if (targetSlot < 0) yield break;

        // Glove hiện ra ngay, tất cả client đều thấy
        RPC_PlayGloveVFX(targetSlot);

        yield return new WaitForSeconds(1f);

        // Tới lúc này mới quyết định: shield chặn hay đẩy lùi thật
        if (_hasShield[targetSlot])
        {
            SetShieldStateForPlayer(GetPlayerIDAtSlot(targetSlot), false);
            RPC_ShieldBlocked(GetPlayerIDAtSlot(targetSlot), userId);
            yield break; // glove vẫn tự ẩn theo GloveRoutine của nó, không cần chờ ở đây
        }

        var pathObj = BoardNodePath.Instance;
        var currentNode = pathObj?.GetNodeByID(GetNodeIDAtSlot(targetSlot));
        if (pathObj != null && currentNode != null)
        {
            var dest = pathObj.GetNodeBeforeSteps(currentNode, 3, out int[] pathIDs);
            SetNodeIDAtSlot(targetSlot, dest.nodeID);
            if (pathIDs.Length > 0)
            {
                RPC_AnimateMovement(targetSlot, pathIDs);
                yield return new WaitForSeconds(pathIDs.Length * (1f / 4f) + 0.4f);
            }
        }

        RPC_FocusOnTargetForPushBack(GetPlayerIDAtSlot(targetSlot));
    }

    private IEnumerator ExecutePositionSwap(int userSlot, int targetSlot, int userId, int targetId)
    {
        if (targetSlot >= 0 && _hasShield[targetSlot])
        {
            SetShieldStateForPlayer(targetId, false);
            RPC_ShieldBlocked(targetId, userId);
            yield break;
        }

        // TP Burst hiện ở token target — trong lúc hiện thì thực hiện swap
        RPC_PlayTPBurst(targetSlot);

        yield return new WaitForSeconds(0.3f); // độ trễ ngắn trước khi thật sự đổi vị trí — chỉnh lại nếu cần khớp timing burst

        int userNode = GetNodeIDAtSlot(userSlot);
        int targetNode = GetNodeIDAtSlot(targetSlot);

        SetNodeIDAtSlot(userSlot, targetNode);
        SetNodeIDAtSlot(targetSlot, userNode);

        RPC_SnapTokensForSwap(userSlot, targetNode, targetSlot, userNode);

        yield return new WaitForSeconds(0.5f);

        RPC_PositionSwapResult(userId, targetId);
        // TP Burst tự tắt sau tổng 2s độc lập, không cần yield thêm ở đây
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
            _localSelectingEffect = (BoardItemEffect)effectId; // NEW

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

    private IEnumerator MoveOneStep(int slot, BoardNode nextNode)
    {
        var token = tokens[slot];

        RPC_AnimateMovement(
            slot,
            new int[]
            {
                nextNode.nodeID
            });

        while (token.IsMoving)
            yield return null;
    }

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

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ShowDirectionSelection(int playerId, int nodeID)
    {
        // Không phải player đang tới lượt thì bỏ qua
        if (Runner.LocalPlayer.PlayerId != playerId)
            return;

        BoardNode node = BoardNodePath.Instance.GetNodeByID(nodeID);

        if (node == null)
            return;

        var ui = FindFirstObjectByType<DirectionSelectionUI>();

        if (ui != null)
        {
            ui.ShowDirectionUI(node);
        }

    }

    private IEnumerator ExecuteMovementStepByStep(
        int slot,
        int playerId,
        int steps)
    {
        BoardState = BoardPhaseState.Moving;

        _remainingSteps = steps;

        _currentMoveNode =
            BoardNodePath.Instance.GetNodeByID(
                GetNodeIDAtSlot(slot));

        if (_currentMoveNode == null)
        {
            yield return FinishTurn(
                slot,
                playerId,
                GetNodeIDAtSlot(slot),
                TileType.Empty);

            yield break;
        }

        while (_remainingSteps > 0)
        {
            BoardNode nextNode =
                BoardNodePath.Instance.GetNextNode(
                    _currentMoveNode,
                    _selectedBranchIndex,
                    _selectedTargetNodeId);

            if (nextNode == null)
                break;

            yield return StartCoroutine(MoveOneStep(slot, nextNode));

            _currentMoveNode = nextNode;
            SetNodeIDAtSlot(slot, nextNode.nodeID);

            // Chạm đích — dừng ngay, không đi thêm dù dư số
            if (_currentMoveNode.isFinishNode)
            {
                _remainingSteps = 0;
                break;
            }

            _remainingSteps--;

            if (_remainingSteps > 0 &&
                _currentMoveNode.nextNodes != null &&
                _currentMoveNode.nextNodes.Count > 1)
            {
                Debug.Log($"[Board] Branch reached at Node {_currentMoveNode.nodeID}");

                BoardState = BoardPhaseState.WaitingForDirection;

                _selectedBranchIndex = 0;
                _selectedTargetNodeId = -1;
                _directionSelected = false;

                RPC_ShowDirectionSelection(
                    playerId,
                    _currentMoveNode.nodeID);

                while (!_directionSelected)
                    yield return null;

                BoardState = BoardPhaseState.Moving;
            }
        }

        yield return FinishTurn(
            slot,
            playerId,
            _currentMoveNode.nodeID,
            _currentMoveNode.tileType);
    }
    #endregion

    #region Tile Resolve
    private IEnumerator FinishTurn(int slot, int playerId, int finalNodeID, TileType tileType)
    {
        BoardState = BoardPhaseState.ResolvingTile;

        var landedNode = BoardNodePath.Instance?.GetNodeByID(finalNodeID);
        if (landedNode != null && landedNode.isFinishNode)
        {
            RPC_PlayerLanded(playerId, finalNodeID, tileType);
            EndGame(playerId);
            yield break; // không resolve tile effect, không tăng _completedThisRound/AdvanceTurn — EndGame lo hết
        }

        switch (tileType)
        {
            case TileType.Steal:
                yield return HandleStealTile(playerId, finalNodeID);
                break;
            case TileType.Trap:
                {
                    BoardPlayerToken token = GetTokenByPlayerId(playerId);

                    if (slot >= 0 && _hasShield[slot])
                    {
                        SetShieldStateForPlayer(playerId, false);
                        RPC_TileMessage(playerId, "SHIELD blocked the trap!");
                    }
                    else
                    {
                        // Nổ trước, đợi 0.5f rồi mới đẩy lùi
                        if (token != null && trapTile != null)
                            trapTile.Trigger(token.transform.position);

                        yield return new WaitForSeconds(0.5f);

                        var pathObj = BoardNodePath.Instance;
                        var currentNode = pathObj?.GetNodeByID(finalNodeID);

                        if (pathObj != null && currentNode != null)
                        {
                            var dest = pathObj.GetNodeBeforeSteps(currentNode, 3, out int[] pathIDs);
                            if (pathIDs.Length > 0)
                            {
                                SetNodeIDAtSlot(slot, dest.nodeID);
                                RPC_AnimateMovement(slot, pathIDs);
                                yield return new WaitForSeconds(pathIDs.Length * (1f / 4f) + 0.4f);
                            }
                        }
                    }

                    RPC_PlayerLanded(playerId, finalNodeID, tileType);
                    yield return new WaitForSeconds(tileResolveDuration);

                    break;
                }
            case TileType.Item:
                ResolveItem(playerId);
                RPC_PlayerLanded(playerId, finalNodeID, tileType);
                yield return new WaitForSeconds(itemResolveDuration);
                break;
            case TileType.Jackpot:
                ResolveJackpot(playerId);
                RPC_PlayerLanded(playerId, finalNodeID, tileType);
                RPC_PlayJackpotChest(finalNodeID);
                yield return new WaitForSeconds(jackpotResolveDuration);
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

        var item = boardItemPool.GetRandom();
        if (inv == null || item == null) return;

        bool ok = inv.AddBoardItem(item.effectType);

        if (ok)
        {
            PlayRewardSfx(itemRewardAudioClip);

            var ui = FindFirstObjectByType<BoardInventoryUI>();
            if (ui != null)
            {
                ui.RefreshAfterRestore();
            }

            RPC_ItemGranted(playerId, (int)item.effectType); // NEW
        }

        if (!ok)
            RPC_TileMessage(playerId, "[BOARD ITEMS FULL]");
        else
            RPC_TileMessage(playerId, $"GOT: {item.itemName} [{item.rarity}]");
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ItemGranted(int playerId, int effectId)
    {
        // Chỉ người vừa ăn item mới thấy animation
        if (Runner == null || Runner.LocalPlayer.PlayerId != playerId) return;

        var effect = (BoardItemEffect)effectId;
        BoardJackpotCardFlyUI.Instance?.PlayJackpotReveal(new[] { effect });
    }

    private void ResolveJackpot(int playerId)
    {
        if (boardItemPool == null) return;
        var inv = PlayerItemInventory.GetForPlayer(playerId);
        if (inv == null) return;

        var grantedEffects = new System.Collections.Generic.List<int>();

        for (int i = 0; i < 2; i++)
        {
            var item = i == 0 ? boardItemPool.GetRandom(ItemRarity.Rare) : boardItemPool.GetRandom();
            if (item == null) continue;

            if (!inv.AddBoardItem(item.effectType))
            {
                RPC_TileMessage(playerId, $"JACKPOT! +{grantedEffects.Count} [FULL]");
                if (grantedEffects.Count > 0)
                    RPC_JackpotItemsGranted(playerId, grantedEffects.ToArray());
                return;
            }

            grantedEffects.Add((int)item.effectType); // lưu ĐÚNG effect vừa add thành công
        }

        if (grantedEffects.Count > 0)
            PlayRewardSfx(jackpotRewardAudioClip);

        RPC_TileMessage(playerId, $"JACKPOT! +{grantedEffects.Count}");

        if (grantedEffects.Count > 0)
            RPC_JackpotItemsGranted(playerId, grantedEffects.ToArray());
    }
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_JackpotItemsGranted(int playerId, int[] effectIds)
    {
        // Chỉ người vừa ăn jackpot mới thấy animation
        if (Runner == null || Runner.LocalPlayer.PlayerId != playerId) return;

        var effects = new BoardItemEffect[effectIds.Length];
        for (int i = 0; i < effectIds.Length; i++)
            effects[i] = (BoardItemEffect)effectIds[i];

        BoardJackpotCardFlyUI.Instance?.PlayJackpotReveal(effects);
    }

    private void PlayRewardSfx(AudioClip clip)
    {
        if (clip != null)
        {
            if (SFXManager.Instance != null)
                SFXManager.Instance.PlaySFX(clip, 1f);
            else
                AudioSource.PlayClipAtPoint(clip, Vector3.zero, 1f);
            return;
        }

        if (SFXManager.Instance != null)
            SFXManager.Instance.PlayButtonClick();
    }

    private void ResolveGamble(int playerId)
    {
        var inv = PlayerItemInventory.GetForPlayer(playerId);
        if (inv == null) return;

        int resultIndex = Random.value >= 0.5f ? 0 : 3;
        RPC_ShowGambleWheel(playerId, resultIndex);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ShowGambleWheel(int playerId, int resultIndex)
    {
        if (gambleWheelUI == null)
        {
            if (gambleWheelRoot != null)
                gambleWheelRoot.SetActive(false);
            return;
        }

        if (gambleWheelRoot != null)
            gambleWheelRoot.SetActive(true);

        gambleWheelUI.ShowWheel(resultIndex, rewardIndex =>
        {
            if (HasStateAuthority)
                ApplyGambleResult(playerId, rewardIndex);
        });
    }

    private void ApplyGambleResult(int playerId, int resultIndex)
    {
        var inv = PlayerItemInventory.GetForPlayer(playerId);
        if (inv == null) return;

        bool win = resultIndex < 3;
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

        RPC_ResetAllStealHandPreviews();
        RPC_SetStealHandPreview(GetSlotByPlayerId(eligibles[0]), true);

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

        int targetSlotForShield = -1;
        for (int i = 0; i < ActivePlayerCount; i++)
            if (GetPlayerIDAtSlot(i) == targetId) { targetSlotForShield = i; break; }

        // Ẩn preview, chạy animation tay chộp — luôn chạy dù sau đó bị shield chặn hay không
        RPC_SetStealHandPreview(targetSlotForShield, false);
        RPC_PlayStealHandTravel(targetSlotForShield);
        yield return new WaitForSeconds(stealHandTravelWait);

        if (targetSlotForShield >= 0 && _hasShield[targetSlotForShield])
        {
            SetShieldStateForPlayer(targetId, false);
            BoardState = BoardPhaseState.ResolvingTile;
            StealerPlayerId = -1;
            RPC_ShieldBlocked(targetId, stealerId);
            yield return new WaitForSeconds(tileResolveDuration);
            yield break;
        }

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

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayerLanded(int playerId, int nodeID, TileType tileType)
    {
        string msg = tileType switch
        {
            TileType.Item => "GOT ITEM!",
            TileType.Steal => "STEAL!",
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
            _targetSelectIndex = 0;
            _waitingForMyItemTarget = false;
            _isSelectingTarget = false;

            if (_eligibleStealTargets.Count > 0)
            {
                BoardHUDController.Instance?.ShowStealSelectionPrompt(stealerId, _eligibleStealTargets, _targetSelectIndex);
                BoardCameraController.Instance?.FocusOnPlayer(_eligibleStealTargets[0]);
            }
            else
            {
                BoardHUDController.Instance?.HideStealSelectionPrompt();
            }
        }
        else
        {
            BoardHUDController.Instance?.HideStealSelectionPrompt();
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

        BoardHUDController.Instance?.HideStealSelectionPrompt();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_StealResult(int stealerId, int targetId, int itemEffect)
    {
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

        // Refresh hand UI ngay — tránh phải đợi tới lượt kế tiếp mới thấy item đổi tay
        FindFirstObjectByType<BoardInventoryUI>()?.RefreshAfterRestore();

        // Card reveal chỉ hiện cho stealer, nạn nhân không thấy gì (item chỉ đơn giản biến mất khỏi hand)
        if (itemEffect >= 0 && Runner != null && Runner.LocalPlayer.PlayerId == stealerId)
        {
            var effect = (BoardItemEffect)itemEffect;
            BoardJackpotCardFlyUI.Instance?.PlayJackpotReveal(new[] { effect });
        }

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
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayJackpotChest(int nodeID)
    {
        if (JackpotChest.TryGet(nodeID, out var chest))
            chest.PlayOpen();
        else
            Debug.LogWarning($"[BoardManager] Không tìm thấy JackpotChest tại node {nodeID}");
    }
    #endregion

    #region Board Complete
    private void CompleteBoardPhase()
    {
        if (!HasStateAuthority) return;

        GameManager.Instance?.SaveBoardPositions(NodeSlot0, NodeSlot1, NodeSlot2, NodeSlot3);

        for (int slot = 0; slot < ActivePlayerCount; slot++)
        {
            int playerId = GetPlayerIDAtSlot(slot);
            int nodeId = GetNodeIDAtSlot(slot);

            if (playerId >= 0)
            {
                GameManager.Instance.SavePlayerBoardPosition(playerId, nodeId);

                Debug.Log($"[BoardManager] Save Position Player={playerId} Node={nodeId}");
            }
        }

        for (int i = 0; i < ActivePlayerCount; i++)
        {
            int pid = GetPlayerIDAtSlot(i);
            if (pid < 0) continue;

            var inv = PlayerItemInventory.GetForPlayer(pid);
            if (inv == null) continue;

            GameManager.Instance?.SaveBoardItemsByPlayer(
                pid,
                inv.BoardItems.Get(0),
                inv.BoardItems.Get(1),
                inv.BoardItems.Get(2),
                inv.BoardItems.Get(3));

        }
        BoardState = BoardPhaseState.BoardComplete;
        RPC_BoardComplete();
        GameManager.Instance?.ProceedFromBoard();
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

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_SubmitBranchSelection(int playerId, int branchIndex, int targetNodeId)
    {
        if (!HasStateAuthority)
            return;

        // Chỉ player đang tới lượt mới được chọn
        if (playerId != CurrentPlayerID)
            return;

        _selectedBranchIndex = branchIndex;
        _selectedTargetNodeId = targetNodeId;
        _directionSelected = true;

        Debug.Log($"[BoardManager] Branch selected by P{playerId}: index={branchIndex}, targetNodeId={targetNodeId}");
    }

    public void SelectBranch(int branchIndex, int targetNodeId = -1)
    {
        if (HasStateAuthority)
        {
            _selectedBranchIndex = branchIndex;
            _selectedTargetNodeId = targetNodeId;
            _directionSelected = true;
        }
        else
        {
            RPC_SubmitBranchSelection(
                Runner.LocalPlayer.PlayerId,
                branchIndex,
                targetNodeId);
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
        if (!HasStateAuthority)
            return;

        for (int slot = 0; slot < ActivePlayerCount; slot++)
        {
            int playerId = GetPlayerIDAtSlot(slot);

            if (GameManager.Instance.TryGetPlayerBoardPosition(playerId, out int nodeId))
            {
                SetNodeIDAtSlot(slot, nodeId);

                Debug.Log($"[Restore] Slot={slot} Player={playerId} Node={nodeId}");
            }
        }

        RPC_SnapTokensToSavedPositions(
            NodeSlot0,
            NodeSlot1,
            NodeSlot2,
            NodeSlot3);

        Debug.Log("[BoardManager] Restore bằng PlayerId hoàn tất");
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

    public void SetShieldStateForPlayer(int playerId, bool active, bool saveToGameManager = true)
    {
        if (playerId < 0) return;

        int slot = GetSlotByPlayerId(playerId);
        if (slot < 0) return;

        _hasShield[slot] = active;

        if (saveToGameManager && GameManager.Instance != null)
            GameManager.Instance.SaveShieldState(playerId, active);

        if (HasStateAuthority)
            RPC_SetShieldVisual(playerId, active);

        var token = GetTokenByPlayerId(playerId);
        token?.SetShieldActive(active);
    }

    public void EndGame(int winnerPlayerId)
    {
        if (!HasStateAuthority)
            return;

        GameManager.Instance.SaveFinalWinner(winnerPlayerId);
        GameManager.Instance.GoToFinal();
    }

    private int GetSlotByPlayerId(int playerId)
    {
        for (int i = 0; i < ActivePlayerCount; i++)
            if (GetPlayerIDAtSlot(i) == playerId) return i;
        return -1;
    }
    public void DebugTeleportPlayerToNode(int playerId, int targetNodeID)
    {
        if (!HasStateAuthority) return;

        int slot = GetSlotByPlayerId(playerId);
        if (slot < 0) return;

        var node = BoardNodePath.Instance?.GetNodeByID(targetNodeID);
        if (node == null)
        {
            Debug.LogWarning($"[BoardManager][DEBUG] Node {targetNodeID} không tồn tại.");
            return;
        }

        SetNodeIDAtSlot(slot, targetNodeID);
        RPC_SnapSingleToken(slot, targetNodeID);

        Debug.Log($"[BoardManager][DEBUG] Teleport P{playerId} → Node {targetNodeID}");

        if (node.isFinishNode)
        {
            Debug.Log($"[BoardManager][DEBUG] Node {targetNodeID} là Finish Node — có thể bấm thêm nút Win để test EndGame.");
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_SnapSingleToken(int slot, int nodeID)
    {
        if (tokens != null && slot < tokens.Length && tokens[slot] != null)
            tokens[slot].SnapToNode(nodeID);
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

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_SetShieldVisual(int playerId, bool active)
    {
        if (!useShieldVfx) return;

        var token = GetTokenByPlayerId(playerId);
        token?.SetShieldActive(active);
    }
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayGloveVFX(int targetSlot)
    {
        if (tokens != null && targetSlot >= 0 && targetSlot < tokens.Length && tokens[targetSlot] != null)
            tokens[targetSlot].PlayGloveHit();
    }
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ResetAllGlovePreviews()
    {
        if (tokens == null) return;
        foreach (var t in tokens)
            t?.SetGlovePreview(false);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_SetGlovePreview(int slot, bool active)
    {
        if (tokens != null && slot >= 0 && slot < tokens.Length && tokens[slot] != null)
            tokens[slot].SetGlovePreview(active);
    }

    // Gọi từ client đang chọn target (proxy), giống pattern RPC_HighlightTarget
    [Rpc(RpcSources.All, RpcTargets.All)]
    private void RPC_SwitchGlovePreview(int prevSlot, int newSlot)
    {
        if (tokens == null) return;
        if (prevSlot >= 0 && prevSlot < tokens.Length && tokens[prevSlot] != null)
            tokens[prevSlot].SetGlovePreview(false);
        if (newSlot >= 0 && newSlot < tokens.Length && tokens[newSlot] != null)
            tokens[newSlot].SetGlovePreview(true);
    }
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ResetAllTPPreviews()
    {
        if (tokens == null) return;
        foreach (var t in tokens)
            t?.SetTPSelectionPreview(false);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_SetTPSelectionPreview(int slot, bool active)
    {
        if (tokens != null && slot >= 0 && slot < tokens.Length && tokens[slot] != null)
            tokens[slot].SetTPSelectionPreview(active);
    }
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ResetAllStealHandPreviews()
    {
        if (tokens == null) return;
        foreach (var t in tokens)
            t?.SetStealHandPreview(false);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_SetStealHandPreview(int slot, bool active)
    {
        if (tokens != null && slot >= 0 && slot < tokens.Length && tokens[slot] != null)
            tokens[slot].SetStealHandPreview(active);
    }

    [Rpc(RpcSources.All, RpcTargets.All)]
    private void RPC_SwitchStealHandPreview(int prevSlot, int newSlot)
    {
        if (tokens == null) return;
        if (prevSlot >= 0 && prevSlot < tokens.Length && tokens[prevSlot] != null)
            tokens[prevSlot].SetStealHandPreview(false);
        if (newSlot >= 0 && newSlot < tokens.Length && tokens[newSlot] != null)
            tokens[newSlot].SetStealHandPreview(true);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayStealHandTravel(int targetSlot)
    {
        if (tokens != null && targetSlot >= 0 && targetSlot < tokens.Length && tokens[targetSlot] != null)
            tokens[targetSlot].PlayStealHandTravel();
    }

    [Rpc(RpcSources.All, RpcTargets.All)]
    private void RPC_SwitchTPPreview(int prevSlot, int newSlot)
    {
        if (tokens == null) return;
        if (prevSlot >= 0 && prevSlot < tokens.Length && tokens[prevSlot] != null)
            tokens[prevSlot].SetTPSelectionPreview(false);
        if (newSlot >= 0 && newSlot < tokens.Length && tokens[newSlot] != null)
            tokens[newSlot].SetTPSelectionPreview(true);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayTPBurst(int targetSlot)
    {
        if (tokens != null && targetSlot >= 0 && targetSlot < tokens.Length && tokens[targetSlot] != null)
            tokens[targetSlot].PlayTPBurst();
    }
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayRushForwardFly(int slot, int targetNodeID)
    {
        if (tokens != null && slot >= 0 && slot < tokens.Length && tokens[slot] != null)
            tokens[slot].PlayRushForwardFly(targetNodeID);
    }
    public void NotifyClientReadyForIntro()
    {
        int myId = Runner != null ? Runner.LocalPlayer.PlayerId : -1;
        if (myId < 0) return;

        RPC_ClientReadyForIntro(myId);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_ClientReadyForIntro(int playerId)
    {
        if (!HasStateAuthority) return;

        int slot = GetSlotByPlayerId(playerId);
        if (slot < 0 || slot >= 4) return;

        if (!_introReady[slot])
        {
            _introReady[slot] = true;
            _introReadyCount++;
            Debug.Log($"[BoardManager] Intro ready: {_introReadyCount}/{ActivePlayerCount} (P{playerId})");
        }
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
        }
        if (HasStateAuthority)
        {
            GUILayout.Space(4);
            GUILayout.Label("[DEBUG] Teleport To Node:");

            GUILayout.BeginHorizontal();
            GUILayout.Label("Node ID:", GUILayout.Width(55));
            string input = GUILayout.TextField(debugTeleportNodeID.ToString(), GUILayout.Width(50));
            if (int.TryParse(input, out int parsed))
                debugTeleportNodeID = parsed;
            GUILayout.EndHorizontal();

            for (int i = 0; i < ActivePlayerCount; i++)
            {
                int pid = GetPlayerIDAtSlot(i);
                if (pid < 0) continue;

                GUI.color = Color.cyan;
                if (GUILayout.Button($"▶ Teleport P{pid} → Node {debugTeleportNodeID}"))
                {
                    DebugTeleportPlayerToNode(pid, debugTeleportNodeID);
                }
                GUI.color = Color.white;
            }
        }
        if (HasStateAuthority)
        {
            GUILayout.Space(4);
            GUILayout.Label("[DEBUG] Force Win:");
            for (int i = 0; i < ActivePlayerCount; i++)
            {
                int pid = GetPlayerIDAtSlot(i);
                if (pid < 0) continue;

                GUI.color = Color.green;
                if (GUILayout.Button($"▶ P{pid} về đích (Win)"))
                {
                    if (!_gameEnded)
                    {
                        _gameEnded = true;
                        EndGame(pid);
                    }
                }
                GUI.color = Color.white;
            }
        }

        GUILayout.EndArea();
    }
    #endregion
}