using UnityEngine;
using Fusion;
using System.Collections.Generic;

/// <summary>
/// Spawn traps với Object Pooling - Network compatible
/// Host quyết định khi nào spawn, RPC broadcast đến clients
/// Mỗi client có pool riêng để tối ưu performance
/// </summary>
public class TrapSpawner : NetworkBehaviour
{
    [Header("Trap Prefabs (Local - không cần NetworkObject)")]
    public GameObject trapLowPrefab;
    public GameObject trapHighPrefab;

    [Header("Spawn Points")]
    public Transform spawnLow;
    public Transform spawnHigh;

    [Header("Pool Settings")]
    [SerializeField] private Transform poolParent;
    [SerializeField] private int initialPoolSizeLow = 10;
    [SerializeField] private int initialPoolSizeHigh = 10;

    [Header("Timing Settings")]
    public float gameDuration = 90f;
    public float startDelay = 2f;
    public float endDelay = 0.5f;

    // Object pools
    private Queue<GameObject> poolLow = new Queue<GameObject>();
    private Queue<GameObject> poolHigh = new Queue<GameObject>();
    private List<GameObject> activeTraps = new List<GameObject>();

    // State
    private float timer;
    private float gameTimer;
    private bool isSpawning = false;
    private bool isInitialized = false;

    private void Awake()
    {
        // Tạo pool parent nếu chưa có
        if (poolParent == null)
        {
            var holder = new GameObject("TrapPoolHolder");
            poolParent = holder.transform;
            poolParent.SetParent(transform);
        }
    }

    public override void Spawned()
    {
        InitializePools();
    }

    /// <summary>
    /// Khởi tạo object pools
    /// </summary>
    private void InitializePools()
    {
        if (isInitialized) return;

        // Pool cho trap low
        if (trapLowPrefab != null)
        {
            for (int i = 0; i < initialPoolSizeLow; i++)
            {
                var obj = CreatePooledObject(trapLowPrefab, "TrapLow");
                poolLow.Enqueue(obj);
            }
        }

        // Pool cho trap high
        if (trapHighPrefab != null)
        {
            for (int i = 0; i < initialPoolSizeHigh; i++)
            {
                var obj = CreatePooledObject(trapHighPrefab, "TrapHigh");
                poolHigh.Enqueue(obj);
            }
        }

        isInitialized = true;
        Debug.Log($"[TrapSpawner] Pools initialized - Low: {initialPoolSizeLow}, High: {initialPoolSizeHigh}");
    }

    private GameObject CreatePooledObject(GameObject prefab, string name)
    {
        var obj = Instantiate(prefab, poolParent);
        obj.name = $"{name}_{poolParent.childCount}";
        obj.SetActive(false);
        
        // Thêm component để tự động return về pool
        var returner = obj.GetComponent<TrapPoolReturner>();
        if (returner == null)
        {
            returner = obj.AddComponent<TrapPoolReturner>();
        }
        returner.Initialize(this, name.Contains("Low") ? TrapType.Low : TrapType.High);
        
        return obj;
    }

    /// <summary>
    /// Bắt đầu spawn traps - gọi bởi MinigameController khi game bắt đầu
    /// </summary>
    public void StartSpawning()
    {
        if (!HasStateAuthority)
        {
            Debug.Log("[TrapSpawner] Only host can start spawning");
            return;
        }

        Debug.Log("[TrapSpawner] Started spawning traps");
        isSpawning = true;
        timer = 0f;
        gameTimer = 0f;
    }

    /// <summary>
    /// Dừng spawn traps
    /// </summary>
    public void StopSpawning()
    {
        Debug.Log("[TrapSpawner] Stopped spawning traps");
        isSpawning = false;
    }

    public override void FixedUpdateNetwork()
    {
        // Chỉ host quyết định spawn timing
        if (!HasStateAuthority) return;
        if (!isSpawning) return;

        // Check null references
        if (trapLowPrefab == null || trapHighPrefab == null) return;
        if (spawnLow == null || spawnHigh == null) return;

        // Check game duration
        if (gameTimer >= gameDuration)
        {
            StopSpawning();
            return;
        }

        gameTimer += Runner.DeltaTime;

        // Tính delay hiện tại (giảm dần theo thời gian)
        float t = Mathf.Clamp01(gameTimer / gameDuration);
        float currentDelay = Mathf.Lerp(startDelay, endDelay, t);

        timer += Runner.DeltaTime;

        // Spawn khi đủ delay
        while (timer >= currentDelay)
        {
            // Random trap type và broadcast đến tất cả clients
            TrapType trapType = (TrapType)Random.Range(0, 2);
            RPC_SpawnTrap(trapType);
            timer -= currentDelay;
        }
    }

