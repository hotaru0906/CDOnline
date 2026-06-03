using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Một ô item trong inventory — icon, số lượng, background.
///
/// SETUP (prefab "ItemSlot"):
///   ├── SlotBG     (Image)           ← slotBackground
///   ├── ItemIcon   (Image ~56x56)    ← iconImage
///   └── CountText  (TMP_Text)        ← countText
/// </summary>
public class BoardItemSlotUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Image    iconImage;
    [SerializeField] private TMP_Text countText;
    [SerializeField] private Image    slotBackground;

    [Header("Colors")]
    [SerializeField] private Color emptyBg  = new Color(0.15f, 0.15f, 0.15f, 0.6f);
    [SerializeField] private Color filledBg = new Color(0.12f, 0.35f, 0.65f, 0.85f);

    public void SetItem(BoardItemData data, int count)
    {
        if (iconImage != null)
        {
            iconImage.sprite = data != null ? data.icon : null;
            iconImage.color  = data != null ? Color.white : new Color(1f, 1f, 1f, 0.2f);
        }

        if (countText != null)
        {
            countText.gameObject.SetActive(count > 1);
            countText.text = $"x{count}";
        }

        if (slotBackground != null)
            slotBackground.color = filledBg;
    }

    public void SetEmpty()
    {
        if (iconImage != null)
        {
            iconImage.sprite = null;
            iconImage.color  = new Color(1f, 1f, 1f, 0.2f);
        }

        if (countText != null)
            countText.gameObject.SetActive(false);

        if (slotBackground != null)
            slotBackground.color = emptyBg;
    }
}

