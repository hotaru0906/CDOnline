using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Fusion;

public class MinigamePlayerRankUI : MonoBehaviour
{
    [SerializeField] private MinigamePlayerEntryUI[] entries = new MinigamePlayerEntryUI[4];

    private static readonly Color[] SlotColors =
    {
        new Color(0.9f, 0.2f, 0.2f),
        new Color(0.2f, 0.4f, 0.9f),
        new Color(0.2f, 0.8f, 0.2f),
        new Color(0.95f, 0.8f, 0.1f)
    };

    private void Start()
    {
        StartCoroutine(WaitAndRefresh());
    }

    private IEnumerator WaitAndRefresh()
    {
        while (BaseMinigameController.Instance == null ||
               BaseMinigameController.Instance.Object == null ||
               !BaseMinigameController.Instance.Object.IsValid)
            yield return null;

        // Chờ game started thay vì chỉ chờ Instance
        while (!BaseMinigameController.Instance.IsGameStarted)
            yield return null;

        yield return null;
        Refresh();
    }

    public void Refresh()
    {
        var players = Object.FindObjectsByType<PlayerMinigameData>(FindObjectsSortMode.None);
        var list = new List<PlayerMinigameData>(players);
        list.Sort((a, b) =>
            a.Object.InputAuthority.PlayerId.CompareTo(b.Object.InputAuthority.PlayerId));

        // Ẩn tất cả entries trước
        for (int i = 0; i < entries.Length; i++)
            if (entries[i] != null) entries[i].gameObject.SetActive(false);

        // Chỉ hiện đúng số player thật
        for (int i = 0; i < list.Count && i < entries.Length; i++)
        {
            if (entries[i] == null) continue;

            var p = list[i];
            var net = p.GetComponent<PlayerNetworkData>();
            string name = net != null
                ? net.PlayerName.ToString()
                : $"P{p.Object.InputAuthority.PlayerId}";

            // Chờ nếu tên chưa sync
            if (string.IsNullOrEmpty(name) || name.StartsWith("Player 0"))
                name = $"P{p.Object.InputAuthority.PlayerId}";

            entries[i].gameObject.SetActive(true);
            entries[i].SetData(
                playerId: p.Object.InputAuthority.PlayerId,
                playerName: name,
                lives: p.Lives,
                slotColor: SlotColors[i],
                isEliminated: p.IsEliminated
            );

            if (net != null)
            {
                entries[i].SetCharacterAvatar(net.CharacterIndex);
            }

        }
    }

    public void UpdateLivesForPlayer(int playerId, int lives)
    {
        foreach (var e in entries)
        {
            if (e == null || e.PlayerId != playerId) continue;
            e.UpdateLives(lives);
            return;
        }
    }

    public void MarkEliminated(int playerId)
    {
        foreach (var e in entries)
        {
            if (e == null || e.PlayerId != playerId) continue;
            e.SetEliminated(true);
            return;
        }
    }
}