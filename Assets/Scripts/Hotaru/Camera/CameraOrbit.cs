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

    [Header("Cursor Settings")]
    public bool lockCursorOnStart = true;
    public bool holdRightClickToRotate = false; // false = luôn xoay camera khi di chuột

    private float _yaw;
    private float _pitch;
    private Vector3 _currentVelocity;
    private float _targetDistance;

    public float Yaw => _yaw;
    public float Pitch => _pitch;

    private void Start()
    {
        _targetDistance = distance;
        
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

        // Check if should rotate camera
        bool canRotate = !holdRightClickToRotate || Input.GetMouseButton(1);

        if (canRotate)
        {
            // Mouse input
            _yaw += Input.GetAxis("Mouse X") * sensitivityX;
            _pitch -= Input.GetAxis("Mouse Y") * sensitivityY;
            _pitch = Mathf.Clamp(_pitch, minY, maxY);
        }

        // Smooth distance
        distance = Mathf.Lerp(distance, _targetDistance, Time.deltaTime * 10f);

        // Rotation
        Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0);

        // Target position (player center + offset up)
        Vector3 targetPos = target.position + Vector3.up * 1.5f;

        // Camera position
        Vector3 offset = rotation * new Vector3(0, 0, -distance);
        Vector3 desiredPosition = targetPos + offset;

        // Smooth position
        transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref _currentVelocity, positionSmoothTime);

        // Look at player
        transform.LookAt(targetPos);
    }

    /// <summary>
    /// Set target ngay lập tức (gọi từ PlayerController khi spawn)
    /// </summary>
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
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
}