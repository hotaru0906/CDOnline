using UnityEngine;
using UnityEngine.UI;

public class PlayerListManager : MonoBehaviour
{
    [Header("Player Slots")]
    public PlayerSlot[] playerSlots = new PlayerSlot[4];
    
    [Header("Ready Button")]
    public Button readyButton;
    public Text readyButtonText; // Text trên nút Ready
    
    [Header("Player Avatars")]
    public Sprite[] playerAvatars; // Mảng avatar cho các player
    
    private int localPlayerIndex = 0; // Index của người chơi local (để test)
    private bool isLocalPlayerReady = false;
    
    void Start()
    {
        // Setup button Ready
        if (readyButton != null)
        {
            readyButton.onClick.AddListener(OnReadyButtonClick);
        }
        
        // Setup dữ liệu mẫu cho testing
        SetupTestData();
    }
    
    void SetupTestData()
    {
        // Player 1 là Host
        if (playerSlots[0] != null)
        {
            playerSlots[0].SetPlayerData("Player 1 (Host)", 
                playerAvatars.Length > 0 ? playerAvatars[0] : null, 
                true);
            playerSlots[0].SetActive(true);
        }
        
        // Player 2, 3, 4
        for (int i = 1; i < playerSlots.Length; i++)
        {
            if (playerSlots[i] != null)
            {
                playerSlots[i].SetPlayerData($"Player {i + 1}", 
                    playerAvatars.Length > i ? playerAvatars[i] : null, 
                    false);
                playerSlots[i].SetActive(true); // Đặt false nếu muốn ẩn slot trống
            }
        }
        
        // Giả sử player local là player 2
        localPlayerIndex = 1;
    }
    
    void OnReadyButtonClick()
    {
        // Toggle trạng thái ready
        isLocalPlayerReady = !isLocalPlayerReady;
        
        // Cập nhật slot của người chơi local
        if (playerSlots[localPlayerIndex] != null)
        {
            playerSlots[localPlayerIndex].SetReady(isLocalPlayerReady);
        }
        
        // Cập nhật text của button
        if (readyButtonText != null)
        {
            readyButtonText.text = isLocalPlayerReady ? "Cancel" : "Ready";
        }
        
        // Kiểm tra xem tất cả người chơi đã ready chưa
        CheckAllPlayersReady();
    }
    
    void CheckAllPlayersReady()
    {
        bool allReady = true;
        
        foreach (PlayerSlot slot in playerSlots)
        {
            if (slot != null && slot.gameObject.activeSelf)
            {
                if (!slot.isReady && !slot.isHost)
                {
                    allReady = false;
                    break;
                }
            }
        }
        
        if (allReady)
        {
            Debug.Log("Tất cả người chơi đã sẵn sàng!");
            // Host có thể bắt đầu game ở đây
        }
    }
    
    // Hàm để thêm/xóa người chơi (dùng cho khi làm online sau này)
    public void AddPlayer(int slotIndex, string playerName, Sprite avatar, bool isHost = false)
    {
        if (slotIndex >= 0 && slotIndex < playerSlots.Length && playerSlots[slotIndex] != null)
        {
            playerSlots[slotIndex].SetPlayerData(playerName, avatar, isHost);
            playerSlots[slotIndex].SetActive(true);
        }
    }
    
    public void RemovePlayer(int slotIndex)
    {
        if (slotIndex >= 0 && slotIndex < playerSlots.Length && playerSlots[slotIndex] != null)
        {
            playerSlots[slotIndex].SetActive(false);
        }
    }
}