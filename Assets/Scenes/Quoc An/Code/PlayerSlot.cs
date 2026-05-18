using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerSlot : MonoBehaviour
{
    [Header("UI References")]
    public Image playerIcon;
    public TextMeshProUGUI playerNameText; // hoặc Text nếu dùng UI cũ
    public Image readyStatusIcon;
    
    [Header("Status Icons")]
    public Sprite checkIcon; // Dấu tích (ready)
    public Sprite crossIcon; // Dấu X (not ready)
    
    [Header("Player Data")]
    public Sprite playerAvatar;
    public string playerName = "Player";
    public bool isReady = false;
    public bool isHost = false;
    
    void Start()
    {
        UpdateDisplay();
    }
    
    public void UpdateDisplay()
    {
        // Hiển thị icon người chơi
        if (playerIcon != null && playerAvatar != null)
        {
            playerIcon.sprite = playerAvatar;
        }
        
        // Hiển thị tên người chơi
        if (playerNameText != null)
        {
            playerNameText.text = playerName;
        }
        
        // Hiển thị trạng thái ready
        UpdateReadyStatus();
    }
    
    public void UpdateReadyStatus()
    {
        if (readyStatusIcon != null)
        {
            // Host luôn hiển thị dấu tích
            if (isHost)
            {
                readyStatusIcon.sprite = checkIcon;
                readyStatusIcon.color = Color.green;
            }
            else
            {
                // Người chơi thường: tích nếu ready, X nếu chưa ready
                readyStatusIcon.sprite = isReady ? checkIcon : crossIcon;
                readyStatusIcon.color = isReady ? Color.green : Color.red;
            }
        }
    }
    
    public void SetReady(bool ready)
    {
        if (!isHost) // Chỉ áp dụng cho người chơi không phải host
        {
            isReady = ready;
            UpdateReadyStatus();
        }
    }
    
    public void SetPlayerData(string name, Sprite avatar, bool host = false)
    {
        playerName = name;
        playerAvatar = avatar;
        isHost = host;
        UpdateDisplay();
    }
    
    public void SetActive(bool active)
    {
        gameObject.SetActive(active);
    }
}