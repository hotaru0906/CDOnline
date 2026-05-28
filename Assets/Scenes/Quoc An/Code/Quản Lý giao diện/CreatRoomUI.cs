using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CreateRoomPanel : MonoBehaviour
{
    [Header("--- INPUT FIELDS ---")]
    public TMP_InputField RoomNameInput;
    public TMP_InputField MaxPlayersInput;
    public TMP_Dropdown MiniGameDropdown;

    [Header("--- BUTTONS ---")]
    public Button createButton;
    public Button backButton;

    void Start()
    {
        if (createButton != null)
            createButton.onClick.AddListener(OnClick_CreateRoom);
        else
            Debug.LogWarning("[CreateRoom] createButton chưa được gán!");

        if (backButton != null)
            backButton.onClick.AddListener(OnClick_Back);
        else
            Debug.LogWarning("[CreateRoom] backButton chưa được gán!");
    }

    void OnClick_Back()
    {
        if (UIManager.Instance != null)
            UIManager.Instance.NavigateBack();
    }

    void OnClick_CreateRoom()
    {
        // ── Lấy dữ liệu ──────────────────────────────
        string roomName = RoomNameInput != null ? RoomNameInput.text.Trim() : "";
        string miniGame = MiniGameDropdown != null
            ? MiniGameDropdown.options[MiniGameDropdown.value].text
            : "None";

        // ── Validate ──────────────────────────────────
        if (string.IsNullOrEmpty(roomName))
        {
            Debug.LogWarning("[CreateRoom] Tên phòng không được để trống!");
            return;
        }

        if (!int.TryParse(MaxPlayersInput.text, out int maxPlayers) || maxPlayers <= 0)
        {
            Debug.LogWarning("[CreateRoom] Số người chơi không hợp lệ!");
            return;
        }

        Debug.Log($"[CreateRoom] Tạo phòng → Name: {roomName} | Max: {maxPlayers} | Game: {miniGame}");

        // ── Gửi data sang Lobby ───────────────────────
        if (LobbyUIManager.Instance != null)
            LobbyUIManager.Instance.SetupLobby(roomName, maxPlayers, miniGame);
        else
            Debug.LogWarning("[CreateRoom] LobbyUIManager.Instance là null!");

        // ── Chuyển sang Panel Lobby ───────────────────
        if (UIManager.Instance != null)
            UIManager.Instance.NavigateTo(UIManager.Instance.UILobby);
        else
            Debug.LogWarning("[CreateRoom] UIManager.Instance là null!");
    }
}