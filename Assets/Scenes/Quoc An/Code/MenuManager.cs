using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Fusion;
using TMPro;
using System.Collections;

public class MenuManager : MonoBehaviour
{
    [Header("Các Canvas")]
    [SerializeField] private GameObject canvasMainMenu;
    [SerializeField] private GameObject canvasPlayOnline;
    [SerializeField] private GameObject canvasFindLobby;
    [SerializeField] private GameObject canvasCreateRoom;
    [SerializeField] private GameObject canvasItemUI;

    [Header("Loading Screen")]
    [SerializeField] private GameObject loadingScreen;        // ← KÉO "Loading canva" vào đây

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

    private LoadingScreenManager _loadingManager; // Cache để gọi StartFakeLoading

    private void Start()
    {
        InitializeScreens();
        SetupButtons();
        SetupCustomization();

        if (loadingScreen != null)
        {
            _loadingManager = loadingScreen.GetComponent<LoadingScreenManager>();
            loadingScreen.SetActive(false);
        }
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

        StartCoroutine(ShowLoadingThenSwitch(targetScreen));
    }

    private IEnumerator ShowLoadingThenSwitch(GameObject targetScreen)
    {
        if (loadingScreen != null)
            loadingScreen.SetActive(true);

        // Dùng thời gian mặc định từ LoadingScreenManager
        yield return new WaitForSeconds(10f);

        _currentScreen = targetScreen;
        _currentScreen.SetActive(true);

        if (loadingScreen != null)
            loadingScreen.SetActive(false);
    }
    #endregion

    #region Lobby Functions
    private void CreateRoom()
    {
        if (lobbyRunner == null)
        {
            Debug.LogError("[MenuManager] LobbyRunner chưa được gán.");
            return;
        }

        var roomName = roomNameInput?.text;
        if (string.IsNullOrWhiteSpace(roomName))
        {
            Debug.LogWarning("[MenuManager] Tên phòng không được để trống.");
            return;
        }

        // === LOADING CHO CREATE ROOM ===
        if (loadingScreen != null && _loadingManager != null)
        {
            loadingScreen.SetActive(true);
            // Dùng thời gian dài hơn một chút cho network
            _loadingManager.StartFakeLoading(2.5f);   // ← Bạn có thể chỉnh số này
        }

        lobbyRunner.CreateSession(roomName);

        // Nếu bạn có callback từ LobbyRunner (ví dụ OnSessionCreated), 
        // hãy gọi loadingScreen.SetActive(false) ở đó.
        // Hiện tại dùng thời gian cố định 2.5 giây
        StartCoroutine(HideLoadingAfterCreateRoom());
    }

    private IEnumerator HideLoadingAfterCreateRoom()
    {
        yield return new WaitForSeconds(2.5f);   // ← Khớp với thời gian ở trên
        if (loadingScreen != null)
            loadingScreen.SetActive(false);
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