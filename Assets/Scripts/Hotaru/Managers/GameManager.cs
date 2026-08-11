using Fusion;
using UnityEngine;
using System;
using System.Collections;

public enum GameState
{
    Lobby,
    Voting,          // Vote chọn minigame
    Tutorial,
    Playing,         // Đang chơi minigame
    Scoreboard,
    Board,           // Phase bàn cờ sau mỗi minigame
    Roulette,        // Cò Quay Nga (cuối game)
    Result,
    PickItem
}

/// <summary>
/// Loại voting hiện tại
/// </summary>
public enum VotingType
{
    MinigameOnly,    // Chỉ vote minigame
}

public class GameManager : NetworkBehaviour
{
    #region Singleton
    public static GameManager Instance { get; private set; }
    #endregion
    public bool IsHost => HasStateAuthority;

    #region UI References
    [Header("UI Panels (Auto-found via UIPanel component)")]
    [SerializeField] private GameObject lobbyUI;
    [SerializeField] private GameObject votingUI;           // UI vote chọn minigame
    [SerializeField] private GameObject minigameTieBreakerUI;
    [SerializeField] private GameObject scoreboardUI;
    [SerializeField] private GameObject resultUI;
    [SerializeField] private GameObject itemPickUI;

    [Header("Minigame UI (Main UI - dùng chung)")]
    [SerializeField] private GameObject minigameCountdownUI;  // Countdown UI chính (dùng chung)
    [SerializeField] private TMPro.TMP_Text countdownText;    // Text hiển thị countdown

    [Header("Minigame Scene UI (Tìm khi scene load)")]
    [SerializeField] private GameObject minigameTutorialUI;   // Tutorial trong minigame scene (mỗi scene khác nhau)
    private UnityEngine.UI.Button _tutorialStartButton;       // Button Start trong tutorial (host only)

    [Header("Countdown Settings")]
    [SerializeField] private float countdownTime = 3f;        // Thời gian countdown

    [Header("Scoreboard Settings")]
    [SerializeField] private float scoreboardDisplayDuration = 3f; // Thời gian hiển thị scoreboard trước khi chuyển sang Voting
    private Coroutine _scoreboardCoroutine;
    private Coroutine _countdownCoroutine;
    private Coroutine _startVotingCoroutine;
    #endregion

    #region Minigame Data
    [Header("Minigames")]
    [SerializeField] private MinigameData[] availableMinigames;

    [Header("Roulette Scene")]
    [SerializeField] private string rouletteSceneName = "Roulette Test";

    [Header("Board Scene")]
    [SerializeField] private string boardSceneName = "BoardScene";

    [Header("Item Pick Settings")]
    [SerializeField] private BoardItemPool boardItemPool;
    [SerializeField] private int itemPickCount = 4;
    [SerializeField] private float itemPickTurnDuration = 10f;
    private Coroutine _itemPickCoroutine;
    #endregion

    #region Networked Properties
    [Networked, OnChangedRender(nameof(OnGameStateChanged))]
    public GameState CurrentState { get; private set; } = GameState.Lobby;

    [Networked]
    public int CurrentRound { get; private set; } = 0;

    [Networked]
    public NetworkBool IsFirstBoard { get; private set; } = true;

    [Networked]
    public int TotalRounds { get; private set; } = 3;

    [Networked]
    public int CurrentMinigameIndex { get; private set; } = -1;

    [Networked]
    public int CurrentMinigameActualIndex { get; private set; } = -1;
    [Networked]
    public NetworkBool HasPlayedBoardIntro { get; set; }

    [Networked]
    public VotingType CurrentVotingType { get; private set; } = VotingType.MinigameOnly;

    [Networked]
    public int FinalWinnerId { get; private set; } = -1;

    public void SaveFinalWinner(int winnerId)
    {
        if (!HasStateAuthority) return;
        FinalWinnerId = winnerId;
    }

    /// <summary>
    /// Xếp hạng minigame vừa kết thúc — PlayerId theo rank 1→4 (-1 = không có)
    /// BoardManager đọc để xác định thứ tự tung xúc xắc.
    /// </summary>
    [Networked] public int MgRank1 { get; private set; } = -1;
    [Networked] public int MgRank2 { get; private set; } = -1;
    [Networked] public int MgRank3 { get; private set; } = -1;
    [Networked] public int MgRank4 { get; private set; } = -1;
    [Networked] public int BoardNodeSlot0 { get; private set; } = 0;
    [Networked] public int BoardNodeSlot1 { get; private set; } = 0;
    [Networked] public int BoardNodeSlot2 { get; private set; } = 0;
    [Networked] public int BoardNodeSlot3 { get; private set; } = 0;

    [Networked] public int CharacterPlayerId0 { get; private set; } = -1;
    [Networked] public int CharacterPlayerId1 { get; private set; } = -1;
    [Networked] public int CharacterPlayerId2 { get; private set; } = -1;
    [Networked] public int CharacterPlayerId3 { get; private set; } = -1;

    [Networked] public int CharacterIndex0 { get; private set; } = 0;
    [Networked] public int CharacterIndex1 { get; private set; } = 0;
    [Networked] public int CharacterIndex2 { get; private set; } = 0;
    [Networked] public int CharacterIndex3 { get; private set; } = 0;

    // ===== PLAYER POSITION STATE =====

    [Networked] public int PositionPlayerId0 { get; private set; } = -1;
    [Networked] public int PositionPlayerId1 { get; private set; } = -1;
    [Networked] public int PositionPlayerId2 { get; private set; } = -1;
    [Networked] public int PositionPlayerId3 { get; private set; } = -1;

    [Networked] public int PositionNode0 { get; private set; } = 0;
    [Networked] public int PositionNode1 { get; private set; } = 0;
    [Networked] public int PositionNode2 { get; private set; } = 0;
    [Networked] public int PositionNode3 { get; private set; } = 0;

    // ===== KEY STATE =====

    // Node hiện tại của từng Key
    [Networked] public int KeyNode0 { get; private set; } = -1;
    [Networked] public int KeyNode1 { get; private set; } = -1;
    [Networked] public int KeyNode2 { get; private set; } = -1;
    [Networked] public int KeyNode3 { get; private set; } = -1;

    // Key đã bị nhặt chưa
    [Networked] public NetworkBool KeyCollected0 { get; private set; } = false;
    [Networked] public NetworkBool KeyCollected1 { get; private set; } = false;
    [Networked] public NetworkBool KeyCollected2 { get; private set; } = false;
    [Networked] public NetworkBool KeyCollected3 { get; private set; } = false;
    [Networked] public int BoardItem_P0_S0 { get; private set; } = -1;
    [Networked] public int BoardItem_P0_S1 { get; private set; } = -1;
    [Networked] public int BoardItem_P0_S2 { get; private set; } = -1;
    [Networked] public int BoardItem_P0_S3 { get; private set; } = -1;

    [Networked] public int BoardItem_P1_S0 { get; private set; } = -1;
    [Networked] public int BoardItem_P1_S1 { get; private set; } = -1;
    [Networked] public int BoardItem_P1_S2 { get; private set; } = -1;
    [Networked] public int BoardItem_P1_S3 { get; private set; } = -1;

    [Networked] public int BoardItem_P2_S0 { get; private set; } = -1;
    [Networked] public int BoardItem_P2_S1 { get; private set; } = -1;
    [Networked] public int BoardItem_P2_S2 { get; private set; } = -1;
    [Networked] public int BoardItem_P2_S3 { get; private set; } = -1;

    [Networked] public int BoardItem_P3_S0 { get; private set; } = -1;
    [Networked] public int BoardItem_P3_S1 { get; private set; } = -1;
    [Networked] public int BoardItem_P3_S2 { get; private set; } = -1;
    [Networked] public int BoardItem_P3_S3 { get; private set; } = -1;

    [Networked] public NetworkBool ShieldActive_P0 { get; private set; } = false;
    [Networked] public NetworkBool ShieldActive_P1 { get; private set; } = false;
    [Networked] public NetworkBool ShieldActive_P2 { get; private set; } = false;
    [Networked] public NetworkBool ShieldActive_P3 { get; private set; } = false;

    [Networked] public int ResourcePlayerId_P0 { get; private set; } = -1;
    [Networked] public int ResourcePlayerId_P1 { get; private set; } = -1;
    [Networked] public int ResourcePlayerId_P2 { get; private set; } = -1;
    [Networked] public int ResourcePlayerId_P3 { get; private set; } = -1;

    // ===== ITEM PICK STATE =====
    [Networked] public int ItemPickSlot0 { get; private set; } = -1; // BoardItemEffect
    [Networked] public int ItemPickSlot1 { get; private set; } = -1;
    [Networked] public int ItemPickSlot2 { get; private set; } = -1;
    [Networked] public int ItemPickSlot3 { get; private set; } = -1;

    [Networked] public NetworkBool ItemPickTaken0 { get; private set; } = false;
    [Networked] public NetworkBool ItemPickTaken1 { get; private set; } = false;
    [Networked] public NetworkBool ItemPickTaken2 { get; private set; } = false;
    [Networked] public NetworkBool ItemPickTaken3 { get; private set; } = false;

    [Networked] public int ItemPickTurnPlayerId { get; private set; } = -1;
    [Networked] public int ItemPickTurnOrderIndex { get; private set; } = -1; // 0=top1, 1=top2, 2=top3

    public void SavePlayerCharacter(int playerId, int characterIndex)
    {
        if (CharacterPlayerId0 == -1 || CharacterPlayerId0 == playerId)
        {
            CharacterPlayerId0 = playerId;
            CharacterIndex0 = characterIndex;
            return;
        }

        if (CharacterPlayerId1 == -1 || CharacterPlayerId1 == playerId)
        {
            CharacterPlayerId1 = playerId;
            CharacterIndex1 = characterIndex;
            return;
        }

        if (CharacterPlayerId2 == -1 || CharacterPlayerId2 == playerId)
        {
            CharacterPlayerId2 = playerId;
            CharacterIndex2 = characterIndex;
            return;
        }

        if (CharacterPlayerId3 == -1 || CharacterPlayerId3 == playerId)
        {
            CharacterPlayerId3 = playerId;
            CharacterIndex3 = characterIndex;
        }
    }

