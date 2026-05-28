using UnityEngine;

/// <summary>
/// C5 — Bẫy xoay gai (Rotating Spike Trap).
/// Phần gai xoay quanh trục Y liên tục.
/// Khi player chạm: knockback theo chiều xoay hiện tại (tiếp tuyến) + hướng ra ngoài.
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
    [SerializeField] private float rotationSpeed = 90f; // độ/giây, dương = theo chiều kim đồng hồ (từ trên nhìn xuống)



    private void Update()
    {
        if (spikePart != null)
            spikePart.Rotate(0f, rotationSpeed * Time.deltaTime, 0f);
    }

    protected override void ApplyEffect(PlayerController player)
    {
        var mgData = GetMinigameData(player);
        if (mgData != null && mgData.CanTakeDamage())
            mgData.Die();
        Debug.Log($"[RotatingSpikeTrap] Killed {player.Object.InputAuthority}");
    }
}
