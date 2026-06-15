using Fusion;
using UnityEngine;

/// <summary>
/// Tank đặt quanh map hình tròn, hướng forward về tâm map.
/// Cycle: Hidden (cooldown) → Aiming (windup 2-3s, hiện visual) → Fire bullet → Hidden lại.
///
/// SETUP:
///   - Đặt Tank quanh map theo hình tròn, xoay forward hướng vào tâm
///   - Assign tankVisual = model tank (ẩn/hiện theo state)
///   - Assign firePoint = điểm xuất phát bullet (thường = nòng tank, forward hướng tâm)
/// </summary>
public class MG4Tank : NetworkBehaviour
{
    private enum TankState : byte { Hidden, Aiming }

    [Header("References")]
    [SerializeField] private GameObject tankVisual;
    [SerializeField] private Transform  firePoint;

    [Header("Timing")]
    [Tooltip("Thời gian aim trước khi bắn (giây)")]
    [SerializeField] private float windupDuration = 2.5f;
    [Tooltip("Thời gian ẩn giữa các lần bắn (giây)")]
    [SerializeField] private float cooldownDuration = 4f;

    [Header("Bullet")]
    [SerializeField] private float bulletSpeed = 6f;
    [SerializeField] private float bulletTravelDistance = 15f;

    [Networked, OnChangedRender(nameof(OnStateChanged))]
    private TankState _state { get; set; }

    [Networked] private TickTimer _timer { get; set; }

    // Runtime — điều chỉnh theo phase
    private float _windup;
    private float _cooldown;

    public override void Spawned()
    {
        _windup   = windupDuration;
        _cooldown = cooldownDuration;

        if (HasStateAuthority)
        {
            _state = TankState.Hidden;
            // Lệch pha ban đầu — tránh tất cả tank bắn cùng lúc
            _timer = TickTimer.CreateFromSeconds(Runner, Random.Range(0f, _cooldown));
        }

        OnStateChanged();
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;
        if (!_timer.Expired(Runner)) return;

        switch (_state)
        {
            case TankState.Hidden:
                _state = TankState.Aiming;
                _timer = TickTimer.CreateFromSeconds(Runner, _windup);
                break;

            case TankState.Aiming:
                Fire();
                _state = TankState.Hidden;
                _timer = TickTimer.CreateFromSeconds(Runner, _cooldown);
                break;
        }
    }

    private void Fire()
    {
        if (MG4BulletPool.Instance == null) return;

        var bullet = MG4BulletPool.Instance.GetBullet();
        if (bullet == null) return;

        Vector3 origin    = firePoint != null ? firePoint.position : transform.position;
        Vector3 direction = firePoint != null ? firePoint.forward  : transform.forward;

        float travelTime = bulletTravelDistance / bulletSpeed;
        bullet.Fire(origin, direction, bulletSpeed, travelTime);

        Debug.Log($"[MG4Tank] {name} fired bullet");
    }

    /// <summary>Gọi từ controller — tăng độ khó theo phase (windup/cooldown ngắn hơn).</summary>
    public void SetPhase(int phase)
    {
        if (!HasStateAuthority) return;

        switch (phase)
        {
            case 1:  _windup = windupDuration;          _cooldown = cooldownDuration;        break;
            case 2:  _windup = windupDuration * 0.85f;  _cooldown = cooldownDuration * 0.8f;  break;
            case 3:  _windup = windupDuration * 0.7f;   _cooldown = cooldownDuration * 0.65f; break;
            default: _windup = windupDuration * 0.6f;   _cooldown = cooldownDuration * 0.5f;  break;
        }
    }

    private void OnStateChanged()
    {
        if (tankVisual != null)
            tankVisual.SetActive(_state == TankState.Aiming);
    }
}