    public int GetPlayerCharacter(int playerId)
    {
        if (CharacterPlayerId0 == playerId) return CharacterIndex0;
        if (CharacterPlayerId1 == playerId) return CharacterIndex1;
        if (CharacterPlayerId2 == playerId) return CharacterIndex2;
        if (CharacterPlayerId3 == playerId) return CharacterIndex3;

        return 0;
    }
    public void SaveBoardPositions(int s0, int s1, int s2, int s3)
    {
        if (!HasStateAuthority) return;
        BoardNodeSlot0 = s0;
        BoardNodeSlot1 = s1;
        BoardNodeSlot2 = s2;
        BoardNodeSlot3 = s3;
    }

    public int[] GetBoardPositions() => new[] { BoardNodeSlot0, BoardNodeSlot1, BoardNodeSlot2, BoardNodeSlot3 };

    public void SavePlayerBoardPosition(int playerId, int nodeId)
    {
        if (!HasStateAuthority)
            return;

        int slot = FindOrAssignPositionSlot(playerId);

        switch (slot)
        {
            case 0:
                PositionPlayerId0 = playerId;
                PositionNode0 = nodeId;
                break;

            case 1:
                PositionPlayerId1 = playerId;
                PositionNode1 = nodeId;
                break;

            case 2:
                PositionPlayerId2 = playerId;
                PositionNode2 = nodeId;
                break;

            case 3:
                PositionPlayerId3 = playerId;
                PositionNode3 = nodeId;
                break;
        }
    }

    public bool TryGetPlayerBoardPosition(int playerId, out int nodeId)
    {
        if (PositionPlayerId0 == playerId)
        {
            nodeId = PositionNode0;
            return true;
        }

        if (PositionPlayerId1 == playerId)
        {
            nodeId = PositionNode1;
            return true;
        }

        if (PositionPlayerId2 == playerId)
        {
            nodeId = PositionNode2;
            return true;
        }

        if (PositionPlayerId3 == playerId)
        {
            nodeId = PositionNode3;
            return true;
        }

        nodeId = 0;
        return false;
    }

    private int FindOrAssignPositionSlot(int playerId)
    {
        if (PositionPlayerId0 == playerId) return 0;
        if (PositionPlayerId1 == playerId) return 1;
        if (PositionPlayerId2 == playerId) return 2;
        if (PositionPlayerId3 == playerId) return 3;

        if (PositionPlayerId0 < 0) return 0;
        if (PositionPlayerId1 < 0) return 1;
        if (PositionPlayerId2 < 0) return 2;
        if (PositionPlayerId3 < 0) return 3;

        return Mathf.Abs(playerId) % 4;
    }
    public void SaveKeyState(int index, int nodeId, bool collected)
    {
        if (!HasStateAuthority)
            return;

        switch (index)
        {
            case 0:
                KeyNode0 = nodeId;
                KeyCollected0 = collected;
                break;

            case 1:
                KeyNode1 = nodeId;
                KeyCollected1 = collected;
                break;

            case 2:
                KeyNode2 = nodeId;
                KeyCollected2 = collected;
                break;

            case 3:
                KeyNode3 = nodeId;
                KeyCollected3 = collected;
                break;
        }
    }

    public int GetKeyNode(int index)
    {
        return index switch
        {
            0 => KeyNode0,
            1 => KeyNode1,
            2 => KeyNode2,
            3 => KeyNode3,
            _ => -1
        };
    }

    public bool GetKeyCollected(int index)
    {
        return index switch
        {
            0 => KeyCollected0,
            1 => KeyCollected1,
            2 => KeyCollected2,
            3 => KeyCollected3,
            _ => false
        };
    }

    public bool HasSavedKeyState()
    {
        return KeyNode0 != -1;
    }

    public void SaveBoardItems(int slot, int s0, int s1, int s2, int s3)
    {
        if (!HasStateAuthority) return;

        Debug.Log($"[GameManager] SAVE PlayerSlot={slot} [{s0}, {s1}, {s2}, {s3}]");

        switch (slot)
        {
            case 0: BoardItem_P0_S0 = s0; BoardItem_P0_S1 = s1; BoardItem_P0_S2 = s2; BoardItem_P0_S3 = s3; break;
            case 1: BoardItem_P1_S0 = s0; BoardItem_P1_S1 = s1; BoardItem_P1_S2 = s2; BoardItem_P1_S3 = s3; break;
            case 2: BoardItem_P2_S0 = s0; BoardItem_P2_S1 = s1; BoardItem_P2_S2 = s2; BoardItem_P2_S3 = s3; break;
            case 3: BoardItem_P3_S0 = s0; BoardItem_P3_S1 = s1; BoardItem_P3_S2 = s2; BoardItem_P3_S3 = s3; break;
        }
    }

    public int[] GetBoardItems(int slot) => slot switch
    {
        0 => new[] { BoardItem_P0_S0, BoardItem_P0_S1, BoardItem_P0_S2, BoardItem_P0_S3 },
        1 => new[] { BoardItem_P1_S0, BoardItem_P1_S1, BoardItem_P1_S2, BoardItem_P1_S3 },
        2 => new[] { BoardItem_P2_S0, BoardItem_P2_S1, BoardItem_P2_S2, BoardItem_P2_S3 },
        3 => new[] { BoardItem_P3_S0, BoardItem_P3_S1, BoardItem_P3_S2, BoardItem_P3_S3 },
        _ => new[] { -1, -1, -1, -1 }
    };

    // ===== BOARD ITEMS BY PLAYER =====

    [Networked] public int BoardItemsPlayerId0 { get; private set; } = -1;
    [Networked] public int BoardItemsPlayerId1 { get; private set; } = -1;
    [Networked] public int BoardItemsPlayerId2 { get; private set; } = -1;
    [Networked] public int BoardItemsPlayerId3 { get; private set; } = -1;

    private int FindOrAssignBoardItemSlot(int playerId)
    {
        if (BoardItemsPlayerId0 == playerId) return 0;
        if (BoardItemsPlayerId1 == playerId) return 1;
        if (BoardItemsPlayerId2 == playerId) return 2;
        if (BoardItemsPlayerId3 == playerId) return 3;

        if (BoardItemsPlayerId0 < 0) return 0;
        if (BoardItemsPlayerId1 < 0) return 1;
        if (BoardItemsPlayerId2 < 0) return 2;
        if (BoardItemsPlayerId3 < 0) return 3;

        return Mathf.Abs(playerId) % 4;
    }

    public void SaveBoardItemsByPlayer(
    int playerId,
    int s0,
    int s1,
    int s2,
    int s3)
    {
        if (!HasStateAuthority)
            return;

        int slot = FindOrAssignBoardItemSlot(playerId);

        switch (slot)
        {
            case 0:
                BoardItemsPlayerId0 = playerId;
                BoardItem_P0_S0 = s0;
                BoardItem_P0_S1 = s1;
                BoardItem_P0_S2 = s2;
                BoardItem_P0_S3 = s3;
                break;

            case 1:
                BoardItemsPlayerId1 = playerId;
                BoardItem_P1_S0 = s0;
                BoardItem_P1_S1 = s1;
                BoardItem_P1_S2 = s2;
                BoardItem_P1_S3 = s3;
                break;

            case 2:
                BoardItemsPlayerId2 = playerId;
                BoardItem_P2_S0 = s0;
                BoardItem_P2_S1 = s1;
                BoardItem_P2_S2 = s2;
                BoardItem_P2_S3 = s3;
                break;

            case 3:
                BoardItemsPlayerId3 = playerId;
                BoardItem_P3_S0 = s0;
                BoardItem_P3_S1 = s1;
                BoardItem_P3_S2 = s2;
                BoardItem_P3_S3 = s3;
                break;
        }
    }

    public int[] GetBoardItemsByPlayer(int playerId)
    {
        if (BoardItemsPlayerId0 == playerId)
            return new[]
            {
                BoardItem_P0_S0,
                BoardItem_P0_S1,
                BoardItem_P0_S2,
                BoardItem_P0_S3
            };

        if (BoardItemsPlayerId1 == playerId)
            return new[]
            {
                BoardItem_P1_S0,
                BoardItem_P1_S1,
                BoardItem_P1_S2,
                BoardItem_P1_S3
            };

        if (BoardItemsPlayerId2 == playerId)
            return new[]
            {
                BoardItem_P2_S0,
                BoardItem_P2_S1,
                BoardItem_P2_S2,
                BoardItem_P2_S3
            };

        if (BoardItemsPlayerId3 == playerId)
            return new[]
            {
                BoardItem_P3_S0,
                BoardItem_P3_S1,
                BoardItem_P3_S2,
                BoardItem_P3_S3
            };

        return new[] { -1, -1, -1, -1 };
    }

    public void SavePlayerResourceState(int playerId)
    {
        if (!HasStateAuthority)
            return;

        int slot = FindOrAssignResourceSlot(playerId);
        if (slot < 0)
            return;

        switch (slot)
        {
            case 0:
                ResourcePlayerId_P0 = playerId;
                break;
            case 1:
                ResourcePlayerId_P1 = playerId;
                break;
            case 2:
                ResourcePlayerId_P2 = playerId;
                break;
            case 3:
                ResourcePlayerId_P3 = playerId;
                break;
        }
    }

    public bool TryGetPlayerResourceState(int playerId)
    {
        switch (GetResourceSlotByPlayerId(playerId))
        {
            case 0:
                return true;
            case 1:
                return true;
            case 2:
                return true;
            case 3:
                return true;
            default:
                return false;
        }
    }

    public void SaveShieldState(int playerId, bool active)
    {
        if (!HasStateAuthority)
            return;

        int slot = FindOrAssignShieldSlot(playerId);
        if (slot < 0)
            return;

        switch (slot)
        {
            case 0: ShieldActive_P0 = active; break;
            case 1: ShieldActive_P1 = active; break;
            case 2: ShieldActive_P2 = active; break;
            case 3: ShieldActive_P3 = active; break;
        }
    }

    public bool TryGetShieldState(int playerId, out bool active)
    {
        switch (GetShieldSlotByPlayerId(playerId))
        {
            case 0: active = ShieldActive_P0; return true;
            case 1: active = ShieldActive_P1; return true;
            case 2: active = ShieldActive_P2; return true;
            case 3: active = ShieldActive_P3; return true;
            default: active = false; return false;
        }
    }

