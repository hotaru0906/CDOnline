using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Fan bài của 1 player.
/// SETUP:
///   1. Tạo 4 GameObject bottom canvas, mỗi cái attach script này
///   2. Assign cardPrefab (prefab BoardCardUI)
/// </summary>
public class BoardHandUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject cardPrefab;

    [Header("Fan Layout — Expanded")]
    [SerializeField] private float expandedCardSpacing = 70f;
    [SerializeField] private float expandedFanAngle = 6f;
    [SerializeField] private float expandedScale = 1f;

    [Header("Fan Layout — Collapsed")]
    [SerializeField] private float collapsedCardSpacing = 20f;
    [SerializeField] private float collapsedFanAngle = 2f;
    [SerializeField] private float collapsedScale = 0.5f;

    [Header("Animation")]
    [SerializeField] private float expandSpeed = 6f;

    public int PlayerId { get; private set; } = -1;
    public bool IsLocalPlayer { get; private set; }

    private bool _itemUsedThisTurn = false;
    private bool _isExpanded = false;
    private readonly List<BoardCardUI> _cards = new();
    private RectTransform _rect;
    private Coroutine _scaleRoutine;
    private CanvasGroup _canvasGroup;

    private void Awake()
    {
        _rect = GetComponent<RectTransform>();

        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    // =====================================================================
    // INIT
    // =====================================================================

    public void Initialize(int playerId, bool isLocal)
    {
        PlayerId = playerId;
        IsLocalPlayer = isLocal;
        RefreshHand();
    }

    // =====================================================================
    // EXPAND / COLLAPSE
    // =====================================================================
    public void SetItemUsed()
    {
        _itemUsedThisTurn = true;
        Collapse();
    }

    public void ResetItemUsed()
    {
        _itemUsedThisTurn = false;
    }
    public void Expand()
    {
        if (_itemUsedThisTurn) return; // không expand nếu đã dùng item
        _isExpanded = true;
        ArrangeCards();
        ScaleTo(expandedScale);
    }

    public void Collapse()
    {
        _isExpanded = false;
        ArrangeCards();
        ScaleTo(collapsedScale);
    }

    public void Show()
    {
        gameObject.SetActive(true);

        _canvasGroup.alpha = 1f;
        _canvasGroup.interactable = true;
        _canvasGroup.blocksRaycasts = true;
    }

    public void Hide()
    {
        _canvasGroup.alpha = 0f;
        _canvasGroup.interactable = false;
        _canvasGroup.blocksRaycasts = false;
    }

    // =====================================================================
    // REFRESH — đọc inventory và rebuild cards
    // =====================================================================

    public void RefreshHand()
    {
        if (PlayerId < 0) return;

        var inv = PlayerItemInventory.GetForPlayer(PlayerId);
        if (inv == null) { ClearCards(); return; }

        var pool = BoardItemPool.Current;

        var items = new List<(int slot, BoardItemEffect effect)>();
        for (int i = 0; i < 4; i++)
        {
            int raw = inv.BoardItems.Get(i);
            if (raw != -1) items.Add((i, (BoardItemEffect)raw));
        }

        // Rebuild luôn, không check count
        ClearCards();
        foreach (var (slot, effect) in items)
        {
            var data = pool?.GetByEffect(effect);
            SpawnCard(data, effect, slot);
        }

        ArrangeCards();
    }

    // =====================================================================
    // CARD SPAWN / REMOVE
    // =====================================================================

    private void SpawnCard(BoardItemData data, BoardItemEffect effect, int slot)
    {
        if (cardPrefab == null) return;

        var go = Instantiate(cardPrefab, transform);
        var card = go.GetComponent<BoardCardUI>();
        if (card == null) return;

        card.Initialize(data, effect, slot, IsLocalPlayer);
        card.OnCardClicked += OnCardClicked;
        _cards.Add(card);
    }

    public void RemoveCard(BoardCardUI card)
    {
        if (_cards.Contains(card))
        {
            _cards.Remove(card);
            Destroy(card.gameObject);
        }
        ArrangeCards();
    }

    private void ClearCards()
    {
        foreach (var c in _cards)
            if (c != null) Destroy(c.gameObject);
        _cards.Clear();
    }

    // =====================================================================
    // LAYOUT
    // =====================================================================

    private void ArrangeCards()
    {
        int count = _cards.Count;
        if (count == 0) return;

        float spacing = _isExpanded ? expandedCardSpacing : collapsedCardSpacing;
        float angle = _isExpanded ? expandedFanAngle : collapsedFanAngle;

        float totalWidth = spacing * (count - 1);
        float startX = -totalWidth / 2f;

        for (int i = 0; i < count; i++)
        {
            if (_cards[i] == null) continue;

            float x = startX + i * spacing;
            float rotation = angle * (((count - 1) / 2f) - i);
            var pos = new Vector2(x, 0f);

            _cards[i].SetBasePosition(pos);
            _cards[i].transform.localRotation = Quaternion.Euler(0f, 0f, rotation);
            _cards[i].transform.SetSiblingIndex(i);
        }
    }

    // =====================================================================
    // SCALE ANIMATION
    // =====================================================================

    private void ScaleTo(float targetScale)
    {
        if (!gameObject.activeInHierarchy)
        {
            // Object đang tắt (hand không dùng tới) — set thẳng scale, không cần animate
            _rect.localScale = Vector3.one * targetScale;
            return;
        }

        if (_scaleRoutine != null) StopCoroutine(_scaleRoutine);
        _scaleRoutine = StartCoroutine(ScaleRoutine(targetScale));
    }

    private IEnumerator ScaleRoutine(float target)
    {
        while (Mathf.Abs(_rect.localScale.x - target) > 0.01f)
        {
            float s = Mathf.Lerp(_rect.localScale.x, target, Time.deltaTime * expandSpeed);
            _rect.localScale = Vector3.one * s;
            yield return null;
        }
        _rect.localScale = Vector3.one * target;
        _scaleRoutine = null;
    }

    // =====================================================================
    // CARD CLICKED — chỉ local player, chỉ khi expanded
    // =====================================================================

    private void OnCardClicked(BoardCardUI card)
    {
        if (!_isExpanded || !IsLocalPlayer) return;

        var bm = BoardManager.Instance;
        if (bm == null) return;

        if (bm.BoardState != BoardPhaseState.WaitingForRoll) return;

        bm.RequestUseItem(card.ItemSlot, card.Effect);

        // KHÔNG RemoveCard ở đây — để RefreshHand tự sync từ inventory
        // Collapse hand sau khi dùng item
        Collapse();
    }
}