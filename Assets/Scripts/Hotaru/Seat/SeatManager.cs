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
    private const int MAX_SEATS = 4;
    private const int INVALID_SEAT = -1;
    #endregion

    #region Networked Properties
    [Networked, Capacity(MAX_SEATS)]
    private NetworkArray<int> SeatOccupants => default;
    #endregion

    #region Settings
    [Header("Seats (assign in Inspector)")]
    [SerializeField] private Seat[] seats;
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
        if (seats.Length != MAX_SEATS)
        {
            Debug.LogError(
                $"[SeatManager] Expected {MAX_SEATS} seats but found {seats.Length}");
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
    public void AssignPlayerToSeat(PlayerRef playerRef, int seatIndex)
    {
        if (!HasStateAuthority)
            return;

        if (seatIndex < 0 || seatIndex >= MAX_SEATS)
            return;

        int playerSlot = GetPlayerSlot(playerRef);

        if (playerSlot == INVALID_SEAT)
            return;

        // Check ghế đích trước
        if (SeatOccupants.Get(seatIndex) != INVALID_SEAT)
        {
            Debug.LogWarning(
                $"Seat {seatIndex} already occupied");
            return;
        }

        // Remove khỏi ghế cũ
        for (int i = 0; i < MAX_SEATS; i++)
        {
            if (SeatOccupants.Get(i) == playerSlot)
            {
                SeatOccupants.Set(i, INVALID_SEAT);
                break;
            }
        }

        SeatOccupants.Set(seatIndex, playerSlot);

        Debug.Log(
            $"[SeatManager] Assigned Player Slot {playerSlot} -> Seat {seatIndex}");
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
            SeatOccupants.Set(i, INVALID_SEAT);
        }
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
                seatIndex++;

            if (seatIndex >= MAX_SEATS) break;

            SeatOccupants.Set(seatIndex, playerSlot);

            var networkCC = player.GetComponent<NetworkCharacterController>();
            if (networkCC != null)
                networkCC.Teleport(GetSeatPosition(seatIndex), GetSeatRotation(seatIndex));
            else
            {
                player.transform.position = GetSeatPosition(seatIndex);
                player.transform.rotation = GetSeatRotation(seatIndex);
            }

            // Freeze player sau khi ngồi xuống ghế
            var pc = player.GetComponent<PlayerController>();
            if (pc != null) pc.SetFrozen(true);

            seatIndex++;
        }
    }

    /// <summary>Giải phóng ghế khi player thoát — gọi từ BasicSpawner.OnPlayerLeft.</summary>
    public void FreeSeat(PlayerRef playerRef)
    {
        if (!HasStateAuthority) return;

        int playerSlot = GetPlayerSlot(playerRef);

        for (int i = 0; i < MAX_SEATS; i++)
        {
            if (SeatOccupants.Get(i) == playerSlot)
            {
                SeatOccupants.Set(i, INVALID_SEAT);
                Debug.Log($"[SeatManager] Freed seat {i} from player slot {playerSlot}");
                return;
            }
        }
    }
    public void UnfreezeAllPlayers()
    {
        if (!HasStateAuthority) return;

        var players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        foreach (var p in players)
            p.SetFrozen(false);
    }
    public bool HasAssignedSeat(PlayerRef playerRef)
    {
        return GetPlayerSeatIndex(playerRef) != INVALID_SEAT;
    }
    #endregion

    #region Private Methods
    private int GetPlayerSlot(PlayerRef playerRef)
    {
        return playerRef.PlayerId;
    }
    #endregion

    #region RPCs

    #endregion
}