    public bool TryRestorePlayerResourceState(int playerId, PlayerItemInventory inventory)
    {
        if (!HasStateAuthority || inventory == null)
            return false;

        if (!TryGetPlayerResourceState(playerId))
            return false;
        return true;
    }

    private int FindOrAssignShieldSlot(int playerId)
    {
        int existed = GetShieldSlotByPlayerId(playerId);
        if (existed >= 0)
            return existed;

        if (ResourcePlayerId_P0 < 0) return 0;
        if (ResourcePlayerId_P1 < 0) return 1;
        if (ResourcePlayerId_P2 < 0) return 2;
        if (ResourcePlayerId_P3 < 0) return 3;

        return Mathf.Abs(playerId) % 4;
    }

    private int GetShieldSlotByPlayerId(int playerId)
    {
        if (ShieldActive_P0 == true && ResourcePlayerId_P0 == playerId) return 0;
        if (ShieldActive_P1 == true && ResourcePlayerId_P1 == playerId) return 1;
        if (ShieldActive_P2 == true && ResourcePlayerId_P2 == playerId) return 2;
        if (ShieldActive_P3 == true && ResourcePlayerId_P3 == playerId) return 3;

        if (ResourcePlayerId_P0 == playerId) return 0;
        if (ResourcePlayerId_P1 == playerId) return 1;
        if (ResourcePlayerId_P2 == playerId) return 2;
        if (ResourcePlayerId_P3 == playerId) return 3;

        return -1;
    }

    private int FindOrAssignResourceSlot(int playerId)
    {
        int existed = GetResourceSlotByPlayerId(playerId);
        if (existed >= 0)
            return existed;

        if (ResourcePlayerId_P0 < 0) return 0;
        if (ResourcePlayerId_P1 < 0) return 1;
        if (ResourcePlayerId_P2 < 0) return 2;
        if (ResourcePlayerId_P3 < 0) return 3;

        // Fallback nếu đủ 4 slot: map theo player id để ổn định.
        return Mathf.Abs(playerId) % 4;
    }

    private int GetResourceSlotByPlayerId(int playerId)
    {
        if (ResourcePlayerId_P0 == playerId) return 0;
        if (ResourcePlayerId_P1 == playerId) return 1;
        if (ResourcePlayerId_P2 == playerId) return 2;
        if (ResourcePlayerId_P3 == playerId) return 3;
        return -1;
    }
    #region Synced Minigame Settings (từ MinigameData, sync cho tất cả clients)
    [Networked] public NetworkBool MG_CanMove { get; private set; } = true;
    [Networked] public NetworkBool MG_CanJump { get; private set; } = true;
    [Networked] public NetworkBool MG_CanCrouch { get; private set; } = true;
    [Networked] public NetworkBool MG_CanAttack { get; private set; } = true;
    [Networked] public NetworkBool MG_CanRun { get; private set; } = true;
    [Networked] public NetworkBool MG_AllowRespawn { get; private set; } = true;
    #endregion
    #endregion

    #region Public Properties

    public MinigameData CurrentMinigameData
    {
        get
        {
            // Ưu tiên actual index đã được host sync để tránh lệch giữa slot vote và minigame thực tế.
            if (CurrentMinigameActualIndex >= 0)
            {
                if (MinigameVotingManager.Instance != null && MinigameVotingManager.Instance.IsReady)
                {
                    var actualData = MinigameVotingManager.Instance.GetMinigameByActualIndex(CurrentMinigameActualIndex);
                    if (actualData != null) return actualData;
                }

                if (availableMinigames != null && CurrentMinigameActualIndex < availableMinigames.Length)
                    return availableMinigames[CurrentMinigameActualIndex];
            }

            if (CurrentMinigameIndex < 0)
                return null;

            // Ưu tiên từ MinigameVotingManager
            if (MinigameVotingManager.Instance != null && MinigameVotingManager.Instance.IsReady)
            {
                var data = MinigameVotingManager.Instance.GetMinigameByAvailableIndex(CurrentMinigameIndex);
                if (data != null) return data;
            }

            // Fallback về availableMinigames
            if (availableMinigames != null && CurrentMinigameIndex < availableMinigames.Length)
            {
                return availableMinigames[CurrentMinigameIndex];
            }

            return null;
        }
    }
    #endregion

    #region Events
    public event Action<GameState, GameState> OnStateChanged;
    public event Action OnItemPickPoolChanged;
    public event Action<int, float> OnItemPickTurnStarted;   // playerId, duration
    public event Action<int> OnItemPickTimerTick;             // remaining seconds
    public event Action<int, int, BoardItemEffect> OnItemPicked; // playerId, slotIndex, effect
    public event Action OnItemPickPhaseEnded;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public override void Spawned()
    {
        // Called when NetworkObject is spawned
        Debug.Log($"[GameManager] Spawned. IsHost: {HasStateAuthority}");

        FindUIReferences();
        HandleStateChange();
    }

    /// <summary>
    /// Tìm UI references - dùng FindObjectsByType với Include Inactive để tìm cả inactive objects
    /// </summary>
    public void FindUIReferences()
    {
        // Tìm tất cả UIPanel kể cả inactive
        var panels = FindObjectsByType<UIPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (var panel in panels)
        {
            RegisterUIPanel(panel);
        }

        Debug.Log($"[GameManager] FindUIReferences - Lobby:{lobbyUI != null}, Voting:{votingUI != null}, Scoreboard:{scoreboardUI != null}, Result:{resultUI != null}, MGTutorial:{minigameTutorialUI != null}, MGCountdown:{minigameCountdownUI != null}");
    }

    /// <summary>
    /// Đăng ký UI Panel - được gọi bởi UIPanel component
    /// </summary>
    public void RegisterUIPanel(UIPanel panel)
    {
        if (panel == null) return;

        switch (panel.PanelType)
        {
            case UIPanelType.Lobby:
                lobbyUI = panel.gameObject;
                break;
            case UIPanelType.Voting:
                votingUI = panel.gameObject;
                break;
            case UIPanelType.MinigameTieBreaker:
                minigameTieBreakerUI = panel.gameObject;
                break;
            case UIPanelType.Scoreboard:
                scoreboardUI = panel.gameObject;
                break;
            case UIPanelType.Result:
                resultUI = panel.gameObject;
                break;
            case UIPanelType.ItemPickCard:          
                itemPickUI = panel.gameObject;
                break;
            case UIPanelType.MinigameTutorial:
                minigameTutorialUI = panel.gameObject;
                SetupTutorialStartButton();
                break;
            case UIPanelType.MinigameCountdown:
                minigameCountdownUI = panel.gameObject;
                // Tìm TMP_Text trong countdown panel (tìm theo tag hoặc tên "CountdownText")
                if (countdownText == null)
                {
                    var texts = panel.GetComponentsInChildren<TMPro.TMP_Text>(true);
                    foreach (var txt in texts)
                    {
                        if (txt.CompareTag("CountdownText") || txt.name.ToLower().Contains("countdown"))
                        {
                            countdownText = txt;
                            Debug.Log($"[GameManager] Found countdown text: {txt.name}");
                            break;
                        }
                    }
                    // Fallback: lấy TMP_Text đầu tiên nếu không tìm thấy
                    if (countdownText == null && texts.Length > 0)
                    {
                        countdownText = texts[0];
                        Debug.Log($"[GameManager] Using first TMP_Text as countdown: {countdownText.name}");
                    }
                }
                break;
        }
    }

    /// <summary>
    /// Tìm và setup Start button trong Tutorial panel
    /// </summary>
    private void SetupTutorialStartButton()
    {
        if (minigameTutorialUI == null) return;

        // Tìm button có tag "MinigameStartButton" hoặc component MinigameStartButton
        var buttons = minigameTutorialUI.GetComponentsInChildren<UnityEngine.UI.Button>(true);
        foreach (var btn in buttons)
        {
            if (btn.CompareTag("MinigameStartButton") || btn.name.ToLower().Contains("start"))
            {
                _tutorialStartButton = btn;
                _tutorialStartButton.onClick.RemoveAllListeners();
                _tutorialStartButton.onClick.AddListener(OnTutorialStartButtonClicked);

                // Chỉ host mới thấy button
                _tutorialStartButton.gameObject.SetActive(HasStateAuthority);
                Debug.Log($"[GameManager] Found and setup tutorial start button: {btn.name}");
                break;
            }
        }
    }

