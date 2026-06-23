using Fusion;
using UnityEngine;

/// <summary>
/// MG5 — Moving box.
/// Di chuyển trái ↔ phải, nhận input Space để drop.
/// </summary>
[RequireComponent(typeof(Collider))]
public class MG5MovingBox : NetworkBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 3f;
    public float moveRange = 3f; // khoảng cách trái ↔ phải từ spawn point

    private MG5Lane _lane;
    private Vector3 _spawnPos;
    private int _direction = 1; // 1 = phải, -1 = trái
    private bool _isMoving = false;

    public void Initialize(MG5Lane lane)
    {
        _lane = lane;
        _spawnPos = transform.position;
        _direction = 1;
        _isMoving = true;
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;
        if (!_isMoving) return;

        // Di chuyển trái ↔ phải quanh spawnPos.x
        var pos = transform.position;
        pos.x += _direction * moveSpeed * Runner.DeltaTime;

        if (Mathf.Abs(pos.x - _spawnPos.x) > moveRange)
        {
            // đổi hướng
            _direction *= -1;
            pos.x = _spawnPos.x + _direction * moveRange;
        }

        transform.position = pos;

        // Input: Space để drop
        //if (GetInput(out PlayerInputData input))
        //{
            //if (input.IsButtonPressed(PlayerInputData.BUTTON_INTERACT) ||
            //if (Input.GetKeyDown(KeyCode.Space)) // fallback local input
            //{
                //Drop();
            //}
        //}
    }

    /// <summary>
    /// Dừng di chuyển và báo lane snap box vào stack.
    /// </summary>
    public void Drop()
    {
        if (!HasStateAuthority) return;
        if (!_isMoving) return;

        _isMoving = false;
        Debug.Log("[MG5MovingBox] Dropped!");

        if (_lane != null)
        {
            _lane.PlaceBox(Object);
        }
    }
}
