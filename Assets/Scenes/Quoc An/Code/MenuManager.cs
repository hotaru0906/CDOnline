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
    if (settingsManager != null)
    {
        // Ẩn màn hình hiện tại trước khi mở Setting
        OnSettingsOpened();
        settingsManager.OpenSettings();
    }
}

    /// <summary>
    /// ✅ Dùng CanvasGroup thay vì SetActive
    /// </summary>
    public void OnSettingsOpened()
    {
        if (_currentScreen != null)
        {
            // ✅ Nếu là MainMenu → dùng CanvasGroup
            if (_currentScreen == canvasMainMenu && mainMenuCanvasGroup != null)
            {
                mainMenuCanvasGroup.alpha = 0f;
                mainMenuCanvasGroup.interactable = false;
                mainMenuCanvasGroup.blocksRaycasts = false;
            }
            else
            {
                _currentScreen.SetActive(false); // Fallback cho screen khác
            }
        }

        Debug.Log("🔧 Settings opened - Menu hidden");
    }

    /// <summary>
    /// ✅ Dùng CanvasGroup thay vì SetActive
    /// </summary>
    public void OnSettingsClosed()
    {
        if (_currentScreen != null)
        {
            // ✅ Nếu là MainMenu → dùng CanvasGroup
            if (_currentScreen == canvasMainMenu && mainMenuCanvasGroup != null)
            {
                mainMenuCanvasGroup.alpha = 1f;
                mainMenuCanvasGroup.interactable = true;
                mainMenuCanvasGroup.blocksRaycasts = true;
            }
            else
            {
                _currentScreen.SetActive(true); // Fallback cho screen khác
            }
        }

        Debug.Log("🏠 Settings closed - Menu restored");
    }

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
        if (_screenHistory.Count == 0) return;

        if (_currentScreen == canvasItemUI && customizationManager != null)
            customizationManager.Deactivate();

        _currentScreen.SetActive(false);
        _currentScreen = _screenHistory.Pop();
        _currentScreen.SetActive(true);
    }

    private void SwitchScreen(GameObject targetScreen)
    {
        if (_currentScreen != null)
        {
            _screenHistory.Push(_currentScreen);
            _currentScreen.SetActive(false);
        }

        _currentScreen = targetScreen;
        _currentScreen.SetActive(true);
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

        lobbyRunner.CreateSession(roomName);
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