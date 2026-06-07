using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

// ============================================================
// LobbyUIManager — quản lý toàn bộ UI Panel Lobby
// Tất cả nằm trong 1 Canvas, dùng CanvasGroup
// OFFLINE: test qua Inspector debugPlayers
// ONLINE:  Uncomment phần Fusion khi sẵn sàng
// ============================================================
public class LobbyUIManager : MonoBehaviour
{
    public static LobbyUIManager Instance;

    // ── Top Bar ──────────────────────────────────────────────
    [Header("--- TOP BAR ---")]
    public Button settingsButton;
    public Button quitRoomButton;
    public TextMeshProUGUI roomNameText;

    // ── Player List ──────────────────────────────────────────
    [Header("--- PLAYER LIST ---")]
    public Transform playerListContainer;
    public GameObject playerRowPrefab;
    public TextMeshProUGUI playerCountText;

    // ── Action Buttons ────────────────────────────────────────
    [Header("--- ACTION BUTTONS ---")]
    [Tooltip("Nút Force Start — chỉ hiện với Host, luôn bấm được")]
    public Button forceStartButton;

    [Tooltip("Nút Start Game — sáng khi tất cả người chơi đã Ready")]
    public Button startGameButton;

    [Tooltip("Nút Ready — chỉ hiện với non-host")]
    public Button readyButton;
    public TextMeshProUGUI readyButtonText;

    // ── Debug / Offline ───────────────────────────────────────
    [Header("--- DEBUG / OFFLINE TEST ---")]
    public bool isHostDebug = true;
    public int maxPlayersDebug = 4;

    public List<DebugPlayerData> debugPlayers = new List<DebugPlayerData>()
    {
        new DebugPlayerData { playerName = "HostPlayer", isReady = true,  isHost = true  },
        new DebugPlayerData { playerName = "Alice",      isReady = false, isHost = false },
    };

    // ── Runtime ───────────────────────────────────────────────
    private List<GameObject> spawnedRows = new List<GameObject>();
    private bool localPlayerReady = false;
    private bool isHost = false;
    private int maxPlayers = 4;

    // ─────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        isHost     = isHostDebug;
        maxPlayers = maxPlayersDebug;

