using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;

public class LobbyRoomManager : MonoBehaviour
{
    [Header("UI Elements")]
    public TextMeshProUGUI roomNameText;
    public TextMeshProUGUI playerCountText;
    public TMP_InputField chatInput;
    public Button sendButton;
    public Button startButton;

    [Header("Chat")]
    public Transform chatContent;           // Kéo "Content" của Scroll View vào
    public GameObject chatMessagePrefab;    // Tạo prefab TextMeshPro sau

    private List<string> messages = new List<string>();

    void Start()
    {
        sendButton.onClick.AddListener(SendChatMessage);
        chatInput.onSubmit.AddListener(_ => SendChatMessage());
        startButton.onClick.AddListener(StartGame);   // sau này connect với network
    }

    public void SetRoomInfo(string roomName, int currentPlayers, int maxPlayers)
    {
        roomNameText.text = "Room: " + roomName;
        playerCountText.text = $"{currentPlayers}/{maxPlayers}";
    }

    private void SendChatMessage()
    {
        if (string.IsNullOrEmpty(chatInput.text)) return;

        string message = $"[Bạn]: {chatInput.text}";
        AddChatMessage(message);

        // TODO: Sau này gửi qua network (Photon/Netcode)
        chatInput.text = "";
        chatInput.ActivateInputField();
    }

    public void AddChatMessage(string message)
    {
        GameObject msgObj = Instantiate(chatMessagePrefab, chatContent);
        msgObj.GetComponent<TextMeshProUGUI>().text = message;
        
        // Auto scroll xuống dưới
        Canvas.ForceUpdateCanvases();
        chatContent.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -chatContent.GetComponent<RectTransform>().sizeDelta.y);
    }


    private void StartGame()
    {
        Debug.Log("Bắt đầu game! (sẽ connect network sau)");
        // TODO: Load scene game chính hoặc gọi network StartGame()
    }
}