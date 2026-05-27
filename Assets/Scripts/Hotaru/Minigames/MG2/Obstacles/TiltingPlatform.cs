using UnityEngine;

/// <summary>
/// C9 — Sàn nghiêng (Tilting Platform).
/// Tính số player đứng bên trái và phải, nghiêng về bên nhiều người hơn.
/// Hai trigger zone (trái/phải) đếm số player vào/ra.
///
/// Setup prefab:
///   - Object gốc: bệ đỡ (pivot điểm giữa)
///   - platformMesh: con của object gốc, mesh sàn (sẽ xoay)
///   - leftZone: Collider trigger bên trái (gọi NotifyPlayerEntered/Exited với side = -1)
///   - rightZone: Collider trigger bên phải (gọi NotifyPlayerEntered/Exited với side = +1)
///   - Gắn component này lên object GỐC
///
/// Lưu ý: Trigger zone trái/phải cần relay component TiltZoneRelay để gọi lên đây.
/// </summary>
public class TiltingPlatform : MonoBehaviour
{
    [Header("Platform")]
    [SerializeField] private Transform platformMesh;   // phần sàn sẽ xoay
    [SerializeField] private float maxTiltAngle = 30f; // góc nghiêng tối đa (độ)
    [SerializeField] private float tiltSpeed = 2f;     // tốc độ nghiêng (độ/giây)
    [SerializeField] private Vector3 tiltAxis = Vector3.forward; // trục nghiêng (thường là Z)

    [Header("Knockback — khi sàn nghiêng đẩy player")]
    [SerializeField] private float slideForce = 4f;
    [SerializeField] private float slideKnockbackDuration = 0.2f;

    private int _playersLeft;
    private int _playersRight;
    private float _currentTiltAngle;
    private float _targetTiltAngle;

    private void Update()
    {
        // Tính góc mục tiêu dựa trên chênh lệch số player
        int diff = _playersRight - _playersLeft; // >0 → nghiêng phải; <0 → nghiêng trái
        _targetTiltAngle = Mathf.Clamp(diff * (maxTiltAngle / 2f), -maxTiltAngle, maxTiltAngle);

        // Lerp về góc mục tiêu
        _currentTiltAngle = Mathf.MoveTowards(
            _currentTiltAngle, _targetTiltAngle, tiltSpeed * Time.deltaTime);

        if (platformMesh != null)
        {
            platformMesh.localRotation = Quaternion.AngleAxis(_currentTiltAngle, tiltAxis);
        }
    }

    // Gọi bởi TiltZoneRelay
    public void NotifyPlayerEntered(int side)
    {
        if (side < 0) _playersLeft = Mathf.Max(0, _playersLeft + 1);
        else          _playersRight = Mathf.Max(0, _playersRight + 1);
    }

    public void NotifyPlayerExited(int side)
    {
        if (side < 0) _playersLeft = Mathf.Max(0, _playersLeft - 1);
        else          _playersRight = Mathf.Max(0, _playersRight - 1);
    }

    /// <summary>
    /// Gọi bởi PlayerController khi đứng trên sàn nghiêng để nhận force trượt.
    /// </summary>
    public void ApplySlideForce(PlayerController player)
    {
        if (Mathf.Abs(_currentTiltAngle) < 5f) return; // chưa nghiêng đủ thì không trượt

        // Hướng trượt: theo hướng nghiêng xuống (dọc theo trục tilt axis)
        Vector3 slideDir = (_currentTiltAngle > 0f) ? transform.right : -transform.right;
        slideDir.y = 0f;

        player.ApplyExternalForce(slideDir * slideForce, slideKnockbackDuration);
    }
}

/// <summary>
/// Helper relay: gắn lên Collider trigger trái/phải của TiltingPlatform.
/// Gọi ngược về TiltingPlatform khi player vào/ra.
/// </summary>
public class TiltZoneRelay : MonoBehaviour
{
    [SerializeField] private TiltingPlatform platform;
    [SerializeField] private int side = -1; // -1 = trái, +1 = phải

    private void OnTriggerEnter(Collider other)
    {
        if (platform == null) return;
        if (!other.TryGetComponent(out PlayerController _)) return;
        platform.NotifyPlayerEntered(side);
    }

    private void OnTriggerExit(Collider other)
    {
        if (platform == null) return;
        if (!other.TryGetComponent(out PlayerController _)) return;
        platform.NotifyPlayerExited(side);
    }
}
