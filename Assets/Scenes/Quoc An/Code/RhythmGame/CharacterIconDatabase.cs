using UnityEngine;

/// <summary>
/// Bảng tra từ CharacterIndex (0..3, đúng như PlayerNetworkData clamp) sang icon Sprite.
/// Tạo asset: Assets > Create > Game > Character Icon Database
///
/// Dùng chung cả icon trong lane của MG Rhythm lẫn bất kỳ chỗ nào khác cần icon nhân vật.
/// </summary>
[CreateAssetMenu(menuName = "Game/Character Icon Database")]
public class CharacterIconDatabase : ScriptableObject
{
    [System.Serializable]
    public class Entry
    {
        public string displayName;
        public Sprite icon;
        [Tooltip("Màu chủ đạo của nhân vật, dùng cho viền lane và hiệu ứng hit.")]
        public Color themeColor = Color.white;
    }

    [Tooltip("Thứ tự PHẢI khớp với CharacterIndex mà CharacterSelectionData trả về. Index 0 = nhân vật đầu tiên.")]
    public Entry[] characters = new Entry[4];

    public Sprite GetIcon(int characterIndex)
    {
        var e = Get(characterIndex);
        return e != null ? e.icon : null;
    }

    public Color GetThemeColor(int characterIndex)
    {
        var e = Get(characterIndex);
        return e != null ? e.themeColor : Color.white;
    }

    public Entry Get(int characterIndex)
    {
        if (characters == null) return null;
        if (characterIndex < 0 || characterIndex >= characters.Length) return null;
        return characters[characterIndex];
    }
}
