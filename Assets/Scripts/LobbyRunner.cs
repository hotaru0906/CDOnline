using UnityEngine;
using Fusion;
using PlayFlow.SDK.Servers;
using System.Collections.Generic;
using System.Threading.Tasks;

public class LobbyRunner : MonoBehaviour
{
    public BasicSpawner _BasicSpawner;

    [Header("PlayFlow Dedicated Server")]
    [Tooltip("Client API key từ PlayFlow dashboard (tab API Keys)")]
    [SerializeField] private string playFlowClientApiKey = "";

    [Tooltip("Region của server. Xem danh sách tại docs.playflowcloud.com")]
    [SerializeField] private string serverRegion = "us-east";

    [Tooltip("Số giây tối đa chờ server online trước khi fallback về Host mode")]
    [SerializeField] private float serverStartTimeoutSeconds = 90f;

    [Tooltip("Tắt để dùng Host mode (peer-to-peer) thay vì PlayFlow dedicated server")]
    [SerializeField] private bool useDedicatedServer = true;

    private void Awake()
    {
        if (_BasicSpawner == null)
        {
            _BasicSpawner = BasicSpawner.Instance ?? FindAnyObjectByType<BasicSpawner>();
        }
    }

    async void Start()
    {
        if (_BasicSpawner == null)
        {
            Debug.LogError("[LobbyRunner] No BasicSpawner found, cannot start lobby.");
            return;
        }

        // Show loading when game starts - waiting for lobby connection
        LoadingScreen.Show("Connecting to server...");

        await _BasicSpawner.StartLobbyAndRunner();
    }

    public async void CreateSession(string sessionName)
    {
        Debug.Log($"[LobbyRunner] Creating session: {sessionName}");

        if (_BasicSpawner == null)
        {
            _BasicSpawner = BasicSpawner.Instance ?? FindAnyObjectByType<BasicSpawner>();
        }

        if (_BasicSpawner == null)
        {
            Debug.LogError("[LobbyRunner] Cannot create session without BasicSpawner.");
            return;
        }

        // Dùng dedicated server nếu đã cấu hình API key
        if (useDedicatedServer && !string.IsNullOrEmpty(playFlowClientApiKey))
        {
            await CreateSessionViaDedicatedServer(sessionName);
        }
        else
        {
            // Fallback: Host mode (peer-to-peer)
            Debug.LogWarning("[LobbyRunner] PlayFlow API key not set. Falling back to Host mode.");
            LoadingScreen.Show("Creating room...");
            await _BasicSpawner.StartHost(sessionName, SceneRef.FromIndex(1));
        }
    }

    /// <summary>
    /// Gọi PlayFlow API để khởi động dedicated server, chờ server online,
    /// rồi join vào session đó như một client bình thường.
    /// </summary>
    private async Task CreateSessionViaDedicatedServer(string sessionName)
    {
        LoadingScreen.Show("Starting dedicated server...");

        var apiClient = new PlayflowServerApiClient(playFlowClientApiKey);

        string instanceId = null;
        try
        {
            var request = new ServerCreateRequest
            {
                name = $"CDOnline-{sessionName}",
                region = serverRegion,
                compute_size = ComputeSizes.Small,
                ttl = 3600, // 60 phút — giới hạn free tier
                custom_data = new Dictionary<string, object>
                {
                    { "session_name", sessionName }
                }
            };

            Debug.Log($"[LobbyRunner] Requesting PlayFlow server for session '{sessionName}'...");
            var response = await apiClient.StartServerAsync(request);
            instanceId = response.instance_id;
            Debug.Log($"[LobbyRunner] Server instance created: {instanceId}. Waiting for it to come online...");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[LobbyRunner] Failed to start PlayFlow server: {e.Message}. Falling back to Host mode.");
            LoadingScreen.Show("Creating room (fallback)...");
            await _BasicSpawner.StartHost(sessionName, SceneRef.FromIndex(1));
            return;
        }

        // Poll cho đến khi server running hoặc timeout
        bool serverReady = await WaitForServerRunning(apiClient, instanceId);

        if (serverReady)
        {
            // Server đã online → join như client bình thường
            // Session đã được server tự đăng ký lên Photon nameserver
            LoadingScreen.Show("Joining room...");
            Debug.Log($"[LobbyRunner] Server is ready. Joining session '{sessionName}' as client...");
            await _BasicSpawner.StartClient(sessionName);
        }
        else
        {
            // Timeout → fallback về Host mode
            Debug.LogWarning($"[LobbyRunner] Server did not start within {serverStartTimeoutSeconds}s. Falling back to Host mode.");
            LoadingScreen.Show("Creating room (fallback)...");
            await _BasicSpawner.StartHost(sessionName, SceneRef.FromIndex(1));
        }
    }

    /// <summary>
    /// Poll PlayFlow API mỗi 3 giây cho đến khi server có status "running" hoặc timeout.
    /// </summary>
    private async Task<bool> WaitForServerRunning(PlayflowServerApiClient apiClient, string instanceId)
    {
        float elapsed = 0f;
        const float pollInterval = 3f;

        while (elapsed < serverStartTimeoutSeconds)
        {
            await Task.Delay((int)(pollInterval * 1000));
            elapsed += pollInterval;

            try
            {
                var details = await apiClient.GetServerDetailsAsync(instanceId);

                LoadingScreen.SetText($"Starting server... ({(int)elapsed}s)");
                Debug.Log($"[LobbyRunner] Server status: {details.status} ({(int)elapsed}s elapsed)");

                if (details.status == InstanceStates.Running)
                    return true;

                if (details.status == InstanceStates.Stopped)
                {
                    Debug.LogError("[LobbyRunner] Server stopped unexpectedly during launch.");
                    return false;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[LobbyRunner] Error polling server status: {e.Message}");
            }
        }

        return false;
    }

    public async void JoinSession(string sessionName)
    {
        Debug.Log($"[LobbyRunner] Joining session: {sessionName}");
        
        // Show loading when joining room
        LoadingScreen.Show("Joining room...");
        
        if (_BasicSpawner == null)
        {
            _BasicSpawner = BasicSpawner.Instance ?? FindAnyObjectByType<BasicSpawner>();
        }

        if (_BasicSpawner == null)
        {
            Debug.LogError("[LobbyRunner] Cannot join session without BasicSpawner.");
            LoadingScreen.Hide();
            return;
        }
        await _BasicSpawner.StartClient(sessionName);
    }
}
