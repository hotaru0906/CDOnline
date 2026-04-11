using UnityEngine;
using Fusion;

/// <summary>
/// Gun system cho Roulette scene
/// - Chỉ player đang có lượt (CurrentShooterSlot) mới tương tác được
/// - Khi tương tác, súng di chuyển đến camera (First Person)
/// - Raycast để detect player target
/// - Bắn xong súng trở về vị trí ban đầu
/// </summary>
public class RouletteGun : MonoBehaviour
{
    [Header("Gun Settings")]
    [SerializeField] private float interactionDistance = 3f;
    [SerializeField] private KeyCode interactKey = KeyCode.E;
    [SerializeField] private KeyCode fireKey = KeyCode.Mouse0;
    
    [Header("Aim Settings")]
    [SerializeField] private float aimRange = 50f;
    [SerializeField] private float targetDetectionRadius = 0.5f;
    [SerializeField] private LayerMask playerLayerMask = -1; // Everything by default
    
    [Header("Gun Position (khi cầm)")]
    [SerializeField] private Vector3 holdPositionOffset = new Vector3(0.3f, -0.2f, 0.5f);
    [SerializeField] private Vector3 holdRotationOffset = new Vector3(0f, 0f, 0f);
    
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 10f;
    [SerializeField] private float rotateSpeed = 10f;
    
    // State
    private Vector3 _originalPosition;
    private Quaternion _originalRotation;
    private bool _isHeld = false;
    private int _currentTargetSlot = -1;
    private Transform _currentTargetTransform;
    
    // Cache
    private RouletteManager _rouletteManager;
    private Camera _mainCamera;
    private NetworkRunner _runner;
    
    // Gizmo
    private Vector3 _lastRayOrigin;
    private Vector3 _lastRayDirection;
    private bool _hasValidTarget;
    private Vector3 _lastHitPoint;

    private void Awake()
    {
        // Lưu vị trí ban đầu
        _originalPosition = transform.position;
        _originalRotation = transform.rotation;
    }

    private void Start()
    {
        _rouletteManager = RouletteManager.Instance;
        _mainCamera = Camera.main;
        
        // Tìm NetworkRunner
        _runner = FindAnyObjectByType<NetworkRunner>();
    }

    private void Update()
    {
        if (_rouletteManager == null || _mainCamera == null || _runner == null)
        {
            // Try to find again
            _rouletteManager = RouletteManager.Instance;
            _mainCamera = Camera.main;
            _runner = FindAnyObjectByType<NetworkRunner>();
            return;
        }

        // Không trong Roulette -> không làm gì
        if (!_rouletteManager.IsRouletteActive) return;

        int localSlot = _rouletteManager.GetLocalPlayerSlot();
        int currentShooterSlot = _rouletteManager.CurrentShooterSlot;
        bool isMyTurn = localSlot == currentShooterSlot && localSlot >= 0;
        bool isWaitingForShot = _rouletteManager.IsWaitingForShot;

        if (!_isHeld)
        {
            // Chưa cầm súng - kiểm tra tương tác
            HandlePickup(isMyTurn, isWaitingForShot);
        }
        else
        {
            // Đang cầm súng
            if (!isMyTurn || !isWaitingForShot)
            {
                // Hết lượt hoặc không còn chờ bắn -> trả súng
                ReturnGun();
            }
            else
            {
                // Cập nhật vị trí súng theo camera
                UpdateGunPosition();
                
                // Detect target
                DetectTarget();
                
                // Xử lý bắn
                HandleFire();
            }
        }
    }

    /// <summary>
    /// Kiểm tra và xử lý nhặt súng
    /// </summary>
    private void HandlePickup(bool isMyTurn, bool isWaitingForShot)
    {
        if (!isMyTurn || !isWaitingForShot) return;
        
        // Kiểm tra khoảng cách với camera
        float distance = Vector3.Distance(transform.position, _mainCamera.transform.position);
        if (distance > interactionDistance) return;
        
        // Nhấn E để nhặt
        if (Input.GetKeyDown(interactKey))
        {
            PickupGun();
        }
    }
    
    /// <summary>
    /// Nhặt súng
    /// </summary>
    private void PickupGun()
    {
        _isHeld = true;
        Debug.Log("[RouletteGun] Gun picked up");
    }
    
    /// <summary>
    /// Trả súng về vị trí ban đầu
    /// </summary>
    private void ReturnGun()
    {
        _isHeld = false;
        _currentTargetSlot = -1;
        _currentTargetTransform = null;
        _hasValidTarget = false;
        
        // Reset về vị trí gốc
        transform.position = _originalPosition;
        transform.rotation = _originalRotation;
        
        Debug.Log("[RouletteGun] Gun returned to original position");
    }
    
