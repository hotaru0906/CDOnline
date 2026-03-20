using UnityEngine;

/// <summary>
/// Static class lưu trữ lựa chọn nhân vật local (trước khi vào lobby)
/// Khi vào lobby, PlayerNetworkData sẽ đọc và sync lên network
/// </summary>
public static class CharacterSelectionData
{
    private const string PREF_CHARACTER_INDEX = "SelectedCharacterIndex";
    private const string PREF_PLAYER_NAME = "PlayerName";

    /// <summary>
    /// Index nhân vật đã chọn (0-3)
    /// </summary>
    public static int SelectedCharacterIndex
    {
        get => PlayerPrefs.GetInt(PREF_CHARACTER_INDEX, 0);
        set
        {
            PlayerPrefs.SetInt(PREF_CHARACTER_INDEX, Mathf.Clamp(value, 0, 3));
            PlayerPrefs.Save();
        }
    }

    /// <summary>
    /// Tên người chơi
    /// </summary>
    public static string PlayerName
    {
        get => PlayerPrefs.GetString(PREF_PLAYER_NAME, "Player");
        set
        {
            PlayerPrefs.SetString(PREF_PLAYER_NAME, string.IsNullOrWhiteSpace(value) ? "Player" : value);
            PlayerPrefs.Save();
        }
    }
}
