using Fusion;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
using UnityEngine;

public class PlayerInputHandler : MonoBehaviour, INetworkRunnerCallbacks
{
    public static PlayerInputHandler Instance { get; private set; }

    private Vector2 _moveInput;
    private NetworkButtons _buttons;
    private bool _isRunning; // Trạng thái giữ Shift
    private bool _isCrouching; // Trạng thái ngồi (C hoặc Left Ctrl)

    // Flag to disable input when UI is active (e.g., typing in text field)
    public bool InputEnabled { get; set; } = true;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        // Don't process input if disabled
        if (!InputEnabled)
        {
            _moveInput = Vector2.zero;
            return;
        }

        // Dùng GetAxisRaw để có instant/crispy movement (không smoothing)
        _moveInput = new Vector2(
            Input.GetAxisRaw("Horizontal"),
            Input.GetAxisRaw("Vertical")
        );

        if (_moveInput.sqrMagnitude > 1f)
        {
            _moveInput.Normalize();
        }

        // Jump và Attack dùng GetButtonDown (chỉ trigger 1 lần)
        if (Input.GetButtonDown("Jump"))
            _buttons.Set(PlayerInputData.BUTTON_JUMP, true);

        // Dùng mouse button cụ thể thay vì Fire1 (vì Fire1 mặc định bao gồm Left Ctrl)
        if (Input.GetMouseButtonDown(0))
            _buttons.Set(PlayerInputData.BUTTON_PUNCH, true);

        // MG5 — Drop box (Space)
        if (Input.GetKeyDown(KeyCode.Space))
            _buttons.Set(PlayerInputData.BUTTON_DROP, true);
        if (Input.GetKeyDown(KeyCode.Space))
        {
            Debug.Log("[PlayerInputHandler] SPACE detected, setting BUTTON_DROP");
            _buttons.Set(PlayerInputData.BUTTON_DROP, true);
        }

        // Run dùng GetKey (giữ Left Shift) - lưu riêng
        _isRunning = Input.GetKey(KeyCode.LeftShift);
        
        // Crouch dùng GetKey (giữ C hoặc Left Ctrl)
        _isCrouching = Input.GetKey(KeyCode.C) || Input.GetKey(KeyCode.LeftControl);
        
    }

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        // Note: OnInput is called by Fusion for the local player only.
        // If you need multiple local players (split-screen), consider:
        // - Using PlayerRef parameter to identify which player needs input
        // - Or having separate input handlers per local player

                // Set running/crouch button từ trạng thái hiện tại (không bị ảnh hưởng bởi reset)
        NetworkButtons finalButtons = _buttons;
        if (_isRunning)
            finalButtons.Set(PlayerInputData.BUTTON_SLIDE, true);
        if (_isCrouching)
            finalButtons.Set(PlayerInputData.BUTTON_CROUCH, true);

        // Lấy camera forward direction để gửi lên server
        Vector3 cameraForward = Vector3.forward;
        if (Camera.main != null)
        {
            cameraForward = Camera.main.transform.forward;
            cameraForward.y = 0f;
            cameraForward.Normalize();
        }

        var playerInput = new PlayerInputData
        {
            MoveDirection = InputEnabled ? _moveInput : Vector2.zero,
            CameraForward = cameraForward,
            Buttons = InputEnabled ? finalButtons : default
        };

        input.Set(playerInput);

        // Reset buttons after consumed - this is the correct place to reset
        _buttons = default;
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) { }
    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnSceneLoadDone(NetworkRunner runner) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
}