    /// <summary>
    /// Cập nhật vị trí súng khi đang cầm (gắn theo camera)
    /// </summary>
    private void UpdateGunPosition()
    {
        if (_mainCamera == null) return;
        
        // Tính vị trí target theo camera
        Vector3 targetPos = _mainCamera.transform.position 
            + _mainCamera.transform.right * holdPositionOffset.x
            + _mainCamera.transform.up * holdPositionOffset.y
            + _mainCamera.transform.forward * holdPositionOffset.z;
        
        Quaternion targetRot = _mainCamera.transform.rotation * Quaternion.Euler(holdRotationOffset);
        
        // Lerp để smooth movement
        transform.position = Vector3.Lerp(transform.position, targetPos, moveSpeed * Time.deltaTime);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotateSpeed * Time.deltaTime);
    }
    
    /// <summary>
    /// Detect player target bằng raycast
    /// </summary>
    private void DetectTarget()
    {
        _currentTargetSlot = -1;
        _currentTargetTransform = null;
        _hasValidTarget = false;
        
        if (_mainCamera == null) return;
        
        // Raycast từ camera
        Ray ray = new Ray(_mainCamera.transform.position, _mainCamera.transform.forward);
        _lastRayOrigin = ray.origin;
        _lastRayDirection = ray.direction;
        
        // SphereCast để dễ ngắm hơn
        if (Physics.SphereCast(ray, targetDetectionRadius, out RaycastHit hit, aimRange, playerLayerMask))
        {
            _lastHitPoint = hit.point;
            
            // Kiểm tra có phải player không
            PlayerNetworkData playerData = hit.collider.GetComponentInParent<PlayerNetworkData>();
            if (playerData != null && playerData.Object != null)
            {
                // Lấy PlayerRef từ NetworkObject
                PlayerRef targetRef = playerData.Object.InputAuthority;
                int targetSlot = _rouletteManager.GetSlotFromPlayerRef(targetRef);
                
                // Kiểm tra có phải target hợp lệ không (còn sống, không phải bản thân)
                int localSlot = _rouletteManager.GetLocalPlayerSlot();
                
                if (targetSlot >= 0 && targetSlot != localSlot && _rouletteManager.IsPlayerAliveBySlot(targetSlot))
                {
                    _currentTargetSlot = targetSlot;
                    _currentTargetTransform = playerData.transform;
                    _hasValidTarget = true;
                    
                    Debug.Log($"[RouletteGun] Target detected: Slot {targetSlot}");
                }
            }
        }
    }
    
    /// <summary>
    /// Xử lý bắn
    /// </summary>
    private void HandleFire()
    {
        if (!Input.GetKeyDown(fireKey)) return;
        
        if (!_hasValidTarget || _currentTargetSlot < 0)
        {
            Debug.Log("[RouletteGun] No valid target to shoot");
            return;
        }
        
        // Gọi RPC để bắn
        Debug.Log($"[RouletteGun] Firing at slot {_currentTargetSlot}");
        _rouletteManager.RPC_RequestShoot(_currentTargetSlot);
        
        // Sau khi bắn, súng sẽ tự động trả về trong Update khi IsWaitingForShot = false
    }
    
    /// <summary>
    /// Kiểm tra xem có thể tương tác với súng không (để hiện prompt UI nếu cần)
    /// </summary>
    public bool CanInteract()
    {
        if (_rouletteManager == null || !_rouletteManager.IsRouletteActive) return false;
        if (_isHeld) return false;
        
        int localSlot = _rouletteManager.GetLocalPlayerSlot();
        int currentShooterSlot = _rouletteManager.CurrentShooterSlot;
        
        if (localSlot != currentShooterSlot || localSlot < 0) return false;
        if (!_rouletteManager.IsWaitingForShot) return false;
        
        if (_mainCamera == null) return false;
        float distance = Vector3.Distance(transform.position, _mainCamera.transform.position);
        return distance <= interactionDistance;
    }
    
    /// <summary>
    /// Trả về slot của target hiện tại (để UI hiển thị nếu cần)
    /// </summary>
    public int GetCurrentTargetSlot() => _currentTargetSlot;
    
    /// <summary>
    /// Kiểm tra súng đang được cầm không
    /// </summary>
    public bool IsHeld => _isHeld;
    
    /// <summary>
    /// Có target hợp lệ không
    /// </summary>
    public bool HasValidTarget => _hasValidTarget;

    #region Gizmos
    
    private void OnDrawGizmos()
    {
        // Vẽ khu vực tương tác
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(_originalPosition != Vector3.zero ? _originalPosition : transform.position, interactionDistance);
    }
    
    private void OnDrawGizmosSelected()
    {
        // Vẽ vị trí gốc
        Gizmos.color = Color.blue;
        if (_originalPosition != Vector3.zero)
        {
            Gizmos.DrawWireCube(_originalPosition, Vector3.one * 0.3f);
        }
        
        // Vẽ raycast
        if (_isHeld)
        {
            // Ray line
            Gizmos.color = _hasValidTarget ? Color.green : Color.red;
            Gizmos.DrawLine(_lastRayOrigin, _lastRayOrigin + _lastRayDirection * aimRange);
            
            // Target sphere
            if (_hasValidTarget)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(_lastHitPoint, targetDetectionRadius * 2f);
                
                // Line to target
                if (_currentTargetTransform != null)
                {
                    Gizmos.DrawLine(_lastRayOrigin, _currentTargetTransform.position + Vector3.up);
                }
            }
            
            // Aim sphere at end of ray
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(_lastRayOrigin + _lastRayDirection * aimRange, targetDetectionRadius);
        }
    }
    
    #endregion
}
