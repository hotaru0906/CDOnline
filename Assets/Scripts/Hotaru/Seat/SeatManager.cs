using Fusion;
using UnityEngine;
using System;
using System.Collections.Generic;

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
    [Networked, Capacity(8)]
    private NetworkArray<int> SeatOccupants => default;

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

            for (int i = 0; i < MAX_SEATS; i++)
            {
                SeatOccupants.Set(i, INVALID_SEAT);
            }
        }

        if (seats == null || seats.Length == 0)
        {
            seats = FindObjectsByType<Seat>(FindObjectsSortMode.None);
        }

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

    public void TryStandUp(PlayerRef playerRef)
    {
        if (!HasStateAuthority)
        {
            RPC_RequestStandUp();
            return;
        }

        StandUpInternal(playerRef);
    }

    public bool IsSeatAvailable(int seatIndex)
    {
        if (seatIndex < 0 || seatIndex >= MAX_SEATS) return false;
        return SeatOccupants.Get(seatIndex) == INVALID_SEAT;
    }

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

    public int GetSeatOccupant(int seatIndex)
    {
        if (seatIndex < 0 || seatIndex >= MAX_SEATS) return INVALID_SEAT;
        return SeatOccupants.Get(seatIndex);
    }

    public Vector3 GetSeatPosition(int seatIndex)
    {
        if (seats == null || seatIndex < 0 || seatIndex >= seats.Length)
            return Vector3.zero;

        if (seats[seatIndex] != null)
            return seats[seatIndex].SitPosition;

        return Vector3.zero;
    }

    public Quaternion GetSeatRotation(int seatIndex)
    {
        if (seats == null || seatIndex < 0 || seatIndex >= seats.Length)
            return Quaternion.identity;

        if (seats[seatIndex] != null)
            return seats[seatIndex].SitRotation;

        return Quaternion.identity;
    }

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
    public void ResetAllSeats()
    {
        if (!HasStateAuthority) return;

        for (int i = 0; i < MAX_SEATS; i++)
        {
            int occupant = SeatOccupants.Get(i);
            if (occupant != INVALID_SEAT)
            {
                UpdatePlayerSeatIndex(occupant, -1);
                SeatOccupants.Set(i, INVALID_SEAT);
            }
        }

        SeatedPlayerCount = 0;
    }

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

            if (GetPlayerSeatIndex(playerRef) != INVALID_SEAT) continue;

            while (seatIndex < MAX_SEATS && SeatOccupants.Get(seatIndex) != INVALID_SEAT)
            {
                seatIndex++;
            }

            if (seatIndex >= MAX_SEATS) break;
            SeatOccupants.Set(seatIndex, playerSlot);
            SeatedPlayerCount++;

            UpdatePlayerSeatIndex(playerSlot, seatIndex);
        }

        Debug.Log($"[SeatManager] Auto-assign complete. Total seated: {SeatedPlayerCount}");
    }
    #endregion

    #region Private Methods
    private void SitDownInternal(int seatIndex, PlayerRef playerRef)
    {
        if (seatIndex >= seats.Length) return;
        if (seatIndex < 0 || seatIndex >= MAX_SEATS) return;
        if (SeatOccupants.Get(seatIndex) != INVALID_SEAT) return;

        int playerSlot = GetPlayerSlot(playerRef);
        if (playerSlot == INVALID_SEAT) return;

        // Nếu đang ngồi chỗ khác → remove
        int currentSeat = GetPlayerSeatIndex(playerRef);
        if (currentSeat != INVALID_SEAT)
        {
            SeatOccupants.Set(currentSeat, INVALID_SEAT);
            SeatedPlayerCount--;
        }

        // Set state
        SeatOccupants.Set(seatIndex, playerSlot);
        SeatedPlayerCount++;

        Debug.Log($"[SeatManager] Player {playerSlot} → seat {seatIndex}");
        UpdatePlayerSeatIndex(playerSlot, seatIndex);

        CheckAutoStart();
    }

    private void StandUpInternal(PlayerRef playerRef)
    {
        int seatIndex = GetPlayerSeatIndex(playerRef);
        if (seatIndex == INVALID_SEAT) return;

        int playerSlot = SeatOccupants.Get(seatIndex);

        SeatOccupants.Set(seatIndex, INVALID_SEAT);
        SeatedPlayerCount--;

        Debug.Log($"[SeatManager] Player {playerSlot} đứng dậy");

        UpdatePlayerSeatIndex(playerSlot, -1);
    }
    private void UpdatePlayerSeatIndex(int playerSlot, int seatIndex)
    {
        var players = FindObjectsByType<PlayerNetworkData>(FindObjectsSortMode.None);

        foreach (var player in players)
        {
            int slot = GetPlayerSlot(player.Object.InputAuthority);

            if (slot == playerSlot)
            {
                var seatInteractor = player.GetComponent<SeatInteractor>();

                if (seatInteractor != null)
                {
                    seatInteractor.SetSeatIndex(seatIndex);
                }

                return;
            }
        }

        // ⚠️ fallback debug
        Debug.LogWarning($"[SeatManager] Could not find player with slot {playerSlot} to update seat!");
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
    #endregion
}
