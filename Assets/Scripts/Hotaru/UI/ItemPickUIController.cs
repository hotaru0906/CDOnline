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

    [Header("SFX")]
    [SerializeField] private AudioClip flipCardSfx; // MỚI: âm thanh lật bài, chỉ người lật nghe

    private bool _localIsTop1; // MỚI

    private void OnEnable()
    {
        TrySubscribe();
        RefreshLocalTop1Flag();
        SyncCurrentStateImmediately();
    }

    private void RefreshLocalTop1Flag() // MỚI
    {
        _localIsTop1 = false;
        if (GameManager.Instance == null || PlayerNetworkData.Local == null) return;

        var ranking = GameManager.Instance.GetLastMinigameRanking();
        if (ranking.Length == 0) return;

        int localPlayerId = PlayerNetworkData.Local.Object.InputAuthority.PlayerId;
        _localIsTop1 = ranking[0] == localPlayerId;
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
        GameManager.Instance.OnTop1RevealAllCards -= HandleTop1RevealAll;       // MỚI
        GameManager.Instance.OnTop1HideRemainingCards -= HandleTop1HideRemaining; // MỚI
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
        GameManager.Instance.OnTop1RevealAllCards += HandleTop1RevealAll;         // MỚI
        GameManager.Instance.OnTop1HideRemainingCards += HandleTop1HideRemaining; // MỚI
    }

    private IEnumerator WaitAndSubscribe()
    {
        while (GameManager.Instance == null) yield return null;
        TrySubscribe();
    }

    private void HandlePoolChanged()
    {
        RefreshLocalTop1Flag(); // MỚI: pool mới nghĩa là round mới, ranking có thể đổi

        for (int i = 0; i < cards.Length; i++)
        {
            if (cards[i] == null) continue;
            int slotIndex = i;
            cards[i].Setup(slotIndex, OnCardClicked);
        }
    }

    // MỚI: Top1 vào lượt -> lật ngửa toàn bộ thẻ còn lại (chỉ hiện với chính Top1)
    private void HandleTop1RevealAll()
    {
        if (!_localIsTop1) return;

        bool playedSound = false;
        for (int i = 0; i < cards.Length; i++)
        {
            if (cards[i] == null || !cards[i].gameObject.activeSelf) continue;
            if (GameManager.Instance.IsItemSlotTaken(i)) continue;

            var itemData = GameManager.Instance.GetItemPickSlotData(i);
            cards[i].RevealFaceUp(itemData != null ? itemData.icon : null);

            if (!playedSound)
            {
                PlayFlipSfx();
                playedSound = true; // chỉ phát 1 lần cho cả loạt lật, không lặp 4 lần
            }
        }
    }

    // MỚI: Top1 vừa chọn xong -> úp lại các thẻ còn lại
    private void HandleTop1HideRemaining()
    {
        if (!_localIsTop1) return;

        for (int i = 0; i < cards.Length; i++)
        {
            if (cards[i] == null || !cards[i].gameObject.activeSelf) continue;
            if (GameManager.Instance.IsItemSlotTaken(i)) continue;

            cards[i].HideFaceDown();
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

        bool isLocalPicker = IsLocalPlayer(playerId);

        // MỚI: người thực sự lật (chính mình pick) hoặc Top1 đang xem ké -> thấy full reveal
        if (isLocalPicker || _localIsTop1)
        {
            var itemData = GameManager.Instance.GetItemPickSlotData(slotIndex);
            card.PlayRevealThenDisappear(itemData != null ? itemData.icon : null);
        }
        else
        {
            card.PlayDisappearOnly();
        }

        // MỚI: chỉ người thực sự lật mới nghe âm thanh - Top1 xem ké KHÔNG nghe
        if (isLocalPicker)
        {
            PlayFlipSfx();
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

    private void PlayFlipSfx() // MỚI
    {
        if (SFXManager.Instance != null && flipCardSfx != null)
            SFXManager.Instance.PlaySFX(flipCardSfx);
    }
}