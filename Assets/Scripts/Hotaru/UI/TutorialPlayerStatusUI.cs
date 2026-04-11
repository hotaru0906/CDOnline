using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// UI hiển thị trạng thái player trong Tutorial
/// </summary>
public class TutorialPlayerStatusUI : MonoBehaviour
{
    [SerializeField] private TMP_Text playerNameText;
    [SerializeField] private Image connectionIndicator;
    [SerializeField] private TMP_Text statusText;

    [Header("Colors")]
    [SerializeField] private Color connectedColor = Color.green;
    [SerializeField] private Color connectingColor = Color.yellow;
    [SerializeField] private Color disconnectedColor = Color.red;

    private PlayerNetworkData _playerData;

    public void SetData(PlayerNetworkData player)
    {
        _playerData = player;
        UpdateStatus();
    }

    public void UpdateStatus()
    {
        if (_playerData == null)
        {
            SetDisconnected();
            return;
        }

        // Update name
        if (playerNameText != null)
        {
            playerNameText.text = _playerData.PlayerName.ToString();
        }

        // Check connection status
        bool isConnected = _playerData.Object != null && _playerData.Object.IsValid;

        if (isConnected)
        {
            SetConnected();
        }
        else
        {
            SetConnecting();
        }
    }

    private void SetConnected()
    {
        if (connectionIndicator != null)
            connectionIndicator.color = connectedColor;

        if (statusText != null)
            statusText.text = "Connected";
    }

    private void SetConnecting()
    {
        if (connectionIndicator != null)
            connectionIndicator.color = connectingColor;

        if (statusText != null)
            statusText.text = "Connecting...";
    }

    private void SetDisconnected()
    {
        if (connectionIndicator != null)
            connectionIndicator.color = disconnectedColor;

        if (statusText != null)
            statusText.text = "Disconnected";
    }
}
