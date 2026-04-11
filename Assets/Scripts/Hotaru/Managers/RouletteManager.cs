using Fusion;
using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// Russian Roulette System Manager
/// - Mỗi 2 MG hoặc 1 nếu vote Roulette > MG sẽ vào Roulette
/// - Người thắng MG gần nhất bắn đầu, sau đó đến người thắng MG trước đó
/// - Mỗi lần thắng MG được 1 viên đạn (tối đa 2)
/// - Mỗi player có đạn riêng: thật hoặc giả (không biết trước)
/// - Người bị bắn sẽ vào chế độ khán giả
/// - Lặp lại cho đến khi còn 1 người cuối cùng
/// </summary>
public class RouletteManager : NetworkBehaviour
{
    #region Singleton
    public static RouletteManager Instance { get; private set; }
    #endregion

    #region Constants
    private const int MAX_PLAYERS = 8;
    private const int MAX_BULLETS = 2;
    private const int MINIGAMES_BEFORE_ROULETTE = 2;
    private const int INVALID_PLAYER = -1;
    private const float TIMER_UPDATE_INTERVAL = 0.5f; // Chỉ update timer mỗi 0.5s
    #endregion

    #region Networked Properties - Player State
    /// <summary>
    /// Số đạn mỗi player có (theo slot index)
    /// </summary>
    [Networked, Capacity(8)]
    private NetworkArray<int> PlayerBullets => default;

    /// <summary>
    /// Số đạn THẬT mỗi player có (0 đến PlayerBullets)
    /// Mỗi viên đạn có trạng thái riêng, được quyết định khi nhận đạn
    /// </summary>
    [Networked, Capacity(8)]
    private NetworkArray<int> PlayerRealBullets => default;

    /// <summary>
    /// Trạng thái sống/chết của player (true = còn sống)
    /// </summary>
    [Networked, Capacity(8)]
    private NetworkArray<NetworkBool> PlayerAlive => default;

    /// <summary>
    /// Mapping: slot index -> PlayerRef (để tránh dùng PlayerId trực tiếp)
    /// </summary>
    [Networked, Capacity(8)]
    private NetworkArray<PlayerRef> PlayerSlots => default;

    /// <summary>
    /// Số player đang tham gia
    /// </summary>
    [Networked]
    public int ActivePlayerCount { get; private set; }
    #endregion

    #region Networked Properties - Shooter Queue (synced)
    /// <summary>
    /// Queue người bắn - synced across all clients
    /// </summary>
    [Networked, Capacity(8)]
    private NetworkArray<int> ShooterQueue => default;

    /// <summary>
    /// Số người trong queue
    /// </summary>
    [Networked]
    private int ShooterQueueCount { get; set; }

    /// <summary>
    /// Index hiện tại trong queue
    /// </summary>
    [Networked]
    private int CurrentQueueIndex { get; set; }
    #endregion

    #region Networked Properties - Game State
    /// <summary>
    /// Player hiện tại đang bắn (slot index)
    /// </summary>
    [Networked, OnChangedRender(nameof(OnCurrentShooterChanged))]
    public int CurrentShooterSlot { get; private set; } = INVALID_PLAYER;

    /// <summary>
    /// Roulette đang active
    /// </summary>
    [Networked, OnChangedRender(nameof(OnRouletteStateChanged))]
    public NetworkBool IsRouletteActive { get; private set; }

    /// <summary>
    /// Số minigame đã chơi kể từ lần Roulette cuối
    /// </summary>
    [Networked]
    public int MinigamesSinceLastRoulette { get; private set; }

    /// <summary>
    /// Đang chờ người chơi chọn mục tiêu
    /// </summary>
    [Networked]
    public NetworkBool IsWaitingForShot { get; private set; }

    /// <summary>
    /// Timer dùng TickTimer thay vì Coroutine
    /// </summary>
    [Networked]
    private TickTimer ShootTimer { get; set; }

    /// <summary>
    /// Timer chờ sau khi bắn (hiển thị kết quả)
    /// </summary>
    [Networked]
    private TickTimer ResultTimer { get; set; }

    /// <summary>
    /// Đang chờ kết quả bắn
    /// </summary>
    [Networked]
    private NetworkBool IsWaitingForResult { get; set; }

    /// <summary>
    /// Thời gian còn lại đã broadcast lần cuối (để tránh spam RPC)
    /// </summary>
    [Networked]
    private float LastBroadcastedTime { get; set; }
    #endregion

