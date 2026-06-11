using UnityEngine;

/// <summary>
/// ScriptableObject lưu thông tin visual của 1 model nhân vật.
/// Tạo asset: Right-click > Create > Player > Model Data
/// Mỗi model tạo 1 asset riêng, kéo sprite avatar vào.
/// </summary>
[CreateAssetMenu(fileName = "NewPlayerModel", menuName = "Player/Model Data")]
public class PlayerModelData : ScriptableObject
{
    [Header("Hiển thị")]
    public string modelName = "Character Name";

    [Tooltip("Sprite avatar hiển thị trong PlayerRow — kéo image của model vào đây")]
    public Sprite avatarSprite;

    [Tooltip("Màu fallback nếu chưa có sprite")]
    public Color fallbackColor = new Color(0.23f, 0.51f, 0.96f);
}