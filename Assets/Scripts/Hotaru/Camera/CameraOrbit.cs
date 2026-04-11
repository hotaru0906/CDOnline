using UnityEngine;

/// <summary>
/// Camera orbit theo style Genshin Impact
/// - Di chuyển player theo hướng camera
/// - Giữ chuột phải để xoay camera
/// </summary>
public class CameraOrbit : MonoBehaviour
{
    [Header("Target")]
    public Transform target;

    [Header("Distance Settings")]
    public float distance = 6f;
    public float minDistance = 2f;
    public float maxDistance = 12f;
    public float scrollSpeed = 2f;

    [Header("Rotation Settings")]
    public float sensitivityX = 3f;
    public float sensitivityY = 2f;
    public float minY = -30f;
    public float maxY = 60f;

    [Header("Smooth Settings")]
    public float positionSmoothTime = 0.1f;
    public float rotationSmoothTime = 0.05f;

    [Header("Collision Settings")]
    [Tooltip("Layer mask cho collision detection (nên bỏ Player layer)")]
    public LayerMask collisionMask = ~0; // Mặc định tất cả layers
    [Tooltip("Offset từ điểm va chạm để tránh clipping")]
    public float collisionOffset = 0.2f;
    [Tooltip("Bán kính sphere cast (0 = dùng raycast)")]
    public float collisionRadius = 0.3f;
    [Tooltip("Tốc độ camera zoom in khi va chạm")]
    public float collisionZoomSpeed = 10f;
    [Tooltip("Tốc độ camera zoom out khi hết va chạm")]
    public float collisionRecoverSpeed = 5f;

    [Header("Cursor Settings")]
    public bool lockCursorOnStart = true;
    public bool holdRightClickToRotate = false; // false = luôn xoay camera khi di chuột

    private float _yaw;
    private float _pitch;
    private Vector3 _currentVelocity;
    private float _targetDistance;
    private float _currentDistance; // Distance hiện tại sau khi xử lý collision
    private bool _rotationLocked = false; // Flag khóa xoay camera

    public float Yaw => _yaw;
    public float Pitch => _pitch;
    public float CurrentDistance => _currentDistance;
    public bool IsRotationLocked => _rotationLocked;

    private void Start()
    {
        _targetDistance = distance;
        _currentDistance = distance;
        
        if (lockCursorOnStart && !holdRightClickToRotate)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        // Initialize rotation from current camera position
        if (target != null)
        {
            Vector3 direction = transform.position - target.position;
            _yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            _pitch = Mathf.Asin(direction.y / direction.magnitude) * Mathf.Rad2Deg;
        }
    }

    private void Update()
    {
        // Toggle cursor lock with Escape
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ToggleCursor();
        }

        // Scroll to zoom
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.01f)
        {
            _targetDistance -= scroll * scrollSpeed;
            _targetDistance = Mathf.Clamp(_targetDistance, minDistance, maxDistance);
        }
    }

    void LateUpdate()
    {
        if (target == null) return;

        // Check if should rotate camera (không xoay nếu bị lock)
        bool canRotate = !_rotationLocked && (!holdRightClickToRotate || Input.GetMouseButton(1));

        if (canRotate)
        {
            // Mouse input
            _yaw += Input.GetAxis("Mouse X") * sensitivityX;
            _pitch -= Input.GetAxis("Mouse Y") * sensitivityY;
            _pitch = Mathf.Clamp(_pitch, minY, maxY);
        }

        // Smooth distance (target distance từ scroll wheel)
        distance = Mathf.Lerp(distance, _targetDistance, Time.deltaTime * 10f);

        // Rotation
        Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0);

        // Target position (player center + offset up)
        Vector3 targetPos = target.position + Vector3.up * 1.5f;

        // Tính hướng từ target đến camera
        Vector3 directionFromTarget = rotation * Vector3.back;
        
        // Tính khoảng cách thực tế sau collision check
        float desiredDistance = distance;
        float actualDistance = CheckCameraCollision(targetPos, directionFromTarget, desiredDistance);
        
        // Smooth collision distance
        // Zoom in nhanh khi có collision, zoom out chậm khi hết collision
        if (actualDistance < _currentDistance)
        {
            // Đang bị block - zoom in nhanh
            _currentDistance = Mathf.Lerp(_currentDistance, actualDistance, Time.deltaTime * collisionZoomSpeed);
        }
        else
        {
            // Không bị block - zoom out chậm hơn
            _currentDistance = Mathf.Lerp(_currentDistance, actualDistance, Time.deltaTime * collisionRecoverSpeed);
        }

        // Camera position với collision-adjusted distance
        Vector3 offset = directionFromTarget * _currentDistance;
        Vector3 desiredPosition = targetPos + offset;

        // Smooth position
        transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref _currentVelocity, positionSmoothTime);

        // Look at player
        transform.LookAt(targetPos);
    }

    /// <summary>
    /// Kiểm tra collision giữa target và camera position
    /// </summary>
    /// <param name="from">Vị trí target (player)</param>
    /// <param name="direction">Hướng từ target đến camera</param>
    /// <param name="maxDistance">Khoảng cách mong muốn</param>
    /// <returns>Khoảng cách thực tế sau collision check</returns>
    private float CheckCameraCollision(Vector3 from, Vector3 direction, float maxDistance)
    {
        RaycastHit hit;
        
        if (collisionRadius > 0)
        {
            // SphereCast cho kết quả mượt hơn
            if (Physics.SphereCast(from, collisionRadius, direction, out hit, maxDistance, collisionMask))
            {
                // Trả về khoảng cách đến điểm va chạm trừ offset
                return Mathf.Max(minDistance, hit.distance - collisionOffset);
            }
        }
        else
        {
            // Raycast đơn giản
            if (Physics.Raycast(from, direction, out hit, maxDistance, collisionMask))
            {
                return Mathf.Max(minDistance, hit.distance - collisionOffset);
            }
        }
        
        // Không có collision - trả về khoảng cách mong muốn
        return maxDistance;
    }

    /// <summary>
    /// Set target ngay lập tức (gọi từ PlayerController khi spawn)
    /// </summary>
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    /// <summary>
    /// Set yaw angle (dùng khi chuyển từ First Person sang Third Person)
    /// </summary>
    public void SetYaw(float yaw)
    {
        _yaw = yaw;
    }

    /// <summary>
    /// Set cả yaw và pitch
    /// </summary>
    public void SetRotation(float yaw, float pitch)
    {
        _yaw = yaw;
        _pitch = Mathf.Clamp(pitch, minY, maxY);
    }

    public void ToggleCursor()
    {
        if (Cursor.lockState == CursorLockMode.Locked)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    public void LockCursor(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    /// <summary>
    /// Get forward direction for movement (Y flattened)
    /// </summary>
    public Vector3 GetForwardDirection()
    {
        Vector3 forward = Quaternion.Euler(0, _yaw, 0) * Vector3.forward;
        return forward.normalized;
    }

    /// <summary>
    /// Get right direction for movement (Y flattened)
    /// </summary>
    public Vector3 GetRightDirection()
    {
        Vector3 right = Quaternion.Euler(0, _yaw, 0) * Vector3.right;
        return right.normalized;
    }

    /// <summary>
    /// Khóa/mở khóa xoay camera
    /// </summary>
    public void SetRotationLocked(bool locked)
    {
        _rotationLocked = locked;
    }
}