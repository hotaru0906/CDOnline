using Fusion;
using Fusion.Sockets;
using System;
using System.Collections;
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
    [SerializeField] private MenuManager menuManager;
    private PlayerInputHandler _inputHandler;
    private Dictionary<PlayerRef, NetworkObject> _spawnedPlayers = new Dictionary<PlayerRef, NetworkObject>();
    private NetworkObject _gameManagerInstance;
    private static BasicSpawner _instance;
    private bool _callbacksAdded = false; // Track nếu callbacks đã được thêm
    public static BasicSpawner Instance => _instance;

    public void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;

        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleUnitySceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleUnitySceneLoaded;
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }

    public async Task StartLobbyAndRunner()
    {
        // If runner is null, create new one
        if (_runner == null)
        {
            _runner = gameObject.AddComponent<NetworkRunner>();
        }

        if (_runner.IsRunning)
        {
            Debug.LogWarning("[BasicSpawner] NetworkRunner already running!");
            return;
        }

        // Cấu hình runner với ProvideInput và callbacks
        EnsureRunnerConfigured();

        var res = await _runner.JoinSessionLobby(SessionLobby.ClientServer);
        if (res.Ok)
        {
            Debug.Log("[BasicSpawner] Joined lobby successfully.");
            // Hide loading khi vào lobby thành công
            LoadingScreen.Hide();
        }
        else
        {
            Debug.LogError($"[BasicSpawner] Failed to join lobby: {res.ShutdownReason}");
            LoadingScreen.Hide();
        }
    }

    public async Task StartHost(string sessionName, SceneRef sceneName)
    {
        if (_runner == null)
        {
            _runner = gameObject.AddComponent<NetworkRunner>();
        }
        
        // Đảm bảo runner được cấu hình đúng
        EnsureRunnerConfigured();
        
        var sceneManager = GetComponent<NetworkSceneManagerDefault>();
        if (sceneManager == null)
        {
            sceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>();
        }

        var res = await _runner.StartGame(new StartGameArgs()
        {
            GameMode = GameMode.Host,
            SessionName = sessionName,
            Scene = sceneName,
            SceneManager = sceneManager
        });
        if (res.Ok)
        {
            Debug.Log("[BasicSpawner] Host started successfully.");
        }
        else
        {
            Debug.LogError($"[BasicSpawner] Failed to start host: {res.ShutdownReason}");
        }
    }

    public async Task StartClient(string sessionName)
    {
        if (_runner == null)
        {
            _runner = gameObject.AddComponent<NetworkRunner>();
        }
        
        // Đảm bảo runner được cấu hình đúng
        EnsureRunnerConfigured();
        
        // Get or create scene manager
        var sceneManager = GetComponent<NetworkSceneManagerDefault>();
        if (sceneManager == null)
        {
            sceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>();
        }

        var res = await _runner.StartGame(new StartGameArgs()
        {
            GameMode = GameMode.Client,
            SessionName = sessionName,
            SceneManager = sceneManager
        });
        if (res.Ok)
        {
            Debug.Log("[BasicSpawner] Client started successfully.");
        }
        else
        {
            Debug.LogError($"[BasicSpawner] Failed to start client: {res.ShutdownReason}");
            LoadingScreen.Hide();
        }
    }
    
    /// <summary>
    /// Đảm bảo NetworkRunner được cấu hình đúng với ProvideInput và callbacks
    /// </summary>
    private void EnsureRunnerConfigured()
    {
        if (_runner == null) return;
        
        // Quan trọng: ProvideInput = true cho phép client gửi input
        _runner.ProvideInput = true;
        
        // Chỉ thêm callbacks nếu chưa thêm
        if (!_callbacksAdded)
        {
            _runner.AddCallbacks(this);
            
            // Thêm PlayerInputHandler
            if (_inputHandler == null)
            {
                _inputHandler = gameObject.AddComponent<PlayerInputHandler>();
            }
            _runner.AddCallbacks(_inputHandler);
            
            _callbacksAdded = true;
            Debug.Log("[BasicSpawner] Runner configured with ProvideInput and callbacks");
        }
    }
    private void SpawnGameManager()
    {
        if (_gameManagerInstance != null)
        {
            Debug.LogWarning("[BasicSpawner] GameManager already spawned!");
            return;
        }

        if (!gameManagerPrefab.IsValid)
        {
            Debug.LogError("[BasicSpawner] GameManager prefab not assigned!");
            return;
        }

        Debug.Log("[BasicSpawner] Spawning GameManager...");
        
        // Spawn với flags để không bị destroy khi đổi scene
        // Tất cả children (VotingManager, MinigameVotingManager) sẽ đi theo
        _gameManagerInstance = _runner.Spawn(
            gameManagerPrefab,
            Vector3.zero,
            Quaternion.identity,
            null, // No input authority - server controlled
            onBeforeSpawned: (runner, obj) =>
            {
                // Object sẽ không bị destroy khi đổi scene
                UnityEngine.Object.DontDestroyOnLoad(obj.gameObject);
            }
        );
    }
    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        // Only Host spawns players
        if (!runner.IsServer) return;

        // Check if player already spawned (avoid duplicate)
        if (_spawnedPlayers.ContainsKey(player))
        {
            Debug.LogWarning($"[BasicSpawner] Player {player} already spawned, skipping.");
            return;
        }

        // Validate playerPrefab
        if (!playerPrefab.IsValid)
        {
            Debug.LogError("[BasicSpawner] Player prefab not assigned! Cannot spawn player.");
            return;
        }

        Debug.Log($"[BasicSpawner] Player {player} joined. Spawning...");

        // Calculate spawn position
        Vector3 spawnPosition = GetSpawnPosition(player);
        Quaternion spawnRotation = GetSpawnRotation(player);

        // Spawn player with input authority
        NetworkObject playerObject = runner.Spawn(
            playerPrefab,
            spawnPosition,
            spawnRotation,
            player // Input authority
        );

        if (playerObject != null)
        {
            // Track spawned players
            _spawnedPlayers[player] = playerObject;
            
            // Note: LoadingScreen.Hide() is called in PlayerNetworkData.Spawned()
            // to ensure it only hides when local player is fully ready
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

    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        Debug.Log($"[BasicSpawner] Shutdown: {shutdownReason}");

        _spawnedPlayers.Clear();
        _gameManagerInstance = null;
        _callbacksAdded = false; // Reset flag để lần sau có thể thêm lại callbacks

        if (PlayerNetworkData.Local != null)
        {
            PlayerNetworkData.Local = null;
        }

        var oldSceneManager = GetComponent<NetworkSceneManagerDefault>();
        if (oldSceneManager != null)
        {
            Destroy(oldSceneManager);
        }

        if (_runner != null)
        {
            _runner.RemoveCallbacks(this);

            if (_inputHandler != null)
            {
                _runner.RemoveCallbacks(_inputHandler);
            }

            Destroy(_runner);
            _runner = null;
        }
    }

    public void OnConnectedToServer(NetworkRunner runner)
    {
        // Client connected - update loading text
        LoadingScreen.SetText("Loading game data...");
    }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
    {
        var ui = ResolveMenuManager();
        if (ui != null)
        {
            ui.UpdateRoomList(sessionList);
        }
    }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnSceneLoadDone(NetworkRunner runner)
    {
        Debug.Log($"[BasicSpawner] Scene load done");
        ResolveMenuManager();
        if (runner.IsServer)
        {
            // Spawn GameManager if not exists
            if (_gameManagerInstance == null)
            {
                SpawnGameManager();
            }

            // Respawn all connected players in new scene
            RespawnAllPlayers();
        }
    }
    public void OnSceneLoadStart(NetworkRunner runner)
    {
        // Show loading when scene starts loading (minigame transition)
        LoadingScreen.Show("Loading...");
    }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }

    #region Helpers
    /// <summary>
    /// Get spawn position for a player. 
    /// Priority: MinigameController spawn points > Scene spawn points > BasicSpawner spawn points > Fallback
    /// </summary>
    private Vector3 GetSpawnPosition(PlayerRef player)
    {
        // 1. Try MinigameController spawn points first (for minigame scenes)
        if (BaseMinigameController.Instance != null)
        {
            var mgSpawnPoint = BaseMinigameController.Instance.GetSpawnPoint(player.PlayerId);
            if (mgSpawnPoint != Vector3.zero)
            {
                Debug.Log($"[BasicSpawner] Using MinigameController spawn point for player {player}");
                return mgSpawnPoint;
            }
        }

        // 2. Try to find spawn points tagged "SpawnPoint" in current scene
        var sceneSpawnPoints = GameObject.FindGameObjectsWithTag("SpawnPoint");
        if (sceneSpawnPoints != null && sceneSpawnPoints.Length > 0)
        {
            int index = player.PlayerId % sceneSpawnPoints.Length;
            Debug.Log($"[BasicSpawner] Using scene SpawnPoint tag for player {player}");
            return sceneSpawnPoints[index].transform.position;
        }

        // 3. Use BasicSpawner's configured spawn points
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

        // 4. Fallback: random position in a circle
        float angle = player.PlayerId * 45f * Mathf.Deg2Rad;
        float radius = 3f;
        return new Vector3(Mathf.Cos(angle) * radius, 1f, Mathf.Sin(angle) * radius);
    }

    /// <summary>
    /// Get spawn rotation for a player.
    /// Priority: MinigameController spawn points > Scene spawn points > BasicSpawner spawn points > Identity
    /// </summary>
    private Quaternion GetSpawnRotation(PlayerRef player)
    {
        // 1. Try MinigameController spawn points first (for minigame scenes)
        if (BaseMinigameController.Instance != null)
        {
            var mgSpawnRotation = BaseMinigameController.Instance.GetSpawnRotation(player.PlayerId);
            if (mgSpawnRotation != Quaternion.identity)
            {
                return mgSpawnRotation;
            }
        }

        // 2. Try to find spawn points tagged "SpawnPoint" in current scene
        var sceneSpawnPoints = GameObject.FindGameObjectsWithTag("SpawnPoint");
        if (sceneSpawnPoints != null && sceneSpawnPoints.Length > 0)
        {
            int index = player.PlayerId % sceneSpawnPoints.Length;
            return sceneSpawnPoints[index].transform.rotation;
        }

        // 3. Use BasicSpawner's configured spawn points
        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            int index = player.PlayerId % spawnPoints.Length;
            if (spawnPoints[index] != null)
            {
                return spawnPoints[index].rotation;
            }

            // Try to find any valid spawn point
            for (int i = 0; i < spawnPoints.Length; i++)
            {
                if (spawnPoints[i] != null)
                {
                    return spawnPoints[i].rotation;
                }
            }
        }

        // 4. Fallback: identity rotation
        return Quaternion.identity;
    }
    #endregion

    /// <summary>
    /// Respawn all connected players. Called when loading a new scene.
    /// </summary>
    private void RespawnAllPlayers()
    {
        Debug.Log("[BasicSpawner] Respawning all players for new scene...");

        // First, despawn existing player objects
        foreach (var kvp in _spawnedPlayers)
        {
            if (kvp.Value != null && kvp.Value.IsValid)
            {
                _runner.Despawn(kvp.Value);
            }
        }
        _spawnedPlayers.Clear();
        
        // Delay spawn để đợi scene objects (MinigameController, etc.) được khởi tạo
        StartCoroutine(DelayedSpawnPlayers());
    }

    private System.Collections.IEnumerator DelayedSpawnPlayers()
    {
        // Đợi 1 frame để scene objects Awake() chạy xong
        yield return null;
        
        // Spawn all connected players
        foreach (var player in _runner.ActivePlayers)
        {
            SpawnPlayerForScene(player);
        }

        // Hide loading after all players spawned
        yield return null; // Wait one more frame to ensure spawn completes
        LoadingScreen.Hide();
    }

    /// <summary>
    /// Spawn a player for the current scene.
    /// </summary>
    private void SpawnPlayerForScene(PlayerRef player)
{
    if (!playerPrefab.IsValid)
    {
        Debug.LogError("[BasicSpawner] Player prefab not assigned!");
        return;
    }

    // Check if player already has an object
    if (_spawnedPlayers.ContainsKey(player))
    {
        Debug.LogWarning($"[BasicSpawner] Player {player} already spawned!");
        return;
    }

    Vector3 spawnPosition = GetSpawnPosition(player);
    Quaternion spawnRotation = GetSpawnRotation(player);

    NetworkObject playerObject = _runner.Spawn(
        playerPrefab,
        spawnPosition,
        spawnRotation,
        player
    );

    if (playerObject != null)
    {
        _spawnedPlayers[player] = playerObject;
        Debug.Log($"[BasicSpawner] Spawned player {player} at {spawnPosition}");
    }
}

private MenuManager ResolveMenuManager()
{
    if (menuManager == null)
    {
        menuManager = FindAnyObjectByType<MenuManager>();
    }

    return menuManager;
}

private void HandleUnitySceneLoaded(Scene scene, LoadSceneMode mode)
{
    menuManager = FindAnyObjectByType<MenuManager>();
}
}
