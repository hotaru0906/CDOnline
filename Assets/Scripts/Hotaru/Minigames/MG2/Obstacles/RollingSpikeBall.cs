using UnityEngine;

/// <summary>
/// C11 — Bóng gai lăn (Rolling Spike Ball).
/// Được spawn bởi RollingSpikeBallSpawner, lăn theo trục X (hướng player chạy).
/// Tự destroy khi ra khỏi map hoặc hết thời gian sống.
///
/// Gắn component này lên prefab bóng gai.
/// Không thừa kế BaseObstacle vì là projectile spawn — quản lý lifecycle riêng.
/// </summary>
[RequireComponent(typeof(Collider))]
public class RollingSpikeBall : MonoBehaviour
{
    [Header("Rolling")]
    [SerializeField] private float rollSpeed = 8f;
    [SerializeField] private float ballRadius = 0.5f;
    [SerializeField] private float lifetime = 6f;       // destroy sau bao lâu
    [SerializeField] private float destroyBelowY = -10f; // destroy nếu rơi khỏi map

    [Header("Knockback")]
    [SerializeField] private float knockbackForce = 14f;
    [SerializeField] private float upForce = 5f;
    [SerializeField] private float knockbackDuration = 0.5f;

    private float _timer;

    private void Update()
    {
        // Lăn theo trục X âm (hướng ngược player chạy nếu player chạy +X)
        // Spawner sẽ set hướng qua rollDirection
        transform.position += _rollDirection * rollSpeed * Time.deltaTime;

        // Xoay theo tốc độ: rotZ = -(speed / radius) * Rad2Deg
        float rotZDelta = -(rollSpeed / ballRadius) * Mathf.Rad2Deg * Time.deltaTime;
        transform.Rotate(0f, 0f, rotZDelta, Space.World);

        // Lifetime
        _timer += Time.deltaTime;
        if (_timer >= lifetime || transform.position.y < destroyBelowY)
        {
            Destroy(gameObject);
        }
    }

    private Vector3 _rollDirection = Vector3.right;

    /// <summary>Set hướng lăn — gọi bởi Spawner sau khi Instantiate.</summary>
    public void SetRollDirection(Vector3 direction)
    {
        _rollDirection = direction.normalized;
    }

    private void OnTriggerEnter(Collider other)
    {
        // Chỉ xử lý trên host (bóng này không phải NetworkBehaviour, nên check HasInputAuthority qua PlayerController)
        if (!other.TryGetComponent(out PlayerController player)) return;
        if (!player.HasStateAuthority) return; // chỉ host xử lý

        // Check minigame đang chạy
        if (BaseMinigameController.Instance != null)
        {
            if (!BaseMinigameController.Instance.IsGameStarted) return;
            if (BaseMinigameController.Instance.IsGameEnded) return;
        }

        Vector3 toPlayer = (player.transform.position - transform.position).normalized;
        toPlayer.y = 0f;

        Vector3 force = toPlayer * knockbackForce + Vector3.up * upForce;
        player.ApplyExternalForce(force, knockbackDuration, overrideInput: true);

        Debug.Log($"[RollingSpikeBall] Hit {player.Object.InputAuthority}");
    }
}
