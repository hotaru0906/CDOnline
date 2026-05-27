using UnityEngine;

/// <summary>
/// C1 — Bóng gai mặt đất (SpikeBall Ground).
/// Nằm trên mặt đất, xoay liên tục.
/// Khi player chạm: knockback lên + lùi về phía sau.
///
/// Setup prefab:
///   - 1 GameObject với Collider (isTrigger = true)
///   - Gắn component này
/// </summary>
public class SpikeBallGround : BaseObstacle
{
    [Header("SpikeBall — Knockback")]
    [SerializeField] private float upForce = 8f;
    [SerializeField] private float backForce = 6f;
    [SerializeField] private float knockbackDuration = 0.4f;

    [Header("SpikeBall — Rotation")]
    [SerializeField] private float rotationSpeed = 120f; // độ/giây

    private void Update()
    {
        // Xoay liên tục — visual only, không cần network
        transform.Rotate(0f, rotationSpeed * Time.deltaTime, 0f);
    }

    protected override void ApplyEffect(PlayerController player)
    {
        // Hướng văng: lên + lùi ra xa tính từ bóng → player
        Vector3 toPlayer = (player.transform.position - transform.position).normalized;
        toPlayer.y = 0f;

        Vector3 force = toPlayer * backForce + Vector3.up * upForce;

        player.ApplyExternalForce(force, knockbackDuration, overrideInput: true);

        Debug.Log($"[SpikeBallGround] Knocked {player.Object.InputAuthority} — force: {force}");
    }
}
