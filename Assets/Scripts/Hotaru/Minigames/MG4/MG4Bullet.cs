using Fusion;
using UnityEngine;

/// <summary>
/// Bullet trong pool — Fire() để bắn, tự Deactivate sau travelTime.
/// Không despawn — chỉ ẩn visual/collider để tái sử dụng.
///
/// SETUP prefab:
///   - NetworkObject + Collider (isTrigger=true) trên root
///   - Gắn script này
///   - Assign visual = model con (mesh) để ẩn/hiện
/// </summary>
[RequireComponent(typeof(Collider))]
public class MG4Bullet : NetworkBehaviour
{
    [Header("Visual")]
    [SerializeField] private GameObject visual;

    [Networked, OnChangedRender(nameof(OnActiveChanged))]
    public NetworkBool IsActive { get; private set; }

    [Networked] private Vector3   Direction { get; set; }
    [Networked] private float     Speed     { get; set; }
    [Networked] private TickTimer LifeTimer { get; set; }

    private Collider _collider;

    private void Awake()
    {
        _collider = GetComponent<Collider>();
        if (_collider != null) _collider.isTrigger = true;
    }

    public override void Spawned()
    {
        OnActiveChanged(); // sync visual ban đầu (ẩn)
    }

    /// <summary>Bắn bullet — chỉ gọi từ host (MG4Tank).</summary>
    public void Fire(Vector3 position, Vector3 direction, float speed, float travelTime)
    {
        if (!HasStateAuthority) return;

        transform.position = position;
        Direction = direction.normalized;
        Speed     = speed;
        LifeTimer = TickTimer.CreateFromSeconds(Runner, travelTime);
        IsActive  = true;
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;
        if (!IsActive) return;

        transform.position += Direction * Speed * Runner.DeltaTime;

        if (LifeTimer.Expired(Runner))
            Deactivate();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!HasStateAuthority) return;
        if (!IsActive) return;

        var pm = other.GetComponent<PlayerMinigameData>();
        if (pm != null && pm.CanTakeDamage())
            pm.LoseLife();

        Deactivate();
    }

    private void Deactivate()
    {
        if (!HasStateAuthority) return;
        IsActive = false;
    }

    private void OnActiveChanged()
    {
        if (visual    != null) visual.SetActive(IsActive);
        if (_collider != null) _collider.enabled = IsActive;
    }
}