using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Fusion;

public class MinigameTutorialPlayerItemUI : MonoBehaviour
{
    [SerializeField] private TMP_Text playerNameText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private Image characterIcon;

    [Tooltip("Index khớp với CharacterIndex (0-3)")]
    [SerializeField] private Sprite[] characterIcons;

    [SerializeField] private Color readyColor = Color.green;
    [SerializeField] private Color notReadyColor = Color.gray;

    private PlayerNetworkData _playerData;

    public void SetData(PlayerNetworkData player)
    {
        _playerData = player;
        if (_playerData == null) return;

        if (playerNameText != null)
            playerNameText.text = _playerData.PlayerName.ToString();

        if (characterIcon != null && characterIcons != null)
        {
            int index = _playerData.CharacterIndex;
            if (index >= 0 && index < characterIcons.Length && characterIcons[index] != null)
                characterIcon.sprite = characterIcons[index];
        }

        UpdateData();
    }

    public void UpdateData()
    {
        if (_playerData == null || statusText == null) return;

        bool isLoaded = IsPlayerLoaded(_playerData.Object.InputAuthority);

        statusText.text = isLoaded ? "READY" : "NOT READY";
        statusText.color = isLoaded ? readyColor : notReadyColor;
    }

    private bool IsPlayerLoaded(PlayerRef playerRef)
    {
        var controllers = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        foreach (var c in controllers)
        {
            if (c.Object.InputAuthority == playerRef)
                return true;
        }
        return false;
    }
}