using UnityEngine;
using Fusion;
using System.Collections.Generic;

/// <summary>
/// Object pool for network obstacles and boosts.
/// Pre-spawns objects and reuses them to avoid runtime allocation.
/// </summary>
public class ObstaclePool : NetworkBehaviour
{
    [Header("Pool Configuration")]
    [SerializeField] private NetworkPrefabRef jumpableObstaclePrefab;
    [SerializeField] private NetworkPrefabRef dodgeableObstaclePrefab;
    //[SerializeField] private NetworkPrefabRef boostPickupPrefab;

    [Header("Pool Sizes")]
    [SerializeField] private int jumpablePoolSize = 10;
    [SerializeField] private int dodgeablePoolSize = 10;
    [SerializeField] private int boostPoolSize = 5;

    [Header("Pool Parent")]
    [SerializeField] private Transform poolParent;

    // Pools
    private Queue<NetworkObject> _jumpablePool = new Queue<NetworkObject>();
    private Queue<NetworkObject> _dodgeablePool = new Queue<NetworkObject>();
    private Queue<NetworkObject> _boostPool = new Queue<NetworkObject>();

    // Active tracking
    private HashSet<NetworkObject> _activeJumpable = new HashSet<NetworkObject>();
    private HashSet<NetworkObject> _activeDodgeable = new HashSet<NetworkObject>();
    private HashSet<NetworkObject> _activeBoosts = new HashSet<NetworkObject>();

    // Hidden position for pooled objects
    private readonly Vector3 POOL_POSITION = new Vector3(0, -1000f, 0);

    private bool _isInitialized = false;

    public bool IsInitialized => _isInitialized;

    public override void Spawned()
    {
        base.Spawned();

        if (Object.HasStateAuthority)
        {
            InitializePools();
        }
    }

    /// <summary>
    /// Initialize all pools with pre-spawned objects.
    /// </summary>
    private void InitializePools()
    {
        if (_isInitialized) return;

        // Create pool parent if not set
        if (poolParent == null)
        {
            var poolObj = new GameObject("ObstaclePool");
            poolParent = poolObj.transform;
            poolParent.position = POOL_POSITION;
        }

        // Pre-spawn jumpable obstacles
        for (int i = 0; i < jumpablePoolSize; i++)
        {
            var obj = SpawnPooledObject(jumpableObstaclePrefab, ObstacleType.Jumpable);
            if (obj != null)
            {
                _jumpablePool.Enqueue(obj);
            }
        }

        // Pre-spawn dodgeable obstacles
        for (int i = 0; i < dodgeablePoolSize; i++)
        {
            var obj = SpawnPooledObject(dodgeableObstaclePrefab, ObstacleType.Dodgeable);
            if (obj != null)
            {
                _dodgeablePool.Enqueue(obj);
            }
        }

        // Pre-spawn boosts
        // for (int i = 0; i < boostPoolSize; i++)
        // {
        //     //var obj = SpawnBoostPooledObject();
        //     if (obj != null)
        //     {
        //         _boostPool.Enqueue(obj);
        //     }
        // }

        _isInitialized = true;
        Debug.Log($"[ObstaclePool] Initialized: {jumpablePoolSize} jumpable, {dodgeablePoolSize} dodgeable, {boostPoolSize} boosts");
    }

    /// <summary>
    /// Spawn a pooled obstacle object (disabled).
    /// </summary>
    private NetworkObject SpawnPooledObject(NetworkPrefabRef prefab, ObstacleType type)
    {
        if (!prefab.IsValid)
        {
            Debug.LogWarning($"[ObstaclePool] {type} prefab not assigned!");
            return null;
        }

        var obj = Runner.Spawn(prefab, POOL_POSITION, Quaternion.identity, Object.StateAuthority);
        if (obj != null)
        {
            var obstacle = obj.GetComponent<Obstacle>();
            if (obstacle != null)
            {
                obstacle.SetPooled(true);
                obstacle.Deactivate();
            }
        }
        return obj;
    }

    /// <summary>
    /// Spawn a pooled boost object (disabled).
    /// </summary>
    // private NetworkObject SpawnBoostPooledObject()
    // {
    //     if (!boostPickupPrefab.IsValid)
    //     {
    //         Debug.LogWarning("[ObstaclePool] Boost prefab not assigned!");
    //         return null;
    //     }

    //     var obj = Runner.Spawn(boostPickupPrefab, POOL_POSITION, Quaternion.identity, Object.StateAuthority);
    //     if (obj != null)
    //     {
    //         var boost = obj.GetComponent<BoostPickup>();
    //         if (boost != null)
    //         {
    //             boost.SetPooled(true);
    //             boost.Deactivate();
    //         }
    //     }
    //     return obj;
    // }

    #region Get From Pool

    /// <summary>
    /// Get a jumpable obstacle from pool.
    /// </summary>
    public Obstacle GetJumpableObstacle(Vector3 position, Quaternion rotation)
    {
        if (!Object.HasStateAuthority) return null;

        NetworkObject obj = GetOrExpandPool(_jumpablePool, jumpableObstaclePrefab, ObstacleType.Jumpable);
        if (obj == null) return null;

        _activeJumpable.Add(obj);

        var obstacle = obj.GetComponent<Obstacle>();
        if (obstacle != null)
        {
            obstacle.Activate(position, rotation);
        }

        return obstacle;
    }

