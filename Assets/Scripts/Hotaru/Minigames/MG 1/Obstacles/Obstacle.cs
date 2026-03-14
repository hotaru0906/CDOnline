using UnityEngine;
using Fusion;

/// <summary>
/// Type of obstacle behavior.
/// </summary>
public enum ObstacleType
{
    Jumpable,   // Long obstacle - player can jump over
    Dodgeable   // Wide obstacle - player must dodge sideways
}

/// <summary>
/// Base class for all obstacles in the race minigame.
/// Obstacles move from boss position toward the start line.
/// </summary>
public class Obstacle : NetworkBehaviour
{
    [Header("Obstacle Settings")]
    [SerializeField] protected ObstacleType obstacleType = ObstacleType.Jumpable;
    [SerializeField] protected float moveSpeed = 15f;           // Speed moving toward start
    [SerializeField] protected float lifetime = 20f;            // Auto-destroy after this time
    [SerializeField] protected float damageOrSlowAmount = 0.5f; // Effect strength

    [Header("Visual")]
    [SerializeField] protected GameObject visualModel;
    [SerializeField] protected ParticleSystem hitEffect;

    // Networked properties
    [Networked] public Vector3 MoveDirection { get; set; }
    [Networked] public TickTimer LifetimeTimer { get; set; }
    [Networked] public NetworkBool IsActive { get; set; }

    // Pooling
    protected bool _isPooled = false;
    protected ObstaclePool _pool;

    // References
    protected TrackSystem _trackSystem;
    protected ObstacleManager _obstacleManager;

    /// <summary>
    /// Gets the obstacle type.
    /// </summary>
    public ObstacleType Type => obstacleType;

    /// <summary>
    /// Whether this obstacle is managed by a pool.
    /// </summary>
    public bool IsPooled => _isPooled;

    public override void Spawned()
    {
        base.Spawned();

        _trackSystem = FindAnyObjectByType<TrackSystem>();
        _obstacleManager = ObstacleManager.Instance;
        _pool = FindAnyObjectByType<ObstaclePool>();

        // Don't auto-activate if pooled (will be activated manually)
        if (Object.HasStateAuthority && !_isPooled)
        {
            LifetimeTimer = TickTimer.CreateFromSeconds(Runner, lifetime);
            IsActive = true;
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority) return;
        if (!IsActive) return;

        // Check lifetime
        if (LifetimeTimer.Expired(Runner))
        {
            DestroyObstacle();
            return;
        }

        // Move toward start
        MoveObstacle();
    }

    /// <summary>
    /// Move the obstacle along its direction.
    /// </summary>
    protected virtual void MoveObstacle()
    {
        transform.position += MoveDirection * moveSpeed * Runner.DeltaTime;
    }

    /// <summary>
    /// Initialize the obstacle with spawn data.
    /// </summary>
    /// <param name="direction">Direction to move (usually toward start)</param>
    /// <param name="speed">Movement speed</param>
    public virtual void Initialize(Vector3 direction, float speed = -1f)
    {
        MoveDirection = direction.normalized;
        if (speed > 0f) moveSpeed = speed;
        IsActive = true;
    }

    /// <summary>
    /// Called when player collides with this obstacle.
    /// </summary>
    /// <param name="player">The player who hit this obstacle.</param>
    public virtual void OnPlayerHit(NetworkObject player)
    {
        if (!Object.HasStateAuthority) return;
        if (!IsActive) return;

        Debug.Log($"[Obstacle] Player hit {obstacleType} obstacle!");

        // Spawn hit effect
        if (hitEffect != null)
        {
            RPC_PlayHitEffect();
        }

        // Apply effect based on type
        ApplyEffect(player);

        // Optionally destroy after hit (can be overridden)
        OnAfterPlayerHit(player);
    }

    /// <summary>
    /// Apply the obstacle's effect to the player.
    /// Override in subclasses for specific behavior.
    /// </summary>
    protected virtual void ApplyEffect(NetworkObject player)
    {
        // Base implementation - can be overridden
        // Example: slow down, damage, knockback, etc.
    }

    /// <summary>
    /// Called after player hit is processed.
    /// Override to change behavior (e.g., don't destroy on hit).
    /// </summary>
    protected virtual void OnAfterPlayerHit(NetworkObject player)
    {
        // Default: don't destroy (obstacle continues moving)
    }

    /// <summary>
    /// Destroy this obstacle (returns to pool if pooled).
    /// </summary>
    public virtual void DestroyObstacle()
    {
        if (!Object.HasStateAuthority) return;

        IsActive = false;

        // Return to pool if pooled, otherwise despawn
        if (_isPooled && _pool != null)
        {
            _pool.ReturnObstacle(this);
        }
        else
        {
            Runner.Despawn(Object);
        }
    }

    #region Pooling Methods

    /// <summary>
    /// Set whether this obstacle is managed by a pool.
    /// </summary>
    public void SetPooled(bool isPooled)
    {
        _isPooled = isPooled;
    }

    /// <summary>
    /// Activate this obstacle from the pool.
    /// </summary>
    public virtual void Activate(Vector3 position, Quaternion rotation)
    {
        transform.position = position;
        transform.rotation = rotation;

        if (Object.HasStateAuthority)
        {
            LifetimeTimer = TickTimer.CreateFromSeconds(Runner, lifetime);
            IsActive = true;
        }

        // Show visual
        if (visualModel != null)
        {
            visualModel.SetActive(true);
        }

        RPC_OnActivated();
    }

    /// <summary>
    /// Deactivate this obstacle and prepare for pooling.
    /// </summary>
    public virtual void Deactivate()
    {
        IsActive = false;
        MoveDirection = Vector3.zero;

        // Hide visual
        if (visualModel != null)
        {
            visualModel.SetActive(false);
        }

        // Stop effects
        if (hitEffect != null)
        {
            hitEffect.Stop();
        }

        RPC_OnDeactivated();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    protected void RPC_OnActivated()
    {
        if (visualModel != null)
        {
            visualModel.SetActive(true);
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    protected void RPC_OnDeactivated()
    {
        if (visualModel != null)
        {
            visualModel.SetActive(false);
        }
        if (hitEffect != null)
        {
            hitEffect.Stop();
        }
    }

    #endregion

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    protected void RPC_PlayHitEffect()
    {
        if (hitEffect != null)
        {
            hitEffect.Play();
        }
    }

    // Collision detection - works with Unity's physics
    private void OnTriggerEnter(Collider other)
    {
        if (!Object.HasStateAuthority) return;
        if (!IsActive) return;

        // Check if it's a player
        var networkObj = other.GetComponentInParent<NetworkObject>();
        if (networkObj != null && other.CompareTag("Player"))
        {
            OnPlayerHit(networkObj);
        }
    }
}
