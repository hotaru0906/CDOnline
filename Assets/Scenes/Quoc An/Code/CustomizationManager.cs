using UnityEngine;
using UnityEngine.UI;
using Unity.Cinemachine;
using TMPro;

public enum CustomizationScene
{
    Menu,   // Dùng Cinemachine cameras
    Lobby   // Dùng Main Camera với offset
}

public class CustomizationManager : MonoBehaviour
{
    private const int HIGH_PRIORITY = 10;
    private const int LOW_PRIORITY = 0;
    private const int CHARACTER_COUNT = 4;

    [Header("Scene Mode")]
    [SerializeField] private CustomizationScene sceneMode = CustomizationScene.Menu;

    [Header("UI")]
    [SerializeField] private Button nextCharacterButton;
    [SerializeField] private Button previousCharacterButton;
    [SerializeField] private Button backButton;
    [SerializeField] private TMP_InputField playerNameInput;
    [SerializeField] private GameObject customizationPanel;

    [Header("Menu Cameras (Cinemachine)")]
    [SerializeField] private CinemachineCamera camMain;
    [SerializeField] private CinemachineCamera[] characterCameras; // 4 cameras cho 4 nhân vật

    [Header("Lobby Camera Settings")]
    [Tooltip("Offset từ player position đến vị trí camera")]
    [SerializeField] private Vector3 lobbyCameraOffset = new Vector3(0f, 1.5f, 2.5f);
    [Tooltip("Camera nhìn vào điểm này (offset từ player)")]
    [SerializeField] private Vector3 lobbyLookAtOffset = new Vector3(0f, 1f, 0f);
    [Tooltip("Thời gian lerp camera")]
    [SerializeField] private float cameraMoveSpeed = 5f;

    private int _currentCharacterIndex;
    private bool _isActive;
    
    // Lobby mode variables
    private Transform _localPlayerTransform;
    private PlayerModelSwitcher _localPlayerModelSwitcher;
    private PlayerNetworkData _localPlayerData;
    private CameraManager _cameraManager;
    private CameraMode _previousCameraMode;
    private Vector3 _originalCameraPosition;
    private Quaternion _originalCameraRotation;
    private bool _isLobbyModeActive;

    public System.Action OnBackToMenu;

    private void Start()
    {
        nextCharacterButton.onClick.AddListener(ShowNextCharacter);
        previousCharacterButton.onClick.AddListener(ShowPreviousCharacter);
        
        if (backButton != null)
            backButton.onClick.AddListener(BackToMenu);

        if (playerNameInput != null)
            playerNameInput.onEndEdit.AddListener(OnPlayerNameChanged);

        // Load saved data từ PlayerPrefs
        _currentCharacterIndex = CharacterSelectionData.SelectedCharacterIndex;
        if (playerNameInput != null)
            playerNameInput.text = CharacterSelectionData.PlayerName;
        
        // Auto-detect scene mode
        if (CameraManager.Instance != null)
            sceneMode = CustomizationScene.Menu;
    }
    
    private void Update()
    {
        // Trong Lobby mode, lerp camera đến vị trí target
        if (_isLobbyModeActive && _localPlayerTransform != null && _cameraManager != null)
        {
            UpdateLobbyCameraPosition();
        }
    }

    /// <summary>
    /// Mở UI customization từ Wardrobe hoặc button
    /// </summary>
    public void OpenCustomizationUI()
    {
        if (sceneMode == CustomizationScene.Lobby)
            ActivateLobbyMode();
        else
            Activate();
    }

    /// <summary>
    /// Đóng UI customization
    /// </summary>
    public void CloseCustomizationUI()
    {
        if (sceneMode == CustomizationScene.Lobby)
            DeactivateLobbyMode();
        else
            Deactivate();
    }

    /// <summary>
    /// Kích hoạt màn hình chọn nhân vật - chuyển sang camera nhân vật đã chọn
    /// </summary>
    public void Activate()
    {
        _isActive = true;
        
        // Load current selection từ PlayerPrefs
        _currentCharacterIndex = CharacterSelectionData.SelectedCharacterIndex;
        if (playerNameInput != null)
            playerNameInput.text = CharacterSelectionData.PlayerName;
        
        SwitchToCharacterCamera(_currentCharacterIndex);
    }

    /// <summary>
    /// Tắt màn hình chọn nhân vật - quay lại camera chính
    /// </summary>
    public void Deactivate()
    {
        _isActive = false;
        SwitchToMainCamera();
    }

    #region Lobby Mode
    
