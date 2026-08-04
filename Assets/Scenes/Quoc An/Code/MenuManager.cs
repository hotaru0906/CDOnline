using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Fusion;
using TMPro;

public class MenuManager : MonoBehaviour
{
    [Header("Các Canvas")]
    [SerializeField] private GameObject canvasMainMenu;
    [SerializeField] private CanvasGroup mainMenuCanvasGroup; // ✅ THÊM
    [SerializeField] private GameObject canvasPlayOnline;
    [SerializeField] private GameObject canvasFindLobby;
    [SerializeField] private GameObject canvasCreateRoom;
    [SerializeField] private GameObject canvasItemUI;

    [Header("Settings")]
    [SerializeField] private SettingsManager settingsManager;

    [Header("Lobby References")]
    [SerializeField] private LobbyRunner lobbyRunner;
    [SerializeField] private TMP_InputField roomNameInput;
    [SerializeField] private LimitPlayerInput playerCountInput;
    [SerializeField] private Transform roomListParent;
    [SerializeField] private RoomItems roomListItemPrefab;
    [SerializeField] private Button createRoomButton;

    [Header("Customization")]
    [SerializeField] private CustomizationManager customizationManager;

    private GameObject _currentScreen;
    private readonly Stack<GameObject> _screenHistory = new();
    private readonly List<RoomItems> _roomItems = new();

    private void Awake()
    {
        // ✅ Tạo CanvasGroup cho MainMenu nếu chưa có
        if (mainMenuCanvasGroup == null && canvasMainMenu != null)
        {
            mainMenuCanvasGroup = canvasMainMenu.GetComponent<CanvasGroup>();
            if (mainMenuCanvasGroup == null)
                mainMenuCanvasGroup = canvasMainMenu.AddComponent<CanvasGroup>();
        }
    }

    private void Start()
    {
        InitializeScreens();
        SetupButtons();
        SetupCustomization();
    }

    private void InitializeScreens()
    {
        canvasPlayOnline.SetActive(false);
        canvasFindLobby.SetActive(false);
        canvasCreateRoom.SetActive(false);
        canvasItemUI.SetActive(false);

        _currentScreen = canvasMainMenu;
        canvasMainMenu.SetActive(true);
    }

    private void SetupButtons()
    {
        if (createRoomButton != null)
            createRoomButton.onClick.AddListener(CreateRoom);
    }

    private void SetupCustomization()
    {
        if (customizationManager != null)
        {
            customizationManager.OnBackToMenu += OnBackFromCustomization;
        }
    }

    private void OnDestroy()
    {
        if (customizationManager != null)
        {
            customizationManager.OnBackToMenu -= OnBackFromCustomization;
        }
    }

    #region Screen Navigation
    public void ShowPlayOnline() => SwitchScreen(canvasPlayOnline);
    public void ShowFindLobby() => SwitchScreen(canvasFindLobby);
    public void ShowCreateRoom() => SwitchScreen(canvasCreateRoom);

    public void ShowSettings()
    {
        if (settingsManager == null) return;

        // Chỉ ẩn menu KHI settings mở thành công
        if (settingsManager.OpenSettings())
            OnSettingsOpened();
    }

    private void SetScreenVisible(GameObject screen, bool visible)
    {
        if (screen == null) return;

        if (screen == canvasMainMenu && mainMenuCanvasGroup != null)
        {
            mainMenuCanvasGroup.alpha          = visible ? 1f : 0f;
            mainMenuCanvasGroup.interactable   = visible;
            mainMenuCanvasGroup.blocksRaycasts = visible;
            screen.SetActive(true); // luôn active, chỉ đổi alpha
        }
        else
        {
            screen.SetActive(visible);
        }
    }

    public void OnSettingsOpened() => SetScreenVisible(_currentScreen, false);
    public void OnSettingsClosed() => SetScreenVisible(_currentScreen, true);

    public void ShowItemUI()
    {
        SwitchScreen(canvasItemUI);
        if (customizationManager != null)
            customizationManager.Activate();
    }

    private void OnBackFromCustomization()
    {
        GoBack();
    }

    public void GoBack()
    {
        // Nếu Settings đang mở thì Back = đóng Settings
        if (settingsManager != null && settingsManager.IsOpen)
        {
            settingsManager.CloseSettings();
            return;
        }

        if (_screenHistory.Count == 0) return;

        if (_currentScreen == canvasItemUI && customizationManager != null)
            customizationManager.Deactivate();

        SetScreenVisible(_currentScreen, false);
        _currentScreen = _screenHistory.Pop();
        SetScreenVisible(_currentScreen, true);
    }

    private void SwitchScreen(GameObject targetScreen)
    {
        if (targetScreen == null || targetScreen == _currentScreen) return;

        if (_currentScreen != null)
        {
            _screenHistory.Push(_currentScreen);
            SetScreenVisible(_currentScreen, false);
        }

        _currentScreen = targetScreen;
        SetScreenVisible(_currentScreen, true);
    }
    #endregion

    #region Lobby Functions
    private void CreateRoom()
    {
        if (lobbyRunner == null)
        {
            Debug.LogError("[MenuManager] LobbyRunner not assigned.");
            return;
        }

        var roomName = roomNameInput?.text;
        if (string.IsNullOrWhiteSpace(roomName))
        {
            Debug.LogWarning("[MenuManager] Room name cannot be empty.");
            return;
        }

        var maxPlayers = playerCountInput != null ? playerCountInput.GetValue() : 4;
        lobbyRunner.CreateSession(roomName, maxPlayers);
    }
    #endregion

    public void UpdateRoomList(List<SessionInfo> sessions)
    {
        ClearRoomItems();

        foreach (var session in sessions)
        {
            var newItem = Instantiate(roomListItemPrefab, roomListParent);
            newItem.SetUp(session, lobbyRunner);
            newItem.gameObject.SetActive(true);
            _roomItems.Add(newItem);
        }
    }

    private void ClearRoomItems()
    {
        foreach (var item in _roomItems)
        {
            if (item != null)
                Destroy(item.gameObject);
        }
        _roomItems.Clear();
    }
}