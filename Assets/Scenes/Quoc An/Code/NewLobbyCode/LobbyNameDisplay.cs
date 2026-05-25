using UnityEngine;
using TMPro;

// ============================================================
// LobbyNameDisplay
// Nhận tên phòng được truyền từ scene/canvas trước đó
// và hiển thị lên UI badge ở đầu màn hình lobby.
//
// OFFLINE  : Dùng Inspector field "debugRoomName" để test
// ONLINE   : Uncomment phần Photon/Mirror bên dưới khi sẵn sàng
// ============================================================

public class LobbyNameDisplay : MonoBehaviour
{
    [Header("UI Reference")]
    [Tooltip("Kéo LobbyNameText (TextMeshPro) vào đây")]
    public TextMeshProUGUI lobbyNameText;

    [Header("Debug / Offline Test")]
    [Tooltip("Tên phòng dùng để test offline — đổi trực tiếp trong Inspector")]
    public string debugRoomName = "Room #1042";

    // --------------------------------------------------------
    // Cách truyền tên phòng từ scene trước:
    // Dùng static variable hoặc PlayerPrefs.
    // Ví dụ ở scene chọn phòng:
    //   PlayerPrefs.SetString("RoomName", tenPhong);
    // --------------------------------------------------------

    void Start()
    {
        string roomName = LoadRoomName();
        DisplayRoomName(roomName);
    }

    string LoadRoomName()
    {
        // --- OFFLINE: Đọc từ PlayerPrefs nếu có, fallback về debugRoomName ---
        if (PlayerPrefs.HasKey("RoomName"))
        {
            string saved = PlayerPrefs.GetString("RoomName");
            Debug.Log($"[LobbyName] Loaded from PlayerPrefs: {saved}");
            return saved;
        }

        Debug.Log($"[LobbyName] No PlayerPrefs key found. Using debug value: {debugRoomName}");
        return debugRoomName;

        // --- ONLINE (Photon PUN2) — uncomment khi tích hợp Photon ---
        // if (PhotonNetwork.InRoom)
        // {
        //     return PhotonNetwork.CurrentRoom.Name;
        // }
        // return debugRoomName;

        // --- ONLINE (Mirror) — uncomment khi tích hợp Mirror ---
        // return NetworkManager.singleton.networkAddress;
    }

    void DisplayRoomName(string roomName)
    {
        if (lobbyNameText == null)
        {
            Debug.LogError("[LobbyName] lobbyNameText chưa được gán trong Inspector!");
            return;
        }

        lobbyNameText.text = $"Game Lobby — {roomName}";
        Debug.Log($"[LobbyName] Displaying: Game Lobby — {roomName}");
    }

    // --------------------------------------------------------
    // Gọi hàm này từ scene chọn phòng trước khi load scene lobby
    // Ví dụ: LobbyNameDisplay.SaveRoomName("My Cool Room");
    // --------------------------------------------------------
    public static void SaveRoomName(string name)
    {
        PlayerPrefs.SetString("RoomName", name);
        PlayerPrefs.Save();
        Debug.Log($"[LobbyName] Saved room name: {name}");
    }
}
