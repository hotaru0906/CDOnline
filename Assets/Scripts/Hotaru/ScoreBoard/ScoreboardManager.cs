using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>
/// Đọc ranking từ GameManager.MgRank1-4 và map sang PlayerNetworkData.
/// Cung cấp dữ liệu cho ScoreboardUI hiển thị.
/// </summary>
public class ScoreboardManager : MonoBehaviour
{
    public static ScoreboardManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// Trả về danh sách PlayerNetworkData đã được sắp xếp theo thứ hạng minigame.
    /// Thứ tự: MgRank1 → MgRank2 → MgRank3 → MgRank4.
    /// Player không có trong ranking sẽ được append vào cuối.
    /// </summary>
    public List<PlayerNetworkData> GetRankedPlayers()
    {
        Debug.Log("GET RANKED PLAYERS CALLED");
        var result = new List<PlayerNetworkData>();

        if (GameManager.Instance == null)
        {
            // Fallback: trả về tất cả player không sắp xếp
            result.AddRange(FindObjectsByType<PlayerNetworkData>(FindObjectsSortMode.None));
            return result;
        }

        // Lấy ranking từ GameManager (PlayerId theo thứ tự rank)
        int[] rankedIds = GameManager.Instance.GetLastMinigameRanking();

        // Lấy tất cả players hiện tại
        var allPlayers = FindObjectsByType<PlayerNetworkData>(FindObjectsSortMode.None);

        // Map PlayerId → PlayerNetworkData
        var playerMap = new Dictionary<int, PlayerNetworkData>();
        foreach (var p in allPlayers)
        {
            if (p.Object != null)
                playerMap[p.Object.InputAuthority.PlayerId] = p;
        }

        // Thêm theo thứ tự rank
        var addedIds = new HashSet<int>();
        foreach (int id in rankedIds)
        {
            if (id >= 0 && playerMap.TryGetValue(id, out var player))
            {
                result.Add(player);
                addedIds.Add(id);
            }
        }

        // Append các player chưa có trong ranking (ví dụ: disconnect rồi reconnect)
        foreach (var p in allPlayers)
        {
            if (p.Object != null && !addedIds.Contains(p.Object.InputAuthority.PlayerId))
                result.Add(p);
        }

        return result;
    }

    /// <summary>
    /// Refresh ScoreboardUI nếu đang active — gọi bởi PlayerNetworkData.OnScoreChanged
    /// </summary>
    public void RefreshFromPlayers()
    {
        var ui = FindFirstObjectByType<ScoreboardUI>();
        if (ui != null && ui.gameObject.activeInHierarchy)
            ui.UpdateScoreboard();
    }

}
