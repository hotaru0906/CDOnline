using Fusion;
using UnityEngine;

/// <summary>
/// MG4 Center Laser (networked)
/// - Host (StateAuthority) drives rotation and handles damage on trigger enter.
/// - Exposes SetPhase(int phase, bool extraTime) so controller can adjust rotation speed / enable state.
/// - Requires a NetworkObject on the same GameObject (attach this script to that NetworkObject).
/// - Collider should be set as isTrigger and sized to cover the center hazard area.
/// </summary>
[RequireComponent(typeof(Collider))]
public class MG4CenterLaser : NetworkBehaviour
{
    [Header("Rotation")]
    [Tooltip("Base rotation speed in degrees per second.")]
    public float baseRotationSpeed = 45f;

    [Tooltip("Multiplier applied in phase 2 (or phase 3)")]
    public float phase2Multiplier = 1.5f;

    [Tooltip("Multiplier applied in phase 4")]
    public float phase4Multiplier = 2f;

    [Tooltip("Extra time multiplier (applied when extraTime flag is true)")]
    public float extraTimeMultiplier = 2f;

    [Header("Damage")]
    [Tooltip("If true, center laser will deal damage on contact.")]
    public bool enableDamage = true;

    [Tooltip("Optional lifetime for temporary center lasers (0 = infinite)")]
    public float lifetime = 0f;

    [Networked] private TickTimer LifeTimer { get; set; }

    // internal
    private float _currentRotationSpeed;
    private int _currentPhase = 0;
    private bool _extraTime = false;

    private void Awake()
    {
        // Ensure collider is trigger for OnTriggerEnter to work as expected
        var col = GetComponent<Collider>();
        if (col != null && !col.isTrigger)
            col.isTrigger = true;
    }

    public override void Spawned()
    {
        // Initialize rotation speed
        _currentRotationSpeed = baseRotationSpeed;

        if (HasStateAuthority && lifetime > 0f && Runner != null)
            LifeTimer = TickTimer.CreateFromSeconds(Runner, lifetime);
    }

    public override void FixedUpdateNetwork()
    {
        // Only host rotates and manages lifetime
        if (!HasStateAuthority) return;

        // Rotate around local Y axis
        transform.Rotate(Vector3.up, _currentRotationSpeed * Runner.DeltaTime, Space.Self);

        if (lifetime > 0f && LifeTimer.Expired(Runner))
        {
            // If center laser is temporary, despawn it
            if (Runner != null && Object != null && Object.IsValid)
                Runner.Despawn(Object);
        }
    }

    /// <summary>
    /// Called by controller (host) to set phase and whether extraTime is active.
    /// Adjusts rotation speed accordingly.
    /// </summary>
    public void SetPhase(int phase, bool extraTime)
    {
        if (!HasStateAuthority) return;

        _currentPhase = phase;
        _extraTime = extraTime;

        float multiplier = 1f;
        switch (phase)
        {
            case 1:
                multiplier = 1f;
                break;
            case 2:
                multiplier = phase2Multiplier;
                break;
            case 3:
                // phase 3 keeps moderate speed (can reuse phase2 multiplier)
                multiplier = phase2Multiplier;
                break;
            case 4:
            default:
                multiplier = phase4Multiplier;
                break;
        }

        if (_extraTime)
            multiplier *= extraTimeMultiplier;

        _currentRotationSpeed = baseRotationSpeed * multiplier;
    }

    private void OnTriggerEnter(Collider other)
    {
        // Only host should apply damage
        if (!HasStateAuthority) return;
        if (!enableDamage) return;

        var pm = other.GetComponent<PlayerMinigameData>();
        if (pm == null) return;

        if (pm.CanTakeDamage())
        {
            pm.LoseLife();
        }
    }

    private void OnDisable()
    {
        if (HasStateAuthority)
            LifeTimer = default;
    }
}
