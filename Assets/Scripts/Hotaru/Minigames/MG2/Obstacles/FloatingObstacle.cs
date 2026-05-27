using UnityEngine;

/// <summary>
/// C2 — Vật di chuyển ngang (Floating Obstacle).
/// Di chuyển qua lại theo trục X hoặc Z dùng Mathf.Sin.
/// Có Collider trigger — knockback khi player chạm phải.
///
/// Setup prefab:
///   - 1 GameObject với Collider isTrigger + NetworkObject
///   - Gắn component này lên root
///
/// Để 2 vật không đồng pha (player phải chọn 1 trong 2):
///   - Đặt phaseOffset = 0 cho vật 1
///   - Đặt phaseOffset = 1.57 (~PI/2) cho vật 2
/// </summary>
public class FloatingObstacle : BaseObstacle
{
    public enum FloatAxis { X, Z }

    [Header("Floating Motion")]
    [SerializeField] private FloatAxis axis = FloatAxis.X;
    [SerializeField] private float floatSpeed = 1.5f;
    [SerializeField] private float floatAmplitude = 2f;
    [SerializeField] private float phaseOffset = 0f;  // khác nhau giữa các instance

    [Header("Knockback")]
    [SerializeField] private float knockbackForce = 8f;
    [SerializeField] private float upForce = 3f;
    [SerializeField] private float knockbackDuration = 0.3f;

    private Vector3 _startPosition;

    private void Start()
    {
        _startPosition = transform.position;
    }

    private void Update()
    {
        float offset = Mathf.Sin((Time.time + phaseOffset) * floatSpeed) * floatAmplitude;

        Vector3 pos = _startPosition;
        if (axis == FloatAxis.X) pos.x += offset;
        else                     pos.z += offset;

        transform.position = pos;
    }

    protected override void ApplyEffect(PlayerController player)
    {
        Vector3 toPlayer = (player.transform.position - transform.position).normalized;
        toPlayer.y = 0f;

        Vector3 force = toPlayer * knockbackForce + Vector3.up * upForce;
        player.ApplyExternalForce(force, knockbackDuration, overrideInput: true);

        Debug.Log($"[FloatingObstacle] Knocked {player.Object.InputAuthority}");
    }
}
