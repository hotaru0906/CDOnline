using Fusion;
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Pool bullet networked — pre-spawn poolSize bullets khi game start,
/// MG4Tank gọi GetBullet() để lấy bullet rảnh ra bắn.
///
/// SETUP: attach vào NetworkObject trong scene, assign bulletPrefab.
/// </summary>
public class MG4BulletPool : NetworkBehaviour
{
    public static MG4BulletPool Instance { get; private set; }

    [SerializeField] private NetworkObject bulletPrefab;
    [SerializeField] private int poolSize = 12;

    private readonly List<MG4Bullet> _pool = new();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public override void Spawned()
    {
        if (!HasStateAuthority) return;
        if (bulletPrefab == null) return;

        for (int i = 0; i < poolSize; i++)
        {
            var no = Runner.Spawn(bulletPrefab, Vector3.zero, Quaternion.identity);
            var bullet = no.GetComponent<MG4Bullet>();
            if (bullet != null) _pool.Add(bullet);
        }

        Debug.Log($"[MG4BulletPool] Pre-spawned {_pool.Count} bullets");
    }

    /// <summary>Lấy 1 bullet rảnh. Trả null nếu pool đầy (tăng poolSize nếu gặp).</summary>
    public MG4Bullet GetBullet()
    {
        foreach (var b in _pool)
            if (b != null && !b.IsActive) return b;

        Debug.LogWarning("[MG4BulletPool] Pool exhausted!");
        return null;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}