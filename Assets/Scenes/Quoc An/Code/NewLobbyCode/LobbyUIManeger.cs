using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

// ============================================================
// LobbyUIManager
// Xử lý: Quit Room, Settings, PlayerList display
//
// OFFLINE  : Log ra Console, test trong Inspector
// ONLINE   : Uncomment phần Photon/Mirror khi sẵn sàng
// ============================================================

public class LobbyUIManager : MonoBehaviour
{
    // ========================================================
    // INSPECTOR REFERENCES
    // ========================================================

    [Header("--- TOP BAR ---")]
    [Tooltip("Kéo QuitRoomButton vào đây")]
    public Button quitRoomButton;

    [Tooltip("Kéo SettingsButton vào đây")]
    public Button settingsButton;

    [Header("--- SCENE NAVIGATION ---")]
    [Tooltip("Tên scene sẽ load khi Quit Room (VD: 'MainMenu')")]
    public string quitToSceneName = "MainMenu";

    [Tooltip("Tên scene sẽ load khi vào Settings (VD: 'Settings')")]
    public string settingsSceneName = "Settings";

    [Tooltip("Nếu TRUE: dùng Canvas thay vì load Scene mới")]
    public bool useCanvasInsteadOfScene = false;

    [Tooltip("Kéo Canvas MainMenu vào đây nếu useCanvasInsteadOfScene = TRUE")]
    public GameObject mainMenuCanvas;

    [Tooltip("Kéo Canvas Settings vào đây nếu useCanvasInsteadOfScene = TRUE")]
    public GameObject settingsCanvas;

    [Tooltip("Kéo LobbyCanvas vào đây để ẩn khi chuyển canvas")]
    public GameObject lobbyCanvas;

    [Header("--- PLAYER LIST ---")]
    [Tooltip("Kéo object Content (trong PlayerScrollView/Viewport/Content) vào đây")]
    public Transform playerListContainer;

    [Tooltip("Kéo PlayerRow prefab vào đây")]
    public GameObject playerRowPrefab;

    [Tooltip("Kéo PlayerCountText (TextMeshPro) vào đây")]
    public TextMeshProUGUI playerCountText;

    [Tooltip("Số player tối đa trong phòng")]
    public int maxPlayers = 4;

    [Header("--- DEBUG / OFFLINE TEST ---")]
    [Tooltip("Danh sách player giả để test offline — thêm/xóa trong Inspector")]
    public List<DebugPlayerData> debugPlayers = new List<DebugPlayerData>()
    {
        new DebugPlayerData { playerName = "HostPlayer", isReady = true,  isHost = true  },
        new DebugPlayerData { playerName = "Alice",      isReady = false, isHost = false },
    };

    // ========================================================
    // RUNTIME DATA
    // ========================================================
    private List<GameObject> playerRowObjects = new List<GameObject>();

    // ========================================================
    // UNITY LIFECYCLE
    // ========================================================
    void Start()
    {
        SetupButtons();
        RefreshPlayerList_Debug();

        Debug.Log("[LobbyUI] LobbyUIManager initialized.");
    }

    // ========================================================
    // BUTTON SETUP
    // ========================================================
    void SetupButtons()
    {
        if (quitRoomButton != null)
            quitRoomButton.onClick.AddListener(OnQuitRoomClicked);
        else
            Debug.LogWarning("[LobbyUI] quitRoomButton chưa được gán trong Inspector!");

        if (settingsButton != null)
            settingsButton.onClick.AddListener(OnSettingsClicked);
        else
            Debug.LogWarning("[LobbyUI] settingsButton chưa được gán trong Inspector!");
    }

