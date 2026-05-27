using UnityEngine;

/// <summary>
/// C5 — Bẫy xoay gai (Rotating Spike Trap).
/// Phần gai xoay quanh trục Y liên tục.
/// Khi player chạm: knockback theo hướng văng ra ngoài.
///
/// Setup prefab:
///   - Object gốc: đế đứng yên
///   - spikePart: con của object gốc, phần gai xoay (Collider isTrigger trên đây)
///   - Gắn component này lên object GỐC
/// </summary>
public class RotatingSpikeTrap : BaseObstacle
{
    [Header("Rotation")]
    [SerializeField] private Transform spikePart;   // phần xoay — gắn Collider trigger ở đây
    [SerializeField] private float rotationSpeed = 90f; // độ/giây

    [Header("Knockback")]
    [SerializeField] private float knockbackForce = 10f;
    [SerializeField] private float upForce = 4f;
    [SerializeField] private float knockbackDuration = 0.35f;

    private void Update()
    {
        if (spikePart != null)
            spikePart.Rotate(0f, rotationSpeed * Time.deltaTime, 0f);
    }

    protected override void ApplyEffect(PlayerController player)
    {
        Vector3 toPlayer = (player.transform.position - transform.position).normalized;
        toPlayer.y = 0f;

        Vector3 force = toPlayer * knockbackForce + Vector3.up * upForce;

        player.ApplyExternalForce(force, knockbackDuration, overrideInput: true);

        Debug.Log($"[RotatingSpikeTrap] Knocked {player.Object.InputAuthority}");
    }
}
