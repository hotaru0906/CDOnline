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

        // Dùng GetAxis để có smooth movement giống PlayerMovement1
        _moveInput = new Vector2(
            Input.GetAxis("Horizontal"),
            Input.GetAxis("Vertical")
        );

        if (_moveInput.sqrMagnitude > 1f)
        {
            _moveInput.Normalize();
        }

        // Accumulate button presses until OnInput consumes them
        // Jump và Attack dùng GetButtonDown (edge detection - chỉ frame nhấn)
        if (Input.GetButtonDown("Jump"))
            _buttons.Set(PlayerInputData.BUTTON_JUMP, true);

        if (Input.GetButtonDown("Fire1"))
            _buttons.Set(PlayerInputData.BUTTON_PUNCH, true);

        // Run dùng GetKey (giữ Left Shift để chạy)
        if (Input.GetKey(KeyCode.LeftShift))
            _buttons.Set(PlayerInputData.BUTTON_SLIDE, true);
    }

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        // Note: OnInput is called by Fusion for the local player only.
        // If you need multiple local players (split-screen), consider:
        // - Using PlayerRef parameter to identify which player needs input
        // - Or having separate input handlers per local player

        var playerInput = new PlayerInputData
        {
            MoveDirection = InputEnabled ? _moveInput : Vector2.zero,
            Buttons = InputEnabled ? _buttons : default
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