    // ========================================================
    // QUIT ROOM
    // ========================================================
    void OnQuitRoomClicked()
    {
        Debug.Log($"[LobbyUI] Quit Room clicked. Target: '{quitToSceneName}'");

        if (useCanvasInsteadOfScene)
        {
            SwitchToCanvas(mainMenuCanvas);
            return;
        }

        // Load scene nếu tên scene đã được điền
        if (!string.IsNullOrEmpty(quitToSceneName))
        {
            // Kiểm tra scene có trong Build Settings không
            bool sceneExists = false;
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCountInBuildSettings; i++)
            {
                string path = UnityEngine.SceneManagement.SceneUtility.GetScenePathByBuildIndex(i);
                if (path.Contains(quitToSceneName))
                {
                    sceneExists = true;
                    break;
                }
            }

            if (sceneExists)
            {
                // --- ONLINE (Photon PUN2) --- uncomment khi tích hợp
                // PhotonNetwork.LeaveRoom();
                // PhotonNetwork.LoadLevel(quitToSceneName);

                UnityEngine.SceneManagement.SceneManager.LoadScene(quitToSceneName);
            }
            else
            {
                Debug.LogWarning($"[LobbyUI] Scene '{quitToSceneName}' không tìm thấy trong Build Settings. " +
                                 $"Vào File > Build Settings > Add Open Scenes để thêm scene.");
            }
        }
        else
        {
            Debug.LogWarning("[LobbyUI] quitToSceneName chưa được điền. " +
                             "Điền tên scene vào Inspector hoặc bật useCanvasInsteadOfScene.");
        }
    }

    // ========================================================
    // SETTINGS
    // ========================================================
    void OnSettingsClicked()
    {
        Debug.Log($"[LobbyUI] Settings clicked. Target: '{settingsSceneName}'");

        if (useCanvasInsteadOfScene)
        {
            SwitchToCanvas(settingsCanvas);
            return;
        }

        if (!string.IsNullOrEmpty(settingsSceneName))
        {
            bool sceneExists = false;
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCountInBuildSettings; i++)
            {
                string path = UnityEngine.SceneManagement.SceneUtility.GetScenePathByBuildIndex(i);
                if (path.Contains(settingsSceneName))
                {
                    sceneExists = true;
                    break;
                }
            }

            if (sceneExists)
                UnityEngine.SceneManagement.SceneManager.LoadScene(settingsSceneName);
            else
                Debug.LogWarning($"[LobbyUI] Scene '{settingsSceneName}' không tìm thấy trong Build Settings.");
        }
        else
        {
            Debug.LogWarning("[LobbyUI] settingsSceneName chưa được điền.");
        }
    }

    // ========================================================
    // CANVAS SWITCHING (thay thế Scene nếu cần)
    // ========================================================
    void SwitchToCanvas(GameObject targetCanvas)
    {
        if (targetCanvas == null)
        {
            Debug.LogWarning("[LobbyUI] targetCanvas chưa được gán trong Inspector!");
            return;
        }

        if (lobbyCanvas != null) lobbyCanvas.SetActive(false);
        targetCanvas.SetActive(true);
        Debug.Log($"[LobbyUI] Switched to canvas: {targetCanvas.name}");
    }

    // ========================================================
    // PLAYER LIST — OFFLINE DEBUG
    // ========================================================
    public void RefreshPlayerList_Debug()
    {
        // Xóa các row cũ
        foreach (var row in playerRowObjects)
            if (row != null) Destroy(row);
        playerRowObjects.Clear();

        // Spawn row cho mỗi debug player
        foreach (var playerData in debugPlayers)
            SpawnPlayerRow(playerData.playerName, playerData.isReady, playerData.isHost);

        // Cập nhật player count text
        UpdatePlayerCount(debugPlayers.Count, maxPlayers);

        Debug.Log($"[LobbyUI] Player list refreshed. {debugPlayers.Count}/{maxPlayers} players.");
    }

    // ========================================================
    // SPAWN 1 PLAYER ROW
    // ========================================================
    void SpawnPlayerRow(string playerName, bool isReady, bool isHost)
    {
        if (playerRowPrefab == null || playerListContainer == null)
        {
            Debug.LogError("[LobbyUI] playerRowPrefab hoặc playerListContainer chưa được gán!");
            return;
        }

        GameObject row = Instantiate(playerRowPrefab, playerListContainer);
        playerRowObjects.Add(row);

        // Tìm các component con trong row
        TextMeshProUGUI nameText   = row.transform.Find("PlayerNameText")?.GetComponent<TextMeshProUGUI>();
        TextMeshProUGUI statusText = row.transform.Find("StatusText")?.GetComponent<TextMeshProUGUI>();
        Toggle readyToggle         = row.transform.Find("ReadyToggle")?.GetComponent<Toggle>();
        Image avatarImage          = row.transform.Find("Avatar")?.GetComponent<Image>();
        GameObject hostBadge       = row.transform.Find("HostBadge")?.gameObject;

        // --- Tên người chơi ---
        if (nameText != null)
            nameText.text = playerName;
        else
            Debug.LogWarning($"[LobbyUI] Không tìm thấy 'PlayerNameText' trong PlayerRow prefab.");

        // --- Trạng thái Ready ---
        if (statusText != null)
        {
            statusText.text  = isReady ? "Ready" : "Not Ready";
            statusText.color = isReady
                ? new Color(74f/255f, 222f/255f, 128f/255f)   // xanh lá
                : new Color(248f/255f, 113f/255f, 113f/255f);  // đỏ nhạt
        }

        if (readyToggle != null)
            readyToggle.isOn = isReady;

        // --- Avatar màu theo tên ---
        if (avatarImage != null)
            avatarImage.color = GetAvatarColor(playerName);

        // --- Host badge ---
        if (hostBadge != null)
            hostBadge.SetActive(isHost);

        Debug.Log($"[LobbyUI] Spawned row — Name: {playerName}, Ready: {isReady}, Host: {isHost}");
    }

    // ========================================================
    // CẬP NHẬT PLAYER COUNT TEXT
    // ========================================================
    void UpdatePlayerCount(int current, int max)
    {
        if (playerCountText != null)
            playerCountText.text = $"{current} / {max}";
    }

    // ========================================================
    // MÀU AVATAR THEO TÊN
    // ========================================================
    Color GetAvatarColor(string playerName)
    {
        Color[] colors = {
            new Color(59f/255f,  130f/255f, 246f/255f), // xanh dương
            new Color(139f/255f, 92f/255f,  246f/255f), // tím
            new Color(34f/255f,  197f/255f, 94f/255f),  // xanh lá
            new Color(249f/255f, 115f/255f, 22f/255f),  // cam
            new Color(236f/255f, 72f/255f,  153f/255f), // hồng
        };
        int index = Mathf.Abs(playerName.GetHashCode()) % colors.Length;
        return colors[index];
    }

    // ========================================================
    // ONLINE — uncomment khi tích hợp Photon PUN2
    // ========================================================
    // public override void OnPlayerEnteredRoom(Photon.Realtime.Player newPlayer)
    // {
    //     Debug.Log($"[LobbyUI] {newPlayer.NickName} joined.");
    //     RefreshPlayerList_Online();
    // }
    //
    // public override void OnPlayerLeftRoom(Photon.Realtime.Player otherPlayer)
    // {
    //     Debug.Log($"[LobbyUI] {otherPlayer.NickName} left.");
    //     RefreshPlayerList_Online();
    // }
    //
    // void RefreshPlayerList_Online()
    // {
    //     foreach (var row in playerRowObjects) Destroy(row);
    //     playerRowObjects.Clear();
    //
    //     foreach (var player in PhotonNetwork.PlayerList)
    //     {
    //         bool ready = player.CustomProperties.ContainsKey("IsReady")
    //                      && (bool)player.CustomProperties["IsReady"];
    //         bool host  = player.IsMasterClient;
    //         SpawnPlayerRow(player.NickName, ready, host);
    //     }
    //     UpdatePlayerCount(PhotonNetwork.PlayerList.Length, PhotonNetwork.CurrentRoom.MaxPlayers);
    // }
}

// ============================================================
// Data class cho debug player list
// Hiển thị đẹp trong Inspector nhờ [System.Serializable]
// ============================================================
[System.Serializable]
public class DebugPlayerData
{
    public string playerName = "Player";
    public bool   isReady    = false;
    public bool   isHost     = false;
}