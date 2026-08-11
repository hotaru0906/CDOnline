using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public enum CameraMode
{
    Fixed,          // Camera cố định (lobby, cutscene)
    FirstPerson,    // Góc nhìn thứ 1 - camera gắn vào đầu player
    ThirdPerson,    // Góc nhìn thứ 3 - CameraOrbit follow player
    Minigame        // Camera chung cho minigame (có thể First/Third/Fixed tùy setup)
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

    [Header("First Person Settings")]
    [Tooltip("Offset từ player position đến vị trí camera (thường là đầu player)")]
    [SerializeField] private Vector3 firstPersonOffset = new Vector3(0f, 1.6f, 0f);

    [Tooltip("Độ nhạy xoay camera First Person")]
    [SerializeField] private float fpSensitivityX = 3f;
    [SerializeField] private float fpSensitivityY = 2f;

    [Tooltip("Giới hạn góc nhìn lên/xuống")]
    [SerializeField] private float fpMinPitch = -80f;
    [SerializeField] private float fpMaxPitch = 80f;

    private float _fpYaw;
    private float _fpPitch;

    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = true;

    private Transform _localPlayerTransform;
    private Transform _currentSharedCameraPosition;
    private CameraMode _currentMode = CameraMode.Fixed;

    // ===== Spectator =====
    private bool _isSpectating = false;
    private readonly List<PlayerController> _spectateTargets = new();
    private int _spectateIndex = 0;

    // Flag để biết đang chờ MinigameCamera setup shared camera
    private bool _pendingSharedCameraMode = false;

    // Flag để khóa xoay camera (dùng khi Voting, Scoreboard, etc.)
    private bool _cameraRotationLocked = false;

    public CameraMode CurrentMode => _currentMode;
    public bool IsPendingSharedCamera => _pendingSharedCameraMode;
    public bool IsCameraRotationLocked => _cameraRotationLocked;
    public Camera MainCamera => mainCamera;
    public CameraOrbit CameraOrbit => cameraOrbit;

    // First Person properties
    public float FPYaw => _fpYaw;
    public float FPPitch => _fpPitch;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject); // Giữ nguyên camera khi load scene mới

        // Auto-find components nếu chưa assign
        if (mainCamera == null)
            mainCamera = Camera.main;

        if (cameraOrbit == null && mainCamera != null)
            cameraOrbit = mainCamera.GetComponent<CameraOrbit>();

        // Đăng ký event khi scene thay đổi
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;

        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// Gọi khi scene mới được load - tự động tìm lại camera
    /// </summary>
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log($"[CameraManager] Scene loaded: {scene.name}, re-initializing camera...");

        // Tìm lại Main Camera trong scene mới
        StartCoroutine(ReinitializeCameraDelayed());
    }

    /// <summary>
    /// Delay một frame để đảm bảo scene đã load xong
    /// </summary>
    private System.Collections.IEnumerator ReinitializeCameraDelayed()
    {
        yield return null; // Đợi 1 frame

        ReinitializeCamera();
    }

    /// <summary>
    /// Tìm lại camera và components - gọi khi scene thay đổi
    /// </summary>
    public void ReinitializeCamera()
    {
        // Tìm Main Camera mới
        mainCamera = Camera.main;

        if (mainCamera == null)
        {
            Debug.LogWarning("[CameraManager] No Main Camera found in scene!");
            return;
        }

        // Tìm CameraOrbit trên camera mới
        cameraOrbit = mainCamera.GetComponent<CameraOrbit>();

        // Nếu đang chờ shared camera mode, disable CameraOrbit và đợi MinigameCamera setup
        if (_pendingSharedCameraMode)
        {
            if (cameraOrbit != null)
            {
                cameraOrbit.enabled = false;
            }
            Debug.Log($"[CameraManager] Re-initialized. Waiting for MinigameCamera to setup shared camera...");
            return;
        }

        // Auto-switch camera dựa trên GameState và MinigameData
        if (GameManager.Instance != null)
        {
            var state = GameManager.Instance.CurrentState;
            var minigameData = GameManager.Instance.CurrentMinigameData;

            // Nếu đang Playing và có MinigameData
            if (state == GameState.Playing && minigameData != null && !minigameData.useSharedCamera)
            {
                if (minigameData.useThirdPersonCamera)
                {
                    Debug.Log("[CameraManager] Auto-switching to Third Person for minigame");
                    SwitchToThirdPersonCamera();
                }
                else
                {
                    Debug.Log("[CameraManager] Auto-switching to First Person for minigame");
                    SwitchToFirstPersonCamera();
                }
                return; // Đã xử lý xong
            }
        }

        // Xử lý camera mode sau khi reinitialize (fallback)
        if (cameraOrbit != null)
        {
            if (_currentMode == CameraMode.FirstPerson)
            {
                cameraOrbit.enabled = false;
            }
            else if (_currentMode == CameraMode.ThirdPerson)
            {
                cameraOrbit.enabled = true;
                if (_localPlayerTransform != null)
                {
                    cameraOrbit.SetTarget(_localPlayerTransform);
                }
            }
            else if (_currentMode == CameraMode.Minigame || _currentMode == CameraMode.Fixed)
            {
                cameraOrbit.enabled = false;
            }
        }

        // Nếu có local player và đang ở ThirdPerson, đảm bảo target được set
        if (_localPlayerTransform != null && cameraOrbit != null && _currentMode == CameraMode.ThirdPerson)
        {
            cameraOrbit.SetTarget(_localPlayerTransform);
        }

        Debug.Log($"[CameraManager] Re-initialized. Camera: {mainCamera.name}, CameraOrbit: {(cameraOrbit != null ? "Found" : "None")}, Mode: {_currentMode}");
    }

    private void Start()
    {
        // Không tự động switch - để PlayerController hoặc GameManager quyết định
        // Main Menu: không có player → không cần làm gì
        // Lobby: player spawn → PlayerController gọi SwitchToFirstPersonCamera()
    }

    private void Update()
    {
        // Toggle cursor với Escape
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ToggleCursor();
        }
    }

    private void LateUpdate()
    {
        // Không xử lý camera nếu đang chờ MinigameCamera setup
        if (_pendingSharedCameraMode) return;

        // Chỉ xử lý First Person trong LateUpdate
        if (_currentMode == CameraMode.FirstPerson && _localPlayerTransform != null)
        {
            UpdateFirstPersonCamera();
        }
    }

    /// <summary>
    /// Cập nhật camera First Person
    /// </summary>
    private void UpdateFirstPersonCamera()
    {
        // Chỉ xử lý mouse input nếu camera rotation không bị khóa
        if (!_cameraRotationLocked)
        {
            // Mouse input
            _fpYaw += Input.GetAxis("Mouse X") * fpSensitivityX;
            _fpPitch -= Input.GetAxis("Mouse Y") * fpSensitivityY;
            _fpPitch = Mathf.Clamp(_fpPitch, fpMinPitch, fpMaxPitch);

            // 👉 Player xoay theo yaw
            _localPlayerTransform.rotation = Quaternion.Euler(0, _fpYaw, 0);
        }

        // 👉 Camera chỉ xử lý pitch (luôn cập nhật vị trí)
        mainCamera.transform.position = _localPlayerTransform.position + firstPersonOffset;
        mainCamera.transform.rotation = Quaternion.Euler(_fpPitch, _fpYaw, 0);
    }

    /// <summary>
    /// Khóa/mở khóa xoay camera
    /// Dùng khi vào Voting, Scoreboard, hoặc các state không cho phép xoay
    /// </summary>
    public void SetCameraRotationLocked(bool locked)
    {
        _cameraRotationLocked = locked;

        // Cũng disable/enable CameraOrbit nếu đang ở Third Person
        if (_currentMode == CameraMode.ThirdPerson && cameraOrbit != null)
        {
            cameraOrbit.SetRotationLocked(locked);
        }

        Debug.Log($"[CameraManager] Camera rotation {(locked ? "LOCKED" : "UNLOCKED")}");
    }

    private void ToggleCursor()
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
    /// Chuyển sang camera cố định (cutscene, menu)
    /// </summary>
    public void SwitchToFixedCamera(Transform customPosition = null)
    {
        _currentMode = CameraMode.Fixed;

        // Disable CameraOrbit
        if (cameraOrbit != null)
        {
            cameraOrbit.enabled = false;
        }

        // Unlock cursor trong Fixed mode
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

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
    /// Chuyển sang góc nhìn thứ 1 (First Person) - Dùng trong Lobby
    /// Camera gắn vào đầu player
    /// </summary>
    public void SwitchToFirstPersonCamera()
    {
        _currentMode = CameraMode.FirstPerson;

        // Disable CameraOrbit
        if (cameraOrbit != null)
        {
            cameraOrbit.enabled = false;
        }

        // Đảm bảo camera render tất cả layers
        if (mainCamera != null)
        {
            mainCamera.cullingMask = -1; // Everything
        }

        // Lock cursor
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Sync rotation từ player nếu có
        if (_localPlayerTransform != null)
        {
            _fpYaw = _localPlayerTransform.eulerAngles.y;
            _fpPitch = 0f;
        }

        // Ẩn model của local player (giống GTA 5)
        SetLocalPlayerModelVisible(false);

        Debug.Log("[CameraManager] Switched to First Person Camera");
    }

    /// <summary>
    /// Chuyển sang góc nhìn thứ 3 (Third Person) - CameraOrbit
    /// </summary>
    public void SwitchToThirdPersonCamera()
    {
        _currentMode = CameraMode.ThirdPerson;

        // Enable CameraOrbit và set target
        if (cameraOrbit != null)
        {
            cameraOrbit.enabled = true;
            cameraOrbit.SetTarget(_localPlayerTransform);

            // Sync yaw từ First Person nếu có
            cameraOrbit.SetYaw(_fpYaw);
        }

        // Đảm bảo camera render tất cả layers
        if (mainCamera != null)
        {
            mainCamera.cullingMask = -1; // Everything
        }

        bool isPlaying = GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Playing;
        if (isPlaying)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        // Hiện lại model của local player
        SetLocalPlayerModelVisible(true);

        Debug.Log("[CameraManager] Switched to Third Person Camera (CameraOrbit)");
    }

    /// <summary>
    /// Ẩn/hiện model của local player
    /// Dùng cho First Person mode
    /// </summary>
    private void SetLocalPlayerModelVisible(bool visible)
    {
        if (_localPlayerTransform == null) return;

        var modelSwitcher = _localPlayerTransform.GetComponent<PlayerModelSwitcher>();
        if (modelSwitcher != null)
        {
            modelSwitcher.SetModelVisible(visible);
        }
    }

    /// <summary>
    /// Chuyển sang camera cho Minigame
    /// </summary>
    /// <param name="cameraPosition">Vị trí camera (null = giữ nguyên vị trí hiện tại)</param>
    /// <param name="lockCursor">Có lock cursor không (tùy gameplay của minigame)</param>
    public void SwitchToMinigameCamera(Transform cameraPosition = null, bool lockCursor = false)
    {
        _fpPitch = 0f;
        _pendingSharedCameraMode = false;

        _currentMode = CameraMode.Minigame;
        _currentSharedCameraPosition = cameraPosition;

        // Disable CameraOrbit
        if (cameraOrbit != null)
        {
            cameraOrbit.enabled = false;
        }

        // Di chuyển main camera đến vị trí minigame (nếu có)
        if (cameraPosition != null && mainCamera != null)
        {
            mainCamera.transform.position = cameraPosition.position;
            mainCamera.transform.rotation = cameraPosition.rotation;
        }

        // Đảm bảo camera render tất cả layers (tránh lỗi không thấy player)
        if (mainCamera != null)
        {
            mainCamera.cullingMask = -1; // -1 = Everything (tất cả layers)
        }

        // Hiện lại model của local player (quan trọng khi chuyển từ FirstPerson)
        SetLocalPlayerModelVisible(true);

        // Cursor setting tùy minigame
        Cursor.lockState = lockCursor ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !lockCursor;

        Debug.Log($"[CameraManager] Switched to Minigame Camera{(cameraPosition != null ? ": " + cameraPosition.name : "")}");
    }

    /// <summary>
    /// Đặt flag chờ MinigameCamera setup shared camera
    /// Gọi trước khi load scene minigame có useSharedCamera = true
    /// </summary>
    public void SetPendingSharedCameraMode(bool pending)
    {
        _pendingSharedCameraMode = pending;

        if (pending)
        {
            // Disable CameraOrbit ngay lập tức để tránh nó cập nhật camera
            if (cameraOrbit != null)
            {
                cameraOrbit.enabled = false;
            }
            Debug.Log("[CameraManager] Set pending shared camera mode - waiting for MinigameCamera...");
        }
    }

    /// <summary>
    /// [DEPRECATED] Dùng SwitchToThirdPersonCamera() thay thế
    /// </summary>
    public void SwitchToPlayerCamera()
    {
        SwitchToThirdPersonCamera();
    }

    /// <summary>
    /// [DEPRECATED] Dùng SwitchToMinigameCamera() thay thế
    /// </summary>
    public void SwitchToSharedCamera(Transform sharedCameraPosition)
    {
        SwitchToMinigameCamera(sharedCameraPosition);
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

    /// <summary>
    /// Lấy hướng forward cho di chuyển (Y flattened)
    /// Dùng cho cả First Person và Third Person
    /// </summary>
    public Vector3 GetForwardDirection()
    {
        if (_currentMode == CameraMode.FirstPerson)
        {
            Vector3 forward = Quaternion.Euler(0, _fpYaw, 0) * Vector3.forward;
            return forward.normalized;
        }
        else if (_currentMode == CameraMode.ThirdPerson && cameraOrbit != null)
        {
            return cameraOrbit.GetForwardDirection();
        }

        return mainCamera.transform.forward;
    }

    /// <summary>
    /// Lấy hướng right cho di chuyển (Y flattened)
    /// </summary>
    public Vector3 GetRightDirection()
    {
        if (_currentMode == CameraMode.FirstPerson)
        {
            Vector3 right = Quaternion.Euler(0, _fpYaw, 0) * Vector3.right;
            return right.normalized;
        }
        else if (_currentMode == CameraMode.ThirdPerson && cameraOrbit != null)
        {
            return cameraOrbit.GetRightDirection();
        }

        return mainCamera.transform.right;
    }

    /// <summary>
    /// Lấy Yaw hiện tại (cho player rotation)
    /// </summary>
    public float GetCurrentYaw()
    {
        if (_currentMode == CameraMode.FirstPerson)
            return _fpYaw;
        else if (_currentMode == CameraMode.ThirdPerson && cameraOrbit != null)
            return cameraOrbit.Yaw;
        return mainCamera.transform.eulerAngles.y;
    }

    public void StartSpectate()
    {
        _isSpectating = true;

        _spectateTargets.Clear();

        PlayerMinigameData[] players = FindObjectsOfType<PlayerMinigameData>();

        foreach (var player in players)
        {
            if (player == null)
                continue;

            if (player.IsEliminated)
                continue;

            PlayerController controller = player.GetComponent<PlayerController>();

            if (controller == null)
                continue;

            _spectateTargets.Add(controller);
        }

        _spectateIndex = 0;

        if (_spectateTargets.Count > 0)
        {
            UpdatePlayerTarget(_spectateTargets[_spectateIndex].transform);

            Debug.Log($"[CameraManager] Spectating {_spectateTargets[_spectateIndex].name}");
        }

        Debug.Log($"[CameraManager] Spectate Targets: {_spectateTargets.Count}");
    }

    private void OnGUI()
    {
        if (!showDebugInfo) return;

        GUILayout.BeginArea(new Rect(10, 10, 300, 150));
        GUILayout.Label($"Camera Mode: {_currentMode}");
        GUILayout.Label($"Local Player: {(_localPlayerTransform != null ? _localPlayerTransform.name : "None")}");
        GUILayout.Label($"CameraOrbit: {(cameraOrbit != null ? (cameraOrbit.enabled ? "Enabled" : "Disabled") : "None")}");
        GUILayout.Label($"Yaw: {GetCurrentYaw():F1}° | Pitch: {(_currentMode == CameraMode.FirstPerson ? _fpPitch : (cameraOrbit?.Pitch ?? 0)):F1}°");
        GUILayout.Label("Press V to toggle First/Third Person");
        GUILayout.Label("Press ESC to toggle cursor");
        GUILayout.EndArea();
    }
}
