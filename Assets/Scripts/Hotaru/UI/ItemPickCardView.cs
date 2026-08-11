using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class ItemPickCardView : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Image frontImage;      // Icon item (BoardItemData.icon)
    [SerializeField] private Image backImage;       // Mặt sau (đã có sẵn UI)
    [SerializeField] private Button pickButton;
    [SerializeField] private RectTransform cardTransform;

    [Header("Animation")]
    [SerializeField] private float flipDuration = 0.35f;
    [SerializeField] private float revealHoldDuration = 0.8f;
    [SerializeField] private float disappearDuration = 0.25f;

    private int _slotIndex;
    private System.Action<int> _onClicked;

    public void Setup(int slotIndex, System.Action<int> onClicked)
    {
        _slotIndex = slotIndex;
        _onClicked = onClicked;
        ResetVisual();

        pickButton.onClick.RemoveAllListeners();
        pickButton.onClick.AddListener(HandleClick);
    }

    public void ResetVisual()
    {
        cardTransform.DOKill();
        cardTransform.localScale = Vector3.one;
        gameObject.SetActive(true);

        frontImage.gameObject.SetActive(false);
        backImage.gameObject.SetActive(true);

        pickButton.interactable = false;
    }

    public void SetInteractable(bool interactable)
    {
        Debug.Log($"[ItemPickCardView] slot={_slotIndex} SetInteractable({interactable})");
        pickButton.interactable = interactable;
    }
    public void HandleClick()
    {
        pickButton.interactable = false;
        _onClicked?.Invoke(_slotIndex);
    }

    /// <summary>Chỉ gọi cho local player vừa chọn - lật lộ mặt rồi biến mất.</summary>
    public void PlayRevealThenDisappear(Sprite itemIcon)
    {
        pickButton.interactable = false;
        if (itemIcon != null) frontImage.sprite = itemIcon;

        Sequence seq = DOTween.Sequence();
        seq.Append(cardTransform.DOScaleX(0f, flipDuration * 0.5f).SetEase(Ease.InQuad));
        seq.AppendCallback(() =>
        {
            backImage.gameObject.SetActive(false);
            frontImage.gameObject.SetActive(true);
        });
        seq.Append(cardTransform.DOScaleX(1f, flipDuration * 0.5f).SetEase(Ease.OutQuad));
        seq.AppendInterval(revealHoldDuration);
        seq.Append(cardTransform.DOScale(0f, disappearDuration).SetEase(Ease.InBack));
        seq.OnComplete(() => gameObject.SetActive(false));
    }

    /// <summary>Client khác — chỉ thấy thẻ biến mất, không lộ mặt.</summary>
    public void PlayDisappearOnly()
    {
        pickButton.interactable = false;
        cardTransform.DOKill();
        cardTransform.DOScale(0f, disappearDuration)
            .SetEase(Ease.InBack)
            .OnComplete(() => gameObject.SetActive(false));
    }
}