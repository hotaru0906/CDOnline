using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Fusion;

public class RoomUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_Text roomNameText;
    [SerializeField] private TMP_Text playerCountText;

    [SerializeField] private Button leaveRoomButton;
    [SerializeField] private Button readyGameButton;
    [SerializeField] private Button startGameButton;
    [SerializeField] private Button forceStartButton;

    [Header("Player Data")]
    [SerializeField] private PlayerInfoUI playerInfoUI;
    [SerializeField] private PlayerInfoItemUI playerInfoItemPrefab;

    [Header("Settings")]
    [SerializeField] private int defaultMaxPlayers = 4;

    private string _roomName;
    private int _currentPlayers;
    private int _maxPlayers;

    private NetworkRunner _runner;

    private void Awake()
    {
        _runner = FindAnyObjectByType<NetworkRunner>();
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        leaveRoomButton.onClick.AddListener(OnLeaveRoomClicked);
        readyGameButton.onClick.AddListener(OnReadyGameClicked);
        startGameButton.onClick.AddListener(OnStartGameClicked);
        forceStartButton.onClick.AddListener(OnForceStartClicked);

        CursorManager.Instance.SetUIMode();

        InitializeRoomInfo();
        UpdateButtons();
    }

    private void Update()
    {
        // Try to find runner if not set yet
        if (_runner == null)
        {
            _runner = FindAnyObjectByType<NetworkRunner>();
            if (_runner != null && _runner.IsRunning)
            {
                InitializeRoomInfo();
            }
        }

        if (_runner != null && _runner.IsRunning)
        {
            UpdatePlayerCount();
        }

        // Update button visibility and interactability
        UpdateButtons();

        if (GameManager.Instance == null) return;

        if (GameManager.Instance.IsHost)
        {
            bool canStart = GameManager.Instance.AreAllPlayersReady();
            startGameButton.interactable = canStart;
        }
    }

    private void InitializeRoomInfo()
    {
        if (_runner == null || !_runner.IsRunning)
        {
            _maxPlayers = defaultMaxPlayers;
            _roomName = "Room";
        }
        else
        {
            // Get session info from runner
            var sessionInfo = _runner.SessionInfo;
            if (sessionInfo != null)
            {
                _roomName = sessionInfo.Name;
                _maxPlayers = defaultMaxPlayers;
            }
            else
            {
                _roomName = "Room";
                _maxPlayers = defaultMaxPlayers;
            }
        }

        var lobbyRunner = FindAnyObjectByType<LobbyRunner>();
        if (lobbyRunner != null)
        {
            _maxPlayers = lobbyRunner.GetDisplayMaxPlayers(_maxPlayers);
        }

        if (roomNameText != null)
            roomNameText.text = _roomName;

        UpdatePlayerCount();
    }

    public void SetUp(string roomName, int currentPlayers, int maxPlayers)
    {
        _roomName = roomName;
        _currentPlayers = currentPlayers;
        _maxPlayers = maxPlayers;

        roomNameText.text = roomName;
        playerCountText.text = $"{currentPlayers}/{maxPlayers}";
    }

    private void UpdatePlayerCount()
    {
        int count = 0;

        foreach (var player in _runner.ActivePlayers)
        {
            count++;
        }

        _currentPlayers = count;
        playerCountText.text = $"{_currentPlayers}/{_maxPlayers}";
    }

    private void UpdateButtons()
    {
        bool isHost = GameManager.Instance != null && GameManager.Instance.IsHost;

        startGameButton.gameObject.SetActive(isHost);
        readyGameButton.gameObject.SetActive(!isHost);
        forceStartButton.gameObject.SetActive(isHost);
    }

    private async void OnLeaveRoomClicked()
    {
        if (_runner == null) return;

        Debug.Log("[RoomUI] Leaving room...");

        // Ensure input is re-enabled
        if (PlayerInputHandler.Instance != null)
        {
            PlayerInputHandler.Instance.InputEnabled = true;
        }

        await _runner.Shutdown();
        UnityEngine.SceneManagement.SceneManager.LoadScene("TestMenu");
    }

    private void OnReadyGameClicked()
    {
        Debug.Log("[RoomUI] Player ready clicked");

        var localPlayer = PlayerNetworkData.Local;

        if (localPlayer != null)
        {
            localPlayer.ToggleReady();
        }
    }

    private void OnStartGameClicked()
    {
        if (GameManager.Instance == null) return;

        if (!GameManager.Instance.IsHost)
        {
            Debug.LogWarning("Only host can start game");
            return;
        }

        Debug.Log("[RoomUI] Host starting match");

        GameManager.Instance.StartMatch();
    }

    private void OnForceStartClicked()
    {
        if (GameManager.Instance == null)
        {
            Debug.LogWarning("[RoomUI] ForceStart: GameManager.Instance is null");
            return;
        }

        Debug.Log($"[RoomUI] ForceStart clicked. IsHost:{GameManager.Instance.IsHost}, RunnerExists:{_runner != null}, RunnerIsRunning:{(_runner != null ? _runner.IsRunning.ToString() : "N/A")}");

        if (!GameManager.Instance.IsHost)
        {
            Debug.LogWarning("[RoomUI] Only host can force start");
            return;
        }

        Debug.Log("[RoomUI] Host force starting");

        GameManager.Instance.StartMatch();
    }
}