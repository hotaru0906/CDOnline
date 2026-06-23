using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Hiển thị danh sách player trong Tutorial UI của minigame.
/// Status: Ready (đã load xong, PlayerController đã spawn) - Not Ready (còn đang loading).
/// </summary>
public class MinigameTutorialPlayerListUI : MonoBehaviour
{
    [SerializeField] private Transform contentParent;
    [SerializeField] private MinigameTutorialPlayerItemUI itemPrefab;

    private List<MinigameTutorialPlayerItemUI> _items = new List<MinigameTutorialPlayerItemUI>();
    private int _lastPlayerCount = -1;

    private void OnEnable()
    {
        RefreshList();
    }

    private void Update()
    {
        int currentCount = FindObjectsByType<PlayerNetworkData>(FindObjectsSortMode.None).Length;

        if (currentCount != _lastPlayerCount)
        {
            RefreshList();
        }

        foreach (var item in _items)
        {
            if (item != null)
                item.UpdateData();
        }
    }

    private void RefreshList()
    {
        foreach (var item in _items)
        {
            if (item != null)
                Destroy(item.gameObject);
        }
        _items.Clear();

        var players = FindObjectsByType<PlayerNetworkData>(FindObjectsSortMode.None);

        foreach (var player in players)
        {
            if (itemPrefab != null && contentParent != null)
            {
                var item = Instantiate(itemPrefab, contentParent);
                item.SetData(player);
                _items.Add(item);
            }
        }

        _lastPlayerCount = players.Length;
    }
}

