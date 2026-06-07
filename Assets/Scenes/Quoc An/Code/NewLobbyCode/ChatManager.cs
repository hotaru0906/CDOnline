using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

// ============================================================
// ChatManager
// Xử lý: nhập tin nhắn, lọc từ tục tĩu, hiển thị lên khung chat
//
// OFFLINE  : Chạy hoàn toàn local, test ngay trong Play Mode
// ONLINE   : Uncomment phần Photon/Mirror để sync qua mạng
// ============================================================

public class ChatManager : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Kéo ChatInputField vào đây")]
    public TMP_InputField chatInputField;

    [Tooltip("Kéo SendBtn vào đây")]
    public Button sendButton;

    [Tooltip("Kéo object Content (nằm trong ChatScrollView/Viewport/Content) vào đây")]
    public Transform messageContainer;

    [Tooltip("Kéo prefab ChatMessage vào đây (TextMeshPro object)")]
    public GameObject messagePrefab;

    [Tooltip("Kéo ChatScrollView vào đây để auto-scroll xuống dưới")]
    public ScrollRect scrollRect;

    [Header("Settings")]
    [Tooltip("Số tin nhắn tối đa hiển thị (tránh lag)")]
    public int maxMessages = 50;

    [Tooltip("Tên người chơi hiện tại — test offline")]
    public string localPlayerName = "HostPlayer";

    // --------------------------------------------------------
    // Danh sách từ cần lọc — thêm từ vào đây
    // Format: { "từgốc", "từthayThế" }  
    // Nếu chỉ muốn censore thành ***** thì để replacements rỗng
    // --------------------------------------------------------
    private readonly List<string> bannedWords = new List<string>
    {
        // Tiếng Anh
        "fuck", "shit", "bitch", "asshole", "bastard", "damn", "crap",
        // Tiếng Việt (không dấu để dễ detect)
        "dit", "cac", "lon", "buoi", "vcl", "dmm", "dm", "clm", "vkl",
        "đit", "cặc", "lồn", "buồi", "đmm", "đm", "clm",
        // Thêm từ khác ở đây
    };

    private List<GameObject> messageObjects = new List<GameObject>();

    void Start()
    {
        // Gán sự kiện cho nút Send
        if (sendButton != null)
            sendButton.onClick.AddListener(OnSendButtonClicked);

        // Cho phép nhấn Enter để gửi
        if (chatInputField != null)
            chatInputField.onSubmit.AddListener(OnInputSubmit);

        Debug.Log("[Chat] ChatManager initialized. Local player: " + localPlayerName);
    }

    // --------------------------------------------------------
    // Gọi khi nhấn nút Send
    // --------------------------------------------------------
    void OnSendButtonClicked()
    {
        SendMessage_Local();
    }

    // --------------------------------------------------------
    // Gọi khi nhấn Enter trong input field
    // --------------------------------------------------------
    void OnInputSubmit(string text)
    {
        SendMessage_Local();
        // Re-focus input field sau khi gửi
        chatInputField.ActivateInputField();
    }

    // --------------------------------------------------------
    // Xử lý gửi tin nhắn (offline)
    // --------------------------------------------------------
    void SendMessage_Local()
    {
        if (chatInputField == null) return;

        string rawText = chatInputField.text.Trim();

        if (string.IsNullOrEmpty(rawText)) return;

        // Lọc từ tục tĩu
        string filteredText = FilterBannedWords(rawText);

        // Hiển thị tin nhắn
        DisplayMessage(localPlayerName, filteredText, MessageType.Player);

        // Log ra console
        Debug.Log($"[Chat] {localPlayerName}: {filteredText}");

        // Xóa input field
        chatInputField.text = "";

        // --- ONLINE (Photon PUN2) — uncomment khi tích hợp ---
        // photonView.RPC("RPC_ReceiveMessage", RpcTarget.All, localPlayerName, filteredText);

        // --- ONLINE (Mirror) --- 
        // CmdSendMessage(localPlayerName, filteredText);
    }

    // --------------------------------------------------------
    // Lọc từ tục tĩu — thay thế bằng *****
    // --------------------------------------------------------
    string FilterBannedWords(string input)
    {
        string result = input;

        foreach (string word in bannedWords)
        {
            // Case-insensitive replace
            string stars = new string('*', word.Length);
            System.Text.RegularExpressions.Regex regex =
                new System.Text.RegularExpressions.Regex(
                    System.Text.RegularExpressions.Regex.Escape(word),
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase
                );
            result = regex.Replace(result, stars);
        }

        return result;
    }

    // --------------------------------------------------------
    // Hiển thị tin nhắn lên UI
    // --------------------------------------------------------
    public enum MessageType { Player, System }

    public void DisplayMessage(string sender, string content, MessageType type = MessageType.Player)
    {
        if (messagePrefab == null || messageContainer == null)
        {
            Debug.LogError("[Chat] messagePrefab hoặc messageContainer chưa được gán!");
            return;
        }

        // Tạo message object
        GameObject msgObj = Instantiate(messagePrefab, messageContainer);
        TextMeshProUGUI tmpText = msgObj.GetComponent<TextMeshProUGUI>();

        if (tmpText == null)
        {
            Debug.LogError("[Chat] messagePrefab không có component TextMeshProUGUI!");
            return;
        }

        // Set nội dung và màu theo loại
        if (type == MessageType.System)
        {
            tmpText.text = $"<i><color=#999999>{content}</color></i>";
        }
        else
        {
            // Màu tên người chơi khác nhau dựa theo hash tên
            string nameColor = GetPlayerColor(sender);
            tmpText.text = $"<color={nameColor}><b>{sender}:</b></color> {content}";
        }

        messageObjects.Add(msgObj);

        // Xóa tin nhắn cũ nếu quá maxMessages
        if (messageObjects.Count > maxMessages)
        {
            Destroy(messageObjects[0]);
            messageObjects.RemoveAt(0);
        }

        // Auto-scroll xuống tin nhắn mới nhất
        ScrollToBottom();
    }

    // --------------------------------------------------------
    // Tạo màu khác nhau cho mỗi tên người chơi
    // --------------------------------------------------------
    string GetPlayerColor(string playerName)
    {
        string[] colors = { "#7EC8F5", "#C8A8F5", "#F5C87E", "#8DF5A8", "#F58D8D" };
        int index = Mathf.Abs(playerName.GetHashCode()) % colors.Length;
        return colors[index];
    }

    // --------------------------------------------------------
    // Auto-scroll xuống dưới cùng
    // --------------------------------------------------------
    void ScrollToBottom()
    {
        if (scrollRect == null) return;
        // Delay 1 frame để Content Size Fitter cập nhật trước
        StartCoroutine(ScrollNextFrame());
    }

    System.Collections.IEnumerator ScrollNextFrame()
    {
        yield return null;
        scrollRect.verticalNormalizedPosition = 0f;
    }

    // --------------------------------------------------------
    // ONLINE: Nhận tin nhắn từ player khác qua mạng
    // --------------------------------------------------------

    // --- Photon PUN2 ---
    // [PunRPC]
    // public void RPC_ReceiveMessage(string sender, string content)
    // {
    //     DisplayMessage(sender, content, MessageType.Player);
    // }

    // --- Mirror ---
    // [Command]
    // void CmdSendMessage(string sender, string content)
    // {
    //     RpcReceiveMessage(sender, content);
    // }
    // [ClientRpc]
    // void RpcReceiveMessage(string sender, string content)
    // {
    //     DisplayMessage(sender, content, MessageType.Player);
    // }

    // --------------------------------------------------------
    // Gọi hàm này để hiển thị system message
    // Ví dụ: chatManager.ShowSystemMessage("Alice joined the room");
    // --------------------------------------------------------
    public void ShowSystemMessage(string content)
    {
        DisplayMessage("", content, MessageType.System);
        Debug.Log($"[Chat][System] {content}");
    }
}