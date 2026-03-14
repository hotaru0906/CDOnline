using UnityEngine;
using Fusion;
using System.Collections.Generic;

public class PlayerListUI : MonoBehaviour
{
    [SerializeField] private Transform content;
    [SerializeField] private PlayerListItemUI playerItemPrefab;

    private NetworkRunner _runner;

    private Dictionary<PlayerRef, PlayerListItemUI> _items
        = new Dictionary<PlayerRef, PlayerListItemUI>();

    private void Awake()
    {
        _runner = FindAnyObjectByType<NetworkRunner>();
    }

    public void RefreshList()
    {
        ClearList();

        foreach (var player in _runner.ActivePlayers)
        {
            PlayerNetworkData data = FindPlayerData(player);

            if (data == null)
                continue;

            CreateItem(player, data);
        }
    }

    private void CreateItem(PlayerRef player, PlayerNetworkData data)
    {
        var item = Instantiate(playerItemPrefab, content);

        item.Setup(data);

        _items[player] = item;
    }

    private void ClearList()
    {
        foreach (var item in _items.Values)
        {
            Destroy(item.gameObject);
        }

        _items.Clear();
    }

    private PlayerNetworkData FindPlayerData(PlayerRef player)
    {
        foreach (var obj in FindObjectsByType<PlayerNetworkData>(FindObjectsSortMode.None))
        {
            if (obj.Object.InputAuthority == player)
                return obj;
        }

        return null;
    }
}