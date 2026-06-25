using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerInfoItemUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_Text playerNameText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private Image characterIcon;
    [SerializeField] private GameObject localPlayerHighlight;

    [Header("Colors")]
    [SerializeField] private Color readyColor    = Color.green;
    [SerializeField] private Color notReadyColor = Color.gray;

    private PlayerNetworkData _playerData;
    private Sprite[] _characterIcons;

    public void SetData(PlayerNetworkData player, Sprite[] characterIcons)
    {
        _playerData      = player;
        _characterIcons  = characterIcons;

        if (_playerData == null) return;

        if (playerNameText != null)
            playerNameText.text = _playerData.PlayerName.ToString();

        if (localPlayerHighlight != null)
            localPlayerHighlight.SetActive(_playerData.Object.HasInputAuthority);

        UpdateData();
    }

    public void UpdateData()
    {
        if (_playerData == null) return;

        if (playerNameText != null)
            playerNameText.text = _playerData.PlayerName.ToString();

        if (statusText != null)
        {
            statusText.text  = _playerData.IsReady ? "READY" : "NOT READY";
            statusText.color = _playerData.IsReady ? readyColor : notReadyColor;
        }

        if (characterIcon != null && _characterIcons != null)
        {
            int index = _playerData.CharacterIndex;
            if (index >= 0 && index < _characterIcons.Length && _characterIcons[index] != null)
            {
                characterIcon.sprite = _characterIcons[index];
                characterIcon.color  = Color.white;
            }
        }
    }
}