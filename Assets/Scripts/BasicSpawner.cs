using Fusion;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

public class BasicSpawner : MonoBehaviour, INetworkRunnerCallbacks
{
    [Header("Player Settings")]
    [SerializeField] private NetworkPrefabRef playerPrefab;
    [SerializeField] private Transform[] spawnPoints;

    [Header("Game Manager")]
    [SerializeField] private NetworkPrefabRef gameManagerPrefab;

    private NetworkRunner _runner;
    private PlayerInputHandler _inputHandler;
    private Dictionary<PlayerRef, NetworkObject> _spawnedPlayers = new Dictionary<PlayerRef, NetworkObject>();
    private NetworkObject _gameManagerInstance;
    private bool _isStartingGame;

    async Task StartGame(GameMode mode)
    {
        // Prevent duplicate calls
        if (_isStartingGame || _runner != null)
        {
            Debug.LogWarning("[BasicSpawner] StartGame already in progress or completed!");
            return;
        }
        _isStartingGame = true;

        try
        {
            // Create the Fusion runner and let it know that we will be providing user input
            _runner = gameObject.AddComponent<NetworkRunner>();
            _runner.ProvideInput = true;

            // Register this spawner as callback (for OnPlayerJoined, etc.)
            _runner.AddCallbacks(this);

            // Add PlayerInputHandler as callback for input collection (only once)
            if (_inputHandler == null)
            {
                _inputHandler = gameObject.AddComponent<PlayerInputHandler>();
            }
            _runner.AddCallbacks(_inputHandler);

            // Create the NetworkSceneInfo from the current scene
            var scene = SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex);

            // Start or join (depends on gamemode) a session with a specific name
            var result = await _runner.StartGame(new StartGameArgs()
            {
                GameMode = mode,
                SessionName = "TestRoom",
                Scene = scene,
                SceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>()
            });

            // Check if StartGame succeeded
            if (!result.Ok)
            {
                Debug.LogError($"[BasicSpawner] Failed to start game: {result.ShutdownReason}");
                // Cleanup on failure
                if (_runner != null)
                {
                    Destroy(_runner);
                    _runner = null;
                }
                _isStartingGame = false;
                return;
            }

            Debug.Log($"[BasicSpawner] Game started successfully as {mode}");

            // Spawn GameManager if we're the Host/Server
            // if (_runner.IsServer)
            // {
            //     SpawnGameManager();
            // }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[BasicSpawner] Exception during StartGame: {ex.Message}");
            _isStartingGame = false;
            throw;
        }
    }

    /// <summary>
    /// Spawn the GameManager NetworkObject. Host only, once per session.
    /// </summary>
    // private void SpawnGameManager()
    // {
    //     if (_gameManagerInstance != null)
    //     {
    //         Debug.LogWarning("[BasicSpawner] GameManager already spawned!");
    //         return;
    //     }

    //     if (!gameManagerPrefab.IsValid)
    //     {
    //         Debug.LogError("[BasicSpawner] GameManager prefab not assigned!");
    //         return;
    //     }

    //     Debug.Log("[BasicSpawner] Spawning GameManager...");
    //     _gameManagerInstance = _runner.Spawn(
    //         gameManagerPrefab,
    //         Vector3.zero,
    //         Quaternion.identity,
    //         null // No input authority - server controlled
    //     );
    // }

    // private async void Start()
    // {
    //     // For testing, start as Host. Change to Client to test joining.
    //     try
    //     {
    //         await StartGame(GameMode.Host);
    //     }
    //     catch (Exception ex)
    //     {
    //         Debug.LogError($"[BasicSpawner] Failed to start: {ex.Message}");
    //     }
    // }

    //Sử dụng button để bắt đầu game thành host
    public async void StartAsHost()
    {
        await StartGame(GameMode.Host);
    }
    public async void StartAsClient()
    {
        await StartGame(GameMode.Client);
    }
    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        // Only Host spawns players
        if (!runner.IsServer) return;

        // Validate playerPrefab
        if (!playerPrefab.IsValid)
        {
            Debug.LogError("[BasicSpawner] Player prefab not assigned! Cannot spawn player.");
            return;
        }

        Debug.Log($"[BasicSpawner] Player {player} joined. Spawning...");

        // Calculate spawn position
        Vector3 spawnPosition = GetSpawnPosition(player);

        // Spawn player with input authority
        NetworkObject playerObject = runner.Spawn(
            playerPrefab,
            spawnPosition,
            Quaternion.identity,
            player // Input authority
        );

        if (playerObject != null)
        {
            // Track spawned players
            _spawnedPlayers[player] = playerObject;
        }
        else
        {
            Debug.LogError($"[BasicSpawner] Failed to spawn player {player}!");
        }
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        // Despawn player when they leave
        if (_spawnedPlayers.TryGetValue(player, out NetworkObject playerObject))
        {
            runner.Despawn(playerObject);
            _spawnedPlayers.Remove(player);
            Debug.Log($"[BasicSpawner] Player {player} left. Despawned.");
        }
    }

    public void OnInput(NetworkRunner runner, NetworkInput input) { }
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

    #region Helpers
    /// <summary>
    /// Get spawn position for a player. Uses spawn points if available, otherwise random position.
    /// </summary>
    private Vector3 GetSpawnPosition(PlayerRef player)
    {
        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            // Use spawn point based on player index
            int index = player.PlayerId % spawnPoints.Length;

            // Validate spawn point is not null
            if (spawnPoints[index] != null)
            {
                return spawnPoints[index].position;
            }

            // Try to find any valid spawn point
            for (int i = 0; i < spawnPoints.Length; i++)
            {
                if (spawnPoints[i] != null)
                {
                    Debug.LogWarning($"[BasicSpawner] SpawnPoint[{index}] is null, using SpawnPoint[{i}] instead.");
                    return spawnPoints[i].position;
                }
            }

            Debug.LogWarning("[BasicSpawner] All spawn points are null, using fallback position.");
        }

        // Fallback: random position in a circle
        float angle = player.PlayerId * 45f * Mathf.Deg2Rad;
        float radius = 3f;
        return new Vector3(Mathf.Cos(angle) * radius, 1f, Mathf.Sin(angle) * radius);
    }
    #endregion
}