        SetupButtons();
        RefreshPlayerList();
        UpdateActionButtons();
    }

    // ── Setup từ CreateRoom ───────────────────────────────────
    public void SetupLobby(string roomName, int playerMax, string miniGame)
    {
        if (roomNameText != null)
            roomNameText.text = $"Game Lobby — {roomName}";

        maxPlayers = playerMax;
        UpdatePlayerCount(debugPlayers.Count, maxPlayers);
        Debug.Log($"[LobbyUI] Setup — Room: {roomName}, Max: {playerMax}, Game: {miniGame}");
    }

    // ── Button Setup ──────────────────────────────────────────
    void SetupButtons()
    {
        if (settingsButton != null)
            settingsButton.onClick.AddListener(OnSettings);
        else Debug.LogWarning("[LobbyUI] settingsButton chưa gán!");

        if (quitRoomButton != null)
            quitRoomButton.onClick.AddListener(OnQuitRoom);
        else Debug.LogWarning("[LobbyUI] quitRoomButton chưa gán!");

        if (forceStartButton != null)
            forceStartButton.onClick.AddListener(OnForceStart);
        else Debug.LogWarning("[LobbyUI] forceStartButton chưa gán!");

        if (startGameButton != null)
            startGameButton.onClick.AddListener(OnStartGame);
        else Debug.LogWarning("[LobbyUI] startGameButton chưa gán!");

        if (readyButton != null)
            readyButton.onClick.AddListener(OnReady);
        else Debug.LogWarning("[LobbyUI] readyButton chưa gán!");
    }

    // ── Action Buttons Logic ──────────────────────────────────

    void OnSettings()
    {
        if (UIManager.Instance != null)
            UIManager.Instance.NavigateTo(UIManager.Instance.UISetting);
    }

    void OnQuitRoom()
    {
        // FUSION STUB: uncomment khi online
        // await runner.Disconnect();
        if (UIManager.Instance != null)
            UIManager.Instance.NavigateTo(UIManager.Instance.UIFindLobby);
    }

    void OnForceStart()
    {
        Debug.Log("[LobbyUI] Force Start! Host bắt đầu game bất kể trạng thái ready.");
        // FUSION STUB:
        // runner.LoadScene(...)
    }

    void OnStartGame()
    {
        if (!AllPlayersReady()) return;
        Debug.Log("[LobbyUI] Start Game! Tất cả đã sẵn sàng.");
        // FUSION STUB:
        // runner.LoadScene(...)
    }

    void OnReady()
    {
        localPlayerReady = !localPlayerReady;
        UpdateReadyButton();
        UpdateStartButton();

        // Cập nhật debug data để hiển thị đúng trên list
        foreach (var p in debugPlayers)
        {
            if (!p.isHost)
            {
                p.isReady = localPlayerReady;
                break;
            }
        }
        RefreshPlayerList();

        // FUSION STUB:
        // RPC_SetReady(localPlayerReady);
        Debug.Log($"[LobbyUI] Local player ready: {localPlayerReady}");
    }

    // ── Cập nhật trạng thái nút ───────────────────────────────

    void UpdateActionButtons()
    {
        // Force Start: chỉ hiện với host
        if (forceStartButton != null)
            forceStartButton.gameObject.SetActive(isHost);

        // Start Game: chỉ hiện với host, sáng/tối theo all ready
        if (startGameButton != null)
        {
            startGameButton.gameObject.SetActive(isHost);
            UpdateStartButton();
        }

        // Ready: chỉ hiện với non-host
        if (readyButton != null)
        {
            readyButton.gameObject.SetActive(!isHost);
            UpdateReadyButton();
        }
    }

    void UpdateStartButton()
    {
        if (startGameButton == null) return;
        bool allReady = AllPlayersReady();
        startGameButton.interactable = allReady;

        // Màu sáng/tối
        var colors = startGameButton.colors;
        colors.normalColor      = allReady ? new Color(0.2f, 0.8f, 0.2f) : new Color(0.4f, 0.4f, 0.4f);
        colors.disabledColor    = new Color(0.35f, 0.35f, 0.35f);
        startGameButton.colors  = colors;
    }

    void UpdateReadyButton()
    {
        if (readyButtonText == null) return;
        readyButtonText.text = localPlayerReady ? "Not Ready" : "Ready";

        if (readyButton == null) return;
        var colors = readyButton.colors;
        colors.normalColor = localPlayerReady
            ? new Color(0.9f, 0.3f, 0.3f)   // đỏ = Not Ready
            : new Color(0.2f, 0.75f, 0.2f);  // xanh = Ready
        readyButton.colors = colors;
    }

    bool AllPlayersReady()
    {
        foreach (var p in debugPlayers)
            if (!p.isHost && !p.isReady) return false;
        return true;
        // FUSION STUB: kiểm tra NetworkDictionary trạng thái player thay vì debugPlayers
    }

    // ── Player List ───────────────────────────────────────────

    public void RefreshPlayerList()
    {
        foreach (var row in spawnedRows)
            if (row != null) Destroy(row);
        spawnedRows.Clear();

        foreach (var p in debugPlayers)
            SpawnPlayerRow(p);

        UpdatePlayerCount(debugPlayers.Count, maxPlayers);
    }

    void SpawnPlayerRow(DebugPlayerData data)
    {
        if (playerRowPrefab == null || playerListContainer == null)
        {
            Debug.LogError("[LobbyUI] Thiếu playerRowPrefab hoặc playerListContainer!");
            return;
        }

        GameObject row = Instantiate(playerRowPrefab, playerListContainer);
        spawnedRows.Add(row);

        // Avatar color
        Image avatar = row.transform.Find("Avatar")?.GetComponent<Image>();
        if (avatar != null) avatar.color = GetAvatarColor(data.playerName);

        // Tên
        TextMeshProUGUI nameText = row.transform.Find("PlayerNameText")?.GetComponent<TextMeshProUGUI>();
        if (nameText != null) nameText.text = data.isHost ? $"{data.playerName} (Host)" : data.playerName;

        // Trạng thái
        TextMeshProUGUI statusText = row.transform.Find("StatusText")?.GetComponent<TextMeshProUGUI>();
        if (statusText != null)
        {
            statusText.text  = data.isReady ? "Ready" : "Not Ready";
            statusText.color = data.isReady
                ? new Color(0.29f, 0.87f, 0.5f)
                : new Color(0.97f, 0.44f, 0.44f);
        }

        // Host badge
        GameObject hostBadge = row.transform.Find("HostBadge")?.gameObject;
        if (hostBadge != null) hostBadge.SetActive(data.isHost);
    }

    void UpdatePlayerCount(int current, int max)
    {
        if (playerCountText != null)
            playerCountText.text = $"{current} / {max}";
    }

    Color GetAvatarColor(string playerName)
    {
        Color[] palette = {
            new Color(0.23f, 0.51f, 0.96f),
            new Color(0.55f, 0.36f, 0.96f),
            new Color(0.13f, 0.77f, 0.37f),
            new Color(0.98f, 0.45f, 0.09f),
            new Color(0.93f, 0.28f, 0.60f),
        };
        return palette[Mathf.Abs(playerName.GetHashCode()) % palette.Length];
    }

    // ── FUSION STUB ───────────────────────────────────────────
    // [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    // void RPC_SetReady(bool ready) { ... }
    //
    // public override void OnPlayerJoined(PlayerRef player) { RefreshPlayerList(); }
    // public override void OnPlayerLeft(PlayerRef player)   { RefreshPlayerList(); }
}

[System.Serializable]
public class DebugPlayerData
{
    public string playerName = "Player";
    public bool   isReady    = false;
    public bool   isHost     = false;
}