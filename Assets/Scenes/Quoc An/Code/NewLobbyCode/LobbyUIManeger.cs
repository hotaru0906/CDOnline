using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

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

    [Tooltip("Text hiển thị số người hiện tại / giới hạn, ví dụ: 2 / 4")]
    public TextMeshProUGUI playerCountText;

    // ── Action Buttons ────────────────────────────────────────
    [Header("--- ACTION BUTTONS ---")]
    public Button forceStartButton;
    public Button startGameButton;
    public Button readyButton;
    public TextMeshProUGUI readyButtonText;

    // ── Debug / Offline ───────────────────────────────────────
    [Header("--- DEBUG / OFFLINE TEST ---")]
    [Tooltip("Bật = đang là Host, tắt = non-host")]
    public bool isHostDebug = true;

    [Tooltip("Giới hạn người chơi (nhận từ CreateRoom hoặc nhập tay để test)")]
    public int maxPlayersDebug = 4;

    public List<DebugPlayerEntry> debugPlayers = new List<DebugPlayerEntry>()
    {
        new DebugPlayerEntry { playerName = "HostPlayer", isReady = true,  isHost = true  },
        new DebugPlayerEntry { playerName = "Alice",      isReady = false, isHost = false },
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

    // ── Nhận data từ CreateRoom ───────────────────────────────
    public void SetupLobby(string roomName, int playerMax, string miniGame)
    {
        if (roomNameText != null)
            roomNameText.text = $"Game Lobby — {roomName}";

        maxPlayers = playerMax;
        UpdatePlayerCount();
        Debug.Log($"[LobbyUI] Setup — Room: {roomName}, Max: {playerMax}, Game: {miniGame}");
    }

    // ── Buttons ───────────────────────────────────────────────
    void SetupButtons()
    {
        if (settingsButton   != null) settingsButton.onClick.AddListener(OnSettings);
        if (quitRoomButton   != null) quitRoomButton.onClick.AddListener(OnQuitRoom);
        if (forceStartButton != null) forceStartButton.onClick.AddListener(OnForceStart);
        if (startGameButton  != null) startGameButton.onClick.AddListener(OnStartGame);
        if (readyButton      != null) readyButton.onClick.AddListener(OnReady);
    }

    void OnSettings()
    {
        if (UIManager.Instance != null)
            UIManager.Instance.NavigateTo(UIManager.Instance.UISetting);
    }

    void OnQuitRoom()
    {
        // FUSION STUB: await runner.Disconnect();
        if (UIManager.Instance != null)
            UIManager.Instance.NavigateTo(UIManager.Instance.UIFindLobby);
    }

    void OnForceStart()
    {
        Debug.Log("[LobbyUI] Force Start — Host bắt đầu bất kể ready.");
        // FUSION STUB: runner.LoadScene(SceneRef.FromIndex(gameSceneIndex));
    }

    void OnStartGame()
    {
        if (!AllPlayersReady()) return;
        Debug.Log("[LobbyUI] Start Game — tất cả đã ready.");
        // FUSION STUB: runner.LoadScene(SceneRef.FromIndex(gameSceneIndex));
    }

    void OnReady()
    {
        localPlayerReady = !localPlayerReady;

        // Cập nhật debug data
        foreach (var p in debugPlayers)
            if (!p.isHost) { p.isReady = localPlayerReady; break; }

        UpdateReadyButton();
        UpdateStartButton();
        RefreshPlayerList();

        // FUSION STUB: RPC_SetReady(localPlayerReady);
        Debug.Log($"[LobbyUI] Local ready: {localPlayerReady}");
    }

    // ── Action button states ──────────────────────────────────
    void UpdateActionButtons()
    {
        if (forceStartButton != null) forceStartButton.gameObject.SetActive(isHost);
        if (startGameButton  != null) startGameButton.gameObject.SetActive(isHost);
        if (readyButton      != null) readyButton.gameObject.SetActive(!isHost);
        UpdateStartButton();
        UpdateReadyButton();
    }

    void UpdateStartButton()
    {
        if (startGameButton == null) return;
        bool allReady = AllPlayersReady();
        startGameButton.interactable = allReady;
        var c = startGameButton.colors;
        c.normalColor     = allReady ? new Color(0.2f, 0.78f, 0.2f) : new Color(0.4f, 0.4f, 0.4f);
        c.disabledColor   = new Color(0.35f, 0.35f, 0.35f);
        startGameButton.colors = c;
    }

    void UpdateReadyButton()
    {
        if (readyButtonText != null)
            readyButtonText.text = localPlayerReady ? "Not Ready" : "Ready";
        if (readyButton == null) return;
        var c = readyButton.colors;
        c.normalColor = localPlayerReady
            ? new Color(0.9f, 0.3f, 0.3f)
            : new Color(0.2f, 0.75f, 0.2f);
        readyButton.colors = c;
    }

    bool AllPlayersReady()
    {
        foreach (var p in debugPlayers)
            if (!p.isHost && !p.isReady) return false;
        return true;
        // FUSION STUB: kiểm tra NetworkDictionary<PlayerRef, bool> readyStates
    }

    // ── Player List ───────────────────────────────────────────
    public void RefreshPlayerList()
    {
        foreach (var row in spawnedRows)
            if (row != null) Destroy(row);
        spawnedRows.Clear();

        foreach (var p in debugPlayers)
            SpawnRow(p);

        UpdatePlayerCount();
    }

    void SpawnRow(DebugPlayerEntry data)
    {
        if (playerRowPrefab == null || playerListContainer == null)
        {
            Debug.LogError("[LobbyUI] Thiếu playerRowPrefab hoặc playerListContainer!");
            return;
        }

        GameObject go = Instantiate(playerRowPrefab, playerListContainer);
        spawnedRows.Add(go);

        PlayerRowUI rowUI = go.GetComponent<PlayerRowUI>();
        if (rowUI != null)
        {
            rowUI.Setup(data.playerName, data.isReady, data.isHost, data.modelData);
        }
        else
        {
            // Fallback nếu chưa gắn PlayerRowUI — dùng Find như cũ
            var nameT   = go.transform.Find("PlayerNameText")?.GetComponent<TMPro.TextMeshProUGUI>();
            var statusT = go.transform.Find("StatusText")?.GetComponent<TMPro.TextMeshProUGUI>();
            var badge   = go.transform.Find("HostBadge")?.gameObject;
            var avatar  = go.transform.Find("Avatar")?.GetComponent<Image>();

            if (nameT   != null) nameT.text   = data.playerName;
            if (statusT != null) { statusT.text = data.isReady ? "Ready" : "Not Ready"; statusT.color = data.isReady ? new Color(0.29f,0.87f,0.5f) : new Color(0.97f,0.44f,0.44f); }
            if (badge   != null) badge.SetActive(data.isHost);
            if (avatar  != null)
            {
                if (data.modelData != null && data.modelData.avatarSprite != null)
                    { avatar.sprite = data.modelData.avatarSprite; avatar.color = Color.white; }
                else
                    avatar.color = GetColorFromName(data.playerName);
            }
        }
    }

    void UpdatePlayerCount()
    {
        if (playerCountText != null)
            playerCountText.text = $"{debugPlayers.Count} / {maxPlayers}";
        // FUSION STUB: playerCountText.text = $"{runner.ActivePlayers.Count()} / {maxPlayers}";
    }

    Color GetColorFromName(string name)
    {
        Color[] p = { new Color(0.23f,0.51f,0.96f), new Color(0.55f,0.36f,0.96f), new Color(0.13f,0.77f,0.37f), new Color(0.98f,0.45f,0.09f), new Color(0.93f,0.28f,0.60f) };
        return p[Mathf.Abs(name.GetHashCode()) % p.Length];
    }

    // ── FUSION STUB ───────────────────────────────────────────
    // [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    // void RPC_SetReady(bool ready) { ... }
    // public override void OnPlayerJoined(PlayerRef player) { RefreshPlayerList(); }
    // public override void OnPlayerLeft(PlayerRef player)   { RefreshPlayerList(); }
}

[System.Serializable]
public class DebugPlayerEntry
{
    public string          playerName = "Player";
    public bool            isReady    = false;
    public bool            isHost     = false;

    [Tooltip("Kéo PlayerModelData asset vào đây để hiện avatar đúng model")]
    public PlayerModelData modelData;
}