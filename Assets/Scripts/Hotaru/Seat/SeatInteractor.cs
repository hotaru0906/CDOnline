using UnityEngine;
using Fusion;

/// <summary>
/// Component trên Player để quản lý trạng thái ngồi ghế
/// - Lưu trữ networked state ngồi/đứng
/// - Xử lý đứng dậy khi đang ngồi (nhấn Space/Jump)
/// - Disable movement khi đang ngồi
/// - Interaction ngồi xuống được xử lý qua PlayerInteractionHandler + Seat
/// </summary>
public class SeatInteractor : NetworkBehaviour
{
    /// <summary>
    /// Đang ngồi ghế nào (-1 nếu không ngồi)
    /// </summary>
    [Networked, OnChangedRender(nameof(OnSittingStateChanged))]
    public int CurrentSeatIndex { get; private set; } = -1;
    
    /// <summary>
    /// Đang ngồi hay không
    /// </summary>
    public bool IsSitting => CurrentSeatIndex >= 0;

    private PlayerController _playerController;

    public override void Spawned()
    {
        _playerController = GetComponent<PlayerController>();
    }

    private void Update()
    {
        // Chỉ local player xử lý input
        if (!HasInputAuthority) return;
        
        // Chỉ xử lý đứng dậy khi đang ngồi
        if (!IsSitting) return;
        
        // Chỉ trong Lobby
        if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.Lobby)
            return;

        // Nhấn Space (Jump) để đứng dậy
        if (Input.GetButtonDown("Jump"))
        {
            RequestStandUp();
        }
    }

    private void RequestStandUp()
    {
        Debug.Log("[SeatInteractor] Requesting stand up (Jump pressed)");
        
        if (SeatManager.Instance != null)
        {
            SeatManager.Instance.TryStandUp(Object.InputAuthority);
        }
    }

    /// <summary>
    /// Cập nhật CurrentSeatIndex từ SeatManager (gọi bởi SeatManager)
    /// </summary>
    public void SetSeatIndex(int seatIndex)
    {
        if (HasStateAuthority)
        {
            CurrentSeatIndex = seatIndex;
        }
        else
        {
            RPC_SetSeatIndex(seatIndex);
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_SetSeatIndex(int seatIndex)
    {
        CurrentSeatIndex = seatIndex;
    }

    private void OnSittingStateChanged()
    {
        Debug.Log($"[SeatInteractor] Sitting state changed. SeatIndex: {CurrentSeatIndex}, IsSitting: {IsSitting}");
        
        // Disable/Enable movement
        if (_playerController != null)
        {
            _playerController.SetMovementEnabled(!IsSitting);
            
            // Freeze player khi ngồi
            _playerController.SetFrozen(IsSitting);
        }
        
        // Cập nhật PlayerNetworkData.IsReady khi ngồi
        var playerData = GetComponent<PlayerNetworkData>();
        if (playerData != null && HasInputAuthority)
        {
            // Auto-ready khi ngồi, un-ready khi đứng
            playerData.SetReady(IsSitting);
        }
    }
}
