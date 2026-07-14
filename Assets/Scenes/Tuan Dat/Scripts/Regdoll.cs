using UnityEngine;

[DisallowMultipleComponent]
public class Regdoll : MonoBehaviour
{
    [Header("Ragdoll Settings")]
    [Tooltip("Optional root animator to disable while ragdoll is active.")]
    [SerializeField] private Animator animator;

    [Tooltip("Optional colliders on the root object that should be disabled while ragdoll is active.")]
    [SerializeField] private Collider[] rootColliders;

    [Tooltip("Optional additional colliders to enable/disable with ragdoll.")]
    [SerializeField] private Collider[] ragdollColliders;

    [Tooltip("Optional rigidbodies used for ragdoll physics.")]
    [SerializeField] private Rigidbody[] ragdollRigidbodies;

    [Header("Force Settings")]
    [Tooltip("Impulse force applied to ragdoll bodies when activated.")]
    [SerializeField] private float activationForce = 4f;

    private bool _isRagdollActive;

    public bool IsRagdollActive => _isRagdollActive;

    private void Awake()
    {
        CacheComponents();
        SetRagdollActive(false);
    }

    private void CacheComponents()
    {
        if (ragdollRigidbodies == null || ragdollRigidbodies.Length == 0)
        {
            ragdollRigidbodies = GetComponentsInChildren<Rigidbody>(true);
        }

        if (ragdollColliders == null || ragdollColliders.Length == 0)
        {
            var allColliders = GetComponentsInChildren<Collider>(true);
            var root = transform;
            var list = new System.Collections.Generic.List<Collider>();
            foreach (var collider in allColliders)
            {
                if (collider == null)
                    continue;

                if (collider.transform == root)
                {
                    continue;
                }

                list.Add(collider);
            }
            ragdollColliders = list.ToArray();
        }

        if (rootColliders == null || rootColliders.Length == 0)
        {
            rootColliders = GetComponents<Collider>();
        }

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }
    }

    /// <summary>
    /// Bật trạng thái ragdoll ngay lập tức.
    /// </summary>
    public void ActivateRagdoll()
    {
        ActivateRagdoll(Vector3.zero);
    }

    /// <summary>
    /// Bật trạng thái ragdoll và thêm lực vào các rigidbody.
    /// </summary>
    public void ActivateRagdoll(Vector3 impulse)
    {
        if (_isRagdollActive)
            return;

        SetRagdollActive(true);

        if (impulse.sqrMagnitude > 0f)
        {
            foreach (var body in ragdollRigidbodies)
            {
                if (body == null || body.isKinematic)
                    continue;

                body.AddForce(impulse, ForceMode.Impulse);
            }
        }
        else
        {
            foreach (var body in ragdollRigidbodies)
            {
                if (body == null || body.isKinematic)
                    continue;

                body.AddForce(Vector3.down * activationForce, ForceMode.Impulse);
            }
        }
    }

    /// <summary>
    /// Tắt trạng thái ragdoll và trả model về trạng thái animation.
    /// </summary>
    public void DeactivateRagdoll()
    {
        if (!_isRagdollActive)
            return;

        SetRagdollActive(false);
    }

    private void SetRagdollActive(bool active)
    {
        _isRagdollActive = active;

        foreach (var body in ragdollRigidbodies)
        {
            if (body == null)
                continue;

            body.isKinematic = !active;
            body.detectCollisions = active;
        }

        foreach (var collider in ragdollColliders)
        {
            if (collider == null)
                continue;

            collider.enabled = active;
        }

        foreach (var collider in rootColliders)
        {
            if (collider == null)
                continue;

            collider.enabled = !active;
        }

        if (animator != null)
        {
            animator.enabled = !active;
        }
    }

    private void OnValidate()
    {
        if (ragdollRigidbodies != null && ragdollRigidbodies.Length == 0)
            ragdollRigidbodies = null;

        if (ragdollColliders != null && ragdollColliders.Length == 0)
            ragdollColliders = null;

        if (rootColliders != null && rootColliders.Length == 0)
            rootColliders = null;
    }
}
