using UnityEngine;
using Fusion;

/// <summary>
/// C10 — Máy ép (Crushing Press).
/// Tường A đứng yên, tường B di chuyển về phía A để ép player.
/// Khi player chạm wallB đang đóng/đã đóng: Die() → respawn tại checkpoint.
///
/// Setup prefab:
///   - Object gốc: trung tâm + NetworkObject
///   - wallA: tường đứng yên (Collider thường, không trigger)
///   - wallB: tường di chuyển:
///       + Collider isTrigger = true
///       + Thêm ObstacleTriggerRelay → kéo root (CrushingPress) vào field 'obstacle'
///   - Gắn component này lên object GỐC
/// </summary>
public class CrushingPress : BaseObstacle
{
    [Header("Press Parts")]
    [SerializeField] private Transform wallB;               // tường di chuyển
    [SerializeField] private Vector3 openLocalPos;          // vị trí mở
    [SerializeField] private Vector3 closeLocalPos;         // vị trí đóng (gần tường A)
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private float returnSpeed = 6f;

    [Header("Timing")]
    [SerializeField] private float openDuration = 3f;       // thời gian ở trạng thái mở
    [SerializeField] private float closeDuration = 1.5f;    // thời gian ở trạng thái đóng

    private enum PressState { Open, Closing, Closed, Opening }
    private PressState _state = PressState.Open;
    private float _stateTimer;

    private void Start()
    {
        _stateTimer = openDuration;
        if (wallB != null) wallB.localPosition = openLocalPos;
    }

    private void Update()
    {
        _stateTimer -= Time.deltaTime;

        switch (_state)
        {
            case PressState.Open:
                if (_stateTimer <= 0f)
                    EnterClosing();
                break;

            case PressState.Closing:
                if (wallB != null)
                {
                    wallB.localPosition = Vector3.MoveTowards(
                        wallB.localPosition, closeLocalPos, moveSpeed * Time.deltaTime);

                    if (Vector3.Distance(wallB.localPosition, closeLocalPos) < 0.05f)
                    {
                        wallB.localPosition = closeLocalPos;
                        EnterClosed();
                    }
                }
                break;

            case PressState.Closed:
                if (_stateTimer <= 0f)
                    EnterOpening();
                break;

            case PressState.Opening:
                if (wallB != null)
                {
                    wallB.localPosition = Vector3.MoveTowards(
                        wallB.localPosition, openLocalPos, returnSpeed * Time.deltaTime);

                    if (Vector3.Distance(wallB.localPosition, openLocalPos) < 0.05f)
                    {
                        wallB.localPosition = openLocalPos;
                        EnterOpen();
                    }
                }
                break;
        }
    }

    private void EnterClosing()
    {
        _state = PressState.Closing;
        Debug.Log("[CrushingPress] CLOSING");
    }

    private void EnterClosed()
    {
        _state = PressState.Closed;
        _stateTimer = closeDuration;
        Debug.Log("[CrushingPress] CLOSED");
    }

    private void EnterOpening()
    {
        _state = PressState.Opening;
    }

    private void EnterOpen()
    {
        _state = PressState.Open;
        _stateTimer = openDuration;
    }

    protected override void HandleHit(PlayerController player)
    {
        // Chỉ gây effect khi đang đóng hoặc closed
        if (_state != PressState.Closing && _state != PressState.Closed) return;
        base.HandleHit(player);
    }

    protected override void ApplyEffect(PlayerController player)
    {
        var mgData = GetMinigameData(player);
        if (mgData != null && mgData.CanTakeDamage())
            mgData.Die();

        Debug.Log($"[CrushingPress] CRUSHED {player.Object.InputAuthority}");
    }
}