    #region Networked Properties - Recent Winners (for vote weight & shoot order)
    /// <summary>
    /// Người thắng MG gần nhất (slot index), INVALID_PLAYER nếu không có
    /// </summary>
    [Networked]
    public int LastMinigameWinnerSlot { get; private set; } = INVALID_PLAYER;

    /// <summary>
    /// Người thắng MG trước đó (slot index)
    /// </summary>
    [Networked]
    public int PreviousMinigameWinnerSlot { get; private set; } = INVALID_PLAYER;
    #endregion

    #region Settings
    [Header("Settings")]
    [SerializeField] private float shootTimeLimit = 15f;
    [SerializeField] private float resultDisplayTime = 3f;
    [SerializeField, Range(0f, 1f)] private float realBulletChance = 0.5f; // Tỷ lệ đạn thật khi nhận
    #endregion

    #region Events
    public event Action OnRouletteStarted;
    public event Action OnRouletteEnded;
    public event Action<int> OnShooterChanged; // slot index
    public event Action<int, int, bool> OnShotFired; // shooterSlot, targetSlot, isRealBullet
    public event Action<int> OnPlayerEliminated; // slot index
    public event Action<int> OnPlayerWon; // slot index
    public event Action<float> OnTimerUpdated;
    public event Action<int, int> OnBulletCountChanged; // slot index, newBulletCount
    public event Action<int> OnPlayerBecameSpectator; // slot index - cho client xử lý spectator
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
        DontDestroyOnLoad(gameObject);
    }

    public override void Spawned()
    {
        Debug.Log($"[RouletteManager] Spawned. IsHost: {HasStateAuthority}");

        if (HasStateAuthority)
        {
            InitializePlayerSlots();
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;
        if (!IsRouletteActive) return;

        // Xử lý timer chờ kết quả
        if (IsWaitingForResult)
        {
            if (ResultTimer.Expired(Runner))
            {
                IsWaitingForResult = false;
                CheckRouletteEnd();
            }
            return;
        }

        // Xử lý timer bắn
        if (IsWaitingForShot)
        {
            if (ShootTimer.Expired(Runner))
            {
                AutoShoot();
            }
            else
            {
                // Chỉ broadcast timer khi thay đổi đáng kể (tránh spam RPC)
                float remaining = ShootTimer.RemainingTime(Runner) ?? 0f;
                if (Mathf.Abs(remaining - LastBroadcastedTime) >= TIMER_UPDATE_INTERVAL)
                {
                    LastBroadcastedTime = remaining;
                    RPC_UpdateTimer(remaining);
                }
            }
        }
    }
    #endregion

    #region Initialization
    /// <summary>
    /// Khởi tạo player slots từ danh sách players hiện tại
    /// </summary>
    private void InitializePlayerSlots()
    {
        ActivePlayerCount = 0;

        // Reset all slots
        for (int i = 0; i < MAX_PLAYERS; i++)
        {
            PlayerSlots.Set(i, PlayerRef.None);
            PlayerAlive.Set(i, false);
            PlayerBullets.Set(i, 0);
            PlayerRealBullets.Set(i, 0);
            ShooterQueue.Set(i, INVALID_PLAYER);
        }

        // Assign players to slots
        foreach (var playerRef in Runner.ActivePlayers)
        {
            if (ActivePlayerCount >= MAX_PLAYERS) break;

            PlayerSlots.Set(ActivePlayerCount, playerRef);
            PlayerAlive.Set(ActivePlayerCount, true);
            ActivePlayerCount++;
        }

        ShooterQueueCount = 0;
        CurrentQueueIndex = 0;
        LastMinigameWinnerSlot = INVALID_PLAYER;
        PreviousMinigameWinnerSlot = INVALID_PLAYER;
        LastBroadcastedTime = 0f;

        Debug.Log($"[RouletteManager] Initialized {ActivePlayerCount} player slots");
    }

    /// <summary>
    /// Refresh player slots (gọi khi có player join/leave)
    /// </summary>
    public void RefreshPlayerSlots()
    {
        if (!HasStateAuthority) return;
        InitializePlayerSlots();
    }
    #endregion

    #region Slot <-> PlayerRef Conversion
    /// <summary>
    /// Lấy slot index từ PlayerRef
    /// </summary>
    public int GetSlotFromPlayerRef(PlayerRef playerRef)
    {
        for (int i = 0; i < ActivePlayerCount; i++)
        {
            if (PlayerSlots.Get(i) == playerRef)
                return i;
        }
        return INVALID_PLAYER;
    }

    /// <summary>
    /// Lấy PlayerRef từ slot index
    /// </summary>
    public PlayerRef GetPlayerRefFromSlot(int slot)
    {
        if (slot < 0 || slot >= ActivePlayerCount)
            return PlayerRef.None;
        return PlayerSlots.Get(slot);
    }

    /// <summary>
    /// Lấy slot của local player
    /// </summary>
    public int GetLocalPlayerSlot()
    {
        if (Runner == null) return INVALID_PLAYER;
        return GetSlotFromPlayerRef(Runner.LocalPlayer);
    }
    #endregion

    #region Public Methods - Game Flow

    /// <summary>
    /// Gọi khi player thắng minigame
    /// </summary>
    public void OnMinigameWinner(PlayerRef winnerRef)
    {
        if (!HasStateAuthority) return;

        int slot = GetSlotFromPlayerRef(winnerRef);
        if (slot == INVALID_PLAYER)
        {
            Debug.LogWarning($"[RouletteManager] Winner PlayerRef not found in slots");
            return;
        }

        OnMinigameWinnerBySlot(slot);
    }

    /// <summary>
    /// Gọi khi player thắng minigame (by slot)
    /// </summary>
    public void OnMinigameWinnerBySlot(int slot)
    {
        if (!HasStateAuthority) return;
        if (slot < 0 || slot >= ActivePlayerCount) return;

        Debug.Log($"[RouletteManager] Slot {slot} won minigame");

        // Update winner history
        PreviousMinigameWinnerSlot = LastMinigameWinnerSlot;
        LastMinigameWinnerSlot = slot;

        // Cộng đạn (tối đa 2)
        int currentBullets = PlayerBullets.Get(slot);
        if (currentBullets < MAX_BULLETS)
        {
            // Thêm 1 viên đạn
            PlayerBullets.Set(slot, currentBullets + 1);
            
            // Quyết định đạn thật/giả NGAY KHI NHẬN (không phải khi bắn)
            bool isRealBullet = UnityEngine.Random.value < realBulletChance;
            if (isRealBullet)
            {
                int currentReal = PlayerRealBullets.Get(slot);
                PlayerRealBullets.Set(slot, currentReal + 1);
            }
            
            RPC_NotifyBulletChange(slot, currentBullets + 1);
            Debug.Log($"[RouletteManager] Slot {slot} received {(isRealBullet ? "REAL" : "BLANK")} bullet. Total: {currentBullets + 1}, Real: {PlayerRealBullets.Get(slot)}");
        }
    }

    /// <summary>
    /// Gọi sau mỗi minigame
    /// </summary>
    public void OnMinigameCompleted()
    {
        if (!HasStateAuthority) return;

        MinigamesSinceLastRoulette++;
        Debug.Log($"[RouletteManager] Minigames since last roulette: {MinigamesSinceLastRoulette}");
    }

    public bool ShouldTriggerRoulette()
    {
        return MinigamesSinceLastRoulette >= MINIGAMES_BEFORE_ROULETTE;
    }

    public bool ShouldTriggerVoting()
    {
        return MinigamesSinceLastRoulette >= 1 && MinigamesSinceLastRoulette < MINIGAMES_BEFORE_ROULETTE;
    }

    /// <summary>
    /// Bắt đầu Roulette
    /// </summary>
    public void StartRoulette()
    {
        if (!HasStateAuthority)
        {
            Debug.LogWarning("[RouletteManager] Only Host can start Roulette");
            return;
        }

        Debug.Log("[RouletteManager] Starting Roulette...");

        MinigamesSinceLastRoulette = 0;

        // Đảm bảo có ít nhất 1 đạn thật trong tất cả players
        EnsureAtLeastOneRealBullet();

        // Setup shooter queue
        SetupShooterQueue();

        IsRouletteActive = true;
        IsWaitingForResult = false;
        LastBroadcastedTime = shootTimeLimit;

        RPC_OnRouletteStarted();

        StartNextShooterTurn();
    }

    /// <summary>
    /// Player bắn vào target
    /// </summary>
    public void Shoot(int targetSlot)
    {
        if (!HasStateAuthority) return;
        if (!IsRouletteActive) return;
        if (!IsWaitingForShot) return;

        int shooterSlot = CurrentShooterSlot;

        // Kiểm tra shooter có đạn không
        int bullets = PlayerBullets.Get(shooterSlot);
        if (bullets <= 0)
        {
            Debug.LogWarning($"[RouletteManager] Shooter slot {shooterSlot} has no bullets!");
            // Skip turn nếu không có đạn
            IsWaitingForShot = false;
            ResultTimer = TickTimer.CreateFromSeconds(Runner, 0.5f);
            IsWaitingForResult = true;
            return;
        }

        if (!IsPlayerAliveBySlot(targetSlot))
        {
            Debug.LogWarning($"[RouletteManager] Target slot {targetSlot} is not alive!");
            return;
        }

        IsWaitingForShot = false;

        // Xác định đạn có phải thật không dựa trên tỷ lệ đạn thật của shooter
        int realBullets = PlayerRealBullets.Get(shooterSlot);
        bool isRealBullet = false;
        
        if (realBullets > 0)
        {
            // Tỷ lệ bắn trúng đạn thật = số đạn thật / tổng đạn
            float realChance = (float)realBullets / bullets;
            isRealBullet = UnityEngine.Random.value < realChance;
        }

        Debug.Log($"[RouletteManager] Slot {shooterSlot} shoots Slot {targetSlot}. Bullets: {bullets}, Real: {realBullets}, Shot is real: {isRealBullet}");

        // Trừ đạn
        PlayerBullets.Set(shooterSlot, bullets - 1);
        if (isRealBullet)
        {
            PlayerRealBullets.Set(shooterSlot, realBullets - 1);
        }
        RPC_NotifyBulletChange(shooterSlot, bullets - 1);

        // Notify kết quả
        RPC_OnShotFired(shooterSlot, targetSlot, isRealBullet);

        if (isRealBullet)
        {
            EliminatePlayer(targetSlot);
        }

        // Bắt đầu timer chờ kết quả
        ResultTimer = TickTimer.CreateFromSeconds(Runner, resultDisplayTime);
        IsWaitingForResult = true;
    }
    #endregion

    #region Private Methods

    /// <summary>
    /// Đảm bảo có ít nhất 1 player có đạn thật
    /// </summary>
    private void EnsureAtLeastOneRealBullet()
    {
        bool hasAnyRealBullet = false;
        int firstAliveWithBullets = INVALID_PLAYER;

        for (int i = 0; i < ActivePlayerCount; i++)
        {
            if (PlayerAlive.Get(i) && PlayerBullets.Get(i) > 0)
            {
                if (firstAliveWithBullets == INVALID_PLAYER)
                    firstAliveWithBullets = i;

                if (PlayerRealBullets.Get(i) > 0)
                {
                    hasAnyRealBullet = true;
                    break;
                }
            }
        }

        // Nếu không có ai có đạn thật, cho người đầu tiên có đạn một viên thật
        if (!hasAnyRealBullet && firstAliveWithBullets != INVALID_PLAYER)
        {
            PlayerRealBullets.Set(firstAliveWithBullets, 1);
            Debug.Log($"[RouletteManager] Ensured slot {firstAliveWithBullets} has at least 1 real bullet");
        }
    }

    /// <summary>
    /// Setup shooter queue - synced across all clients
    /// Chỉ thêm player có đạn vào queue
    /// </summary>
    private void SetupShooterQueue()
    {
        // Reset queue
        for (int i = 0; i < MAX_PLAYERS; i++)
        {
            ShooterQueue.Set(i, INVALID_PLAYER);
        }
        ShooterQueueCount = 0;
        CurrentQueueIndex = 0;

        // Người thắng MG gần nhất bắn đầu tiên (nếu có đạn)
        if (LastMinigameWinnerSlot != INVALID_PLAYER 
            && IsPlayerAliveBySlot(LastMinigameWinnerSlot)
            && PlayerBullets.Get(LastMinigameWinnerSlot) > 0)
        {
            AddToShooterQueue(LastMinigameWinnerSlot);
        }

        // Người thắng MG trước đó bắn tiếp theo (nếu có đạn)
        if (PreviousMinigameWinnerSlot != INVALID_PLAYER 
            && PreviousMinigameWinnerSlot != LastMinigameWinnerSlot 
            && IsPlayerAliveBySlot(PreviousMinigameWinnerSlot)
            && PlayerBullets.Get(PreviousMinigameWinnerSlot) > 0)
        {
            AddToShooterQueue(PreviousMinigameWinnerSlot);
        }

        // Thêm các player còn lại có đạn
        for (int i = 0; i < ActivePlayerCount; i++)
        {
            if (IsPlayerAliveBySlot(i) && !IsInShooterQueue(i) && PlayerBullets.Get(i) > 0)
            {
                AddToShooterQueue(i);
            }
        }

        Debug.Log($"[RouletteManager] Shooter queue count: {ShooterQueueCount}");
    }

    private void AddToShooterQueue(int slot)
    {
        if (ShooterQueueCount >= MAX_PLAYERS) return;
        ShooterQueue.Set(ShooterQueueCount, slot);
        ShooterQueueCount++;
    }

    private bool IsInShooterQueue(int slot)
    {
        for (int i = 0; i < ShooterQueueCount; i++)
        {
            if (ShooterQueue.Get(i) == slot)
                return true;
        }
        return false;
    }

    private void RemoveFromShooterQueue(int slot)
    {
        // Find and remove
        int foundIndex = -1;
        for (int i = 0; i < ShooterQueueCount; i++)
        {
            if (ShooterQueue.Get(i) == slot)
            {
                foundIndex = i;
                break;
            }
        }

        if (foundIndex == -1) return;

        // Shift elements
        for (int i = foundIndex; i < ShooterQueueCount - 1; i++)
        {
            ShooterQueue.Set(i, ShooterQueue.Get(i + 1));
        }
        ShooterQueueCount--;

        // Adjust current index if needed
        if (CurrentQueueIndex > foundIndex)
        {
            CurrentQueueIndex--;
        }
        else if (CurrentQueueIndex >= ShooterQueueCount && ShooterQueueCount > 0)
        {
            CurrentQueueIndex = 0;
        }
    }

    private void StartNextShooterTurn()
    {
        if (!HasStateAuthority) return;

        // Tìm shooter tiếp theo còn sống VÀ CÓ ĐẠN
        int attempts = 0;
        while (attempts < ShooterQueueCount)
        {
            if (CurrentQueueIndex >= ShooterQueueCount)
            {
                CurrentQueueIndex = 0;
            }

            int nextShooter = ShooterQueue.Get(CurrentQueueIndex);
            CurrentQueueIndex++;

            // Kiểm tra còn sống VÀ có đạn
            if (nextShooter != INVALID_PLAYER 
                && IsPlayerAliveBySlot(nextShooter) 
                && PlayerBullets.Get(nextShooter) > 0)
            {
                CurrentShooterSlot = nextShooter;
                ShootTimer = TickTimer.CreateFromSeconds(Runner, shootTimeLimit);
                IsWaitingForShot = true;
                LastBroadcastedTime = shootTimeLimit;

                Debug.Log($"[RouletteManager] Slot {nextShooter}'s turn to shoot (has {PlayerBullets.Get(nextShooter)} bullets)");
                RPC_OnShooterTurnStarted(nextShooter);
                return;
            }

            attempts++;
        }

        // Không còn ai có thể bắn - kiểm tra kết thúc
        Debug.Log("[RouletteManager] No valid shooter found in queue");
        CheckRouletteEnd();
    }

    private void AutoShoot()
    {
        if (!HasStateAuthority) return;

        // Kiểm tra shooter có đạn không
        if (PlayerBullets.Get(CurrentShooterSlot) <= 0)
        {
            Debug.Log($"[RouletteManager] Auto-shoot skipped: Slot {CurrentShooterSlot} has no bullets");
            IsWaitingForShot = false;
            ResultTimer = TickTimer.CreateFromSeconds(Runner, 0.5f);
            IsWaitingForResult = true;
            return;
        }

        // Chọn ngẫu nhiên một player còn sống (không phải bản thân)
        List<int> validTargets = new List<int>();
        for (int i = 0; i < ActivePlayerCount; i++)
        {
            if (i != CurrentShooterSlot && IsPlayerAliveBySlot(i))
            {
                validTargets.Add(i);
            }
        }

        if (validTargets.Count > 0)
        {
            int randomTarget = validTargets[UnityEngine.Random.Range(0, validTargets.Count)];
            Debug.Log($"[RouletteManager] Auto-shoot: Slot {CurrentShooterSlot} -> Slot {randomTarget}");
            Shoot(randomTarget);
        }
        else
        {
            // Không có target hợp lệ
            Debug.LogWarning("[RouletteManager] Auto-shoot: No valid targets");
            IsWaitingForShot = false;
            CheckRouletteEnd();
        }
    }

    private void EliminatePlayer(int slot)
    {
        if (!HasStateAuthority) return;
        if (slot < 0 || slot >= ActivePlayerCount) return;

        PlayerAlive.Set(slot, false);
        Debug.Log($"[RouletteManager] Slot {slot} eliminated!");

        // Remove từ shooter queue ngay lập tức
        RemoveFromShooterQueue(slot);

        // Nếu đang tới lượt người này, skip
        if (CurrentShooterSlot == slot)
        {
            IsWaitingForShot = false;
        }

        // Clear winner status nếu là winner
        if (LastMinigameWinnerSlot == slot)
        {
            LastMinigameWinnerSlot = PreviousMinigameWinnerSlot;
            PreviousMinigameWinnerSlot = INVALID_PLAYER;
        }
        else if (PreviousMinigameWinnerSlot == slot)
        {
            PreviousMinigameWinnerSlot = INVALID_PLAYER;
        }

        // Notify elimination
        RPC_OnPlayerEliminated(slot);
        
        // Notify để chuyển player thành spectator
        RPC_OnPlayerBecameSpectator(slot);
    }

    private void CheckRouletteEnd()
    {
        if (!HasStateAuthority) return;

        int aliveCount = GetAlivePlayerCount();

        if (aliveCount <= 1)
        {
            // Kết thúc - có người thắng
            int winner = GetFirstAliveSlot();
            EndRoulette(winner);
        }
        else
        {
            // Kiểm tra còn ai có đạn không
            bool anyoneHasBullets = false;
            for (int i = 0; i < ActivePlayerCount; i++)
            {
                if (IsPlayerAliveBySlot(i) && PlayerBullets.Get(i) > 0)
                {
                    anyoneHasBullets = true;
                    break;
                }
            }

            if (!anyoneHasBullets)
            {
                // Không ai còn đạn - quay lại minigame
                Debug.Log("[RouletteManager] No bullets left, returning to minigame...");
                EndRouletteForMinigame();
            }
            else
            {
                // Rebuild queue và tiếp tục
                SetupShooterQueue();
                StartNextShooterTurn();
            }
        }
    }

    private void EndRoulette(int winnerSlot)
    {
        if (!HasStateAuthority) return;

        IsRouletteActive = false;
        IsWaitingForShot = false;
        IsWaitingForResult = false;
        CurrentShooterSlot = INVALID_PLAYER;

        Debug.Log($"[RouletteManager] Roulette ended. Winner slot: {winnerSlot}");

        if (winnerSlot != INVALID_PLAYER)
        {
            RPC_OnPlayerWon(winnerSlot);
        }

        RPC_OnRouletteEnded();

        if (GameManager.Instance != null)
        {
            PlayerRef winnerRef = winnerSlot != INVALID_PLAYER ? GetPlayerRefFromSlot(winnerSlot) : PlayerRef.None;
            GameManager.Instance.OnRouletteComplete(winnerRef != PlayerRef.None ? winnerRef.PlayerId : -1);
        }
    }

    private void EndRouletteForMinigame()
    {
        if (!HasStateAuthority) return;

        IsRouletteActive = false;
        IsWaitingForShot = false;
        IsWaitingForResult = false;
        CurrentShooterSlot = INVALID_PLAYER;

        Debug.Log("[RouletteManager] Roulette paused, returning to minigame...");

        RPC_OnRouletteEnded();

        if (GameManager.Instance != null)
        {
            // -1 means no winner yet, continue with minigame
            GameManager.Instance.OnRouletteComplete(-1);
        }
    }
    #endregion

    #region Helper Methods

    public bool IsPlayerAliveBySlot(int slot)
    {
        if (slot < 0 || slot >= ActivePlayerCount) return false;
        return PlayerAlive.Get(slot);
    }

    public int GetPlayerBulletsBySlot(int slot)
    {
        if (slot < 0 || slot >= ActivePlayerCount) return 0;
        return PlayerBullets.Get(slot);
    }

    public int GetPlayerRealBulletsBySlot(int slot)
    {
        if (slot < 0 || slot >= ActivePlayerCount) return 0;
        return PlayerRealBullets.Get(slot);
    }

    public int GetAlivePlayerCount()
    {
        int count = 0;
        for (int i = 0; i < ActivePlayerCount; i++)
        {
            if (PlayerAlive.Get(i))
                count++;
        }
        return count;
    }

    public List<int> GetAliveSlots()
    {
        var result = new List<int>();
        for (int i = 0; i < ActivePlayerCount; i++)
        {
            if (PlayerAlive.Get(i))
                result.Add(i);
        }
        return result;
    }

    private int GetFirstAliveSlot()
    {
        for (int i = 0; i < ActivePlayerCount; i++)
        {
            if (PlayerAlive.Get(i))
                return i;
        }
        return INVALID_PLAYER;
    }

    /// <summary>
    /// Lấy vote weight của player (người thắng MG gần nhất có 2 vote)
    /// </summary>
    public int GetPlayerVoteWeight(int slot)
    {
        if (slot == LastMinigameWinnerSlot)
            return 2;
        return 1;
    }

    /// <summary>
    /// Lấy vote weight từ PlayerRef
    /// </summary>
    public int GetPlayerVoteWeightByRef(PlayerRef playerRef)
    {
        int slot = GetSlotFromPlayerRef(playerRef);
        return GetPlayerVoteWeight(slot);
    }

    /// <summary>
    /// Kiểm tra player có thể bắn không (còn sống và có đạn)
    /// </summary>
    public bool CanPlayerShoot(int slot)
    {
        return IsPlayerAliveBySlot(slot) && PlayerBullets.Get(slot) > 0;
    }

    /// <summary>
    /// Reset cho game mới
    /// </summary>
    public void ResetForNewGame()
    {
        if (!HasStateAuthority) return;

        InitializePlayerSlots();
        MinigamesSinceLastRoulette = 0;
        IsRouletteActive = false;
        IsWaitingForShot = false;
        IsWaitingForResult = false;
        CurrentShooterSlot = INVALID_PLAYER;
    }
    #endregion

    #region RPCs

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_OnRouletteStarted()
    {
        OnRouletteStarted?.Invoke();
        Debug.Log("[RouletteManager] RPC: Roulette started");
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_OnRouletteEnded()
    {
        OnRouletteEnded?.Invoke();
        Debug.Log("[RouletteManager] RPC: Roulette ended");
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_OnShooterTurnStarted(int shooterSlot)
    {
        OnShooterChanged?.Invoke(shooterSlot);
        Debug.Log($"[RouletteManager] RPC: Slot {shooterSlot}'s turn");
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_OnShotFired(int shooterSlot, int targetSlot, NetworkBool isRealBullet)
    {
        OnShotFired?.Invoke(shooterSlot, targetSlot, isRealBullet);
        Debug.Log($"[RouletteManager] RPC: Shot - Slot {shooterSlot} -> Slot {targetSlot}, Real: {isRealBullet}");
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_OnPlayerEliminated(int slot)
    {
        OnPlayerEliminated?.Invoke(slot);
        Debug.Log($"[RouletteManager] RPC: Slot {slot} eliminated");
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_OnPlayerBecameSpectator(int slot)
    {
        OnPlayerBecameSpectator?.Invoke(slot);
        Debug.Log($"[RouletteManager] RPC: Slot {slot} became spectator");
        
        // Nếu là local player, xử lý spectator mode
        if (GetLocalPlayerSlot() == slot)
        {
            EnableSpectatorMode();
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_OnPlayerWon(int slot)
    {
        OnPlayerWon?.Invoke(slot);
        Debug.Log($"[RouletteManager] RPC: Slot {slot} won!");
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_NotifyBulletChange(int slot, int newCount)
    {
        OnBulletCountChanged?.Invoke(slot, newCount);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_UpdateTimer(float remaining)
    {
        OnTimerUpdated?.Invoke(remaining);
    }

    /// <summary>
    /// Client gọi để request bắn
    /// </summary>
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_RequestShoot(int targetSlot, RpcInfo info = default)
    {
        // Verify người gọi là shooter hiện tại
        int callerSlot = GetSlotFromPlayerRef(info.Source);
        if (callerSlot != CurrentShooterSlot)
        {
            Debug.LogWarning($"[RouletteManager] Invalid shooter request: slot {callerSlot} != current {CurrentShooterSlot}");
            return;
        }

        // Verify người gọi có đạn
        if (PlayerBullets.Get(callerSlot) <= 0)
        {
            Debug.LogWarning($"[RouletteManager] Shooter slot {callerSlot} has no bullets!");
            return;
        }

        Shoot(targetSlot);
    }
    #endregion

    #region Spectator Mode
    /// <summary>
    /// Bật spectator mode cho local player
    /// </summary>
    private void EnableSpectatorMode()
    {
        Debug.Log("[RouletteManager] Enabling spectator mode for local player");
        
        // Tìm PlayerNetworkData của local player và set spectator
        // Team có thể override logic này
        var localPlayerRef = Runner.LocalPlayer;
        if (Runner.TryGetPlayerObject(localPlayerRef, out var playerObject))
        {
            var playerData = playerObject.GetComponent<PlayerNetworkData>();
            if (playerData != null)
            {
                // Giả sử PlayerNetworkData có method hoặc property để set spectator
                // playerData.SetSpectatorMode(true);
                Debug.Log($"[RouletteManager] Player {localPlayerRef} is now a spectator");
            }
        }
        
        // Có thể disable input, ẩn character, etc.
        // Team implement theo nhu cầu
    }
    #endregion

    #region Callbacks

    private void OnCurrentShooterChanged()
    {
        OnShooterChanged?.Invoke(CurrentShooterSlot);
    }

    private void OnRouletteStateChanged()
    {
        Debug.Log($"[RouletteManager] Roulette state: {(IsRouletteActive ? "Active" : "Inactive")}");
    }
    #endregion

    #region Seat-Based Teleportation
    
    [Header("Roulette Spawn Points")]
    [SerializeField] private Transform[] rouletteSpawnPoints; // Gán trong Inspector (8 điểm)
    
    /// <summary>
    /// Dictionary mapping playerSlot -> seatIndex từ SeatManager
    /// Lưu lại khi bắt đầu match để dùng cho Roulette teleport
    /// </summary>
    private Dictionary<int, int> _playerSlotToSeat = new Dictionary<int, int>();
    
    /// <summary>
    /// Lưu seat mapping từ SeatManager (gọi trước khi vào Roulette)
    /// </summary>
    public void SaveSeatMapping()
    {
        _playerSlotToSeat.Clear();
        
        if (SeatManager.Instance != null)
        {
            _playerSlotToSeat = SeatManager.Instance.GetPlayerSlotToSeatMapping();
            Debug.Log($"[RouletteManager] Saved {_playerSlotToSeat.Count} seat mappings");
        }
    }
    
    /// <summary>
    /// Lấy seat index của player slot
    /// </summary>
    public int GetSeatIndexForSlot(int playerSlot)
    {
        if (_playerSlotToSeat.TryGetValue(playerSlot, out int seatIndex))
        {
            return seatIndex;
        }
        return playerSlot; // Fallback: dùng slot như seat index
    }
    
    /// <summary>
    /// Lấy vị trí spawn trong Roulette scene dựa trên seat
    /// </summary>
    public Vector3 GetRouletteSpawnPosition(int playerSlot)
    {
        int seatIndex = GetSeatIndexForSlot(playerSlot);
        
        if (rouletteSpawnPoints != null && seatIndex >= 0 && seatIndex < rouletteSpawnPoints.Length)
        {
            if (rouletteSpawnPoints[seatIndex] != null)
                return rouletteSpawnPoints[seatIndex].position;
        }
        
        // Fallback: vòng tròn
        float angle = seatIndex * (360f / 8f) * Mathf.Deg2Rad;
        return new Vector3(Mathf.Cos(angle) * 3f, 0f, Mathf.Sin(angle) * 3f);
    }
    
    /// <summary>
    /// Lấy rotation spawn trong Roulette scene dựa trên seat
    /// </summary>
    public Quaternion GetRouletteSpawnRotation(int playerSlot)
    {
        int seatIndex = GetSeatIndexForSlot(playerSlot);
        
        if (rouletteSpawnPoints != null && seatIndex >= 0 && seatIndex < rouletteSpawnPoints.Length)
        {
            if (rouletteSpawnPoints[seatIndex] != null)
                return rouletteSpawnPoints[seatIndex].rotation;
        }
        
        // Fallback: nhìn vào tâm
        float angle = seatIndex * (360f / 8f);
        return Quaternion.Euler(0f, angle + 180f, 0f);
    }
    
    /// <summary>
    /// Teleport tất cả players đến vị trí Roulette dựa trên seat
    /// Gọi sau khi Roulette scene load xong
    /// </summary>
    public void TeleportPlayersToRoulettePositions()
    {
        var players = FindObjectsByType<PlayerNetworkData>(FindObjectsSortMode.None);
        
        foreach (var player in players)
        {
            int slot = GetSlotFromPlayerRef(player.Object.InputAuthority);
            if (slot < 0) continue;
            
            Vector3 position = GetRouletteSpawnPosition(slot);
            Quaternion rotation = GetRouletteSpawnRotation(slot);
            
            var networkCC = player.GetComponent<NetworkCharacterController>();
            if (networkCC != null)
            {
                networkCC.Teleport(position, rotation);
            }
            else
            {
                player.transform.position = position;
                player.transform.rotation = rotation;
            }
            
            Debug.Log($"[RouletteManager] Teleported slot {slot} to seat-based position");
        }
    }
    #endregion
}
