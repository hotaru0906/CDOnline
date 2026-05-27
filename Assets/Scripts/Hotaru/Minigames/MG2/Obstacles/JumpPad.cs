using UnityEngine;

/// <summary>
/// C6 — Bẫy nhảy (Jump Pad).
/// Phóng player lên cao + về phía trước khi chạm.
/// Có animation lò xo (nén xuống → bật lên) và cooldown.
///
/// Setup prefab:
///   - Object gốc: đế + Collider isTrigger + NetworkObject
///     (Collider đặt trực tiếp trên root, KHÔNG phải trên springPart)
///   - springPart: con của object gốc, chỉ dùng để animation — KHÔNG có Collider riêng
///   - Gắn component này lên object GỐC
/// </summary>
public class JumpPad : BaseObstacle
{
    [Header("Jump Pad — Force")]
    [SerializeField] private float jumpForce = 18f;
    [SerializeField] private float forwardForce = 8f;
    [SerializeField] private float knockbackDuration = 0.6f;

    [Header("Jump Pad — Spring Animation")]
    [SerializeField] private Transform springPart;
    [SerializeField] private float compressY = -0.3f;       // độ nén xuống (local Y offset)
    [SerializeField] private float compressSpeed = 20f;
    [SerializeField] private float returnSpeed = 10f;
    [SerializeField] private float cooldown = 0.5f;

    private Vector3 _springRestPosition;
    private Vector3 _springTargetPosition;
    private bool _isCompressing;
    private float _cooldownTimer;

    private void Start()
    {
        if (springPart != null)
            _springRestPosition = springPart.localPosition;
    }

    private void Update()
    {
        if (springPart == null) return;

        if (_cooldownTimer > 0f)
        {
            _cooldownTimer -= Time.deltaTime;

            if (_isCompressing)
            {
                // Nén xuống
                springPart.localPosition = Vector3.MoveTowards(
                    springPart.localPosition,
                    _springTargetPosition,
                    compressSpeed * Time.deltaTime
                );

                if (Vector3.Distance(springPart.localPosition, _springTargetPosition) < 0.01f)
                    _isCompressing = false;
            }
            else
            {
                // Bật về vị trí cũ
                springPart.localPosition = Vector3.MoveTowards(
                    springPart.localPosition,
                    _springRestPosition,
                    returnSpeed * Time.deltaTime
                );
            }
        }
    }

    protected override void HandleHit(PlayerController player)
    {
        // Cooldown để tránh spam
        if (_cooldownTimer > 0f) return;

        _cooldownTimer = cooldown;
        _isCompressing = true;
        _springTargetPosition = _springRestPosition + new Vector3(0f, compressY, 0f);

        base.HandleHit(player);
    }

    protected override void ApplyEffect(PlayerController player)
    {
        // Phóng về hướng player đang đứng + lên cao
        Vector3 forward = player.transform.forward;
        forward.y = 0f;
        forward.Normalize();

        Vector3 force = forward * forwardForce + Vector3.up * jumpForce;

        player.ApplyExternalForce(force, knockbackDuration, overrideInput: true);

        Debug.Log($"[JumpPad] Launched {player.Object.InputAuthority} — force: {force}");
    }
}