    /// <summary>
    /// Get a dodgeable obstacle from pool.
    /// </summary>
    public Obstacle GetDodgeableObstacle(Vector3 position, Quaternion rotation)
    {
        if (!Object.HasStateAuthority) return null;

        NetworkObject obj = GetOrExpandPool(_dodgeablePool, dodgeableObstaclePrefab, ObstacleType.Dodgeable);
        if (obj == null) return null;

        _activeDodgeable.Add(obj);

        var obstacle = obj.GetComponent<Obstacle>();
        if (obstacle != null)
        {
            obstacle.Activate(position, rotation);
        }

        return obstacle;
    }

    /// <summary>
    /// Get a boost pickup from pool.
    /// </summary>
    public BoostPickup GetBoostPickup(Vector3 position, Quaternion rotation)
    {
        if (!Object.HasStateAuthority) return null;

        NetworkObject obj = GetOrExpandBoostPool();
        if (obj == null) return null;

        _activeBoosts.Add(obj);

        var boost = obj.GetComponent<BoostPickup>();
        if (boost != null)
        {
            boost.Activate(position, rotation);
        }

        return boost;
    }

    /// <summary>
    /// Get from pool or expand if empty.
    /// </summary>
    private NetworkObject GetOrExpandPool(Queue<NetworkObject> pool, NetworkPrefabRef prefab, ObstacleType type)
    {
        // Try to get from pool
        while (pool.Count > 0)
        {
            var obj = pool.Dequeue();
            if (obj != null && obj.IsValid)
            {
                return obj;
            }
        }

        // Pool empty - expand
        Debug.Log($"[ObstaclePool] Expanding {type} pool...");
        return SpawnPooledObject(prefab, type);
    }

    /// <summary>
    /// Get boost from pool or expand.
    /// </summary>
    private NetworkObject GetOrExpandBoostPool()
    {
        while (_boostPool.Count > 0)
        {
            var obj = _boostPool.Dequeue();
            if (obj != null && obj.IsValid)
            {
                return obj;
            }
        }

        Debug.Log("[ObstaclePool] Expanding boost pool...");
        // return SpawnBoostPooledObject();
        return null;
    }

    #endregion

    #region Return To Pool

    /// <summary>
    /// Return an obstacle to the pool.
    /// </summary>
    public void ReturnObstacle(Obstacle obstacle)
    {
        if (!Object.HasStateAuthority) return;
        if (obstacle == null) return;

        var networkObj = obstacle.Object;
        if (networkObj == null || !networkObj.IsValid) return;

        obstacle.Deactivate();
        obstacle.transform.position = POOL_POSITION;

        // Return to correct pool
        switch (obstacle.Type)
        {
            case ObstacleType.Jumpable:
                _activeJumpable.Remove(networkObj);
                _jumpablePool.Enqueue(networkObj);
                break;
            case ObstacleType.Dodgeable:
                _activeDodgeable.Remove(networkObj);
                _dodgeablePool.Enqueue(networkObj);
                break;
        }
    }

    /// <summary>
    /// Return a boost to the pool.
    /// </summary>
    public void ReturnBoost(BoostPickup boost)
    {
        if (!Object.HasStateAuthority) return;
        if (boost == null) return;

        var networkObj = boost.Object;
        if (networkObj == null || !networkObj.IsValid) return;

        boost.Deactivate();
        boost.transform.position = POOL_POSITION;

        _activeBoosts.Remove(networkObj);
        _boostPool.Enqueue(networkObj);
    }

    #endregion

    #region Pool Management

    /// <summary>
    /// Return all active objects to pool.
    /// </summary>
    public void ReturnAllToPool()
    {
        if (!Object.HasStateAuthority) return;

        // Return jumpable
        foreach (var obj in _activeJumpable)
        {
            if (obj != null && obj.IsValid)
            {
                var obstacle = obj.GetComponent<Obstacle>();
                if (obstacle != null)
                {
                    obstacle.Deactivate();
                    obstacle.transform.position = POOL_POSITION;
                }
                _jumpablePool.Enqueue(obj);
            }
        }
        _activeJumpable.Clear();

        // Return dodgeable
        foreach (var obj in _activeDodgeable)
        {
            if (obj != null && obj.IsValid)
            {
                var obstacle = obj.GetComponent<Obstacle>();
                if (obstacle != null)
                {
                    obstacle.Deactivate();
                    obstacle.transform.position = POOL_POSITION;
                }
                _dodgeablePool.Enqueue(obj);
            }
        }
        _activeDodgeable.Clear();

        // Return boosts
        foreach (var obj in _activeBoosts)
        {
            if (obj != null && obj.IsValid)
            {
                var boost = obj.GetComponent<BoostPickup>();
                if (boost != null)
                {
                    boost.Deactivate();
                    boost.transform.position = POOL_POSITION;
                }
                _boostPool.Enqueue(obj);
            }
        }
        _activeBoosts.Clear();

        Debug.Log("[ObstaclePool] All objects returned to pool.");
    }

    /// <summary>
    /// Get pool statistics.
    /// </summary>
    public (int jumpableAvailable, int jumpableActive, int dodgeableAvailable, int dodgeableActive, int boostAvailable, int boostActive) GetPoolStats()
    {
        return (
            _jumpablePool.Count, _activeJumpable.Count,
            _dodgeablePool.Count, _activeDodgeable.Count,
            _boostPool.Count, _activeBoosts.Count
        );
    }

    #endregion

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        base.Despawned(runner, hasState);

        // Clean up all pooled objects
        CleanupPools();
    }

    private void CleanupPools()
    {
        // Note: NetworkObjects are auto-despawned when runner stops
        _jumpablePool.Clear();
        _dodgeablePool.Clear();
        _boostPool.Clear();
        _activeJumpable.Clear();
        _activeDodgeable.Clear();
        _activeBoosts.Clear();
    }
}
