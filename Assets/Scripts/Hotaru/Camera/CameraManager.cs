using UnityEngine;

public enum CameraMode
{
    Fixed,      // Camera cố định (lobby, cutscene)
    Player,     // Camera follow player (CameraOrbit)
    Shared      // Camera chung cho minigame
}

/// <summary>
/// Quản lý camera trong game - tích hợp với CameraOrbit
/// - Fixed mode: Camera cố định trong lobby/cutscene
/// - Player mode: CameraOrbit follow local player
/// - Shared mode: Camera chung cho minigame
/// </summary>
public class CameraManager : MonoBehaviour
{
    public static CameraManager Instance { get; private set; }

    [Header("Main Camera")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private CameraOrbit cameraOrbit;

    [Header("Fixed Cameras")]
    [Tooltip("Camera cố định cho lobby (optional)")]
    [SerializeField] private Transform lobbyCameraPosition;

    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = true;

    private Transform _localPlayerTransform;
    private Transform _currentSharedCameraPosition;
    private CameraMode _currentMode = CameraMode.Fixed;

    public CameraMode CurrentMode => _currentMode;
    public Camera MainCamera => mainCamera;
    public CameraOrbit CameraOrbit => cameraOrbit;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Auto-find components nếu chưa assign
        if (mainCamera == null)
            mainCamera = Camera.main;
        
        if (cameraOrbit == null && mainCamera != null)
            cameraOrbit = mainCamera.GetComponent<CameraOrbit>();
    }

    private void Start()
    {
        // Không tự động switch - để PlayerController hoặc GameManager quyết định
        // Main Menu: không có player → không cần làm gì
        // Lobby: player spawn → PlayerController gọi SwitchToPlayerCamera()
    }

    /// <summary>
    /// Đăng ký local player - gọi khi player spawn và HasInputAuthority
    /// </summary>
    public void RegisterLocalPlayer(Transform playerTransform)
    {
        if (playerTransform == null)
        {
            Debug.LogError("[CameraManager] Player transform is null!");
            return;
        }

        _localPlayerTransform = playerTransform;
        
        // Set target cho CameraOrbit
        if (cameraOrbit != null)
        {
            cameraOrbit.SetTarget(playerTransform);
        }

        Debug.Log($"[CameraManager] Registered local player: {playerTransform.name}");
    }

    /// <summary>
    /// Hủy đăng ký local player - gọi khi player despawn
    /// </summary>
    public void UnregisterLocalPlayer()
    {
        _localPlayerTransform = null;

        if (cameraOrbit != null)
        {
            cameraOrbit.SetTarget(null);
        }

        // Không tự động chuyển camera - để GameManager quyết định
        Debug.Log("[CameraManager] Unregistered local player");
    }

    /// <summary>
    /// Chuyển sang camera cố định (lobby, cutscene)
    /// </summary>
    public void SwitchToFixedCamera(Transform customPosition = null)
    {
        _currentMode = CameraMode.Fixed;

        // Disable CameraOrbit
        if (cameraOrbit != null)
        {
            cameraOrbit.enabled = false;
            cameraOrbit.LockCursor(false); // Unlock cursor trong lobby
        }

        // Di chuyển camera đến vị trí cố định
        Transform targetPos = customPosition ?? lobbyCameraPosition;
        if (targetPos != null && mainCamera != null)
        {
            mainCamera.transform.position = targetPos.position;
            mainCamera.transform.rotation = targetPos.rotation;
        }

        Debug.Log("[CameraManager] Switched to Fixed Camera");
    }

    /// <summary>
    /// Chuyển sang camera follow player (CameraOrbit)
    /// </summary>
    public void SwitchToPlayerCamera()
    {
        _currentMode = CameraMode.Player;

        // Enable CameraOrbit và set target
        if (cameraOrbit != null)
        {
            cameraOrbit.enabled = true;
            cameraOrbit.SetTarget(_localPlayerTransform);
            cameraOrbit.LockCursor(true); // Lock cursor khi chơi
        }

        Debug.Log("[CameraManager] Switched to Player Camera (CameraOrbit)");
    }

    /// <summary>
    /// Chuyển sang shared camera cho minigame
    /// </summary>
    public void SwitchToSharedCamera(Transform sharedCameraPosition)
    {
        if (sharedCameraPosition == null)
        {
            Debug.LogError("[CameraManager] Shared camera position is null!");
            return;
        }

        _currentMode = CameraMode.Shared;
        _currentSharedCameraPosition = sharedCameraPosition;

        // Disable CameraOrbit
        if (cameraOrbit != null)
        {
            cameraOrbit.enabled = false;
        }

        // Di chuyển main camera đến vị trí shared
        if (mainCamera != null)
        {
            mainCamera.transform.position = sharedCameraPosition.position;
            mainCamera.transform.rotation = sharedCameraPosition.rotation;
        }

        Debug.Log($"[CameraManager] Switched to Shared Camera: {sharedCameraPosition.name}");
    }

    /// <summary>
    /// Update player target (khi respawn hoặc đổi character)
    /// </summary>
    public void UpdatePlayerTarget(Transform newTarget)
    {
        _localPlayerTransform = newTarget;

        if (cameraOrbit != null && newTarget != null)
        {
            cameraOrbit.SetTarget(newTarget);
        }
    }

    /// <summary>
    /// Kiểm tra player đã được đăng ký chưa
    /// </summary>
    public bool HasLocalPlayer()
    {
        return _localPlayerTransform != null;
    }

    /// <summary>
    /// Lấy transform của local player
    /// </summary>
    public Transform GetLocalPlayer()
    {
        return _localPlayerTransform;
    }

    private void OnGUI()
    {
        if (!showDebugInfo) return;

        GUILayout.BeginArea(new Rect(10, 10, 300, 120));
        GUILayout.Label($"Camera Mode: {_currentMode}");
        GUILayout.Label($"Local Player: {(_localPlayerTransform != null ? _localPlayerTransform.name : "None")}");
        GUILayout.Label($"CameraOrbit: {(cameraOrbit != null ? (cameraOrbit.enabled ? "Enabled" : "Disabled") : "None")}");
        GUILayout.Label($"Target: {(cameraOrbit != null && cameraOrbit.target != null ? cameraOrbit.target.name : "None")}");
        GUILayout.EndArea();
    }
}
