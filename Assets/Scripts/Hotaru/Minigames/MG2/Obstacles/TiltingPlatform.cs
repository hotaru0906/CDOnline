using UnityEngine;

/// <summary>
/// C9 — Sàn nghiêng (Tilting Platform).
/// Tính số player đứng bên trái và phải, nghiêng về bên nhiều người hơn.
/// Càng nhiều player cùng 1 bên → sàn nghiêng nhanh hơn.
///
/// Setup prefab:
///   - Object gốc: bệ đỡ (pivot điểm giữa) — gắn component này ở đây
///   - platformMesh: con của object gốc, mesh sàn (sẽ xoay)
///   - leftZoneRelay:  child GameObject có BoxCollider (Is Trigger ✓) + TiltZoneRelay (side = -1)
///   - rightZoneRelay: child GameObject có BoxCollider (Is Trigger ✓) + TiltZoneRelay (side = +1)
///   → TiltZoneRelay tự tìm TiltingPlatform ở parent nếu không gán tay.
/// </summary>
public class TiltingPlatform : MonoBehaviour
{
    [Header("Platform")]
    [SerializeField] private Transform platformMesh;         // phần sàn sẽ xoay
    [SerializeField] private float maxTiltAngle = 30f;       // góc nghiêng tối đa (độ)
    [SerializeField] private float tiltSpeed = 2f;           // tốc độ nghiêng cơ bản (độ/giây)
    [SerializeField] private float tiltSpeedPerPlayer = 1.5f;// tốc độ cộng thêm mỗi player cùng bên
    [SerializeField] private Vector3 tiltAxis = Vector3.forward; // trục nghiêng (thường là Z)

    [Header("Trigger Zones (assign child TiltZoneRelay objects)")]
    [SerializeField] private TiltZoneRelay leftZoneRelay;    // child bên trái  (side = -1)
    [SerializeField] private TiltZoneRelay rightZoneRelay;   // child bên phải (side = +1)

    [Header("Knockback — khi sàn nghiêng đẩy player")]
    [SerializeField] private float slideForce = 4f;
    [SerializeField] private float slideKnockbackDuration = 0.2f;

    private int _playersLeft;
    private int _playersRight;
    private float _currentTiltAngle;
    private float _targetTiltAngle;

    private void Update()
    {
        // Góc mục tiêu theo chênh lệch số player (>0 → nghiêng phải, <0 → nghiêng trái)
        int diff = _playersRight - _playersLeft;
        _targetTiltAngle = Mathf.Clamp(diff * (maxTiltAngle / 2f), -maxTiltAngle, maxTiltAngle);

        // Tốc độ tỷ lệ với số player ở bên đông hơn
        int heavySide = Mathf.Max(_playersLeft, _playersRight);
        float effectiveSpeed = tiltSpeed + heavySide * tiltSpeedPerPlayer;

        _currentTiltAngle = Mathf.MoveTowards(
            _currentTiltAngle, _targetTiltAngle, effectiveSpeed * Time.deltaTime);

        if (platformMesh != null)
            platformMesh.localRotation = Quaternion.AngleAxis(_currentTiltAngle, tiltAxis);
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


