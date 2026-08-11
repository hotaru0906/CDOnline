using UnityEngine;
using TMPro;
using System.Collections;

public class ItemPickUIController : MonoBehaviour
{
    [Header("Cards (đúng thứ tự slot 0-3)")]
    [SerializeField] private ItemPickCardView[] cards = new ItemPickCardView[4];

    [Header("Texts")]
    [SerializeField] private TMP_Text turnAnnouncementText; // "[Name] is picking a card"
    [SerializeField] private TMP_Text timerText;

    private void OnEnable()
    {
        TrySubscribe();
        SyncCurrentStateImmediately();
    }

    private void SyncCurrentStateImmediately()
    {
        if (GameManager.Instance == null) return;
        if (GameManager.Instance.CurrentState != GameState.PickItem) return;

        for (int i = 0; i < cards.Length; i++)
        {
            if (cards[i] == null) continue;

            if (GameManager.Instance.IsItemSlotTaken(i))
                cards[i].gameObject.SetActive(false);
            else
                cards[i].Setup(i, OnCardClicked);
        }

        int turnPlayerId = GameManager.Instance.ItemPickTurnPlayerId;
        if (turnPlayerId >= 0)
            HandleTurnStarted(turnPlayerId, 0f);
    }
    private void OnDisable()
    {
        if (GameManager.Instance == null) return;
        GameManager.Instance.OnItemPickPoolChanged -= HandlePoolChanged;
        GameManager.Instance.OnItemPickTurnStarted -= HandleTurnStarted;
        GameManager.Instance.OnItemPickTimerTick -= HandleTimerTick;
        GameManager.Instance.OnItemPicked -= HandleItemPicked;
        GameManager.Instance.OnItemPickPhaseEnded -= HandlePhaseEnded;
    }

    private void TrySubscribe()
    {
        if (GameManager.Instance == null)
        {
            StartCoroutine(WaitAndSubscribe());
            return;
        }

        GameManager.Instance.OnItemPickPoolChanged += HandlePoolChanged;
        GameManager.Instance.OnItemPickTurnStarted += HandleTurnStarted;
        GameManager.Instance.OnItemPickTimerTick += HandleTimerTick;
        GameManager.Instance.OnItemPicked += HandleItemPicked;
        GameManager.Instance.OnItemPickPhaseEnded += HandlePhaseEnded;
    }

    private IEnumerator WaitAndSubscribe()
    {
        while (GameManager.Instance == null) yield return null;
        TrySubscribe();
    }

    private void HandlePoolChanged()
    {
        for (int i = 0; i < cards.Length; i++)
        {
            if (cards[i] == null) continue;
            int slotIndex = i;
            cards[i].Setup(slotIndex, OnCardClicked);
        }
    }

    private void HandleTurnStarted(int playerId, float duration)
    {
        bool isLocalTurn = IsLocalPlayer(playerId);

        var pnd = GameManager.Instance.GetPlayerNetworkData(playerId);
        string displayName = pnd != null ? pnd.PlayerName.ToString() : $"Player {playerId}";

        if (turnAnnouncementText != null)
            turnAnnouncementText.text = $"{displayName} is picking a card";

        foreach (var card in cards)
        {
            if (card == null) continue;
            // chỉ bật interactable cho card CHƯA bị lấy
            bool taken = GameManager.Instance.IsItemSlotTaken(System.Array.IndexOf(cards, card));
            card.SetInteractable(isLocalTurn && !taken);
        }
    }

    private void HandleTimerTick(int remainingSeconds)
    {
        if (timerText != null) timerText.text = remainingSeconds.ToString();
    }

    private void HandleItemPicked(int playerId, int slotIndex, BoardItemEffect effect)
    {
        if (slotIndex < 0 || slotIndex >= cards.Length) return;
        var card = cards[slotIndex];
        if (card == null) return;

        if (IsLocalPlayer(playerId))
        {
            var itemData = GameManager.Instance.GetItemPickSlotData(slotIndex);
            card.PlayRevealThenDisappear(itemData != null ? itemData.icon : null);
        }
        else
        {
            card.PlayDisappearOnly();
        }
    }

    private void HandlePhaseEnded()
    {
        if (turnAnnouncementText != null) turnAnnouncementText.text = string.Empty;
        if (timerText != null) timerText.text = string.Empty;
    }

    private void OnCardClicked(int slotIndex)
    {
        if (PlayerNetworkData.Local == null) return;
        int localPlayerId = PlayerNetworkData.Local.Object.InputAuthority.PlayerId;
        GameManager.Instance.PickItem(localPlayerId, slotIndex);
    }

    private bool IsLocalPlayer(int playerId)
    {
        return PlayerNetworkData.Local != null &&
               PlayerNetworkData.Local.Object.InputAuthority.PlayerId == playerId;
    }
}