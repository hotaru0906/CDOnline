using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Item UI cho mỗi player trong Player Info panel
/// </summary>
public class PlayerInfoItemUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_Text playerNameText;
    [SerializeField] private Image statusImage;
    [SerializeField] private Image characterIcon;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private GameObject localPlayerHighlight;

    [Header("Character Icons")]
    [Tooltip("Index khớp với CharacterIndex (0-3)")]
    [SerializeField] private Sprite[] characterIcons;

    [Header("Character Backgrounds")]
    [Tooltip("Index khớp với CharacterIndex (0-3). Drag từng background tương ứng: Panda, Ếch, Thỏ, Chồn")]
    [SerializeField] private Sprite[] characterBackgrounds;

    [Header("Status Images")]
    [SerializeField] private Sprite readySprite;
    [SerializeField] private Sprite notReadySprite;

    private PlayerNetworkData _playerData;

    /// <summary>
    /// Set data cho item
    /// </summary>
    public void SetData(PlayerNetworkData player)
    {
        _playerData = player;

        if (_playerData == null)
            return;

        if (_playerData.Object == null || !_playerData.Object.IsValid)
            return;

        // Player name
        if (playerNameText != null)
            playerNameText.text = _playerData.PlayerName.ToString();

        // Local player highlight
        if (localPlayerHighlight != null)
            localPlayerHighlight.SetActive(_playerData.Object.HasInputAuthority);

        UpdateData();
    }

    /// <summary>
    /// Update realtime data
    /// </summary>
    public void UpdateData()
    {
        if (_playerData == null)
            return;

        // Object chưa Spawn hoặc đã Despawn
        if (_playerData.Object == null || !_playerData.Object.IsValid)
            return;

        // Update name (có thể thay đổi)
        if (playerNameText != null)
            playerNameText.text = _playerData.PlayerName.ToString();

        // Ready status image
        if (statusImage != null)
        {
            statusImage.sprite = _playerData.IsReady ? readySprite : notReadySprite;
            statusImage.enabled = statusImage.sprite != null;
        }

        int index = _playerData.CharacterIndex;

        // Background theo CharacterIndex
        if (backgroundImage != null)
        {
            if (index >= 0 && index < characterBackgrounds.Length && characterBackgrounds[index] != null)
            {
                backgroundImage.sprite = characterBackgrounds[index];
                backgroundImage.enabled = true;
            }
            else if (backgroundImage.sprite == null)
            {
                backgroundImage.enabled = false;
            }
        }

        // Icon theo CharacterIndex
        if (characterIcon != null && characterIcons != null)
        {
            if (index >= 0 && index < characterIcons.Length && characterIcons[index] != null)
            {
                characterIcon.sprite = characterIcons[index];
            }
        }
    }
}