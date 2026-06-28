using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// MGUI — Player card hiển thị Icon, Tên, Score/HP trong gameplay.
/// Gắn vào PlayerCard prefab.
/// Dựa trên PlayerInfoItemUI, thay ready status bằng score.
/// </summary>
public class MGUIPlayerCard : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_Text playerNameText;
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private Image characterIcon;
    [SerializeField] private GameObject localPlayerHighlight;

    [Header("Character Icons")]
    [Tooltip("Index khớp với CharacterIndex (0-3)")]
    [SerializeField] private Sprite[] characterIcons;

    private PlayerNetworkData _playerData;
    private int _currentScore = 0;

    /// <summary>
    /// Gọi 1 lần khi khởi tạo card — assign PlayerNetworkData.
    /// </summary>
    public void SetData(PlayerNetworkData player)
    {
        _playerData = player;
        if (_playerData == null) return;

        // Tên player
        if (playerNameText != null)
            playerNameText.text = _playerData.PlayerName.ToString();

        // Highlight nếu là local player
        if (localPlayerHighlight != null)
            localPlayerHighlight.SetActive(_playerData.Object.HasInputAuthority);

        // Icon theo CharacterIndex
        if (characterIcon != null && characterIcons != null)
        {
            int index = _playerData.CharacterIndex;
            if (index >= 0 && index < characterIcons.Length && characterIcons[index] != null)
                characterIcon.sprite = characterIcons[index];
        }

        // Score khởi tạo = 0
        UpdateScore(0);
    }

    /// <summary>
    /// Cập nhật tên nếu thay đổi runtime.
    /// </summary>
    public void UpdateData()
    {
        if (_playerData == null) return;

        if (playerNameText != null)
            playerNameText.text = _playerData.PlayerName.ToString();

        // Icon cập nhật lại nếu CharacterIndex thay đổi
        if (characterIcon != null && characterIcons != null)
        {
            int index = _playerData.CharacterIndex;
            if (index >= 0 && index < characterIcons.Length && characterIcons[index] != null)
                characterIcon.sprite = characterIcons[index];
        }
    }

    /// <summary>
    /// Gọi từ bên ngoài để cập nhật điểm.
    /// </summary>
    public void UpdateScore(int score)
    {
        _currentScore = score;
        if (scoreText != null)
            scoreText.text = $"Score: {score}";
    }

    /// <summary>
    /// Gọi từ bên ngoài để cập nhật HP thay vì Score.
    /// </summary>
    public void UpdateHP(int current, int max)
    {
        if (scoreText != null)
            scoreText.text = $"HP: {current}/{max}";
    }

    public PlayerNetworkData PlayerData => _playerData;
    public int CurrentScore => _currentScore;
}