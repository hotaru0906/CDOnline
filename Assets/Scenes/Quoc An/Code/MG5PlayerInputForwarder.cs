using Fusion;
using UnityEngine;

/// <summary>
/// MG5 — Forward input Drop từ Player đến Box hiện tại của lane mình.
/// Gắn vào Player Prefab. KHÔNG đụng vào NetworkCharacterController gốc.
/// </summary>
public class MG5PlayerInputForwarder : NetworkBehaviour
{
    private MG5MovingBox _currentBox;

    /// <summary>
    /// Gọi từ MG5Lane mỗi khi spawn box mới cho player này.
    /// </summary>
    public void SetCurrentBox(MG5MovingBox box)
    {
        _currentBox = box;
    }
    private void Awake()
    {
        Debug.Log("[MG5PlayerInputForwarder] AWAKE called — script exists on object.");
    }

        public override void Spawned()
    {
        Debug.Log($"[MG5PlayerInputForwarder] SPAWNED called. HasInputAuthority={HasInputAuthority}, HasStateAuthority={HasStateAuthority}");
    }
    public override void FixedUpdateNetwork()
    {
        Debug.Log($"[MG5PlayerInputForwarder] Tick running. HasInputAuthority={HasInputAuthority}");

        if (GetInput(out PlayerInputData input))
        {
            Debug.Log("[MG5PlayerInputForwarder] GetInput = true");
            if (input.IsButtonPressed(PlayerInputData.BUTTON_DROP))
            {
                Debug.Log($"[MG5PlayerInputForwarder] BUTTON_DROP pressed! _currentBox = {(_currentBox != null ? _currentBox.name : "NULL")}");
                if (_currentBox != null)
                    _currentBox.Drop();
            }
        }
    }
}