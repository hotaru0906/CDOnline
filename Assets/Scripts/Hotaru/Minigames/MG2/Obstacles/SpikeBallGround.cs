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
    [Header("SpikeBall — Rotation")]
    [SerializeField] private float rotationSpeed = 120f; // độ/giây

    private void Update()
    {
        // Xoay liên tục — visual only, không cần network
        transform.Rotate(0f, rotationSpeed * Time.deltaTime, 0f);
    }

    protected override void ApplyEffect(PlayerController player)
    {
        var mgData = GetMinigameData(player);
        if (mgData != null && mgData.CanTakeDamage())
            mgData.Die();
        Debug.Log($"[SpikeBallGround] Killed {player.Object.InputAuthority}");
    }
}
