using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections;

/// <summary>
/// 1 lá bài trong tay player.
/// SETUP (prefab "BoardCard"):
///   ├── FrontFace  (GameObject)
///   │     ├── CardImage  (Image)     ← cardImage
///   │     └── DescText   (TMP_Text)  ← descText
///   └── BackFace   (GameObject)      ← backFace
/// </summary>
public class BoardCardUI : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("References")]
    [SerializeField] private GameObject frontFace;
    [SerializeField] private GameObject backFace;
    [SerializeField] private Image      cardImage;
    [SerializeField] private TMP_Text   descText;

    [Header("Hover")]
    [SerializeField] private float hoverRiseAmount = 40f;
    [SerializeField] private float hoverSpeed      = 10f;

    public BoardItemEffect Effect    { get; private set; }
    public int             ItemSlot  { get; private set; }

    private bool          _isLocal;
    private Vector2       _basePosition;
    private RectTransform _rect;
    private Coroutine     _moveRoutine;

    public System.Action<BoardCardUI> OnCardClicked;

    private void Awake()
    {
        _rect = GetComponent<RectTransform>();
    }

    // =====================================================================
    // INIT
    // =====================================================================

    public void Initialize(BoardItemData data, BoardItemEffect effect, int itemSlot, bool isLocal)
    {
        Effect   = effect;
        ItemSlot = itemSlot;
        _isLocal = isLocal;

        if (cardImage != null) cardImage.sprite = data?.icon;
        if (descText  != null) descText.text    = data?.description ?? effect.ToString();

        frontFace?.SetActive(isLocal);
        backFace?.SetActive(!isLocal);

        // Chỉ local player mới interact được
        foreach (var g in GetComponentsInChildren<Graphic>())
            g.raycastTarget = isLocal;
    }

    public void SetBasePosition(Vector2 pos)
    {
        _basePosition          = pos;
        _rect.anchoredPosition = pos;
    }

    // =====================================================================
    // HOVER & CLICK
    // =====================================================================

    public void OnPointerEnter(PointerEventData _)
    {
        if (!_isLocal) return;
        _rect.SetAsLastSibling();
        MoveTo(_basePosition + Vector2.up * hoverRiseAmount);
    }

    public void OnPointerExit(PointerEventData _)
    {
        if (!_isLocal) return;
        MoveTo(_basePosition);
    }

    public void OnPointerClick(PointerEventData _)
    {
        if (!_isLocal) return;
        OnCardClicked?.Invoke(this);
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
        _moveRoutine = null;
    }
}