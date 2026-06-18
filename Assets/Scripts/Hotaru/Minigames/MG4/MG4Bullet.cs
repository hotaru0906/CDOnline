using Fusion;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class MG4Bullet : NetworkBehaviour
{
    [Header("Visual")]
    [SerializeField] private GameObject visual;

    [Networked, OnChangedRender(nameof(OnActiveChanged))]
    public NetworkBool IsActive { get; private set; }

    [Networked, OnChangedRender(nameof(OnPositionChanged))]
    private Vector3 NetworkedPosition { get; set; }

    [Networked] private Vector3   Direction { get; set; }
    [Networked] private float     Speed     { get; set; }
    [Networked] private TickTimer LifeTimer { get; set; }

    private Collider _collider;

    private void Awake()
    {
        _collider           = GetComponent<Collider>();
        _collider.isTrigger = true;

        if (visual    != null) visual.SetActive(false);
        _collider.enabled = false;
    }

    public override void Spawned()
    {
        ApplyActiveState();
    }

    public void Fire(Vector3 position, Vector3 direction, float speed, float travelTime)
    {
        if (!HasStateAuthority) return;

        NetworkedPosition  = position;
        transform.position = position;
        transform.forward = direction;
        Direction          = direction.normalized;
        Speed              = speed;
        LifeTimer          = TickTimer.CreateFromSeconds(Runner, travelTime);
        IsActive           = true;

        ApplyActiveState();
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;
        if (!IsActive) return;

        // Di chuyển qua Networked position để client sync được
        transform.position += Direction * Speed * Runner.DeltaTime;
        NetworkedPosition = transform.position;

        if (LifeTimer.Expired(Runner))
            Deactivate();
    }

    public override void Render()
    {
        // Client interpolate position mượt hơn
        if (!HasStateAuthority && IsActive)
            transform.position = NetworkedPosition;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!HasStateAuthority) return;
        if (!IsActive) return;
        if (other.GetComponent<MG4Tank>() != null) return;

        var pm = other.GetComponent<PlayerMinigameData>();
        if (pm != null && pm.CanTakeDamage())
        {
            pm.LoseLife();
            RPC_OnHitPlayer(pm.Object.InputAuthority);
        }

        Deactivate();
    }

    private void Deactivate()
    {
        if (!HasStateAuthority) return;
        IsActive          = false;
        NetworkedPosition = Vector3.zero;
        ApplyActiveState();
    }

    private void OnActiveChanged()  => ApplyActiveState();
    private void OnPositionChanged() => transform.position = NetworkedPosition;

    private void ApplyActiveState()
    {
        if (visual    != null) visual.SetActive(IsActive);
        if (_collider != null) _collider.enabled = IsActive;
    }

    // Broadcast hit để client play VFX/sound
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_OnHitPlayer(PlayerRef playerRef)
    {
        Debug.Log($"[MG4Bullet] Hit P{playerRef}");
        // TODO: play hit VFX ở đây
    }
}