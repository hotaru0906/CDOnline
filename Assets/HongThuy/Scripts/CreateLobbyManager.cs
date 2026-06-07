using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class CreateLobbyManager : MonoBehaviour
{
    [Header("UI Elements (TextMeshPro)")]
    [SerializeField] private TMP_InputField lobbyNameInput;       
    [SerializeField] private TMP_Dropdown playerLimitDropdown;    
    [SerializeField] private TMP_Dropdown miniGameLimitDropdown;  
    
    [Header("Buttons")]
    [SerializeField] private Button backButton;
    [SerializeField] private Button createRoomButton;

    // Các biến lưu trữ giá trị thực tế để dùng sau này
    private string lobbyName = "";
    private int playerLimit = 2;       
    private int miniGameLimit = 5;     

    void Start()
    {
        // 1. Khởi tạo dữ liệu sạch (Chỉ chứa các mốc số cần chọn)
        SetupPlayerLimitDropdown();
        SetupMiniGameDropdown();

        // 2. Gán giá trị mặc định hiển thị lúc vừa vào game
        // (Không dùng lệnh ép chữ caption nữa để Unity tự lấy dữ liệu index 0 hiển thị lên)
        if (playerLimitDropdown != null)
        {
            playerLimitDropdown.value = 0; 
            playerLimitDropdown.RefreshShownValue(); // Cập nhật giao diện lập tức thành "2 Players"
        }

        if (miniGameLimitDropdown != null)
        {
            miniGameLimitDropdown.value = 0;
            miniGameLimitDropdown.RefreshShownValue(); // Cập nhật giao diện lập tức thành "5 Rounds"
        }

        // 3. Lắng nghe sự kiện thay đổi từ UI
        if (lobbyNameInput != null) lobbyNameInput.onValueChanged.AddListener(OnLobbyNameChanged);
        if (playerLimitDropdown != null) playerLimitDropdown.onValueChanged.AddListener(OnPlayerLimitChanged);
        if (miniGameLimitDropdown != null) miniGameLimitDropdown.onValueChanged.AddListener(OnMiniGameLimitChanged);

        // 4. Lắng nghe sự kiện click nút bấm
        if (backButton != null) backButton.onClick.AddListener(HandleBack);
        if (createRoomButton != null) createRoomButton.onClick.AddListener(HandleCreateRoom);
    }

    // Thiết lập danh sách chọn số người chơi (2 - 4)
    private void SetupPlayerLimitDropdown()
    {
        if (playerLimitDropdown == null) return;

        playerLimitDropdown.ClearOptions();
        List<string> options = new List<string>();
        
        for (int i = 2; i <= 4; i++)
        {
            options.Add(i + " Players");
        }
        playerLimitDropdown.AddOptions(options);
        playerLimit = 2; 
    }

    // Thiết lập danh sách chọn số màn chơi (5 - 10)
    private void SetupMiniGameDropdown()
    {
        if (miniGameLimitDropdown == null) return;

        miniGameLimitDropdown.ClearOptions();
        List<string> options = new List<string>();
        
        for (int i = 5; i <= 10; i++)
        {
            options.Add(i + " Rounds");
        }
        miniGameLimitDropdown.AddOptions(options);
        miniGameLimit = 5; 
    }

    private void OnLobbyNameChanged(string value)
    {
        lobbyName = value;
    }

    private void OnPlayerLimitChanged(int index)
    {
        playerLimit = index + 2; // index 0 = 2 Players, index 1 = 3 Players...
        Debug.Log("Số lượng người chơi đã chọn: " + playerLimit);
    }

    private void OnMiniGameLimitChanged(int index)
    {
        miniGameLimit = index + 5; // index 0 = 5 Rounds, index 1 = 6 Rounds...
        Debug.Log("Số màn chơi đã chọn: " + miniGameLimit);
    }

    private void HandleCreateRoom()
    {
        if (string.IsNullOrEmpty(lobbyName.Trim()))
        {
            Debug.LogWarning("Vui lòng nhập tên phòng trước khi tạo!");
            return;
        }

        Debug.Log($"--- [XÁC NHẬN TẠO PHÒNG] ---");
        Debug.Log($"-> Tên phòng: {lobbyName}");
        Debug.Log($"-> Số người chơi: {playerLimit}");
        Debug.Log($"-> Số lượng màn chơi: {miniGameLimit}");
    }

    private void HandleBack()
    {
        Debug.Log("Quay lại Menu chính.");
    }
}