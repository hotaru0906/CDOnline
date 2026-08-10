using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Hiển thị danh sách player trong Tutorial UI của minigame.
/// Status: Ready (đã load xong, PlayerController đã spawn) - Not Ready (còn đang loading).
/// </summary>
public class MinigameTutorialPlayerListUI : MonoBehaviour
{
    [SerializeField] private Transform contentParent;
    [SerializeField] private MinigameTutorialPlayerItemUI itemPrefab;

    private List<MinigameTutorialPlayerItemUI> _items = new List<MinigameTutorialPlayerItemUI>();
    private HashSet<int> _lastPlayerIds = new HashSet<int>();

    private void OnEnable()
    {
        RefreshList();
    }

    private void Update()
    {
        var players = GetSortedPlayers();

        if (!IsSamePlayerSet(players))
        {
            RefreshList(players);
        }

        foreach (var item in _items)
        {
            if (item != null)
                item.UpdateData();
        }
    }

    /// <summary>
    /// Lấy danh sách player và sắp xếp theo PlayerId - đây là khóa ỔN ĐỊNH và ĐỒNG BỘ qua mạng.
    /// KHÔNG dựa vào thứ tự trả về của FindObjectsByType (FindObjectsSortMode.None), vì thứ tự
    /// đó chỉ phản ánh cách object tồn tại cục bộ trên từng máy (Host/Client load xong ở thời
    /// điểm khác nhau => thứ tự khác nhau), dẫn đến 1 slot UI hiển thị tên khác nhau giữa các máy.
    /// </summary>
    private List<PlayerNetworkData> GetSortedPlayers()
    {
        var players = FindObjectsByType<PlayerNetworkData>(FindObjectsSortMode.None)
            .Where(p => p != null && p.Object != null)
            .OrderBy(p => p.Object.InputAuthority.PlayerId)
            .ToList();

        return players;
    }

    private bool IsSamePlayerSet(List<PlayerNetworkData> players)
    {
        if (players.Count != _lastPlayerIds.Count)
            return false;

        foreach (var p in players)
        {
            if (!_lastPlayerIds.Contains(p.Object.InputAuthority.PlayerId))
                return false;
        }

        return true;
    }

    private void RefreshList()
    {
        RefreshList(GetSortedPlayers());
    }

    private void RefreshList(List<PlayerNetworkData> players)
    {
        foreach (var item in _items)
        {
            if (item != null)
                Destroy(item.gameObject);
        }
        _items.Clear();

        foreach (var player in players)
        {
            if (itemPrefab != null && contentParent != null)
            {
                var item = Instantiate(itemPrefab, contentParent);
                item.SetData(player);
                _items.Add(item);
            }
        }

        _lastPlayerIds = new HashSet<int>(players.Select(p => p.Object.InputAuthority.PlayerId));
    }
}