using UnityEngine;

/// <summary>
/// C7 — Bánh xe gai (Spiked Wheel).
/// Bánh xe di chuyển kiểu pingpong theo trục Z, tự xoay theo tốc độ di chuyển.
/// Khi player chạm: knockback mạnh.
///
/// Setup prefab:
///   - Object gốc: khung đứng yên
///   - wheelPart: con của object gốc, phần bánh xe có Collider isTrigger
///   - Gắn component này lên object GỐC
/// </summary>
public class SpikedWheel : BaseObstacle
{
    [Header("Wheel — Movement")]
    [SerializeField] private Transform wheelPart;       // phần bánh xe
    [SerializeField] private float moveSpeed = 4f;      // tốc độ di chuyển (m/s)
    [SerializeField] private float moveRange = 3f;      // nửa khoảng di chuyển (m) theo trục Z local
    [SerializeField] private float wheelRadius = 0.5f;  // bán kính bánh xe (để tính vòng quay)



    private float _localZ;
    private int _direction = 1;

    private void Update()
    {
        if (wheelPart == null) return;

        // Di chuyển pingpong theo trục Z local
        _localZ += _direction * moveSpeed * Time.deltaTime;

        if (_localZ >= moveRange)
        {
            _localZ = moveRange;
            _direction = -1;
        }
        else if (_localZ <= -moveRange)
        {
            _localZ = -moveRange;
            _direction = 1;
        }

        wheelPart.localPosition = new Vector3(
            wheelPart.localPosition.x,
            wheelPart.localPosition.y,
            _localZ
        );

        // Xoay theo tốc độ di chuyển: rotX = -(speed / radius) * Rad2Deg
        float rotXDelta = -(_direction * moveSpeed / wheelRadius) * Mathf.Rad2Deg * Time.deltaTime;
        wheelPart.Rotate(rotXDelta, 0f, 0f, Space.Self);
    }

    protected override void ApplyEffect(PlayerController player)
    {
        var mgData = GetMinigameData(player);
        if (mgData != null && mgData.CanTakeDamage())
            mgData.Die();
        Debug.Log($"[SpikedWheel] Killed {player.Object.InputAuthority}");
    }
}
