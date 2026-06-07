using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerRowUI : MonoBehaviour
{
    // ========================================================
    // INSPECTOR REFERENCES
    // ========================================================

    [Header("--- ICON ---")]
    public Image playerIcon;
    public Sprite defaultIcon;

    [Header("--- INFO ---")]
    public TextMeshProUGUI playerNameText;
    public TextMeshProUGUI itemCountText;

    [Header("--- HIGHLIGHT (TURN) ---")]
    [Tooltip("Image viền sáng khi đến lượt")]
    public Image highlightBorder;

    [Tooltip("Màu khi đang có lượt")]
    public Color activeColor   = new Color(100f/255f, 180f/255f, 255f/255f, 1f);

    [Tooltip("Màu khi không có lượt")]
    public Color inactiveColor = new Color(1f, 1f, 1f, 0f);

    [Header("--- DEBUG TEST ---")]
    [Tooltip("Test tăng item trong Inspector")]
    public bool debugAddItem = false;

    // ========================================================
    // RUNTIME
    // ========================================================
    private int currentItemCount = 0;

    // ========================================================
    // UPDATE (chỉ để test Inspector)
    // ========================================================
    void Update()
    {
        if (debugAddItem)
        {
            debugAddItem = false;
            AddItem(1);
        }
    }

    // ========================================================
    // SETUP
    // ========================================================

    public void Setup(string name, Sprite icon, int itemCount, bool isCurrentTurn)
    {
        // Tên
        if (playerNameText != null)
            playerNameText.text = name;

        // Icon
        if (playerIcon != null)
            playerIcon.sprite = (icon != null) ? icon : defaultIcon;

        // Item count
        SetItemCount(itemCount);

        // Highlight lượt
        SetHighlight(isCurrentTurn);
    }

    // ========================================================
    // ITEM COUNT
    // ========================================================

    public void SetItemCount(int count)
    {
        currentItemCount = count;

        if (itemCountText != null)
            itemCountText.text = $"Item: {currentItemCount}";
    }

    public void AddItem(int amount = 1)
    {
        SetItemCount(currentItemCount + amount);
        Debug.Log($"[PlayerRow] {playerNameText?.text} → Item: {currentItemCount}");
    }

    // ========================================================
    // HIGHLIGHT TURN
    // ========================================================

    public void SetHighlight(bool isActive)
    {
        if (highlightBorder == null) return;

        highlightBorder.gameObject.SetActive(isActive);
        highlightBorder.color = isActive ? activeColor : inactiveColor;
    }
}