using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;  // QUAN TRỌNG: Thêm dòng này để dùng TextMeshPro

// Class lưu thông tin phòng
[System.Serializable]
public class RoomData
{
    public string roomName;
    public int playerLimit;
    public int miniGameLimit;

    public RoomData(string name, int players, int miniGames)
    {
        roomName = name;
        playerLimit = players;
        miniGameLimit = miniGames;
    }
}

public class RoomManager : MonoBehaviour
{
    public static RoomManager Instance;

    [Header("Create Room UI")]
    public TMP_InputField inputRoomName;  // Hoặc TMP_InputField nếu dùng TMP
    public TMP_Dropdown dropdownPlayerLimit;  // THAY ĐỔI: Dùng TMP_Dropdown
    public TMP_Dropdown dropdownMiniGameLimit;  // THAY ĐỔI: Dùng TMP_Dropdown

    [Header("Find Lobby UI")]
    public Transform roomListContent;
    public GameObject roomItemPrefab;

    [Header("Notification")]
    public Text notificationText;  // Hoặc TextMeshProUGUI

    // Danh sách phòng đã tạo
    private List<RoomData> roomList = new List<RoomData>();

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        // Setup giá trị mặc định cho dropdown
        SetupDropdowns();
    }

    // Setup dropdown với giá trị mặc định
    void SetupDropdowns()
    {
        // Set giá trị mặc định cho Player Limit (index 0 = 2 người)
        if (dropdownPlayerLimit != null)
        {
            dropdownPlayerLimit.value = 0;  // Chọn option đầu tiên
            dropdownPlayerLimit.RefreshShownValue();
        }

        // Set giá trị mặc định cho MiniGame Limit (index 0 = 5 games)
        if (dropdownMiniGameLimit != null)
        {
            dropdownMiniGameLimit.value = 0;  // Chọn option đầu tiên
            dropdownMiniGameLimit.RefreshShownValue();
        }
    }

    // Lấy số người chơi từ dropdown (index + 2)
    int GetPlayerLimitFromDropdown()
    {
        // Index 0 = 2 người, Index 1 = 3 người, Index 2 = 4 người
        return dropdownPlayerLimit.value + 2;
    }

    // Lấy số mini game từ dropdown (index + 5)
    int GetMiniGameLimitFromDropdown()
    {
        // Index 0 = 5 games, Index 1 = 6 games, ..., Index 5 = 10 games
        return dropdownMiniGameLimit.value + 5;
    }

    // Hàm tạo phòng (gọi từ button)
    public void CreateRoom()
    {
        string roomName = inputRoomName.text;

        // Kiểm tra tên phòng rỗng
        if (string.IsNullOrEmpty(roomName))
        {
            ShowNotification("Vui lòng nhập tên phòng!");
            return;
        }

        // Kiểm tra từ tục tĩu
        if (ProfanityFilter.ContainsProfanity(roomName))
        {
            ShowNotification(ProfanityFilter.GetErrorMessage());
            return;
        }

        // Lấy giá trị từ dropdown
        int playerLimit = GetPlayerLimitFromDropdown();
        int miniGameLimit = GetMiniGameLimitFromDropdown();

        // Tạo phòng
        RoomData newRoom = new RoomData(roomName, playerLimit, miniGameLimit);
        roomList.Add(newRoom);

        // Log console
        Debug.Log("=== ĐÃ TẠO PHÒNG MỚI ===");
        Debug.Log("Tên phòng: " + newRoom.roomName);
        Debug.Log("Số người chơi: " + newRoom.playerLimit);
        Debug.Log("Số mini game: " + newRoom.miniGameLimit);
        Debug.Log("Dropdown Player Index: " + dropdownPlayerLimit.value);
        Debug.Log("Dropdown MiniGame Index: " + dropdownMiniGameLimit.value);
        Debug.Log("========================");

        // Xóa input
        ClearInputFields();

        // Hiển thị thông báo thành công
        ShowNotification("Tạo phòng thành công!");

        // Chuyển về Play Online sau 1.5 giây
        Invoke("BackToPlayOnline", 1.5f);
    }

    // Xóa các input field và reset dropdown
    void ClearInputFields()
    {
        inputRoomName.text = "";
        dropdownPlayerLimit.value = 0;
        dropdownMiniGameLimit.value = 0;
        dropdownPlayerLimit.RefreshShownValue();
        dropdownMiniGameLimit.RefreshShownValue();
    }

    // Quay về Play Online
    void BackToPlayOnline()
    {
        CanvasManager.Instance.ShowPlayOnline();
    }

    // Hiển thị thông báo
    void ShowNotification(string message)
    {
        Debug.LogWarning(message);
        
        if (notificationText != null)
        {
            notificationText.text = message;
            notificationText.gameObject.SetActive(true);
            Invoke("HideNotification", 2f);
        }
    }

    void HideNotification()
    {
        if (notificationText != null)
        {
            notificationText.gameObject.SetActive(false);
        }
    }

    // Hiển thị danh sách phòng trong Find Lobby
    public void RefreshRoomList()
    {
        // Xóa các phòng cũ
        foreach (Transform child in roomListContent)
        {
            Destroy(child.gameObject);
        }

        // Kiểm tra có phòng không
        if (roomList.Count == 0)
        {
            Debug.Log("Chưa có phòng nào được tạo!");
            return;
        }

        // Tạo mới từ danh sách
        foreach (RoomData room in roomList)
        {
            GameObject roomItem = Instantiate(roomItemPrefab, roomListContent);
            Text roomText = roomItem.GetComponentInChildren<Text>();
            
            // Nếu dùng TextMeshPro thì dùng dòng này thay vì dòng trên:
            // TextMeshProUGUI roomText = roomItem.GetComponentInChildren<TextMeshProUGUI>();
            
            if (roomText != null)
            {
                roomText.text = $"{room.roomName} - {room.playerLimit} người - {room.miniGameLimit} mini games";
            }
        }

        Debug.Log("Đã refresh danh sách phòng. Tổng: " + roomList.Count + " phòng");
    }

    // DEBUG: Hàm test tạo phòng mẫu (tùy chọn)
    public void CreateTestRooms()
    {
        roomList.Add(new RoomData("Phòng Test 1", 2, 5));
        roomList.Add(new RoomData("Phòng Test 2", 4, 10));
        roomList.Add(new RoomData("Phòng Test 3", 3, 7));
        Debug.Log("Đã tạo 3 phòng test!");
    }
}