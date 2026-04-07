using Fusion;
using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// Quản lý hệ thống ghế trong Lobby
/// - Mỗi player có 1 ghế cố định
/// - Sync seat assignments cho tất cả clients
/// - Dictionary mapping playerSlot -> seatIndex
/// </summary>
public class SeatManager : NetworkBehaviour
{
    #region Singleton
    public static SeatManager Instance { get; private set; }
    #endregion

    #region Constants
    private const int MAX_SEATS = 8;
    private const int INVALID_SEAT = -1;
    #endregion

    #region Networked Properties
    /// <summary>
    /// Mapping: seat index -> player slot (-1 nếu trống)
    /// </summary>
    [Networked, Capacity(8)]
    private NetworkArray<int> SeatOccupants => default;

    /// <summary>
    /// Số ghế đang có người ngồi
    /// </summary>
    [Networked, OnChangedRender(nameof(OnSeatedCountChanged))]
    public int SeatedPlayerCount { get; private set; }

    /// <summary>
    /// Số ghế tối thiểu để auto-start
    /// </summary>
    [Networked]
    public int MinPlayersToStart { get; private set; } = 2;
    #endregion

    #region Settings
    [Header("Settings")]
    [SerializeField] private int minPlayersToAutoStart = 2;
    [SerializeField] private bool autoStartWhenReady = true;
    
    [Header("Seats (assign in Inspector)")]
    [SerializeField] private Seat[] seats;
    #endregion

    #region Events
    public event Action<int, int> OnPlayerSeated;      // seatIndex, playerSlot
    public event Action<int, int> OnPlayerUnseated;    // seatIndex, playerSlot
    public event Action OnAllPlayersSeated;
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

