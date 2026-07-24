using UnityEngine;

[RequireComponent(typeof(Animator))]
public class Regdoll : MonoBehaviour
{
    [Header("Ragdoll Parts")]
    [Tooltip("Optional: nếu để trống, hệ thống sẽ tự lấy các Rigidbody/Collider trong con.")]
    [SerializeField] private Rigidbody[] ragdollRigidbodies;
    [SerializeField] private Collider[] ragdollColliders;
    [Tooltip("Nếu bật, Animator sẽ bị tắt khi ragdoll kích hoạt. Đây là chế độ nên dùng cho chết/respawn.")]
    [SerializeField] private bool disableAnimatorOnRagdoll = true;

    private Animator _animator;
    private bool _isRagdollActive;

    private void Awake()
    {
        _animator = GetComponent<Animator>();

        if (ragdollRigidbodies == null || ragdollRigidbodies.Length == 0)
            ragdollRigidbodies = GetComponentsInChildren<Rigidbody>(true);

        if (ragdollColliders == null || ragdollColliders.Length == 0)
            ragdollColliders = GetComponentsInChildren<Collider>(true);

        DeactivateRagdoll();
    }

    public void ActivateRagdoll()
    {
        if (_isRagdollActive)
            return;

        if (disableAnimatorOnRagdoll && _animator != null)
            _animator.enabled = false;

        SetRagdollState(true);
        _isRagdollActive = true;
    }

    public void DeactivateRagdoll()
    {
        if (!_isRagdollActive)
            return;

        SetRagdollState(false);

        if (disableAnimatorOnRagdoll && _animator != null)
            _animator.enabled = true;

        _isRagdollActive = false;
    }

    private void SetRagdollState(bool active)
    {
        foreach (var body in ragdollRigidbodies)
        {
            if (body == null)
                continue;

            body.isKinematic = !active;
            body.useGravity = active;

            if (!active)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
        }

        foreach (var collider in ragdollColliders)
        {
            if (collider == null)
                continue;

            collider.enabled = active;
        }
    }
}
