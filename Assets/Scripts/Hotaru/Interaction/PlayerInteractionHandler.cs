using UnityEngine;
using Fusion;

/// <summary>
/// Xử lý interaction của local player
/// - Raycast để detect interactable objects
/// - Hiển thị UI prompt khi nhìn vào object
/// - Xử lý Tab để show player info
/// </summary>
public class PlayerInteractionHandler : NetworkBehaviour
{
    [Header("Raycast Settings")]
    [Tooltip("Khoảng cách raycast tối đa")]
    [SerializeField] private float raycastDistance = 5f;
    
    [Tooltip("Layer của các object có thể tương tác")]
    [SerializeField] private LayerMask interactableLayer;
    
    [Tooltip("Offset từ camera để bắt đầu raycast")]
    [SerializeField] private Vector3 raycastOffset = Vector3.zero;
    
    [Header("UI References")]
    [Tooltip("Prefab cho Interaction Prompt UI (World Space)")]
    [SerializeField] private InteractionPromptUI promptPrefab;
    
    [Header("Player Info")]
    [Tooltip("Phím để hiện thông tin người chơi")]
    [SerializeField] private KeyCode playerInfoKey = KeyCode.Tab;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugRay = true;
    
    private InteractableObject _currentTarget;
    private InteractionPromptUI _promptInstance;
    private Camera _mainCamera;
    private bool _isShowingPlayerInfo;
    
    // Events
    public System.Action<bool> OnPlayerInfoToggle;
    public System.Action<InteractableObject> OnTargetChanged;
    
    public InteractableObject CurrentTarget => _currentTarget;
    public bool IsShowingPlayerInfo => _isShowingPlayerInfo;

    public override void Spawned()
    {
        // Chỉ local player mới xử lý interaction
        if (!HasInputAuthority)
        {
            enabled = false;
            return;
        }
        
        _mainCamera = Camera.main;
        
        // Tạo prompt UI instance
        if (promptPrefab != null)
        {
            _promptInstance = Instantiate(promptPrefab);
            _promptInstance.Hide();
        }
        
        Debug.Log($"[PlayerInteractionHandler] Initialized for local player. Camera: {(_mainCamera != null ? _mainCamera.name : "NULL")}, PlayerNetworkData.Local: {(PlayerNetworkData.Local != null ? PlayerNetworkData.Local.PlayerName.ToString() : "NULL")}");
    }
    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (_promptInstance != null)
        {
            Destroy(_promptInstance.gameObject);
        }
    }

    private void Update()
    {
        if (!HasInputAuthority) return;
        
        // Handle Tab key for player info
        HandlePlayerInfoKey();
        
        // Raycast to find interactable objects
        PerformInteractionRaycast();
        
        // Handle interaction input
        HandleInteractionInput();
    }

    /// <summary>
    /// Xử lý phím Tab để hiện/ẩn thông tin người chơi
    /// </summary>
    private void HandlePlayerInfoKey()
    {
        if (Input.GetKeyDown(playerInfoKey))
        {
            _isShowingPlayerInfo = true;
            OnPlayerInfoToggle?.Invoke(true);
            Debug.Log("[PlayerInteractionHandler] Player Info: SHOW");
        }
        else if (Input.GetKeyUp(playerInfoKey))
        {
            _isShowingPlayerInfo = false;
            OnPlayerInfoToggle?.Invoke(false);
            Debug.Log("[PlayerInteractionHandler] Player Info: HIDE");
        }
    }

    /// <summary>
    /// Raycast để tìm interactable objects
    /// </summary>
    private void PerformInteractionRaycast()
    {
        if (_mainCamera == null)
        {
            _mainCamera = Camera.main;
            if (_mainCamera == null) 
            {
                Debug.LogWarning("[PlayerInteractionHandler] Camera.main is null!");
                return;
            }
        }
        
        // Raycast từ center của camera
        Ray ray = new Ray(
            _mainCamera.transform.position + raycastOffset,
            _mainCamera.transform.forward
        );
        
        if (showDebugRay)
        {
            Debug.DrawRay(ray.origin, ray.direction * raycastDistance, Color.green);
        }
        
        InteractableObject newTarget = null;
        
        if (Physics.Raycast(ray, out RaycastHit hit, raycastDistance, interactableLayer))
        {
            // Tìm InteractableObject trên object bị hit
            var interactable = hit.collider.GetComponent<InteractableObject>();
            if (interactable == null)
            {
                interactable = hit.collider.GetComponentInParent<InteractableObject>();
            }
            
            if (interactable != null && interactable.CanInteract())
            {
                // Kiểm tra khoảng cách
                float distance = Vector3.Distance(transform.position, hit.point);
                if (distance <= interactable.InteractionRange)
                {
                    newTarget = interactable;
                }
            }
        }
        
        // Target changed
        if (newTarget != _currentTarget)
        {
            // End previous interaction
            if (_currentTarget != null)
            {
                _currentTarget.EndInteraction();
            }
            
            _currentTarget = newTarget;
            OnTargetChanged?.Invoke(_currentTarget);
            
            // Update UI
            UpdatePromptUI();
        }
    }

    /// <summary>
    /// Xử lý input tương tác - chỉ nhấn 1 lần
    /// </summary>
    private void HandleInteractionInput()
    {
        if (_currentTarget == null) return;
        
        if (Input.GetKeyDown(_currentTarget.InteractionKey))
        {
            _currentTarget.Interact();
        }
    }

    /// <summary>
    /// Cập nhật Prompt UI
    /// </summary>
    private void UpdatePromptUI()
    {
        if (_promptInstance == null) return;
        
        if (_currentTarget != null)
        {
            _promptInstance.Show(_currentTarget);
        }
        else
        {
            _promptInstance.Hide();
        }
    }

    /// <summary>
    /// Force clear target (dùng khi cần)
    /// </summary>
    public void ClearTarget()
    {
        if (_currentTarget != null)
        {
            _currentTarget.EndInteraction();
            _currentTarget = null;
            UpdatePromptUI();
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        // Vẽ raycast range
        if (_mainCamera != null || Camera.main != null)
        {
            var cam = _mainCamera ?? Camera.main;
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(
                cam.transform.position + raycastOffset,
                cam.transform.position + raycastOffset + cam.transform.forward * raycastDistance
            );
        }
    }
#endif
}
