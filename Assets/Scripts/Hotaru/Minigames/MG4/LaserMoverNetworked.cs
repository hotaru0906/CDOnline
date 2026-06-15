using Fusion;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class LaserMoverNetworked : NetworkBehaviour
{
    [Header("Lifetime")]
    public float lifetime = 6f;

    [Networked] private TickTimer LifeTimer { get; set; }

    // Movement (not networked because host drives transform)
    private Vector3 _direction = Vector3.down;
    private float _speed = 4f;

    /// <summary>
    /// Called by MG4LaserHazard right after spawn to initialize direction/speed.
    /// Must be called on StateAuthority (host) immediately after Runner.Spawn.
    /// </summary>
    public void Initialize(Vector3 direction, float speed, float customLifetime = -1f)
    {
        _direction = direction.normalized;
        _speed = speed;
        if (customLifetime > 0f) lifetime = customLifetime;

        if (HasStateAuthority && Runner != null)
            LifeTimer = TickTimer.CreateFromSeconds(Runner, lifetime);
    }

    public override void FixedUpdateNetwork()
    {
        // Only host moves the laser
        if (!HasStateAuthority) return;

        transform.position += _direction * _speed * Runner.DeltaTime;

        if (LifeTimer.Expired(Runner))
            ReturnToPool();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!HasStateAuthority) return;

        // Try get PlayerMinigameData on collided object (works for player colliders)
        var pm = other.GetComponent<PlayerMinigameData>();
        if (pm != null)
        {
            if (pm.CanTakeDamage())
            {
                pm.LoseLife(); // Host-only call — valid here
            }
        }

        // Return laser to pool / despawn after hit
        ReturnToPool();
    }

    private void ReturnToPool()
    {
        // Despawn the network object (host only)
        if (Runner != null && Object != null && Object.IsValid)
        {
            Runner.Despawn(Object);
        }
        else
        {
            // Fallback for editor tests (non-networked)
            gameObject.SetActive(false);
        }
    }

    private void OnDisable()
    {
        // Safety: ensure timer cleared on disable
        if (HasStateAuthority)
            LifeTimer = default;
    }
}
