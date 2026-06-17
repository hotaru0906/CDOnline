using Fusion;
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Pool bullet networked — pre-spawn poolSize bullets khi game start.
///
/// SETUP:
///   - Attach vào NetworkObject trong scene
///   - Assign bulletPrefab (NetworkObject + MG4Bullet)
///   - Bullets sẽ được gom dưới 1 holder GameObject
/// </summary>
public class MG4BulletPool : NetworkBehaviour
{
    public static MG4BulletPool Instance { get; private set; }

    [SerializeField] private NetworkObject bulletPrefab;
    [SerializeField] private int           poolSize = 20;

    private readonly List<MG4Bullet> _pool = new();
    private GameObject _holder; // parent gom tất cả bullets

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public override void Spawned()
    {
        if (!HasStateAuthority) return;
        if (bulletPrefab == null)
        {
            Debug.LogError("[MG4BulletPool] bulletPrefab chưa assign!");
            return;
        }

        // Tạo holder để gom bullets trong Hierarchy
        _holder = new GameObject("=== BulletHolder ===");

        for (int i = 0; i < poolSize; i++)
        {
            var no     = Runner.Spawn(bulletPrefab, Vector3.zero, Quaternion.identity);
            var bullet = no.GetComponent<MG4Bullet>();
            if (bullet == null) continue;

            // Gom vào holder
            bullet.transform.SetParent(_holder.transform);
            _pool.Add(bullet);
        }

        Debug.Log($"[MG4BulletPool] Spawned {_pool.Count} bullets");
    }

    /// <summary>Lấy 1 bullet rảnh (IsActive = false).</summary>
    public MG4Bullet GetBullet()
    {
        foreach (var b in _pool)
            if (b != null && !b.IsActive) return b;

        Debug.LogWarning("[MG4BulletPool] Pool exhausted — tăng poolSize!");
        return null;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (_holder != null) Destroy(_holder);
    }
}