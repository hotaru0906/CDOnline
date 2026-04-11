using UnityEngine;
using TMPro;
using System.Linq;

/// <summary>
/// Quản lý hiển thị Scoreboard sau khi minigame kết thúc.
/// Gắn vào ScoreboardPanel trong Canvas.
/// </summary>
public class ScoreboardUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_Text winnerText;
    [SerializeField] private TMP_Text[] playerRankTexts; // Danh sách player theo thứ hạng
    [SerializeField] private TMP_Text minigameNameText;
    
    [Header("Optional")]
    [SerializeField] private GameObject winnerHighlight;
    
    private void OnEnable()
    {
        // Khi scoreboard được hiển thị, populate data
        UpdateScoreboard();
    }
    
    /// <summary>
    /// Cập nhật UI scoreboard với data từ minigame vừa kết thúc
    /// </summary>
    public void UpdateScoreboard()
    {
        // Lấy minigame data
        var minigameData = GameManager.Instance?.CurrentMinigameData;
        if (minigameNameText != null && minigameData != null)
        {
            minigameNameText.text = minigameData.minigameName;
        }
        
        // Lấy danh sách players và sort theo kết quả
        var players = FindObjectsByType<PlayerNetworkData>(FindObjectsSortMode.None)
            .OrderByDescending(p => IsWinner(p))
            .ThenByDescending(p => !IsEliminated(p))
            .ToArray();
        
        // Hiển thị winner
        var winner = players.FirstOrDefault(p => IsWinner(p));
        if (winnerText != null)
        {
            winnerText.text = winner != null 
                ? $"🏆 {winner.PlayerName} WINS!" 
                : "No Winner";
        }
        
        // Hiển thị rankings
        for (int i = 0; i < playerRankTexts.Length; i++)
        {
            if (playerRankTexts[i] == null) continue;
            
            if (i < players.Length)
            {
                var player = players[i];
                string status = IsWinner(player) ? "🏆" : (IsEliminated(player) ? "💀" : "");
                playerRankTexts[i].text = $"{i + 1}. {player.PlayerName} {status}";
                playerRankTexts[i].gameObject.SetActive(true);
            }
            else
            {
                playerRankTexts[i].gameObject.SetActive(false);
            }
        }
        
        // Highlight winner
        if (winnerHighlight != null)
        {
            winnerHighlight.SetActive(winner != null);
        }
    }
    
    private bool IsWinner(PlayerNetworkData player)
    {
        if (MinigameController.Instance == null) return false;
        
        var winnerRef = MinigameController.Instance.Winner;
        return winnerRef != Fusion.PlayerRef.None && 
               player.Object.InputAuthority == winnerRef;
    }
    
    private bool IsEliminated(PlayerNetworkData player)
    {
        var minigameData = player.GetComponent<PlayerMinigameData>();
        return minigameData != null && minigameData.IsEliminated;
    }
}
