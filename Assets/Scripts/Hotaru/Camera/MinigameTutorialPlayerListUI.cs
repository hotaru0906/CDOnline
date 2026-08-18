using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Hiển thị danh sách player trong Tutorial UI của minigame.
/// Status: Ready (đã load xong, PlayerController đã spawn VÀ dữ liệu network đã sync)
///        - Not Ready (còn đang loading, hoặc dữ liệu network chưa sync xong).
///
/// Do Host/Client có thể kết nối lệch thời điểm, thông tin (icon, tên) của 1 player
/// có thể hiện SAI/placeholder trong lúc dữ liệu network chưa kịp đến — trường hợp này
/// UI sẽ tự ép status = NOT READY (xem MinigameTutorialPlayerItemUI.UpdateData) và
/// tự làm mới định kỳ mỗi <see cref="refreshInterval"/> giây cho tới khi đúng.
/// </summary>
public class MinigameTutorialPlayerListUI : MonoBehaviour
{
    [SerializeField] private Transform contentParent;
    [SerializeField] private MinigameTutorialPlayerItemUI itemPrefab;

    [Tooltip("Chu kỳ (giây) làm mới lại icon/tên/status của từng item.")]
    [SerializeField] private float refreshInterval = 1f;

    private List<MinigameTutorialPlayerItemUI> _items = new List<MinigameTutorialPlayerItemUI>();
    private HashSet<int> _lastPlayerIds = new HashSet<int>();

    private void OnEnable()
    {
        RefreshList();

        // Refresh dữ liệu hiển thị (icon/tên/status) định kỳ, KHÔNG chạy mỗi frame,
        // vì việc này chỉ cần đủ nhanh để bắt kịp lúc network sync xong, không cần realtime.
        InvokeRepeating(nameof(RefreshItemsData), refreshInterval, refreshInterval);
    }

    private void OnDisable()
    {
        CancelInvoke(nameof(RefreshItemsData));
    }

    private void Update()
    {
        // Theo dõi join/leave: cần đủ nhanh để danh sách không bị trễ khi có người vào/ra phòng.
        // Việc này rẻ (so sánh HashSet id), khác với việc refresh icon/tên/status ở trên.
        var players = GetSortedPlayers();

        if (!IsSamePlayerSet(players))
        {
            RefreshList(players);
        }
    }

    private void RefreshItemsData()
    {
        foreach (var item in _items)
        {
            if (item != null)
                item.UpdateData();
        }
    }

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