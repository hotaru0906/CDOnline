using UnityEngine;
using System.Collections;

/// <summary>
/// MGUI — Tự động tìm player và gán data vào 4 card.
/// Gắn vào PlayerCardHolder.
/// </summary>
public class MGUIPlayerCardHolder : MonoBehaviour
{
    [SerializeField] private MGUIPlayerCard[] playerCards; // kéo 4 PlayerCard vào

    private void Start()
    {
        // Delay 1 frame để đảm bảo tất cả PlayerNetworkData đã Spawned
        StartCoroutine(PopulateCards());
    }

    private IEnumerator PopulateCards()
    {
        // Đợi 1 giây để Fusion spawn xong tất cả player
        yield return new WaitForSeconds(1f);

        // Ẩn tất cả card trước
        foreach (var card in playerCards)
            card.gameObject.SetActive(false);

        // Tìm tất cả player trong scene
        var allPlayers = FindObjectsByType<PlayerNetworkData>(FindObjectsSortMode.None);

        Debug.Log($"[MGUIPlayerCardHolder] Found {allPlayers.Length} players.");

        // Gán data vào từng card theo thứ tự
        for (int i = 0; i < playerCards.Length; i++)
        {
            if (i < allPlayers.Length)
            {
                playerCards[i].gameObject.SetActive(true);
                playerCards[i].SetData(allPlayers[i]);
                Debug.Log($"[MGUIPlayerCardHolder] Card {i} = {allPlayers[i].PlayerName}");
            }
        }
    }
}