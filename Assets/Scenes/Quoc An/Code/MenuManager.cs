using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using Fusion;
using TMPro;

public class MenuManager : MonoBehaviour
{
    [Header("Các Canvas")]
    [SerializeField] private GameObject canvasMainMenu;
    [SerializeField] private CanvasGroup mainMenuCanvasGroup;
    [SerializeField] private GameObject canvasPlayOnline;
    [SerializeField] private GameObject canvasFindLobby;
    [SerializeField] private GameObject canvasCreateRoom;
    [SerializeField] private GameObject canvasItemUI;

    [Header("UI Slide Animators (ButtonGroups)")]
    [SerializeField] private UISlideAnimator mainMenuAnimator;      // BUTTONGROUP của Main Menu
    [SerializeField] private UISlideAnimator playOnlineAnimator;    // BUTTONGROUP của Play Online
    [SerializeField] private UISlideAnimator findLobbyAnimator;     // BUTTONGROUP của Find Lobby
    [SerializeField] private UISlideAnimator createRoomAnimator;    // BUTTONGROUP của Create Room
    [SerializeField] private UISlideAnimator itemUIAnimator;        // BUTTONGROUP của Item UI

    [Header("Animation Settings")]
    [SerializeField] private float slideAnimationDuration = 0.5f;

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
    private UISlideAnimator _currentAnimator;
    private readonly Stack<GameObject> _screenHistory = new();
    private readonly Stack<UISlideAnimator> _animatorHistory = new();
    private readonly List<RoomItems> _roomItems = new();

    private enum SlideDirection { Forward, Backward }

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

        // ✅ Set hidden position cho các animator trước khi bắt đầu
        if (playOnlineAnimator != null)
            playOnlineAnimator.SetHiddenPositionImmediate();
        if (findLobbyAnimator != null)
            findLobbyAnimator.SetHiddenPositionImmediate();
        if (createRoomAnimator != null)
            createRoomAnimator.SetHiddenPositionImmediate();
        if (itemUIAnimator != null)
            itemUIAnimator.SetHiddenPositionImmediate();

        _currentScreen = canvasMainMenu;
        _currentAnimator = mainMenuAnimator;
        canvasMainMenu.SetActive(true);
        
        // ✅ Đảm bảo main menu ở vị trí visible
        if (mainMenuAnimator != null)
            mainMenuAnimator.SetVisiblePositionImmediate();
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
    public void ShowPlayOnline() => SlideScreen(canvasPlayOnline, playOnlineAnimator, SlideDirection.Forward);
    public void ShowFindLobby() => SlideScreen(canvasFindLobby, findLobbyAnimator, SlideDirection.Forward);
    public void ShowCreateRoom() => SlideScreen(canvasCreateRoom, createRoomAnimator, SlideDirection.Forward);

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
        SlideScreen(canvasItemUI, itemUIAnimator, SlideDirection.Forward);
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

        GameObject previousScreen = _screenHistory.Pop();
        UISlideAnimator previousAnimator = _animatorHistory.Pop();
        
        // ✅ Không push vào history lần 2 (vì đã pop rồi)
        SlideScreen(previousScreen, previousAnimator, SlideDirection.Backward, addToHistory: false);
    }

    /// <summary>
    /// Slide từ screen hiện tại sang screen mới với animation
    /// </summary>
    private void SlideScreen(GameObject targetScreen, UISlideAnimator targetAnimator, SlideDirection direction, bool addToHistory = true)
    {
        if (targetScreen == null || targetScreen == _currentScreen) return;

        // ✅ Chỉ lưu vào history nếu addToHistory = true
        if (addToHistory && _currentScreen != null)
        {
            _screenHistory.Push(_currentScreen);
            _animatorHistory.Push(_currentAnimator);
        }

        StartCoroutine(AnimateScreenTransition(_currentScreen, _currentAnimator, targetScreen, targetAnimator, direction));
        
        _currentScreen = targetScreen;
        _currentAnimator = targetAnimator;
    }

    /// <summary>
    /// Animate transition giữa 2 screens
    /// </summary>
    private IEnumerator AnimateScreenTransition(
        GameObject fromScreen, UISlideAnimator fromAnimator,
        GameObject toScreen, UISlideAnimator toAnimator,
        SlideDirection direction)
    {
        // ✅ Bật canvas mới
        toScreen.SetActive(true);

        // ✅ Animate cả 2 cùng lúc
        if (fromAnimator != null)
        {
            fromAnimator.HideAsync(); // Hide async - không chờ
        }

        if (toAnimator != null)
        {
            yield return toAnimator.ShowAsync(); // Show và chờ hoàn thành
        }

        // ✅ Tắt canvas cũ sau khi animation xong
        if (fromScreen != null)
            fromScreen.SetActive(false);
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