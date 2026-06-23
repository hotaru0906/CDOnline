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
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private Image characterIcon;
    [SerializeField] private GameObject localPlayerHighlight;

    [Header("Character Icons")]
    [Tooltip("Index khớp với CharacterIndex (0-3)")]
    [SerializeField] private Sprite[] characterIcons;

    [Header("Colors")]
    [SerializeField] private Color readyColor = Color.green;
    [SerializeField] private Color notReadyColor = Color.gray;

    private PlayerNetworkData _playerData;

    /// <summary>
    /// Set data cho item
    /// </summary>
    public void SetData(PlayerNetworkData player)
    {
        _playerData = player;

        if (_playerData == null) return;

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
        if (_playerData == null) return;

        // Update name (có thể thay đổi)
        if (playerNameText != null)
            playerNameText.text = _playerData.PlayerName.ToString();

        // Ready status
        if (statusText != null)
        {
            statusText.text = _playerData.IsReady ? "READY" : "NOT READY";
            statusText.color = _playerData.IsReady ? readyColor : notReadyColor;
        }

        // Icon theo CharacterIndex
        if (characterIcon != null && characterIcons != null)
        {
            int index = _playerData.CharacterIndex;
            if (index >= 0 && index < characterIcons.Length && characterIcons[index] != null)
            {
                characterIcon.sprite = characterIcons[index];
            }
        }
    }
}