    /// <summary>
    /// RPC để sync spawn trap trên tất cả clients
    /// </summary>
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_SpawnTrap(TrapType trapType)
    {
        SpawnTrapLocal(trapType);
    }

    /// <summary>
    /// Spawn trap locally từ pool
    /// </summary>
    private void SpawnTrapLocal(TrapType trapType)
    {
        GameObject trap = null;
        Vector3 position;
        Quaternion rotation;

        if (trapType == TrapType.Low)
        {
            trap = GetFromPool(poolLow, trapLowPrefab, "TrapLow");
            position = spawnLow.position;
            rotation = Quaternion.identity;
        }
        else
        {
            trap = GetFromPool(poolHigh, trapHighPrefab, "TrapHigh");
            position = spawnHigh.position;
            rotation = spawnHigh.rotation;
        }

        if (trap != null)
        {
            trap.transform.position = position;
            trap.transform.rotation = rotation;
            trap.SetActive(true);
            activeTraps.Add(trap);
        }
    }

    /// <summary>
    /// Lấy object từ pool, tạo mới nếu cần
    /// </summary>
    private GameObject GetFromPool(Queue<GameObject> pool, GameObject prefab, string name)
    {
        // Lấy từ pool nếu có
        while (pool.Count > 0)
        {
            var obj = pool.Dequeue();
            if (obj != null)
            {
                return obj;
            }
        }

        // Pool rỗng - tạo mới
        Debug.Log($"[TrapSpawner] Pool empty, creating new {name}");
        return CreatePooledObject(prefab, name);
    }

    /// <summary>
    /// Trả object về pool - gọi bởi TrapPoolReturner
    /// </summary>
    public void ReturnToPool(GameObject obj, TrapType trapType)
    {
        if (obj == null) return;

        obj.SetActive(false);
        obj.transform.SetParent(poolParent);
        activeTraps.Remove(obj);

        if (trapType == TrapType.Low)
        {
            poolLow.Enqueue(obj);
        }
        else
        {
            poolHigh.Enqueue(obj);
        }
    }

    /// <summary>
    /// Reset spawner cho round mới - trả tất cả active traps về pool
    /// </summary>
    public void ResetSpawner()
    {
        timer = 0f;
        gameTimer = 0f;
        isSpawning = false;

        // Return all active traps to pool
        for (int i = activeTraps.Count - 1; i >= 0; i--)
        {
            var trap = activeTraps[i];
            if (trap != null)
            {
                var returner = trap.GetComponent<TrapPoolReturner>();
                if (returner != null)
                {
                    ReturnToPool(trap, returner.TrapType);
                }
                else
                {
                    trap.SetActive(false);
                }
            }
        }
        activeTraps.Clear();

        Debug.Log("[TrapSpawner] Reset complete - all traps returned to pool");
    }

    /// <summary>
    /// Cleanup khi destroy
    /// </summary>
    private void OnDestroy()
    {
        // Clear pools
        poolLow.Clear();
        poolHigh.Clear();
        activeTraps.Clear();
    }
}

/// <summary>
/// Enum định nghĩa loại trap
/// </summary>
public enum TrapType
{
    Low = 0,
    High = 1
}

/// <summary>
/// Component tự động trả trap về pool khi ra khỏi màn hình hoặc hết lifetime
/// Attach vào trap prefab
/// </summary>
public class TrapPoolReturner : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float lifetime = 10f;
    [SerializeField] private float boundaryX = 50f; // Khoảng cách X để coi là out of bounds

    private TrapSpawner spawner;
    private TrapType trapType;
    private float spawnTime;
    private Vector3 spawnPosition;

    public TrapType TrapType => trapType;

    public void Initialize(TrapSpawner spawner, TrapType type)
    {
        this.spawner = spawner;
        this.trapType = type;
    }

    private void OnEnable()
    {
        spawnTime = Time.time;
        spawnPosition = transform.position;
    }

    private void Update()
    {
        // Check lifetime
        if (Time.time - spawnTime > lifetime)
        {
            ReturnToPool();
            return;
        }

        // Check nếu đã di chuyển quá xa (out of bounds)
        float distanceFromSpawn = Mathf.Abs(transform.position.x - spawnPosition.x);
        if (distanceFromSpawn > boundaryX)
        {
            ReturnToPool();
        }
    }

    private void ReturnToPool()
    {
        if (spawner != null)
        {
            spawner.ReturnToPool(gameObject, trapType);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    // Optional: Return khi va chạm với boundary trigger
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("TrapBoundary"))
        {
            ReturnToPool();
        }
    }
}