    /// <summary>
    /// Kích hoạt customization trong Lobby - di chuyển camera ra xa player
    /// </summary>
    private void ActivateLobbyMode()
    {
        _isActive = true;
        _isLobbyModeActive = true;
        
        // Tìm local player
        FindLocalPlayer();
        
        if (_localPlayerTransform == null)
        {
            Debug.LogError("[CustomizationManager] Cannot find local player!");
            return;
        }
        
        // Cache camera manager
        _cameraManager = CameraManager.Instance;
        if (_cameraManager == null)
        {
            Debug.LogError("[CustomizationManager] CameraManager not found!");
            return;
        }
        
        // Lưu camera mode hiện tại
        _previousCameraMode = _cameraManager.CurrentMode;
        
        // Lưu vị trí camera gốc
        if (_cameraManager.MainCamera != null)
        {
            _originalCameraPosition = _cameraManager.MainCamera.transform.position;
            _originalCameraRotation = _cameraManager.MainCamera.transform.rotation;
        }
        
        // Disable CameraOrbit để control camera manually
        if (_cameraManager.CameraOrbit != null)
            _cameraManager.CameraOrbit.enabled = false;
        
        // Disable player movement
        var playerController = _localPlayerTransform.GetComponent<PlayerController>();
        if (playerController != null)
            playerController.SetMovementEnabled(false);
        
        // Show UI panel
        if (customizationPanel != null)
            customizationPanel.SetActive(true);
        
        // Load current character index
        if (_localPlayerModelSwitcher != null)
            _currentCharacterIndex = _localPlayerModelSwitcher.GetCurrentModelIndex();
        else
            _currentCharacterIndex = CharacterSelectionData.SelectedCharacterIndex;
        
        // Update name input
        if (playerNameInput != null && _localPlayerData != null)
            playerNameInput.text = _localPlayerData.PlayerName.ToString();
        
        // Unlock cursor for UI
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        
        Debug.Log($"[CustomizationManager] Lobby mode activated. Offset: {lobbyCameraOffset}");
    }
    
    /// <summary>
    /// Tắt customization trong Lobby - trả camera về mode cũ
    /// </summary>
    private void DeactivateLobbyMode()
    {
        _isActive = false;
        _isLobbyModeActive = false;
        
        // Save selection
        SaveSelection();
        
        // Hide UI panel
        if (customizationPanel != null)
            customizationPanel.SetActive(false);
        
        // Re-enable player movement
        if (_localPlayerTransform != null)
        {
            var playerController = _localPlayerTransform.GetComponent<PlayerController>();
            if (playerController != null)
                playerController.SetMovementEnabled(true);
        }
        
        // Restore camera mode
        if (_cameraManager != null)
        {
            // Re-enable CameraOrbit
            if (_cameraManager.CameraOrbit != null)
                _cameraManager.CameraOrbit.enabled = true;
            
            // Switch back to previous mode
            switch (_previousCameraMode)
            {
                case CameraMode.FirstPerson:
                    _cameraManager.SwitchToFirstPersonCamera();
                    break;
                case CameraMode.ThirdPerson:
                    _cameraManager.SwitchToThirdPersonCamera();
                    break;
            }
        }
        
        // Lock cursor again
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        
        Debug.Log("[CustomizationManager] Lobby mode deactivated");
    }
    
    /// <summary>
    /// Update camera position trong Lobby mode
    /// </summary>
    private void UpdateLobbyCameraPosition()
    {
        if (_cameraManager.MainCamera == null || _localPlayerTransform == null) return;
        
        // Tính toán vị trí camera target (phía trước player)
        Vector3 playerForward = _localPlayerTransform.forward;
        Vector3 targetPos = _localPlayerTransform.position 
            + playerForward * lobbyCameraOffset.z 
            + Vector3.up * lobbyCameraOffset.y
            + _localPlayerTransform.right * lobbyCameraOffset.x;
        
        // Điểm nhìn vào
        Vector3 lookAtPoint = _localPlayerTransform.position + lobbyLookAtOffset;
        
        // Lerp camera position
        _cameraManager.MainCamera.transform.position = Vector3.Lerp(
            _cameraManager.MainCamera.transform.position,
            targetPos,
            Time.deltaTime * cameraMoveSpeed
        );
        
        // Lerp camera rotation
        Quaternion targetRot = Quaternion.LookRotation(lookAtPoint - _cameraManager.MainCamera.transform.position);
        _cameraManager.MainCamera.transform.rotation = Quaternion.Slerp(
            _cameraManager.MainCamera.transform.rotation,
            targetRot,
            Time.deltaTime * cameraMoveSpeed
        );
    }
    
