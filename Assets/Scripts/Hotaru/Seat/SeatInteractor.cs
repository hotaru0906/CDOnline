using UnityEngine;
using Fusion;

public class SeatInteractor : NetworkBehaviour
{
    [Networked, OnChangedRender(nameof(OnSittingStateChanged))]
    public int CurrentSeatIndex { get; private set; } = -1;
    private int _lastSeatIndex = -1;
    public bool IsSitting => CurrentSeatIndex >= 0;

    private PlayerController _playerController;
    private PlayerAnimator _playerAnimator;

    public override void Spawned()
    {
        _playerController = GetComponent<PlayerController>();
        _playerAnimator = GetComponent<PlayerAnimator>();
    }

    private void Update()
    {
        if (!HasInputAuthority) return;

        if (!IsSitting) return;

        if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.Lobby)
            return;

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

    public void SetSeatIndex(int seatIndex)
    {
        if (!HasStateAuthority) return;

        _lastSeatIndex = CurrentSeatIndex;
        CurrentSeatIndex = seatIndex;
    }

    private void OnSittingStateChanged()
    {
        Debug.Log($"[SeatInteractor] Sitting state changed. SeatIndex: {CurrentSeatIndex}, IsSitting: {IsSitting}");

        // Movement
        if (_playerController != null)
        {
            _playerController.SetMovementEnabled(!IsSitting);
            _playerController.SetFrozen(IsSitting);
        }

        if (SeatManager.Instance != null)
        {
            int seatToUse = IsSitting ? CurrentSeatIndex : _lastSeatIndex;

            if (seatToUse >= 0)
            {
                var pos = SeatManager.Instance.GetSeatPosition(seatToUse);
                var rot = SeatManager.Instance.GetSeatRotation(seatToUse);

                if (IsSitting)
                {
                    transform.SetPositionAndRotation(pos, rot);
                }
                else
                {
                    Vector3 standPos = pos + rot * Vector3.forward * 1f;
                    standPos.y = pos.y;

                    transform.SetPositionAndRotation(standPos, rot);
                }
            }
        }

        // Animation
        if (_playerAnimator != null)
        {
            _playerAnimator.SetSittingOnChair(IsSitting);
        }

        // Ready state
        var playerData = GetComponent<PlayerNetworkData>();
        if (playerData != null && HasInputAuthority)
        {
            playerData.SetReady(IsSitting);
        }
    }
}
