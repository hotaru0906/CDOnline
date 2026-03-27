using UnityEngine;

/// <summary>
/// Gắn vào các vật cản (xoay, di chuyển) để đẩy player khi va chạm.
/// Tính toán lực dựa trên vận tốc của obstacle và hướng va chạm.
/// </summary>
[RequireComponent(typeof(Collider))]
public class ObstacleImpact : MonoBehaviour
{
    [Header("Impact Settings")]
    [Tooltip("Lực đẩy cơ bản")]
    [SerializeField] private float baseForce = 10f;
    
    [Tooltip("Hệ số nhân với vận tốc obstacle")]
    [SerializeField] private float velocityMultiplier = 1.5f;
    
    [Tooltip("Lực đẩy lên trên (để player bị bật lên)")]
    [SerializeField] private float upwardForce = 3f;
    
    [Tooltip("Lực tối thiểu (ngay cả khi obstacle đứng yên)")]
    [SerializeField] private float minForce = 5f;
    
    [Tooltip("Lực tối đa")]
    [SerializeField] private float maxForce = 30f;

    [Header("Rotation Impact")]
    [Tooltip("Có tính vận tốc từ rotation không")]
    [SerializeField] private bool useRotationVelocity = true;
    
    [Tooltip("Bán kính để tính vận tốc xoay (khoảng cách từ tâm)")]
    [SerializeField] private float rotationRadius = 2f;

    [Header("Cooldown")]
    [Tooltip("Thời gian cooldown giữa các lần đẩy cùng 1 player")]
    [SerializeField] private float impactCooldown = 0.3f;

    // Tracking velocity
    private Vector3 _lastPosition;
    private Quaternion _lastRotation;
    private Vector3 _linearVelocity;
    private Vector3 _angularVelocity;
    
    // Cooldown tracking
    private System.Collections.Generic.Dictionary<int, float> _playerCooldowns = new();

    private void Start()
    {
        _lastPosition = transform.position;
        _lastRotation = transform.rotation;
    }

    private void FixedUpdate()
    {
        // Tính vận tốc tuyến tính
        _linearVelocity = (transform.position - _lastPosition) / Time.fixedDeltaTime;
        _lastPosition = transform.position;

        // Tính vận tốc góc (xoay)
        if (useRotationVelocity)
        {
            Quaternion deltaRotation = transform.rotation * Quaternion.Inverse(_lastRotation);
            deltaRotation.ToAngleAxis(out float angle, out Vector3 axis);
            
            if (angle > 180f) angle -= 360f;
            _angularVelocity = axis * (angle * Mathf.Deg2Rad / Time.fixedDeltaTime);
        }
        _lastRotation = transform.rotation;

        // Update cooldowns
        UpdateCooldowns();
    }

    private void UpdateCooldowns()
    {
        var keysToRemove = new System.Collections.Generic.List<int>();
        var keys = new System.Collections.Generic.List<int>(_playerCooldowns.Keys);
        
        foreach (var key in keys)
        {
            _playerCooldowns[key] -= Time.fixedDeltaTime;
            if (_playerCooldowns[key] <= 0)
            {
                keysToRemove.Add(key);
            }
        }
        
        foreach (var key in keysToRemove)
        {
            _playerCooldowns.Remove(key);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        TryApplyImpact(collision.collider, collision.GetContact(0).point);
    }

    private void OnCollisionStay(Collision collision)
    {
        // Tiếp tục đẩy nếu vẫn tiếp xúc và đã hết cooldown
        TryApplyImpact(collision.collider, collision.GetContact(0).point);
    }

    private void OnTriggerEnter(Collider other)
    {
        TryApplyImpact(other, other.ClosestPoint(transform.position));
    }

    private void OnTriggerStay(Collider other)
    {
        TryApplyImpact(other, other.ClosestPoint(transform.position));
    }

    private void TryApplyImpact(Collider other, Vector3 contactPoint)
    {
        // Kiểm tra có phải player không
        if (!other.TryGetComponent(out PlayerController player)) return;
        
        // Chỉ xử lý nếu player có state authority (tránh duplicate trên client)
        if (!player.Object.HasStateAuthority) return;
        
        // Kiểm tra cooldown
        int playerId = player.GetInstanceID();
        if (_playerCooldowns.ContainsKey(playerId)) return;

        // Tính vận tốc tại điểm va chạm
        Vector3 impactVelocity = CalculateVelocityAtPoint(contactPoint);
        
        // Tính hướng đẩy (từ obstacle ra player)
        Vector3 pushDirection = (other.transform.position - transform.position).normalized;
        pushDirection.y = 0; // Chỉ lấy hướng ngang
        
        // Nếu không có hướng rõ ràng, dùng hướng của velocity
        if (pushDirection.sqrMagnitude < 0.01f)
        {
            pushDirection = impactVelocity.normalized;
            pushDirection.y = 0;
        }
        
        if (pushDirection.sqrMagnitude < 0.01f)
        {
            pushDirection = Vector3.forward;
        }
        pushDirection.Normalize();

        // Tính độ mạnh của lực
        float impactSpeed = impactVelocity.magnitude;
        float force = baseForce + (impactSpeed * velocityMultiplier);
        force = Mathf.Clamp(force, minForce, maxForce);

        // Tạo vector lực cuối cùng
        Vector3 finalForce = pushDirection * force;
        finalForce.y = upwardForce; // Thêm lực đẩy lên

        // Áp dụng lực
        player.ApplyExternalForce(finalForce);
        
        // Set cooldown
        _playerCooldowns[playerId] = impactCooldown;

        Debug.Log($"[ObstacleImpact] Applied force {finalForce} to player (speed: {impactSpeed:F1}, force: {force:F1})");
    }

    /// <summary>
    /// Tính vận tốc tại một điểm cụ thể (bao gồm cả vận tốc từ rotation)
    /// </summary>
    private Vector3 CalculateVelocityAtPoint(Vector3 worldPoint)
    {
        Vector3 velocity = _linearVelocity;

        if (useRotationVelocity)
        {
            // Vận tốc từ rotation = angular velocity x (point - center)
            Vector3 relativePos = worldPoint - transform.position;
            Vector3 rotationalVelocity = Vector3.Cross(_angularVelocity, relativePos);
            velocity += rotationalVelocity;
        }

        return velocity;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        // Vẽ bán kính rotation
        if (useRotationVelocity)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, rotationRadius);
        }

        // Vẽ hướng velocity hiện tại
        if (Application.isPlaying)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawRay(transform.position, _linearVelocity);
            
            if (useRotationVelocity)
            {
                Gizmos.color = Color.blue;
                Gizmos.DrawRay(transform.position, _angularVelocity);
            }
        }
    }
#endif
}
