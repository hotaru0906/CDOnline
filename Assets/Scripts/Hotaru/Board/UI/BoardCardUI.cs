using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections;

public class BoardCardUI : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("References")]
    [SerializeField] private Image    cardImage;
    [SerializeField] private TMP_Text descText;

    [Header("Hover")]
    [SerializeField] private float hoverRiseAmount = 40f;
    [SerializeField] private float hoverSpeed      = 10f;

    public BoardItemEffect Effect   { get; private set; }
    public int             ItemSlot { get; private set; }
    public int             OwnerId  { get; private set; }

    private bool          _isLocal;
    private Vector2       _basePosition;
    private RectTransform _rect;
    private Coroutine     _moveRoutine;

    public System.Action<BoardCardUI> OnCardClicked;

    private void Awake() => _rect = GetComponent<RectTransform>();

    // =====================================================================
    // INIT
    // =====================================================================

    public void Initialize(BoardItemData data, BoardItemEffect effect, int itemSlot, int ownerId, bool isLocal)
    {
        Effect   = effect;
        ItemSlot = itemSlot;
        OwnerId  = ownerId;
        _isLocal = isLocal;

        if (cardImage != null) cardImage.sprite = data?.icon;
        if (descText  != null) descText.text    = data?.itemName ?? effect.ToString();

        // Tất cả đều thấy mặt trước — không còn backFace
        foreach (var g in GetComponentsInChildren<Graphic>())
            g.raycastTarget = isLocal; // chỉ local mới click được
    }

    public void SetBasePosition(Vector2 pos)
    {
        _basePosition          = pos;
        _rect.anchoredPosition = pos;
    }

    // =====================================================================
    // HOVER — gửi RPC để sync với tất cả clients
    // =====================================================================

    public void OnPointerEnter(PointerEventData _)
    {
        if (!_isLocal) return;
        _rect.SetAsLastSibling();
        BoardManager.Instance?.RPC_CardHoverEnter(OwnerId, ItemSlot);
    }

    public void OnPointerExit(PointerEventData _)
    {
        if (!_isLocal) return;
        BoardManager.Instance?.RPC_CardHoverExit(OwnerId, ItemSlot);
    }

    public void OnPointerClick(PointerEventData _)
    {
        if (!_isLocal) return;
        OnCardClicked?.Invoke(this);
    }

    // =====================================================================
    // MOVEMENT — gọi trực tiếp từ BoardInventoryUI khi nhận RPC
    // =====================================================================

    public void AnimateHoverEnter()
    {
        _rect.SetAsLastSibling();
        MoveTo(_basePosition + Vector2.up * hoverRiseAmount);
    }

    public void AnimateHoverExit()
    {
        MoveTo(_basePosition);
    }

    private void MoveTo(Vector2 target)
    {
        if (_moveRoutine != null) StopCoroutine(_moveRoutine);
        _moveRoutine = StartCoroutine(MoveRoutine(target));
    }

    private IEnumerator MoveRoutine(Vector2 target)
    {
        while (Vector2.Distance(_rect.anchoredPosition, target) > 0.5f)
        {
            _rect.anchoredPosition = Vector2.Lerp(
                _rect.anchoredPosition, target, Time.deltaTime * hoverSpeed);
            yield return null;
        }
        _rect.anchoredPosition = target;
        _moveRoutine           = null;
    }
}