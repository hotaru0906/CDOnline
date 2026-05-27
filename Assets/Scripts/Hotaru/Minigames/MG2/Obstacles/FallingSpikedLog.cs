using UnityEngine;

/// <summary>
/// C4 — Khúc gỗ gai rơi (Falling Spiked Log).
/// Được spawn bởi SpikedLogSpawner, rơi xuống từ trên cao.
/// Xoay nhẹ khi rơi. Destroy khi chạm đất hoặc hết thời gian sống.
///
/// Gắn component này lên prefab khúc gỗ gai.
/// </summary>
[RequireComponent(typeof(Collider))]
public class FallingSpikedLog : MonoBehaviour
{
    [Header("Fall Settings")]
    [SerializeField] private float fallSpeed = 5f;          // tốc độ rơi ban đầu
    [SerializeField] private float fallAcceleration = 9f;   // gia tốc khi rơi (giả lập gravity)
    [SerializeField] private float rotationSpeed = 60f;     // tốc độ xoay khi rơi (độ/giây)
    [SerializeField] private float lifetime = 8f;
    [SerializeField] private float destroyBelowY = -5f;

    [Header("Knockback")]
    [SerializeField] private float knockbackForce = 10f;
    [SerializeField] private float upForce = 8f;
    [SerializeField] private float knockbackDuration = 0.45f;

    private float _currentFallSpeed;
    private float _timer;

    private void Start()
    {
        _currentFallSpeed = fallSpeed;
    }

    private void Update()
    {
        // Rơi với gia tốc
        _currentFallSpeed += fallAcceleration * Time.deltaTime;
        transform.position += Vector3.down * _currentFallSpeed * Time.deltaTime;

        // Xoay ngẫu nhiên xung quanh trục Y
        transform.Rotate(0f, rotationSpeed * Time.deltaTime, 0f, Space.World);

        // Lifetime + check đáy map
        _timer += Time.deltaTime;
        if (_timer >= lifetime || transform.position.y < destroyBelowY)
            Destroy(gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        // Destroy khi chạm đất (layer Ground)
        if (other.CompareTag("Ground"))
        {
            Destroy(gameObject);
            return;
        }

        if (!other.TryGetComponent(out PlayerController player)) return;
        if (!player.HasStateAuthority) return;

        if (BaseMinigameController.Instance != null)
        {
            if (!BaseMinigameController.Instance.IsGameStarted) return;
            if (BaseMinigameController.Instance.IsGameEnded) return;
        }

        Vector3 toPlayer = (player.transform.position - transform.position).normalized;
        toPlayer.y = 0f;

        Vector3 force = toPlayer * knockbackForce + Vector3.up * upForce;
        player.ApplyExternalForce(force, knockbackDuration, overrideInput: true);

        Debug.Log($"[FallingSpikedLog] Hit {player.Object.InputAuthority}");
    }
}
