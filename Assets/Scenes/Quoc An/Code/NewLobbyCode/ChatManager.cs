using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

// ============================================================
// ChatManager — Chat lobby với profanity filter VI + EN
// Gửi bằng Enter, không có nút Send
// OFFLINE: test trực tiếp, tin nhắn hiện ngay
// ONLINE:  Uncomment phần Fusion khi sẵn sàng
// ============================================================
public class ChatManager : MonoBehaviour
{
    // ── References ────────────────────────────────────────────
    [Header("--- UI REFERENCES ---")]
    [Tooltip("ScrollRect chứa nội dung chat")]
    public ScrollRect chatScrollRect;

    [Tooltip("Content bên trong ScrollRect")]
    public Transform chatContent;

    [Tooltip("Prefab 1 dòng chat (có TMP)")]
    public GameObject chatLinePrefab;

    [Tooltip("InputField để nhập tin nhắn — nhấn Enter để gửi")]
    public TMP_InputField chatInput;

    // ── Settings ──────────────────────────────────────────────
    [Header("--- SETTINGS ---")]
    [Tooltip("Tên người chơi local (để test offline)")]
    public string localPlayerName = "HostPlayer";

    [Tooltip("Số dòng chat tối đa trước khi xóa dòng cũ nhất")]
    public int maxChatLines = 50;

    [Header("--- DEBUG TEST ---")]
    [Tooltip("Nhấn nút này trong Inspector để gửi tin nhắn test")]
    public string debugMessageToSend = "Hello everyone!";

    // ── Profanity filter ──────────────────────────────────────
    // Tiếng Việt + Tiếng Anh — mở rộng tùy ý
    private static readonly HashSet<string> bannedWords = new HashSet<string>(
        System.StringComparer.OrdinalIgnoreCase)
    {
        // EN
        "fuck", "shit", "bitch", "asshole", "bastard", "damn", "crap",
        "dick", "pussy", "cock", "whore", "slut", "nigger", "faggot",
        // VI (latin không dấu để match dễ hơn)
        "dit", "buoi", "lon", "cac", "cu", "dm", "vcl", "vkl",
        "clm", "dcm", "đm", "đmm", "cmm", "cml", "đcm",
        "má mày", "mẹ mày", "bố mày", "thằng chó", "con chó",
        "đồ chó", "thứ chó", "ngu", "óc chó", "óc bò"
    };

    // ── Runtime ───────────────────────────────────────────────
    private List<GameObject> chatLines = new List<GameObject>();

    // ─────────────────────────────────────────────────────────

    void Start()
    {
        if (chatInput != null)
        {
            // Lắng nghe phím Enter
            chatInput.onSubmit.AddListener(OnSubmitChat);
        }
        else Debug.LogWarning("[Chat] chatInput chưa gán!");

        // Tin nhắn hệ thống khi vào lobby
        AddSystemMessage("Bạn đã vào phòng. Chào mừng!");
    }

    // ── Nhận input Enter ──────────────────────────────────────
    void OnSubmitChat(string message)
    {
        SendMessage_Local(message);

        // Xóa input và giữ focus để tiếp tục nhập
        chatInput.text = "";
        chatInput.ActivateInputField();
    }

    // ── Gửi tin nhắn local ───────────────────────────────────
    void SendMessage_Local(string message)
    {
        message = message.Trim();
        if (string.IsNullOrEmpty(message)) return;

        string filtered = FilterProfanity(message);
        string formatted = $"<b>{localPlayerName}:</b> {filtered}";

        AddChatLine(formatted, Color.white);

        // FUSION STUB:
        // RPC_SendChatMessage(localPlayerName, filtered);
    }

    // ── Thêm 1 dòng chat vào UI ───────────────────────────────
    public void AddChatLine(string text, Color color)
    {
        if (chatContent == null || chatLinePrefab == null)
        {
            Debug.LogWarning("[Chat] Thiếu chatContent hoặc chatLinePrefab!");
            return;
        }

        // Xóa dòng cũ nhất nếu quá giới hạn
        if (chatLines.Count >= maxChatLines)
        {
            Destroy(chatLines[0]);
            chatLines.RemoveAt(0);
        }

        GameObject line = Instantiate(chatLinePrefab, chatContent);
        TextMeshProUGUI tmp = line.GetComponentInChildren<TextMeshProUGUI>();
        if (tmp != null)
        {
            tmp.text  = text;
            tmp.color = color;
        }

        chatLines.Add(line);

        // Scroll xuống cuối
        Canvas.ForceUpdateCanvases();
        if (chatScrollRect != null)
            chatScrollRect.verticalNormalizedPosition = 0f;
    }

    // ── Tin nhắn hệ thống (màu vàng) ─────────────────────────
    public void AddSystemMessage(string text)
    {
        AddChatLine($"<i>[System] {text}</i>", new Color(1f, 0.85f, 0.3f));
    }

    // ── Profanity Filter ──────────────────────────────────────
    string FilterProfanity(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;

        string[] words = input.Split(' ');
        for (int i = 0; i < words.Length; i++)
        {
            // Strip dấu câu để so sánh
            string clean = System.Text.RegularExpressions.Regex
                .Replace(words[i], @"[^\w]", "");

            if (bannedWords.Contains(clean))
                words[i] = new string('*', words[i].Length);
        }
        return string.Join(" ", words);
    }

    // ── Nhận tin từ người khác (gọi khi online) ──────────────
    public void ReceiveMessage(string senderName, string message)
    {
        string filtered   = FilterProfanity(message);
        string formatted  = $"<b>{senderName}:</b> {filtered}";
        AddChatLine(formatted, Color.white);
    }

    // ── Inspector Debug ───────────────────────────────────────
#if UNITY_EDITOR
    [ContextMenu("Debug: Send Test Message")]
    void Debug_SendTestMessage()
    {
        SendMessage_Local(debugMessageToSend);
    }
#endif

    // ── FUSION STUB ───────────────────────────────────────────
    // [Rpc(RpcSources.InputAuthority, RpcTargets.All)]
    // void RPC_SendChatMessage(string sender, string message)
    // {
    //     ReceiveMessage(sender, message);
    // }
}