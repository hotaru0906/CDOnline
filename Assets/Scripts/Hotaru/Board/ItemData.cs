using UnityEngine;

/// <summary>
/// Dữ liệu của 1 item — tạo bằng Create > Board > Item Data trong Project window.
/// </summary>
[CreateAssetMenu(menuName = "Board/Item Data")]
public class ItemData : ScriptableObject
{
    [Header("Info")]
    public string itemName = "Unknown Item";

    [TextArea]
    public string description;

    [Header("Effect")]
    public ItemEffect effectType = ItemEffect.None;

    [Header("Visual")]
    public Sprite icon;
}
