using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class CardUI : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("References")]
    public Image cardArtImage;
    public TextMeshProUGUI cardNameText;
    public TextMeshProUGUI cornerTL;
    public TextMeshProUGUI cornerBR;

    [HideInInspector] public CardData data;
    [HideInInspector] public int cardIndex;

    private Vector3 basePosition;
    private Quaternion baseRotation;
    private bool isSelected = false;

    private InventoryManager manager;

    public void Setup(CardData cardData, int index, InventoryManager mgr)
    {
        data = cardData;
        cardIndex = index;
        manager = mgr;

        // Hiển thị art
        if (cardData.cardArt != null)
            cardArtImage.sprite = cardData.cardArt;

        // Hiển thị tên
        cardNameText.text = cardData.cardName.ToUpper();

        // Hiển thị số lượng ở 2 góc
        RefreshQuantity();
    }

    public void RefreshQuantity()
    {
        cornerTL.text = data.quantity.ToString();
        cornerBR.text = data.quantity.ToString();
    }

    // Lưu vị trí gốc sau khi InventoryManager đã xếp bài xong
    public void SaveBaseTransform()
    {
        basePosition = transform.localPosition;
        baseRotation = transform.localRotation;
    }

    public void SetSelected(bool selected)
{
    isSelected = selected;
    if (selected)
        transform.localPosition = basePosition + Vector3.up * 30f;
    else
        transform.localPosition = basePosition;
}

    public void OnPointerClick(PointerEventData eventData)
    {
        manager.OnCardClicked(cardIndex);
    }

  public void OnPointerEnter(PointerEventData eventData)
{
    if (!isSelected)
        transform.localPosition = basePosition + Vector3.up * 15f;
}
  public void OnPointerExit(PointerEventData eventData)
{
    if (!isSelected)
        transform.localPosition = basePosition;
}
}