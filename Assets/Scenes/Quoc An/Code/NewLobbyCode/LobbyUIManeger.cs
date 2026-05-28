using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class LobbyUIManager : MonoBehaviour
{
    public static LobbyUIManager Instance;

    // ========================================================
    // INSPECTOR REFERENCES
    // ========================================================

    [Header("--- TOP BAR ---")]
    [Tooltip("Kéo QuitRoomButton vào đây")]
    public Button quitRoomButton;

    [Tooltip("Kéo SettingsButton vào đây")]
    public Button settingsButton;

    [Header("--- ROOM INFO ---")]
    [Tooltip("Kéo Text hiển thị tên phòng vào đây")]
    public TextMeshProUGUI roomNameTitle;


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
    [Tooltip("Danh sách player giả để test offline")]
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
    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        SetupButtons();
        RefreshPlayerList_Debug();
        Debug.Log("[LobbyUI] LobbyUIManager initialized.");
    }

    // ========================================================
    // NHẬN DỮ LIỆU TỪ CREATE ROOM
    // ========================================================
    public void SetupLobby(string roomName, int playerMax, string miniGame)
    {
        // Hiển thị tên phòng
        if (roomNameTitle != null)
            roomNameTitle.text = roomName;
        else
            Debug.LogWarning("[LobbyUI] roomNameTitle chưa được gán!");


        // Cập nhật maxPlayers cho player count
        maxPlayers = playerMax;
        UpdatePlayerCount(debugPlayers.Count, maxPlayers);

        Debug.Log($"[LobbyUI] Setup — Room: {roomName}, Max: {playerMax}, Game: {miniGame}");
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
    // QUIT ROOM → Về PlayOnline qua UIManager
    // ========================================================
    void OnQuitRoomClicked()
    {
        Debug.Log("[LobbyUI] Quit Room clicked.");

        // --- ONLINE (Photon PUN2) --- uncomment khi tích hợp
        // PhotonNetwork.LeaveRoom();

        // Dùng UIManager để về PlayOnline
        if (UIManager.Instance != null)
            UIManager.Instance.QuitToPlayOnline();
        else
            Debug.LogWarning("[LobbyUI] UIManager.Instance là null!");
    }

    // ========================================================
    // SETTINGS → Dùng UIManager Navigate
    // ========================================================
    void OnSettingsClicked()
    {
        Debug.Log("[LobbyUI] Settings clicked.");

        if (UIManager.Instance != null)
            UIManager.Instance.NavigateTo(UIManager.Instance.UISetting);
        else
            Debug.LogWarning("[LobbyUI] UIManager.Instance là null!");
    }

    // ========================================================
    // PLAYER LIST — OFFLINE DEBUG
    // ========================================================
    public void RefreshPlayerList_Debug()
    {
        foreach (var row in playerRowObjects)
            if (row != null) Destroy(row);
        playerRowObjects.Clear();

        foreach (var playerData in debugPlayers)
            SpawnPlayerRow(playerData.playerName, playerData.isReady, playerData.isHost);

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

        TextMeshProUGUI nameText   = row.transform.Find("PlayerNameText")?.GetComponent<TextMeshProUGUI>();
        TextMeshProUGUI statusText = row.transform.Find("StatusText")?.GetComponent<TextMeshProUGUI>();
        Toggle readyToggle         = row.transform.Find("ReadyToggle")?.GetComponent<Toggle>();
        Image avatarImage          = row.transform.Find("Avatar")?.GetComponent<Image>();
        GameObject hostBadge       = row.transform.Find("HostBadge")?.gameObject;

        if (nameText != null)
            nameText.text = playerName;
        else
            Debug.LogWarning("[LobbyUI] Không tìm thấy 'PlayerNameText' trong PlayerRow prefab.");

        if (statusText != null)
        {
            statusText.text  = isReady ? "Ready" : "Not Ready";
            statusText.color = isReady
                ? new Color(74f/255f,  222f/255f, 128f/255f)
                : new Color(248f/255f, 113f/255f, 113f/255f);
        }

        if (readyToggle != null)
            readyToggle.isOn = isReady;

        if (avatarImage != null)
            avatarImage.color = GetAvatarColor(playerName);

        if (hostBadge != null)
            hostBadge.SetActive(isHost);

        Debug.Log($"[LobbyUI] Spawned row — Name: {playerName}, Ready: {isReady}, Host: {isHost}");
    }

    // ========================================================
    // CẬP NHẬT PLAYER COUNT
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
            new Color(59f/255f,  130f/255f, 246f/255f),
            new Color(139f/255f, 92f/255f,  246f/255f),
            new Color(34f/255f,  197f/255f, 94f/255f),
            new Color(249f/255f, 115f/255f, 22f/255f),
            new Color(236f/255f, 72f/255f,  153f/255f),
        };
        int index = Mathf.Abs(playerName.GetHashCode()) % colors.Length;
        return colors[index];
    }

    // ========================================================
    // ONLINE — uncomment khi tích hợp Photon PUN2
    // ========================================================
    // public override void OnPlayerEnteredRoom(Photon.Realtime.Player newPlayer) { ... }
    // public override void OnPlayerLeftRoom(Photon.Realtime.Player otherPlayer) { ... }
}

// ============================================================
// Data class cho debug player list
// ============================================================
[System.Serializable]
public class DebugPlayerData
{
    public string playerName = "Player";
    public bool   isReady    = false;
    public bool   isHost     = false;
}