    /// <summary>
    /// Tìm local player trong scene
    /// </summary>
    private void FindLocalPlayer()
    {
        foreach (var player in FindObjectsOfType<PlayerNetworkData>())
        {
            if (player.Object != null && player.Object.HasInputAuthority)
            {
                _localPlayerData = player;
                _localPlayerTransform = player.transform;
                _localPlayerModelSwitcher = player.GetComponent<PlayerModelSwitcher>();
                return;
            }
        }
    }
    
    #endregion

    private void ShowNextCharacter()
    {
        if (!_isActive) return;
        
        _currentCharacterIndex = (_currentCharacterIndex + 1) % CHARACTER_COUNT;
        
        if (sceneMode == CustomizationScene.Lobby)
            SwitchPlayerModel(_currentCharacterIndex);
        else
            SwitchToCharacterCamera(_currentCharacterIndex);
    }

    private void ShowPreviousCharacter()
    {
        if (!_isActive) return;
        
        _currentCharacterIndex = (_currentCharacterIndex - 1 + CHARACTER_COUNT) % CHARACTER_COUNT;
        
        if (sceneMode == CustomizationScene.Lobby)
            SwitchPlayerModel(_currentCharacterIndex);
        else
            SwitchToCharacterCamera(_currentCharacterIndex);
    }

    /// <summary>
    /// Chuyển model của player trong Lobby mode
    /// </summary>
    private void SwitchPlayerModel(int index)
    {
        if (_localPlayerModelSwitcher == null)
        {
            Debug.LogWarning("[CustomizationManager] PlayerModelSwitcher not found!");
            return;
        }
        
        _localPlayerModelSwitcher.SetCharacterModel(index);
        
        // Cập nhật PlayerNetworkData để sync với các player khác
        if (_localPlayerData != null)
        {
            _localPlayerData.SetCharacterIndex(index);
        }
        
        Debug.Log($"[CustomizationManager] Switched to model {index}");
    }

    private void BackToMenu()
    {
        SaveSelection();
        
        if (sceneMode == CustomizationScene.Lobby)
        {
            DeactivateLobbyMode();
        }
        else
        {
            Deactivate();
            OnBackToMenu?.Invoke();
        }
    }

    private void OnPlayerNameChanged(string newName)
    {
        CharacterSelectionData.PlayerName = newName;
        
        // Trong Lobby mode, cập nhật tên player trực tiếp
        if (sceneMode == CustomizationScene.Lobby && _localPlayerData != null)
        {
            _localPlayerData.SetPlayerName(newName);
        }
    }

    private void SaveSelection()
    {
        CharacterSelectionData.SelectedCharacterIndex = _currentCharacterIndex;
        Debug.Log($"[CustomizationManager] Saved: Character {_currentCharacterIndex}, Name: {CharacterSelectionData.PlayerName}");
        
        // Trong Lobby mode, cập nhật PlayerNetworkData
        if (sceneMode == CustomizationScene.Lobby && _localPlayerData != null)
        {
            // Đã được cập nhật realtime qua SwitchPlayerModel và OnPlayerNameChanged
        }
    }

    #region Debug

#if UNITY_EDITOR
    /// <summary>
    /// Hiển thị offset values trong Inspector để dễ điều chỉnh
    /// </summary>
    private void OnValidate()
    {
        // Clamp values hợp lý
        lobbyCameraOffset.y = Mathf.Clamp(lobbyCameraOffset.y, -5f, 10f);
        lobbyCameraOffset.z = Mathf.Clamp(lobbyCameraOffset.z, 0.5f, 10f);
        cameraMoveSpeed = Mathf.Clamp(cameraMoveSpeed, 0.5f, 20f);
    }
#endif

    #endregion

    private void SwitchToCharacterCamera(int index)
    {
        if (characterCameras == null || characterCameras.Length == 0) return;

        // Set main camera low priority
        if (camMain != null)
            camMain.Priority = LOW_PRIORITY;

        // Set all character cameras low, then set selected one high
        for (int i = 0; i < characterCameras.Length; i++)
        {
            if (characterCameras[i] != null)
                characterCameras[i].Priority = (i == index) ? HIGH_PRIORITY : LOW_PRIORITY;
        }
    }

    private void SwitchToMainCamera()
    {
        // Set main camera high priority
        if (camMain != null)
            camMain.Priority = HIGH_PRIORITY;

        // Set all character cameras low
        if (characterCameras != null)
        {
            foreach (var cam in characterCameras)
            {
                if (cam != null)
                    cam.Priority = LOW_PRIORITY;
            }
        }
    }

    public int GetSelectedCharacterIndex() => _currentCharacterIndex;
}