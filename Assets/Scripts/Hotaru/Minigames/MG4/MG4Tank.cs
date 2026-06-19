using Fusion;
using UnityEngine;

public class MG4Tank : NetworkBehaviour
{
    private enum TankState : byte { Inactive, Hidden, Aiming }

    [Header("References")]
    [SerializeField] private GameObject tankVisual;
    [SerializeField] private Transform  firePoint;

    [Header("Timing")]
    [SerializeField] private float windupDuration    = 2.5f;
    [SerializeField] private float cooldownDuration  = 4f;

    [Header("Bullet")]
    [SerializeField] private float bulletSpeed          = 6f;
    [SerializeField] private float bulletTravelDistance = 15f;

    [Header("Audio")]
    [SerializeField] private AudioClip shootSFX;

    [Networked, OnChangedRender(nameof(OnStateChanged))]
    private TankState _state { get; set; }

    [Networked] private TickTimer _timer { get; set; }

    private float _windup;
    private float _cooldown;

    private AudioSource _audioSource;

    public override void Spawned()
    {
        _windup   = windupDuration;
        _cooldown = cooldownDuration;

        if (HasStateAuthority)
            _state = TankState.Inactive; // không làm gì cho đến khi Activate()

        OnStateChanged();
    }

    /// <summary>
    /// Gọi từ controller khi muốn tank bắt đầu cycle.
    /// phaseDelay: delay ngẫu nhiên để lệch pha với các tank khác trong cùng batch.
    /// </summary>
    public void Activate(float phaseDelay = 0f)
    {
        if (!HasStateAuthority) return;
        if (_state != TankState.Inactive) return;

        _state = TankState.Hidden;
        _timer = TickTimer.CreateFromSeconds(Runner, phaseDelay);
    }

    /// <summary>Dừng cycle — tank ẩn lại, chờ Activate() tiếp theo.</summary>
    public void Deactivate()
    {
        if (!HasStateAuthority) return;
        _state = TankState.Inactive;
        _timer = default;
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;
        if (_state == TankState.Inactive) return;
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

        RPC_PlayShootSound();
        Debug.Log($"[MG4Tank] {name} fired");
    }

    public void SetPhase(int phase)
    {
        if (!HasStateAuthority) return;

        switch (phase)
        {
            case 1:  _windup = windupDuration;         _cooldown = cooldownDuration;         break;
            case 2:  _windup = windupDuration * 0.85f; _cooldown = cooldownDuration * 0.8f;  break;
            case 3:  _windup = windupDuration * 0.7f;  _cooldown = cooldownDuration * 0.65f; break;
            default: _windup = windupDuration * 0.6f;  _cooldown = cooldownDuration * 0.5f;  break;
        }
    }

    private void OnStateChanged()
    {
        if (tankVisual != null)
            tankVisual.SetActive(_state == TankState.Aiming);
    }

    private void Awake()
    {
        _audioSource = gameObject.AddComponent<AudioSource>();

        _audioSource.playOnAwake = false;
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayShootSound()
    {
        if (shootSFX != null)
            _audioSource.PlayOneShot(shootSFX);
    }
}