using UnityEngine;

/// <summary>
/// Dữ liệu của 1 Board item — tạo bằng Create > Board > Board Item Data trong Project window.
/// </summary>
[CreateAssetMenu(menuName = "Board/Board Item Data")]
public class BoardItemData : ScriptableObject
{
    [Header("Info")]
    public string itemName = "Unknown Board Item";

    [TextArea]
    public string description;

    [Header("Effect")]
    public BoardItemEffect effectType = BoardItemEffect.None;

    [Header("Rarity")]
    [Tooltip("Common=6, Rare=3, Legendary=1 — ảnh hưởng xác suất xuất hiện trong pool")]
    public ItemRarity rarity = ItemRarity.Common;

    [Header("Visual")]
    public Sprite icon;
}