    /// <summary>
    /// Gọi khi Host nhấn nút Start trong Tutorial
    /// </summary>
    private void OnTutorialStartButtonClicked()
    {
        if (!HasStateAuthority)
        {
            Debug.LogWarning("[GameManager] Only host can start minigame");
            return;
        }

        if (CurrentState != GameState.Tutorial)
        {
            Debug.LogWarning($"[GameManager] Cannot start, current state: {CurrentState}");
            return;
        }

        Debug.Log("[GameManager] Host clicked Start - hiding tutorial and starting countdown");

        // Bắt đầu countdown (sync to all clients)
        RPC_StartCountdown();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_StartCountdown()
    {
        // Ẩn tutorial, hiện countdown
        SetActiveUI(minigameTutorialUI, false);
        SetActiveUI(minigameCountdownUI, true);

        // Báo MinigameController chuyển phase
        if (BaseMinigameController.Instance != null)
        {
            BaseMinigameController.Instance.OnCountdownStarted();
        }

        // Host chạy countdown coroutine
        if (HasStateAuthority)
        {
            if (_countdownCoroutine != null)
            {
                StopCoroutine(_countdownCoroutine);
            }
            _countdownCoroutine = StartCoroutine(RunCountdown());
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_StartRoundCountdown()
    {
        SetActiveUI(minigameTutorialUI, false);
        SetActiveUI(minigameCountdownUI, true);

        if (HasStateAuthority)
        {
            if (_countdownCoroutine != null)
                StopCoroutine(_countdownCoroutine);

            _countdownCoroutine = StartCoroutine(RunRoundCountdown());
        }
    }

    private IEnumerator RunCountdown()
    {
        float remaining = countdownTime;

        while (remaining > 0)
        {
            // Update UI for all clients
            RPC_UpdateCountdownUI(Mathf.CeilToInt(remaining));

            yield return new WaitForSeconds(1f);
            remaining -= 1f;
        }

        // Hiện "GO!"
        RPC_UpdateCountdownUI(0);

        yield return new WaitForSeconds(0.5f);

        // Countdown xong -> chuyển sang Playing
        RPC_CountdownComplete();

        _countdownCoroutine = null;
    }

    private IEnumerator RunRoundCountdown()
    {
        float remaining = countdownTime;

        while (remaining > 0)
        {
            RPC_UpdateCountdownUI(Mathf.CeilToInt(remaining));

            yield return new WaitForSeconds(1f);

            remaining -= 1f;
        }

        RPC_UpdateCountdownUI(0);

        yield return new WaitForSeconds(0.5f);

        RPC_FinishRoundCountdown();

        _countdownCoroutine = null;
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_UpdateCountdownUI(int count)
    {
        if (countdownText != null)
        {
            countdownText.text = count > 0 ? count.ToString() : "GO!";
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_CountdownComplete()
    {
        // Ẩn countdown UI
        SetActiveUI(minigameCountdownUI, false);

        // Host chuyển state sang Playing
        if (HasStateAuthority)
        {
            CurrentState = GameState.Playing;
        }

        // Báo MinigameController bắt đầu game
        if (BaseMinigameController.Instance != null)
        {
            BaseMinigameController.Instance.OnCountdownComplete();
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ShowMinigameCountdown()
    {
        SetActiveUI(minigameTutorialUI, false);
        SetActiveUI(minigameCountdownUI, true);
    }

    /// <summary>
    /// Ẩn countdown UI - gọi khi countdown kết thúc
    /// </summary>
    public void HideMinigameCountdown()
    {
        SetActiveUI(minigameCountdownUI, false);
    }

    /// <summary>
    /// Hiển thị Tutorial UI - gọi bởi MinigameController khi scene đã load xong
    /// </summary>
    public void ShowMinigameTutorial()
    {
        // Tìm lại UI vì scene mới load
        FindUIReferences();

        // Hiện tutorial UI cho tất cả player
        SetActiveUI(minigameTutorialUI, true);
        SetActiveUI(minigameCountdownUI, false);

        // Unlock và show cursor cho tất cả player để có thể nhấn button
        if (CursorManager.Instance != null)
        {
            CursorManager.Instance.SetUIMode();
        }

        // Setup lại button nếu cần
        SetupTutorialStartButton();

        Debug.Log("[GameManager] Showing minigame tutorial UI (all players, cursor unlocked)");
    }

    /// <summary>
    /// Hiển thị Scoreboard trong minigame scene - thay thế cho scoreboard của MinigameController
    /// </summary>
    public void ShowMinigameScoreboard()
    {
        SetActiveUI(minigameTutorialUI, false);
        SetActiveUI(minigameCountdownUI, false);
        SetActiveUI(scoreboardUI, true);

        // Hiện cursor
        if (CursorManager.Instance != null)
        {
            CursorManager.Instance.ShowCursor();
        }

        Debug.Log("[GameManager] Showing scoreboard");
    }
    public bool AreAllPlayersReady()
    {
        var players = FindObjectsByType<PlayerNetworkData>(FindObjectsSortMode.None);

        // Không có player nào
        if (players.Length == 0)
            return false;

        // Cần tối thiểu 2 players để start (không cho phép solo)
        if (players.Length < 2)
            return false;

        int readyCount = 0;
        int clientCount = 0;

        // Check tất cả players NGOẠI TRỪ HOST
        // Dùng InputAuthority thay vì HasStateAuthority (vì Host là StateAuthority của mọi thứ trong Hosted mode)
        foreach (var p in players)
        {
            // Bỏ qua host (host không cần ready vì host là người start game)
            // Host's InputAuthority = Runner.LocalPlayer (trên máy host)
            if (p.Object.InputAuthority == Runner.LocalPlayer)
                continue;

            clientCount++;

            // Client đã ready
            if (p.IsReady)
                readyCount++;
        }

        bool allReady = clientCount > 0 && readyCount == clientCount;
        return allReady;
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_FinishRoundCountdown()
    {
        SetActiveUI(minigameCountdownUI, false);
    }
    #endregion

    #region State Change Callback
    private void OnGameStateChanged()
    {
        Debug.Log($"[GameManager] State changed to: {CurrentState}");

        if (HasStateAuthority)
        {
            HandleStateChange();
        }
        else
        {
            // Client delay 1 frame để đảm bảo scene objects đã Awake()
            StartCoroutine(DelayedHandleStateChange());
        }
    }

    private IEnumerator DelayedHandleStateChange()
    {
        yield return null; // chờ 1 frame
        FindUIReferences(); // tìm lại UI sau khi scene load
        HandleStateChange();
    }

    private void HandleStateChange()
    {
        FindUIReferences();

        switch (CurrentState)
        {
            case GameState.Lobby: HandleLobbyState(); break;
            case GameState.Voting: HandleVotingState(); break;
            case GameState.Tutorial:
                HandleTutorialState();
                ShowMinigameTutorial();
                break;
            case GameState.Playing: HandlePlayingState(); break;
            case GameState.Scoreboard: HandleScoreboardState(); break;
            case GameState.Board: HandleBoardState(); break;
            case GameState.Roulette: HandleRouletteState(); break;
            case GameState.Result: HandleResultState(); break;
            case GameState.PickItem: HandlePickItemState(); break;
        }
    }
    #endregion

    #region Host-Only Game Flow Methods
    public void StartMatch()
    {
        if (!HasStateAuthority)
        {
            Debug.LogWarning("[GameManager] Only Host can call StartMatch()");
            return;
        }

        Debug.Log("[GameManager] Starting match...");
        CurrentRound = 0;
        FinalWinnerId = -1;

        // Auto-assign tất cả players vào ghế
        if (SeatManager.Instance != null)
        {
            SeatManager.Instance.AutoAssignAllPlayersToSeats();
        }

        // Lưu seat mapping cho Roulette teleport
        if (RouletteManager.Instance != null)
        {
            RouletteManager.Instance.SaveSeatMapping();
            RouletteManager.Instance.ResetForNewGame();
        }

        // Reset MinigameVotingManager để chuẩn bị danh sách minigame mới
        if (MinigameVotingManager.Instance != null)
        {
            MinigameVotingManager.Instance.ResetPlayedMinigames();
            MinigameVotingManager.Instance.PrepareNextVotingRound();
        }

        // Bat dau game bang Board dau tien
        IsFirstBoard = true;
        StartBoard();
    }

    /// <summary>
    /// Bắt đầu một minigame ngẫu nhiên
    /// </summary>
    public void StartRandomMinigame()
    {
        if (!HasStateAuthority) return;

        if (availableMinigames == null || availableMinigames.Length == 0)
        {
            Debug.LogError("[GameManager] No minigames available!");
            return;
        }

        int randomIndex = UnityEngine.Random.Range(0, availableMinigames.Length);
        Debug.Log($"[GameManager] Starting random minigame #{randomIndex}: {availableMinigames[randomIndex].minigameName}");

        StartMinigame(randomIndex);
    }

    /// <summary>
    /// Bắt đầu voting phase
    /// </summary>
    /// <param name="votingType">Loại voting</param>
    public void StartVoting(VotingType votingType = VotingType.MinigameOnly)
    {
        if (!HasStateAuthority)
        {
            Debug.LogWarning("[GameManager] Only Host can call StartVoting()");
            return;
        }
        CurrentVotingType = votingType;
        Debug.Log($"[GameManager] Starting voting phase... Type: {votingType}");

        if (CurrentState == GameState.Voting)
        {
            // State không thay đổi nên OnChangedRender sẽ không kích hoạt.
            // Dùng RPC để force re-enter voting state trên tất cả clients.
            Debug.Log("[GameManager] Already in Voting state, forcing refresh via RPC");
            RPC_ForceRefreshVotingState();
        }
        else
        {
            ChangeState(GameState.Voting);
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ForceRefreshVotingState()
    {
        Debug.Log("[GameManager] RPC_ForceRefreshVotingState received");
        HandleVotingState();
    }

    public void StartMinigame(int minigameIndex)
    {
        Debug.Log($"[GameManager] StartMinigame called with index: {minigameIndex}, HasStateAuthority: {HasStateAuthority}");

        if (!HasStateAuthority)
        {
            Debug.LogWarning("[GameManager] Only Host can call StartMinigame()");
            return;
        }

        int resolvedActualIndex = minigameIndex;
        if (MinigameVotingManager.Instance != null && MinigameVotingManager.Instance.IsReady)
        {
            int actualFromAvailable = MinigameVotingManager.Instance.GetActualIndexByAvailableIndex(minigameIndex);
            if (actualFromAvailable >= 0)
            {
                resolvedActualIndex = actualFromAvailable;
            }
        }

        StartMinigameActual(resolvedActualIndex);
    }

    public void StartMinigameActual(int actualMinigameIndex)
    {
        if (!HasStateAuthority)
        {
            Debug.LogWarning("[GameManager] Only Host can call StartMinigameActual()");
            return;
        }

        MinigameData minigameData = null;

        if (MinigameVotingManager.Instance != null && MinigameVotingManager.Instance.IsReady)
        {
            if (actualMinigameIndex < 0 || actualMinigameIndex >= MinigameVotingManager.Instance.TotalMinigameCount)
            {
                Debug.LogError($"[GameManager] Invalid actual minigame index: {actualMinigameIndex}");
                return;
            }

            minigameData = MinigameVotingManager.Instance.GetMinigameByActualIndex(actualMinigameIndex);
            CurrentMinigameActualIndex = actualMinigameIndex;
            CurrentMinigameIndex = MinigameVotingManager.Instance.GetAvailableIndexByActualIndex(actualMinigameIndex);

            if (minigameData == null)
            {
                Debug.LogError($"[GameManager] Failed to get minigame data for actual index: {actualMinigameIndex}");
                return;
            }

            Debug.Log($"[GameManager] Starting actual minigame #{actualMinigameIndex}: {minigameData.minigameName} (from MinigameVotingManager actual index)");
            MinigameVotingManager.Instance.MarkMinigamePlayedByActualIndex(actualMinigameIndex);
        }
        else
        {
            if (availableMinigames == null)
            {
                Debug.LogError("[GameManager] availableMinigames is NULL and MinigameVotingManager not ready!");
                return;
            }

            if (actualMinigameIndex < 0 || actualMinigameIndex >= availableMinigames.Length)
            {
                Debug.LogError($"[GameManager] Invalid minigame index: {actualMinigameIndex}, availableMinigames.Length: {availableMinigames.Length}");
                return;
            }

            minigameData = availableMinigames[actualMinigameIndex];
            CurrentMinigameIndex = actualMinigameIndex;
            CurrentMinigameActualIndex = actualMinigameIndex;
            Debug.Log($"[GameManager] Starting fallback minigame #{actualMinigameIndex}: {minigameData.minigameName} (from availableMinigames)");
        }

        CurrentRound++;

        Debug.Log($"[GameManager] Selected minigame - availableIndex: {CurrentMinigameIndex}, actualIndex: {CurrentMinigameActualIndex}");

        // Sync minigame settings cho tất cả clients
        if (minigameData != null)
        {
            MG_CanMove = minigameData.canMove;
            MG_CanJump = minigameData.canJump;
            MG_CanCrouch = minigameData.canCrouch;
            MG_CanAttack = minigameData.canAttack;
            MG_CanRun = minigameData.canRun;
            MG_AllowRespawn = minigameData.allowRespawn;

            Debug.Log($"[GameManager] Synced MG settings - Move:{MG_CanMove}, Jump:{MG_CanJump}, Crouch:{MG_CanCrouch}, Attack:{MG_CanAttack}, Run:{MG_CanRun}, Respawn:{MG_AllowRespawn}");
        }

        // Vào Tutorial state trước, scene sẽ load trong HandleTutorialState
        Debug.Log("[GameManager] Calling ChangeState(Tutorial)");
        ChangeState(GameState.Tutorial);
    }

    /// <summary>
    /// Chuyển từ Tutorial sang Playing - được gọi bởi MinigameController sau countdown
    /// </summary>
    public void StartPlayingState()
    {
        if (!HasStateAuthority)
        {
            Debug.LogWarning("[GameManager] Only Host can call StartPlayingState()");
            return;
        }

        if (CurrentState != GameState.Tutorial)
        {
            Debug.LogWarning($"[GameManager] StartPlayingState called but current state is {CurrentState}");
            return;
        }

        Debug.Log("[GameManager] Tutorial complete, changing to Playing state");
        ChangeState(GameState.Playing);
    }
    public void EndMinigame(int winnerId = -1)
    {
        if (!HasStateAuthority)
        {
            Debug.LogWarning("[GameManager] Only Host can call EndMinigame()");
            return;
        }

        Debug.Log($"[GameManager] Ending minigame... Winner: {winnerId}");

        if (RouletteManager.Instance != null)
        {
            RouletteManager.Instance.OnMinigameCompleted();

            if (winnerId >= 0)
            {
                PlayerRef winnerRef = PlayerRefFromPlayerId(winnerId);
                if (winnerRef != PlayerRef.None)
                {
                    RouletteManager.Instance.OnMinigameWinner(winnerRef);
                }
            }
        }
        ChangeState(GameState.Scoreboard);
    }
    public void ShowScoreboard()
    {
        if (!HasStateAuthority)
        {
            Debug.LogWarning("[GameManager] Only Host can call ShowScoreboard()");
            return;
        }

        Debug.Log("[GameManager] Showing scoreboard...");
        ChangeState(GameState.Scoreboard);
    }

    /// <summary>
    /// Sau khi Scoreboard — luôn đi đến Board phase.
    /// Flow mới: Scoreboard -> Board -> (Voting | Roulette)
    /// </summary>
    public void ProceedFromScoreboard()
    {
        if (!HasStateAuthority)
        {
            Debug.LogWarning("[GameManager] Only Host can call ProceedFromScoreboard()");
            return;
        }

        Debug.Log("[GameManager] Scoreboard done — moving to Board phase");
        StartBoard();
    }

    /// <summary>
    /// Bắt đầu Board phase — load BoardScene.
    /// </summary>
    public void StartBoard()
    {
        if (!HasStateAuthority)
        {
            Debug.LogWarning("[GameManager] Only Host can call StartBoard()");
            return;
        }

        Debug.Log("[GameManager] Starting Board phase...");
        ChangeState(GameState.Board);
    }

    /// <summary>
    /// Gọi bởi BoardSceneController khi BoardScene đã load xong.
    /// Host teleport players và bắt đầu BoardManager.
    /// </summary>
    public void OnBoardSceneReady()
    {
        Debug.Log("[GameManager] Board scene ready");

        if (!HasStateAuthority) return;

        if (BoardManager.Instance == null)
        {
            Debug.LogError("[GameManager] BoardManager.Instance is NULL!");
            return;
        }

        int[] ranking = GetLastMinigameRanking();

        if (ranking.Length == 0)
        {
            var playerList = new System.Collections.Generic.List<int>();
            foreach (var playerRef in Runner.ActivePlayers)
                playerList.Add(playerRef.PlayerId);

            ranking = playerList.ToArray();

            for (int i = ranking.Length - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (ranking[i], ranking[j]) = (ranking[j], ranking[i]);
            }

            Debug.Log($"[GameManager] No ranking found — random order for first board: [{string.Join(", ", ranking)}]");
            Debug.Log($"Board Number = {CurrentRound}");
        }

        StartCoroutine(StartBoardWhenReady(ranking));
    }

    private IEnumerator StartBoardWhenReady(int[] ranking)
    {
        while (PlayerNetworkData.Local == null)
            yield return null;

        Debug.Log("[GameManager] Local Player ready -> Start Board");

        BoardManager.Instance.StartBoardPhase(ranking);

        yield return null;
        RestoreBoardItems();
        RestorePlayerResourceStates();

        int[] saved = GetBoardPositions();
        if (saved[0] != 0 || saved[1] != 0 || saved[2] != 0 || saved[3] != 0)
            BoardManager.Instance.RestoreBoardPositions(saved);

        RestorePlayerResourceStates();
    }

    private void SaveCurrentBoardItems()
    {
        if (!HasStateAuthority) return;
        if (BoardManager.Instance == null) return;

        Debug.Log("========== SAVE CURRENT BOARD ITEMS ==========");

        for (int slot = 0; slot < BoardManager.Instance.ActivePlayerCount; slot++)
        {
            int playerId = BoardManager.Instance.GetPlayerIDAtSlot(slot);
            if (playerId < 0)
                continue;

            var inv = PlayerItemInventory.GetForPlayer(playerId);
            if (inv == null)
            {
                Debug.LogWarning($"No inventory for Player {playerId}");
                continue;
            }

            SaveBoardItems(
                slot,
                inv.BoardItems.Get(0),
                inv.BoardItems.Get(1),
                inv.BoardItems.Get(2),
                inv.BoardItems.Get(3));

            Debug.Log(
                $"Saved Slot {slot}: " +
                $"[{inv.BoardItems.Get(0)}, " +
                $"{inv.BoardItems.Get(1)}, " +
                $"{inv.BoardItems.Get(2)}, " +
                $"{inv.BoardItems.Get(3)}]");
        }

        SaveCurrentPlayerResources();
    }

    private void SaveCurrentPlayerResources()
    {
        if (!HasStateAuthority || BoardManager.Instance == null)
            return;

        for (int slot = 0; slot < BoardManager.Instance.ActivePlayerCount; slot++)
        {
            int playerId = BoardManager.Instance.GetPlayerIDAtSlot(slot);
            if (playerId < 0)
                continue;

            var inv = PlayerItemInventory.GetForPlayer(playerId);
            if (inv == null)
                continue;

            SavePlayerResourceState(playerId);
        }
    }

    private void RestorePlayerResourceStates()
    {
        if (!HasStateAuthority)
            return;

        foreach (var playerRef in Runner.ActivePlayers)
        {
            int playerId = playerRef.PlayerId;
            var inv = PlayerItemInventory.GetForPlayer(playerId);
            if (inv == null)
                continue;

            TryRestorePlayerResourceState(playerId, inv);
        }
    }

    public bool TryRestoreBoardItemsForPlayer(int playerId, PlayerItemInventory inventory)
    {
        if (!HasStateAuthority || inventory == null)
            return false;

        int[] savedItems = GetBoardItemsByPlayer(playerId);

        bool hasAny = false;
        foreach (var v in savedItems)
        {
            if (v != -1)
            {
                hasAny = true;
                break;
            }
        }

        if (!hasAny)
            return false;

        for (int s = 0; s < 4; s++)
            inventory.RemoveBoardItem(s);

        for (int s = 0; s < 4; s++)
        {
            if (savedItems[s] != -1)
            {
                bool ok = inventory.AddBoardItem((BoardItemEffect)savedItems[s]);
                if (!ok)
                    Debug.LogWarning($"[GameManager] Failed to restore board item {savedItems[s]} to P{playerId} at slot {s}");
            }
        }

        Debug.Log($"[GameManager] Restored Board items for P{playerId}: [{string.Join(", ", savedItems)}]");
        return true;
    }

    public void RestoreBoardItems()
    {
        if (!HasStateAuthority)
        {
            Debug.Log($"Restore HasStateAuthority = {HasStateAuthority}");
            return;
        }

        if (BoardManager.Instance == null)
        {
            Debug.LogWarning("[GameManager] RestoreBoardItems skipped because BoardManager is null.");
            return;
        }

        if (BoardManager.Instance.ActivePlayerCount <= 0)
        {
            Debug.LogWarning("[GameManager] RestoreBoardItems skipped because board slots are not initialized yet.");
            return;
        }

        for (int i = 0; i < BoardManager.Instance.ActivePlayerCount; i++)
        {
            int pid = BoardManager.Instance.GetPlayerIDAtSlot(i);
            if (pid < 0) continue;

            var inv = PlayerItemInventory.GetForPlayer(pid);
            if (inv == null)
            {
                Debug.LogWarning($"[GameManager] No inventory for Player {pid} during board-item restore.");
                continue;
            }

            TryRestoreBoardItemsForPlayer(pid, inv);
        }

        if (BoardManager.Instance != null)
        {
            for (int i = 0; i < BoardManager.Instance.ActivePlayerCount; i++)
            {
                int pid = BoardManager.Instance.GetPlayerIDAtSlot(i);
                if (pid < 0) continue;

                bool shieldActive = false;
                if (TryGetShieldState(pid, out bool savedShield))
                    shieldActive = savedShield;

                BoardManager.Instance.SetShieldStateForPlayer(pid, shieldActive, saveToGameManager: false);
            }
        }
    }
    public void ProceedFromBoard()
    {
        if (!HasStateAuthority)
            return;

        Debug.Log("[GameManager] Board complete -> Start Voting");

        StartVoting(VotingType.MinigameOnly);
    }

    /// <summary>
    /// Lưu xếp hạng minigame — gọi bởi MinigameController trước EndMinigame().
    /// rankedPlayerIds: PlayerId theo thứ tự rank 1 → rank N.
    /// </summary>
    public void SetMinigameRanking(int[] rankedPlayerIds)
    {
        if (!HasStateAuthority) return;

        MgRank1 = rankedPlayerIds.Length > 0 ? rankedPlayerIds[0] : -1;
        MgRank2 = rankedPlayerIds.Length > 1 ? rankedPlayerIds[1] : -1;
        MgRank3 = rankedPlayerIds.Length > 2 ? rankedPlayerIds[2] : -1;
        MgRank4 = rankedPlayerIds.Length > 3 ? rankedPlayerIds[3] : -1;

        Debug.Log($"[GameManager] Minigame ranking set: {string.Join(", ", rankedPlayerIds)}");
    }

    /// <summary>
    /// Trả về mảng PlayerId theo rank (bỏ qua slot -1).
    /// </summary>
    public int[] GetLastMinigameRanking()
    {
        var list = new System.Collections.Generic.List<int>();
        if (MgRank1 >= 0) list.Add(MgRank1);
        if (MgRank2 >= 0) list.Add(MgRank2);
        if (MgRank3 >= 0) list.Add(MgRank3);
        if (MgRank4 >= 0) list.Add(MgRank4);
        return list.ToArray();
    }

    /// <summary>
    /// Bắt đầu Roulette (Cò Quay Nga)
    /// </summary>
    public void StartRoulette()
    {
        if (!HasStateAuthority)
        {
            Debug.LogWarning("[GameManager] Only Host can call StartRoulette()");
            return;
        }

        Debug.Log("[GameManager] Starting Roulette...");
        ChangeState(GameState.Roulette);
    }

    /// <summary>
    /// Gọi bởi RouletteManager khi Roulette kết thúc
    /// </summary>
    /// <param name="winnerId">PlayerId của người thắng cuối cùng, -1 nếu không xác định</param>
    public void OnRouletteComplete(int winnerId)
    {
        if (!HasStateAuthority) return;

        Debug.Log($"[GameManager] Roulette complete. Winner: {winnerId}");

        // Check số player còn sống
        int aliveCount = RouletteManager.Instance?.GetAlivePlayerCount() ?? 0;

        if (aliveCount <= 1)
        {
            // Chỉ còn 1 người - kết thúc game
            FinalWinnerId = winnerId;
            Debug.Log("[GameManager] Only 1 player left. Showing final results...");
            ChangeState(GameState.Result);
        }
        else
        {
            // Còn nhiều người - tiếp tục với minigame mới
            Debug.Log("[GameManager] Multiple players left. Starting voting for next minigame...");
            StartVoting(VotingType.MinigameOnly);
        }
    }


    public void ReturnToLobby()
    {
        if (!HasStateAuthority)
        {
            Debug.LogWarning("[GameManager] Only Host can call ReturnToLobby()");
            return;
        }

        Debug.Log("[GameManager] Returning to lobby...");
        CurrentRound = 0;
        CurrentMinigameIndex = -1;
        CurrentMinigameActualIndex = -1;
        FinalWinnerId = -1;

        // Reset Roulette state
        if (RouletteManager.Instance != null)
        {
            RouletteManager.Instance.ResetForNewGame();
        }

        ChangeState(GameState.Lobby);
    }
    public void OnRouletteSceneReady()
    {
        Debug.Log("[GameManager] Roulette scene ready, starting roulette gameplay");

        // Teleport players đến vị trí Roulette dựa trên seat từ Lobby
        if (RouletteManager.Instance != null)
        {
            RouletteManager.Instance.TeleportPlayersToRoulettePositions();
        }

        // Start Roulette (host only)
        if (HasStateAuthority && RouletteManager.Instance != null)
        {
            Debug.Log("[GameManager] Host starting RouletteManager.StartRoulette()");
            RouletteManager.Instance.StartRoulette();
        }
        else if (RouletteManager.Instance == null)
        {
            Debug.LogError("[GameManager] RouletteManager.Instance is NULL!");
        }
    }
    #endregion

    #region Item Pick Phase

    protected virtual void HandlePickItemState()
    {
        Debug.Log("[GameManager] Entered PickItem state");

        SetActiveUI(lobbyUI, false);
        SetActiveUI(votingUI, false);
        SetActiveUI(minigameTieBreakerUI, false);
        SetActiveUI(scoreboardUI, false);
        SetActiveUI(resultUI, false);
        SetActiveUI(minigameTutorialUI, false);
        SetActiveUI(minigameCountdownUI, false);
        SetActiveUI(itemPickUI, true); 

        if (CameraManager.Instance != null)
            CameraManager.Instance.SetCameraRotationLocked(true);

        if (CursorManager.Instance != null)
            CursorManager.Instance.ShowCursor();

        if (PlayerInputHandler.Instance != null)
            PlayerInputHandler.Instance.InputEnabled = false;

        if (!HasStateAuthority) return;

        GenerateItemPickPool();
        BeginItemPickTurns();
    }

    private void StartItemPickPhase()
    {
        if (!HasStateAuthority) return;
        ChangeState(GameState.PickItem);
    }

    private void GenerateItemPickPool()
    {
        if (!HasStateAuthority) return;

        if (boardItemPool == null)
        {
            Debug.LogError("[GameManager] BoardItemPool chưa được assign trong Inspector! Bỏ qua ItemPick phase.");
            FinishItemPickPhase();
            return;
        }

        var picked = new System.Collections.Generic.List<BoardItemEffect>();
        int guard = 0;
        while (picked.Count < itemPickCount && guard < itemPickCount * 20)
        {
            guard++;
            var data = boardItemPool.GetRandom();
            if (data == null) break;
            picked.Add(data.effectType); // Cho phép trùng thẻ giữa các slot (roll độc lập)
        }

        while (picked.Count < itemPickCount)
            picked.Add(BoardItemEffect.None);

        ItemPickSlot0 = (int)picked[0];
        ItemPickSlot1 = (int)picked[1];
        ItemPickSlot2 = (int)picked[2];
        ItemPickSlot3 = (int)picked[3];

        ItemPickTaken0 = false;
        ItemPickTaken1 = false;
        ItemPickTaken2 = false;
        ItemPickTaken3 = false;

        Debug.Log($"[GameManager] Item pick pool: [{picked[0]}, {picked[1]}, {picked[2]}, {picked[3]}]");

        RPC_NotifyItemPickPoolChanged();
    }

    private void BeginItemPickTurns()
    {
        if (!HasStateAuthority) return;

        ItemPickTurnOrderIndex = 0;
        StartNextItemPickTurn();
    }

    private void StartNextItemPickTurn()
    {
        if (!HasStateAuthority) return;

        var ranking = GetLastMinigameRanking(); // rank1..rankN theo playerId
        int maxPickers = Mathf.Min(3, ranking.Length); // Chỉ top1-2-3 được chọn, top4 bỏ qua

        if (ItemPickTurnOrderIndex >= maxPickers || !HasRemainingItemSlots())
        {
            FinishItemPickPhase();
            return;
        }

        int playerId = ranking[ItemPickTurnOrderIndex];
        ItemPickTurnPlayerId = playerId;

        Debug.Log($"[GameManager] ItemPick turn #{ItemPickTurnOrderIndex} -> Player {playerId}");

        RPC_NotifyItemPickTurnStarted(playerId, itemPickTurnDuration);

        if (_itemPickCoroutine != null)
            StopCoroutine(_itemPickCoroutine);
        _itemPickCoroutine = StartCoroutine(RunItemPickTurnTimer());
    }

    private IEnumerator RunItemPickTurnTimer()
    {
        float remaining = itemPickTurnDuration;

        while (remaining > 0)
        {
            RPC_UpdateItemPickTimer(Mathf.CeilToInt(remaining));

            yield return new WaitForSeconds(1f);
            remaining -= 1f;

            // Nếu turn đã được xử lý sớm hơn (player bấm chọn) thì dừng timer luôn
            if (ItemPickTurnPlayerId < 0)
                yield break;
        }

        Debug.Log($"[GameManager] Player {ItemPickTurnPlayerId} hết giờ chọn item -> auto pick");
        AutoPickRandomItemForCurrentTurn();
        _itemPickCoroutine = null;
    }

    /// <summary>
    /// Gọi từ UI (client hoặc host đều gọi hàm này) khi player bấm chọn 1 thẻ.
    /// </summary>
    public void PickItem(int playerId, int slotIndex)
    {
        RPC_RequestPickItem(playerId, slotIndex);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_RequestPickItem(int playerId, int slotIndex)
    {
        if (!HasStateAuthority) return;
        TryPickItem(playerId, slotIndex);
    }

    private void TryPickItem(int playerId, int slotIndex)
    {
        if (!HasStateAuthority) return;
        if (CurrentState != GameState.PickItem) return;
        if (playerId != ItemPickTurnPlayerId) return; // không đúng lượt
        if (slotIndex < 0 || slotIndex >= itemPickCount) return;
        if (IsItemSlotTaken(slotIndex)) return; // thẻ đã bị lấy

        ApplyItemPick(playerId, slotIndex);
    }

    private void AutoPickRandomItemForCurrentTurn()
    {
        if (!HasStateAuthority) return;
        if (ItemPickTurnPlayerId < 0) return;

        var freeSlots = new System.Collections.Generic.List<int>();
        for (int i = 0; i < itemPickCount; i++)
            if (!IsItemSlotTaken(i)) freeSlots.Add(i);

        if (freeSlots.Count == 0)
        {
            ItemPickTurnPlayerId = -1;
            ItemPickTurnOrderIndex++;
            StartNextItemPickTurn();
            return;
        }

        int slot = freeSlots[UnityEngine.Random.Range(0, freeSlots.Count)];
        ApplyItemPick(ItemPickTurnPlayerId, slot);
    }

    private void ApplyItemPick(int playerId, int slotIndex)
    {
        BoardItemEffect effect = GetItemPickSlotEffect(slotIndex);
        SetItemSlotTaken(slotIndex, true);

        var inv = PlayerItemInventory.GetForPlayer(playerId);
        if (inv != null && effect != BoardItemEffect.None)
        {
            bool added = inv.AddBoardItem(effect);
            if (added)
            {
                // Đồng bộ ngay vào networked backup theo playerId để không mất khi qua BoardScene
                SaveBoardItemsByPlayer(
                    playerId,
                    inv.BoardItems.Get(0),
                    inv.BoardItems.Get(1),
                    inv.BoardItems.Get(2),
                    inv.BoardItems.Get(3));
            }
            else
            {
                Debug.LogWarning($"[GameManager] Player {playerId} đầy túi đồ, không thể nhận thêm item {effect}");
            }
        }

        Debug.Log($"[GameManager] Player {playerId} picked slot {slotIndex}: {effect}");

        RPC_NotifyItemPicked(playerId, slotIndex, (int)effect);

        if (_itemPickCoroutine != null)
        {
            StopCoroutine(_itemPickCoroutine);
            _itemPickCoroutine = null;
        }

        ItemPickTurnPlayerId = -1;
        ItemPickTurnOrderIndex++;
        StartNextItemPickTurn();
    }

    private void FinishItemPickPhase()
    {
        if (!HasStateAuthority) return;

        ItemPickTurnPlayerId = -1;
        ItemPickTurnOrderIndex = -1;

        Debug.Log("[GameManager] ItemPick phase kết thúc -> chuyển sang Scoreboard");

        RPC_NotifyItemPickPhaseEnded();

        ProceedFromScoreboard();
    }

    private bool HasRemainingItemSlots()
    {
        for (int i = 0; i < itemPickCount; i++)
            if (!IsItemSlotTaken(i)) return true;
        return false;
    }

    public bool IsItemSlotTaken(int index) => index switch
    {
        0 => ItemPickTaken0,
        1 => ItemPickTaken1,
        2 => ItemPickTaken2,
        3 => ItemPickTaken3,
        _ => true
    };

    private void SetItemSlotTaken(int index, bool taken)
    {
        switch (index)
        {
            case 0: ItemPickTaken0 = taken; break;
            case 1: ItemPickTaken1 = taken; break;
            case 2: ItemPickTaken2 = taken; break;
            case 3: ItemPickTaken3 = taken; break;
        }
    }

    public BoardItemEffect GetItemPickSlotEffect(int index) => index switch
    {
        0 => (BoardItemEffect)ItemPickSlot0,
        1 => (BoardItemEffect)ItemPickSlot1,
        2 => (BoardItemEffect)ItemPickSlot2,
        3 => (BoardItemEffect)ItemPickSlot3,
        _ => BoardItemEffect.None
    };

    /// <summary>Lấy BoardItemData (tên, icon, mô tả...) của 1 slot — dùng cho UI sau này.</summary>
    public BoardItemData GetItemPickSlotData(int index)
    {
        if (boardItemPool == null) return null;
        return boardItemPool.GetByEffect(GetItemPickSlotEffect(index));
    }

    public int ItemPickCount => itemPickCount;

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_NotifyItemPickPoolChanged()
    {
        OnItemPickPoolChanged?.Invoke();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_NotifyItemPickTurnStarted(int playerId, float duration)
    {
        OnItemPickTurnStarted?.Invoke(playerId, duration);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_UpdateItemPickTimer(int remainingSeconds)
    {
        OnItemPickTimerTick?.Invoke(remainingSeconds);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_NotifyItemPicked(int playerId, int slotIndex, int effect)
    {
        OnItemPicked?.Invoke(playerId, slotIndex, (BoardItemEffect)effect);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_NotifyItemPickPhaseEnded()
    {
        OnItemPickPhaseEnded?.Invoke();
    }

    #endregion

    #region State Handlers (Override in subclass or extend)
    protected virtual void HandleLobbyState()
    {
        Debug.Log("[GameManager] Entered Lobby state");

        // Show lobby UI, hide others
        SetActiveUI(lobbyUI, true);
        SetActiveUI(votingUI, false);
        SetActiveUI(minigameTieBreakerUI, false);
        SetActiveUI(scoreboardUI, false);
        SetActiveUI(resultUI, false);
        SetActiveUI(minigameTutorialUI, false);
        SetActiveUI(minigameCountdownUI, false);

        // Hiện cursor trong lobby
        if (CursorManager.Instance != null)
        {
            CursorManager.Instance.SetUIMode();
            CursorManager.Instance.ShowCursor();
        }

        if (AudioManager.Instance != null)
            AudioManager.Instance.EnterMainBGM();

        // Reset player ready states (host only)
        if (HasStateAuthority)
        {
            ResetAllPlayersReady();
        }
    }

    protected virtual void HandleVotingState()
    {
        Debug.Log("[GameManager] Entered Voting state");

        SetActiveUI(lobbyUI, false);
        SetActiveUI(votingUI, false);
        SetActiveUI(minigameTieBreakerUI, false);
        SetActiveUI(scoreboardUI, false);
        SetActiveUI(resultUI, false);
        SetActiveUI(minigameTutorialUI, false);
        SetActiveUI(minigameCountdownUI, false);

        SetActiveUI(votingUI, true);

        // XÓA ĐOẠN NÀY:
        // if (MinigameVotingManager.Instance != null && MinigameVotingManager.Instance.IsReady && HasStateAuthority)
        //     MinigameVotingManager.Instance.PrepareNextVotingRound();

        if (CameraManager.Instance != null)
            CameraManager.Instance.SetCameraRotationLocked(true);

        if (PlayerInputHandler.Instance != null)
            PlayerInputHandler.Instance.InputEnabled = false;

        if (CursorManager.Instance != null)
            CursorManager.Instance.ShowCursor();

        if (HasStateAuthority && VotingManager.Instance != null && !VotingManager.Instance.IsVotingActive)
        {
            if (_startVotingCoroutine == null)
                _startVotingCoroutine = StartCoroutine(StartVotingWhenReady());
        }
        else if (VotingManager.Instance == null)
            Debug.LogError("[GameManager] VotingManager.Instance is NULL!");
    }
    IEnumerator StartVotingWhenReady()
    {
        yield return new WaitUntil(() =>
            VotingManager.Instance != null &&
            VotingManager.Instance.IsReady
        );

        // Đảm bảo PrepareNextVotingRound chạy trước StartVoting
        if (MinigameVotingManager.Instance != null && MinigameVotingManager.Instance.IsReady)
            MinigameVotingManager.Instance.PrepareNextVotingRound();

        VotingManager.Instance.StartVoting();
        _startVotingCoroutine = null;
    }
    protected virtual void HandleTutorialState()
    {
        Debug.Log("[GameManager] Entered Tutorial state");

        // Ẩn tất cả UI panels (minigame UI sẽ được show sau khi scene load)
        SetActiveUI(lobbyUI, false);
        SetActiveUI(votingUI, false);
        SetActiveUI(minigameTieBreakerUI, false);
        SetActiveUI(scoreboardUI, false);
        SetActiveUI(resultUI, false);
        SetActiveUI(minigameTutorialUI, false);
        SetActiveUI(minigameCountdownUI, false);

        // Khóa xoay camera khi xem tutorial
        if (CameraManager.Instance != null)
        {
            CameraManager.Instance.SetCameraRotationLocked(true);
        }

        if (CursorManager.Instance != null)
            CursorManager.Instance.ShowCursor();

        // Tạm thời disable player input (sẽ enable lại khi Playing)
        if (PlayerInputHandler.Instance != null)
        {
            PlayerInputHandler.Instance.InputEnabled = false;
        }

        // Bật minigame BGM ngay từ Tutorial (thay vì đợi đến Playing).
        var tutorialMgData = CurrentMinigameData;
        if (AudioManager.Instance != null)
            AudioManager.Instance.EnterMinigameBGM(tutorialMgData?.minigameBGM);

        // HOST: Load scene minigame
        if (!HasStateAuthority)
        {
            Debug.Log("[GameManager] Not host, waiting for scene load");
            return;
        }

        // Lấy MinigameData - ưu tiên từ MinigameVotingManager
        MinigameData minigameData = CurrentMinigameData;

        // Fallback về availableMinigames nếu không lấy được từ MinigameVotingManager
        if (minigameData == null && availableMinigames != null && CurrentMinigameIndex >= 0 && CurrentMinigameIndex < availableMinigames.Length)
        {
            minigameData = availableMinigames[CurrentMinigameIndex];
        }

        if (minigameData == null)
        {
            Debug.LogError($"[GameManager] No valid minigame data for index {CurrentMinigameIndex}!");
            return;
        }

        Debug.Log($"[GameManager] Loading minigame scene: {minigameData.sceneName}");

        // Setup camera mode
        RPC_SetupMinigameCamera(minigameData.useSharedCamera);

        // Load scene - Fusion sẽ sync tất cả clients
        int sceneIndex = GetSceneIndex(minigameData.sceneName);
        if (sceneIndex < 0)
        {
            Debug.LogError($"[GameManager] Minigame scene '{minigameData.sceneName}' not found in Build Settings!");
            return;
        }
        var sceneRef = SceneRef.FromIndex(sceneIndex);
        if (sceneRef.IsValid)
        {
            Debug.Log($"[GameManager] Loading minigame scene: {minigameData.sceneName} (index {sceneIndex})");

            SaveCurrentBoardItems();

            Runner.LoadScene(sceneRef);
        }
        else
        {
            Debug.LogError($"[GameManager] Invalid SceneRef for scene '{minigameData.sceneName}' index {sceneIndex}!");
        }
    }

    protected virtual void HandlePlayingState()
    {
        Debug.Log("[GameManager] Entered Playing state");

        // Ẩn tất cả UI panels
        SetActiveUI(lobbyUI, false);
        SetActiveUI(votingUI, false);
        SetActiveUI(scoreboardUI, false);
        SetActiveUI(resultUI, false);
        SetActiveUI(minigameTutorialUI, false);
        SetActiveUI(minigameCountdownUI, false);

        // Mở khóa xoay camera khi chơi
        if (CameraManager.Instance != null)
        {
            CameraManager.Instance.SetCameraRotationLocked(false);
        }

        // Bật lại player input
        if (PlayerInputHandler.Instance != null)
        {
            PlayerInputHandler.Instance.InputEnabled = true;
        }

        // Ẩn cursor khi chơi
        if (CursorManager.Instance != null)
        {
            CursorManager.Instance.HideCursor();
        }
    }

    /// <summary>
    /// Lấy scene index từ tên scene (cần setup trong Build Settings)
    /// </summary>
    private int GetSceneIndex(string sceneName)
    {
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCountInBuildSettings; i++)
        {
            string path = UnityEngine.SceneManagement.SceneUtility.GetScenePathByBuildIndex(i);
            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            if (name == sceneName)
            {
                return i;
            }
        }
        Debug.LogError($"[GameManager] Scene '{sceneName}' not found in Build Settings! Check that the scene is added in File > Build Settings.");
        return -1; // Not found
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_SetupMinigameCamera(bool useSharedCamera)
    {
        // Minigame scene sẽ tự setup shared camera nếu cần
        // Thông báo cho CameraManager về mode
        Debug.Log($"[GameManager] Minigame camera mode: {(useSharedCamera ? "Shared/Minigame" : "ThirdPerson")}");

        if (CameraManager.Instance != null)
        {
            if (useSharedCamera)
            {
                // Đặt flag để CameraManager biết đang chờ MinigameCamera setup
                // Điều này ngăn FirstPerson/ThirdPerson camera override trong khi chờ scene load
                CameraManager.Instance.SetPendingSharedCameraMode(true);
            }
            else
            {
                // Minigame dùng Third Person camera (như gameplay bình thường)
                CameraManager.Instance.SwitchToThirdPersonCamera();
            }
        }
    }
    protected virtual void HandleScoreboardState()
    {
        Debug.Log("[GameManager] Entered Scoreboard state");

        SetActiveUI(lobbyUI, false);
        SetActiveUI(votingUI, false);
        SetActiveUI(minigameTieBreakerUI, false);
        SetActiveUI(scoreboardUI, true);
        SetActiveUI(resultUI, false);
        SetActiveUI(minigameTutorialUI, false);
        SetActiveUI(minigameCountdownUI, false);
        SetActiveUI(itemPickUI, false);

        // Khóa xoay camera khi xem scoreboard
        if (CameraManager.Instance != null)
        {
            CameraManager.Instance.SetCameraRotationLocked(true);
        }

        // Hiện cursor khi xem scoreboard
        if (CursorManager.Instance != null)
        {
            CursorManager.Instance.ShowCursor();
        }

        // Tắt player input khi xem scoreboard
        if (PlayerInputHandler.Instance != null)
        {
            PlayerInputHandler.Instance.InputEnabled = false;
        }

        // Auto-proceed to voting after delay (host only)
        if (HasStateAuthority)
        {
            if (_scoreboardCoroutine != null)
            {
                StopCoroutine(_scoreboardCoroutine);
            }
            _scoreboardCoroutine = StartCoroutine(AutoProceedFromScoreboard());
        }
    }

    private IEnumerator AutoProceedFromScoreboard()
    {
        Debug.Log($"[GameManager] Scoreboard will auto-proceed in {scoreboardDisplayDuration}s...");
        yield return new WaitForSeconds(scoreboardDisplayDuration);

        Debug.Log("[GameManager] Auto-proceeding from scoreboard -> starting ItemPick phase...");
        StartItemPickPhase(); // ĐỔI: trước đây gọi ProceedFromScoreboard(), giờ gọi StartItemPickPhase()
        _scoreboardCoroutine = null;
    }

    protected virtual void HandleBoardState()
    {
        Debug.Log("[GameManager] Entered Board state");

        SetActiveUI(lobbyUI, false);
        SetActiveUI(votingUI, false);
        SetActiveUI(minigameTieBreakerUI, false);
        SetActiveUI(scoreboardUI, false);
        SetActiveUI(resultUI, false);
        SetActiveUI(minigameTutorialUI, false);
        SetActiveUI(minigameCountdownUI, false);

        // Lock cursor — board dùng click UI để tung xúc xắc
        if (CursorManager.Instance != null)
            CursorManager.Instance.ShowCursor();

        if (CameraManager.Instance != null)
            CameraManager.Instance.SetCameraRotationLocked(false);

        // Disable player input (không di chuyển character khi ở board)
        if (PlayerInputHandler.Instance != null)
            PlayerInputHandler.Instance.InputEnabled = false;

        if (AudioManager.Instance != null)
            AudioManager.Instance.EnterMainBGM();

        if (!HasStateAuthority) return;

        int sceneIndex = GetSceneIndex(boardSceneName);
        if (sceneIndex < 0)
        {
            Debug.LogError($"[GameManager] BoardScene '{boardSceneName}' not found in Build Settings — cannot load board!");
            return;
        }
        var sceneRef = SceneRef.FromIndex(sceneIndex);
        if (sceneRef.IsValid)
        {
            Debug.Log($"[GameManager] Loading BoardScene: {boardSceneName} (index {sceneIndex})");
            Runner.LoadScene(sceneRef);
        }
        else
        {
            Debug.LogError($"[GameManager] Invalid SceneRef for BoardScene index {sceneIndex}!");
        }
    }

    protected virtual void HandleRouletteState()
    {
        Debug.Log("[GameManager] Entered Roulette state");

        // Ẩn tất cả UI - Roulette xử lí bằng gameplay 3D
        SetActiveUI(lobbyUI, false);
        SetActiveUI(votingUI, false);
        SetActiveUI(minigameTieBreakerUI, false);
        SetActiveUI(scoreboardUI, false);
        SetActiveUI(resultUI, false);
        SetActiveUI(minigameTutorialUI, false);
        SetActiveUI(minigameCountdownUI, false);

        // Chuyển sang First Person camera trong Roulette
        if (CameraManager.Instance != null)
        {
            CameraManager.Instance.SwitchToFirstPersonCamera();
            CameraManager.Instance.SetCameraRotationLocked(false);
        }

        // Bật player input cho gameplay 3D
        if (PlayerInputHandler.Instance != null)
        {
            PlayerInputHandler.Instance.InputEnabled = true;
        }

        // Ẩn cursor cho gameplay 3D
        if (CursorManager.Instance != null)
        {
            CursorManager.Instance.HideCursor();
        }

        // HOST: Load Roulette scene. RouletteSceneController trong scene đó sẽ
        // gọi OnRouletteSceneReady() để teleport players và bắt đầu roulette.
        if (!HasStateAuthority) return;

        int sceneIndex = GetSceneIndex(rouletteSceneName);
        if (sceneIndex < 0)
        {
            Debug.LogError($"[GameManager] Roulette scene '{rouletteSceneName}' not found in Build Settings!");
            return;
        }
        var sceneRef = SceneRef.FromIndex(sceneIndex);
        if (sceneRef.IsValid)
        {
            Debug.Log($"[GameManager] Loading Roulette scene: {rouletteSceneName} (index {sceneIndex})");
            Runner.LoadScene(sceneRef);
        }
        else
        {
            Debug.LogError($"[GameManager] Invalid SceneRef for Roulette scene index {sceneIndex}!");
        }
    }

    protected virtual void HandleResultState()
    {
        Debug.Log("[GameManager] Entered Result state");

        SetActiveUI(lobbyUI, false);
        SetActiveUI(votingUI, false);
        SetActiveUI(minigameTieBreakerUI, false);
        SetActiveUI(scoreboardUI, false);
        SetActiveUI(resultUI, true);
        SetActiveUI(minigameTutorialUI, false);
        SetActiveUI(minigameCountdownUI, false);

        // Hiện cursor
        if (CursorManager.Instance != null)
        {
            CursorManager.Instance.ShowCursor();
        }
    }
    #endregion

    #region Private Helpers
    public void ShowMinigameTieBreakerPanel()
    {
        SetActiveUI(votingUI, false);
        SetActiveUI(minigameTieBreakerUI, true);
    }

    public void HideMinigameTieBreakerPanel()
    {
        SetActiveUI(minigameTieBreakerUI, false);

        if (CurrentState == GameState.Voting)
        {
            SetActiveUI(votingUI, true);
        }
    }

    private void SetActiveUI(GameObject uiObject, bool active)
    {
        if (uiObject != null)
        {
            uiObject.SetActive(active);
            Debug.Log($"[GameManager] SetActiveUI: {uiObject.name} = {active}");
        }
        else
        {
            Debug.LogWarning("[GameManager] SetActiveUI: uiObject is NULL!");
        }
    }

    private void ResetAllPlayersReady()
    {
        var players = FindObjectsByType<PlayerNetworkData>(FindObjectsSortMode.None);
        foreach (var player in players)
        {
            // Note: This requires adding a ResetReady RPC to PlayerNetworkData
            // Or handling via networked property changes
        }
    }

    private void ChangeState(GameState newState)
    {
        Debug.Log($"[GameManager] ChangeState called: {newState}, HasStateAuthority: {HasStateAuthority}");

        if (!HasStateAuthority)
        {
            Debug.LogWarning("[GameManager] ChangeState rejected - not host");
            return;
        }

        var oldState = CurrentState;
        CurrentState = newState;

        Debug.Log($"[GameManager] State: {oldState} -> {newState}");
        OnStateChanged?.Invoke(oldState, newState);
    }

    /// <summary>
    /// Convert PlayerId to PlayerRef
    /// </summary>
    private PlayerRef PlayerRefFromPlayerId(int playerId)
    {
        foreach (var playerRef in Runner.ActivePlayers)
        {
            if (playerRef.PlayerId == playerId)
                return playerRef;
        }
        return PlayerRef.None;
    }
    public PlayerNetworkData GetPlayerNetworkData(int playerId)
    {
        var players = FindObjectsByType<PlayerNetworkData>(FindObjectsSortMode.None);
        foreach (var p in players)
        {
            if (p != null && p.Object != null && p.Object.InputAuthority.PlayerId == playerId)
                return p;
        }
        return null;
    }
    #endregion

}