    public override void Spawned()
    {
        Debug.Log($"[SeatManager] Spawned. IsHost: {HasStateAuthority}");

        if (HasStateAuthority)
        {
            MinPlayersToStart = minPlayersToAutoStart;
            
            // Initialize all seats as empty
            for (int i = 0; i < MAX_SEATS; i++)
            {
                SeatOccupants.Set(i, INVALID_SEAT);
            }
        }

        // Find seats in scene if not assigned
        if (seats == null || seats.Length == 0)
        {
            seats = FindObjectsByType<Seat>(FindObjectsSortMode.None);
        }

        // Register seats
        for (int i = 0; i < seats.Length; i++)
        {
            if (seats[i] != null)
            {
                seats[i].Initialize(i);
            }
        }

        Debug.Log($"[SeatManager] Found {seats.Length} seats");
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
    #endregion

    #region Public Methods

    /// <summary>
    /// Player ngồi vào ghế
    /// </summary>
    public void TrySitDown(int seatIndex, PlayerRef playerRef)
    {
        if (!HasStateAuthority) 
        {
            // Client gọi RPC để request
            RPC_RequestSitDown(seatIndex);
            return;
        }

        SitDownInternal(seatIndex, playerRef);
    }

    /// <summary>
    /// Player đứng dậy
    /// </summary>
    public void TryStandUp(PlayerRef playerRef)
    {
        if (!HasStateAuthority)
        {
            RPC_RequestStandUp();
            return;
        }

        StandUpInternal(playerRef);
    }

    /// <summary>
    /// Kiểm tra ghế có trống không
    /// </summary>
    public bool IsSeatAvailable(int seatIndex)
    {
        if (seatIndex < 0 || seatIndex >= MAX_SEATS) return false;
        return SeatOccupants.Get(seatIndex) == INVALID_SEAT;
    }

    /// <summary>
    /// Lấy seat index của player (-1 nếu không ngồi)
    /// </summary>
    public int GetPlayerSeatIndex(PlayerRef playerRef)
    {
        int playerSlot = GetPlayerSlot(playerRef);
        if (playerSlot == INVALID_SEAT) return INVALID_SEAT;

        for (int i = 0; i < MAX_SEATS; i++)
        {
            if (SeatOccupants.Get(i) == playerSlot)
                return i;
        }
        return INVALID_SEAT;
    }

    /// <summary>
    /// Lấy player slot đang ngồi ở seat
    /// </summary>
    public int GetSeatOccupant(int seatIndex)
    {
        if (seatIndex < 0 || seatIndex >= MAX_SEATS) return INVALID_SEAT;
        return SeatOccupants.Get(seatIndex);
    }

    /// <summary>
    /// Lấy vị trí ghế (dùng cho teleport)
    /// </summary>
    public Vector3 GetSeatPosition(int seatIndex)
    {
        if (seats == null || seatIndex < 0 || seatIndex >= seats.Length)
            return Vector3.zero;
        
        if (seats[seatIndex] != null)
            return seats[seatIndex].SitPosition;
            
        return Vector3.zero;
    }

    /// <summary>
    /// Lấy rotation ghế
    /// </summary>
    public Quaternion GetSeatRotation(int seatIndex)
    {
        if (seats == null || seatIndex < 0 || seatIndex >= seats.Length)
            return Quaternion.identity;
        
        if (seats[seatIndex] != null)
            return seats[seatIndex].SitRotation;
            
        return Quaternion.identity;
    }

    /// <summary>
    /// Dictionary mapping: playerSlot -> seatIndex
    /// Dùng cho RouletteManager teleport
    /// </summary>
    public Dictionary<int, int> GetPlayerSlotToSeatMapping()
    {
        var mapping = new Dictionary<int, int>();
        
        for (int seatIndex = 0; seatIndex < MAX_SEATS; seatIndex++)
        {
            int playerSlot = SeatOccupants.Get(seatIndex);
            if (playerSlot != INVALID_SEAT)
            {
                mapping[playerSlot] = seatIndex;
            }
        }

        return mapping;
    }

    /// <summary>
    /// Reset tất cả ghế (khi về lobby)
    /// </summary>
    public void ResetAllSeats()
    {
        if (!HasStateAuthority) return;

        for (int i = 0; i < MAX_SEATS; i++)
        {
            int occupant = SeatOccupants.Get(i);
            if (occupant != INVALID_SEAT)
            {
                SeatOccupants.Set(i, INVALID_SEAT);
            }
        }
        SeatedPlayerCount = 0;

        // Notify all clients
        RPC_NotifyAllSeatsReset();
    }

    /// <summary>
    /// Tự động assign tất cả players vào ghế khi start game
    /// Không cần tương tác - chỉ cần có đủ player
    /// </summary>
    public void AutoAssignAllPlayersToSeats()
    {
        if (!HasStateAuthority) return;

        var players = FindObjectsByType<PlayerNetworkData>(FindObjectsSortMode.None);
        int seatIndex = 0;

        Debug.Log($"[SeatManager] Auto-assigning {players.Length} players to seats...");

        foreach (var player in players)
        {
            if (seatIndex >= MAX_SEATS || seatIndex >= seats.Length) break;

            PlayerRef playerRef = player.Object.InputAuthority;
            int playerSlot = GetPlayerSlot(playerRef);

            // Skip nếu player đã ngồi rồi
            if (GetPlayerSeatIndex(playerRef) != INVALID_SEAT) continue;

            // Tìm ghế trống tiếp theo
            while (seatIndex < MAX_SEATS && SeatOccupants.Get(seatIndex) != INVALID_SEAT)
            {
                seatIndex++;
            }

            if (seatIndex >= MAX_SEATS) break;

            // Assign player vào ghế
            SeatOccupants.Set(seatIndex, playerSlot);
            SeatedPlayerCount++;

            Debug.Log($"[SeatManager] Auto-assigned player slot {playerSlot} to seat {seatIndex}");

            // Notify all clients
            RPC_NotifyPlayerSeated(seatIndex, playerSlot);

            seatIndex++;
        }

        Debug.Log($"[SeatManager] Auto-assign complete. Total seated: {SeatedPlayerCount}");
    }
    #endregion

    #region Private Methods

    private void SitDownInternal(int seatIndex, PlayerRef playerRef)
    {
        if (seatIndex < 0 || seatIndex >= MAX_SEATS)
        {
            Debug.LogWarning($"[SeatManager] Invalid seat index: {seatIndex}");
            return;
        }

        // Check if seat is available
        if (SeatOccupants.Get(seatIndex) != INVALID_SEAT)
        {
            Debug.Log($"[SeatManager] Seat {seatIndex} already occupied");
            return;
        }

        int playerSlot = GetPlayerSlot(playerRef);
        if (playerSlot == INVALID_SEAT)
        {
            Debug.LogWarning($"[SeatManager] Could not get slot for player {playerRef}");
            return;
        }

        // Check if player is already sitting somewhere
        int currentSeat = GetPlayerSeatIndex(playerRef);
        if (currentSeat != INVALID_SEAT)
        {
            // Stand up first
            SeatOccupants.Set(currentSeat, INVALID_SEAT);
            SeatedPlayerCount--;
        }

        // Sit down
        SeatOccupants.Set(seatIndex, playerSlot);
        SeatedPlayerCount++;

        Debug.Log($"[SeatManager] Player slot {playerSlot} sat on seat {seatIndex}. Total seated: {SeatedPlayerCount}");

        // Notify all clients
        RPC_NotifyPlayerSeated(seatIndex, playerSlot);

        // Check auto-start
        CheckAutoStart();
    }

    private void StandUpInternal(PlayerRef playerRef)
    {
        int seatIndex = GetPlayerSeatIndex(playerRef);
        if (seatIndex == INVALID_SEAT)
        {
            Debug.Log($"[SeatManager] Player {playerRef} not sitting");
            return;
        }

        int playerSlot = SeatOccupants.Get(seatIndex);
        SeatOccupants.Set(seatIndex, INVALID_SEAT);
        SeatedPlayerCount--;

        Debug.Log($"[SeatManager] Player slot {playerSlot} stood up from seat {seatIndex}");

        // Notify all clients
        RPC_NotifyPlayerUnseated(seatIndex, playerSlot);
    }

    private int GetPlayerSlot(PlayerRef playerRef)
    {
        // Thử lấy slot từ RouletteManager trước
        if (RouletteManager.Instance != null)
        {
            int slot = RouletteManager.Instance.GetSlotFromPlayerRef(playerRef);
            if (slot != INVALID_SEAT)
            {
                return slot;
            }
            // RouletteManager trả về -1, fallback sang PlayerId
            Debug.Log($"[SeatManager] RouletteManager returned -1 for player {playerRef.PlayerId}, using PlayerId as slot");
        }

        // Fallback: use PlayerId
        return playerRef.PlayerId;
    }

    private void CheckAutoStart()
    {
        if (!autoStartWhenReady) return;
        if (SeatedPlayerCount < MinPlayersToStart) return;

        // Check if all seated players are ready
        var players = FindObjectsByType<PlayerNetworkData>(FindObjectsSortMode.None);
        int readyCount = 0;

        foreach (var player in players)
        {
            int seatIndex = GetPlayerSeatIndex(player.Object.InputAuthority);
            if (seatIndex != INVALID_SEAT && player.IsReady)
            {
                readyCount++;
            }
        }

        // All seated players ready -> auto start
        if (readyCount >= MinPlayersToStart && readyCount == SeatedPlayerCount)
        {
            Debug.Log($"[SeatManager] All {readyCount} seated players ready! Auto-starting...");
            OnAllPlayersSeated?.Invoke();

            if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Lobby)
            {
                GameManager.Instance.StartMatch();
            }
        }
    }

