using UnityEngine;
using Fusion;

/// <summary>
/// Pickup that grants player a speed boost for a duration.
/// </summary>
public class BoostPickup : NetworkBehaviour
{
    [Header("Boost Settings")]
    [SerializeField] private float boostDuration = 10f;         // How long boost lasts
    [SerializeField] private float boostMultiplier = 1.5f;      // Speed multiplier (1.5 = 50% faster)
    [SerializeField] private float lifetime = 30f;              // Auto-destroy after this time

    [Header("Visual")]
    [SerializeField] private GameObject visualModel;
    [SerializeField] private ParticleSystem pickupEffect;
    [SerializeField] private ParticleSystem idleEffect;
    [SerializeField] private float rotationSpeed = 90f;         // Degrees per second
    [SerializeField] private float bobAmplitude = 0.3f;         // Up/down bob distance
    [SerializeField] private float bobSpeed = 2f;               // Bob frequency

    [Header("Audio")]
    [SerializeField] private AudioClip pickupSound;

    // Networked properties
    [Networked] public TickTimer LifetimeTimer { get; set; }
    [Networked] public NetworkBool IsCollected { get; set; }
    [Networked] public Vector3 BasePosition { get; set; }

    // Pooling
    private bool _isPooled = false;
    private ObstaclePool _pool;

    // References
    private ObstacleManager _obstacleManager;

    /// <summary>
    /// Whether this boost is managed by a pool.
    /// </summary>
    public bool IsPooled => _isPooled;

    /// <summary>
    /// Gets the boost duration.
    /// </summary>
    public float BoostDuration => boostDuration;

    /// <summary>
    /// Gets the boost multiplier.
    /// </summary>
    public float BoostMultiplier => boostMultiplier;

    public override void Spawned()
    {
        base.Spawned();

        _obstacleManager = ObstacleManager.Instance;
        _pool = FindAnyObjectByType<ObstaclePool>();
        BasePosition = transform.position;

        // Don't auto-activate if pooled
        if (Object.HasStateAuthority && !_isPooled)
        {
            LifetimeTimer = TickTimer.CreateFromSeconds(Runner, lifetime);
            IsCollected = false;
        }

        // Start idle effect (if not pooled)
        if (idleEffect != null && !_isPooled)
        {
            idleEffect.Play();
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority) return;
        if (IsCollected) return;

        // Check lifetime
        if (LifetimeTimer.Expired(Runner))
        {
            ReturnToPoolOrDespawn();
            return;
        }
    }

    /// <summary>
    /// Return to pool if pooled, otherwise despawn.
    /// </summary>
    private void ReturnToPoolOrDespawn()
    {
        if (_isPooled && _pool != null)
        {
            _pool.ReturnBoost(this);
        }
        else
        {
            Runner.Despawn(Object);
        }
    }

    public override void Render()
    {
        if (IsCollected) return;

        // Visual animation (rotation + bob)
        if (visualModel != null)
        {
            // Rotate
            visualModel.transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);

            // Bob up and down
            float bob = Mathf.Sin(Time.time * bobSpeed) * bobAmplitude;
            transform.position = BasePosition + Vector3.up * bob;
        }
    }

    /// <summary>
    /// Initialize boost pickup.
    /// </summary>
    public void Initialize(float duration = -1f, float multiplier = -1f)
    {
        if (duration > 0f) boostDuration = duration;
        if (multiplier > 0f) boostMultiplier = multiplier;
    }

    /// <summary>
    /// Called when player collects this boost.
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        if (!Object.HasStateAuthority) return;
        if (IsCollected) return;

        // Check if it's a player
        var networkObj = other.GetComponentInParent<NetworkObject>();
        if (networkObj != null && other.CompareTag("Player"))
        {
            CollectBoost(networkObj);
        }
    }

    /// <summary>
    /// Process boost collection.
    /// </summary>
    private void CollectBoost(NetworkObject player)
    {
        IsCollected = true;

        Debug.Log($"[BoostPickup] Player collected boost! Duration: {boostDuration}s, Multiplier: {boostMultiplier}x");

        // Notify ObstacleManager to apply boost effect
        if (_obstacleManager != null)
        {
            _obstacleManager.ApplyBoostEffect(player.InputAuthority, boostDuration, boostMultiplier);
        }

        // Play effects
        RPC_OnCollected();

        // Return to pool or despawn
        ReturnToPoolOrDespawn();
    }

    #region Pooling Methods

    /// <summary>
    /// Set whether this boost is managed by a pool.
    /// </summary>
    public void SetPooled(bool isPooled)
    {
        _isPooled = isPooled;
    }

    /// <summary>
    /// Activate this boost from the pool.
    /// </summary>
    public void Activate(Vector3 position, Quaternion rotation)
    {
        transform.position = position;
        transform.rotation = rotation;
        BasePosition = position;

        if (Object.HasStateAuthority)
        {
            LifetimeTimer = TickTimer.CreateFromSeconds(Runner, lifetime);
            IsCollected = false;
        }

        // Show visual
        if (visualModel != null)
        {
            visualModel.SetActive(true);
        }

        // Start idle effect
        if (idleEffect != null)
        {
            idleEffect.Play();
        }

        RPC_OnActivated();
    }

    /// <summary>
    /// Deactivate this boost and prepare for pooling.
    /// </summary>
    public void Deactivate()
    {
        IsCollected = true; // Prevent interactions

        // Hide visual
        if (visualModel != null)
        {
            visualModel.SetActive(false);
        }

        // Stop effects
        if (idleEffect != null)
        {
            idleEffect.Stop();
        }
        if (pickupEffect != null)
        {
            pickupEffect.Stop();
        }

        RPC_OnDeactivated();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_OnActivated()
    {
        if (visualModel != null)
        {
            visualModel.SetActive(true);
        }
        if (idleEffect != null)
        {
            idleEffect.Play();
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_OnDeactivated()
    {
        if (visualModel != null)
        {
            visualModel.SetActive(false);
        }
        if (idleEffect != null)
        {
            idleEffect.Stop();
        }
    }

    #endregion

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_OnCollected()
    {
        // Stop idle effect
        if (idleEffect != null)
        {
            idleEffect.Stop();
        }

        // Play pickup effect
        if (pickupEffect != null)
        {
            pickupEffect.Play();
        }

        // Play sound
        if (pickupSound != null)
        {
            AudioSource.PlayClipAtPoint(pickupSound, transform.position);
        }

        // Hide visual
        if (visualModel != null)
        {
            visualModel.SetActive(false);
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, 0.5f);
        Gizmos.DrawIcon(transform.position + Vector3.up, "d_SpeedScale", true);
    }
#endif
}
