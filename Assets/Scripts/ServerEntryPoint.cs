using UnityEngine;
using Fusion;
using PlayFlow;
using System.Threading.Tasks;

/// <summary>
/// Entry point cho PlayFlow dedicated server build.
/// Attach script này vào một GameObject trong startup scene (cùng scene với BasicSpawner).
/// Tự động phát hiện headless mode và start Fusion với GameMode.Server.
/// Client build sẽ bỏ qua script này hoàn toàn.
/// </summary>
public class ServerEntryPoint : MonoBehaviour
{
    [Header("Server Settings")]
    [Tooltip("Session name dùng khi không đọc được PlayFlow config")]
    [SerializeField] private string fallbackSessionName = "CDOnlineServer";

    [Tooltip("Build index của LobbyRoom scene (kiểm tra File > Build Settings)")]
    [SerializeField] private int lobbySceneIndex = 1;

    private async void Start()
    {
        // Chỉ chạy khi build headless (-batchmode), bỏ qua hoàn toàn trên client
        if (!Application.isBatchMode) return;

        Debug.Log("[ServerEntryPoint] Headless mode detected. Starting as dedicated server...");

        // Đợi 1 frame để BasicSpawner.Awake() chạy xong
        await Task.Yield();

        if (BasicSpawner.Instance == null)
        {
            Debug.LogError("[ServerEntryPoint] BasicSpawner.Instance not found! " +
                           "Ensure BasicSpawner is in the same scene as ServerEntryPoint.");
            return;
        }

        string sessionName = ReadSessionNameFromConfig();
        SceneRef sceneRef = SceneRef.FromIndex(lobbySceneIndex);

        await BasicSpawner.Instance.StartServer(sessionName, sceneRef);
    }

    /// <summary>
    /// Đọc session name từ playflow.json (được PlayFlow inject vào server khi launch).
    /// Thứ tự ưu tiên: custom_data["session_name"] > match_id > fallbackSessionName
    /// </summary>
    private string ReadSessionNameFromConfig()
    {
        PlayFlowServerConfig config = PlayFlowServerConfig.LoadConfig(useLocalConfig: false);

        if (config == null)
        {
            Debug.LogWarning($"[ServerEntryPoint] PlayFlow config (playflow.json) not found. " +
                             $"Using fallback session name: '{fallbackSessionName}'");
            return fallbackSessionName;
        }

        // Ưu tiên custom_data["session_name"] — được client truyền vào khi gọi StartServerAsync()
        if (config.custom_data != null &&
            config.custom_data.TryGetValue("session_name", out var sessionNameObj) &&
            sessionNameObj != null)
        {
            string name = sessionNameObj.ToString();
            Debug.Log($"[ServerEntryPoint] Session name from PlayFlow custom_data: '{name}'");
            return name;
        }

        // Fallback về match_id nếu không có custom session name
        if (!string.IsNullOrEmpty(config.match_id))
        {
            Debug.Log($"[ServerEntryPoint] Using match_id as session name: '{config.match_id}'");
            return config.match_id;
        }

        Debug.LogWarning($"[ServerEntryPoint] No session name found in PlayFlow config. " +
                         $"Using fallback: '{fallbackSessionName}'");
        return fallbackSessionName;
    }
}