    private void OnSeatedCountChanged()
    {
        Debug.Log($"[SeatManager] Seated count changed to: {SeatedPlayerCount}");
    }
    #endregion

    #region RPCs

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestSitDown(int seatIndex, RpcInfo info = default)
    {
        SitDownInternal(seatIndex, info.Source);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestStandUp(RpcInfo info = default)
    {
        StandUpInternal(info.Source);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_NotifyPlayerSeated(int seatIndex, int playerSlot)
    {
        Debug.Log($"[SeatManager] RPC: Player slot {playerSlot} seated at {seatIndex}");
        OnPlayerSeated?.Invoke(seatIndex, playerSlot);

        // Update seat visual
        if (seats != null && seatIndex >= 0 && seatIndex < seats.Length && seats[seatIndex] != null)
        {
            seats[seatIndex].SetOccupied(true, playerSlot);
        }

        // Update player state
        UpdatePlayerSittingState(playerSlot, true, seatIndex);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_NotifyPlayerUnseated(int seatIndex, int playerSlot)
    {
        Debug.Log($"[SeatManager] RPC: Player slot {playerSlot} unseated from {seatIndex}");
        OnPlayerUnseated?.Invoke(seatIndex, playerSlot);

        // Update seat visual
        if (seats != null && seatIndex >= 0 && seatIndex < seats.Length && seats[seatIndex] != null)
        {
            seats[seatIndex].SetOccupied(false, INVALID_SEAT);
        }

        // Update player state - truyền seatIndex để teleport ra phía trước ghế
        UpdatePlayerSittingState(playerSlot, false, seatIndex);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_NotifyAllSeatsReset()
    {
        Debug.Log("[SeatManager] All seats reset");
        
        // Reset all seat visuals
        if (seats != null)
        {
            foreach (var seat in seats)
            {
                if (seat != null)
                    seat.SetOccupied(false, INVALID_SEAT);
            }
        }
    }

    private void UpdatePlayerSittingState(int playerSlot, bool isSitting, int seatIndex)
    {
        // Find player by slot and update their sitting state
        var players = FindObjectsByType<PlayerNetworkData>(FindObjectsSortMode.None);
        foreach (var player in players)
        {
            int slot = GetPlayerSlot(player.Object.InputAuthority);
            if (slot == playerSlot)
            {
                // Update SeatInteractor (sẽ trigger OnSittingStateChanged để disable movement)
                var seatInteractor = player.GetComponent<SeatInteractor>();
                if (seatInteractor != null)
                {
                    seatInteractor.SetSeatIndex(isSitting ? seatIndex : -1);
                }

                // Teleport player
                var networkCC = player.GetComponent<NetworkCharacterController>();
                
                if (isSitting && seatIndex >= 0)
                {
                    // Teleport đến ghế
                    if (networkCC != null)
                    {
                        networkCC.Teleport(GetSeatPosition(seatIndex), GetSeatRotation(seatIndex));
                    }
                    else
                    {
                        player.transform.position = GetSeatPosition(seatIndex);
                        player.transform.rotation = GetSeatRotation(seatIndex);
                    }
                }
                else if (!isSitting && seatIndex >= 0)
                {
                    // Đứng dậy - teleport ra phía trước ghế một chút
                    Vector3 standPosition = GetSeatPosition(seatIndex) + GetSeatRotation(seatIndex) * Vector3.forward * 1f;
                    standPosition.y = GetSeatPosition(seatIndex).y;
                    
                    if (networkCC != null)
                    {
                        networkCC.Teleport(standPosition, GetSeatRotation(seatIndex));
                    }
                    else
                    {
                        player.transform.position = standPosition;
                        player.transform.rotation = GetSeatRotation(seatIndex);
                    }
                }
                break;
            }
        }
    }
    #endregion
}
