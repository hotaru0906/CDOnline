using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerListItemUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_InputField playerNameText;
    [SerializeField] private Image playerColor;
    [SerializeField] private GameObject hostIndicator;
    [SerializeField] private TMP_Text readyStatusText;

    private PlayerNetworkData _playerData;
    private bool _isEditingName = false;

    public void Setup(PlayerNetworkData playerData)
    {
        _playerData = playerData;

        UpdateUI();

        bool isLocalPlayer = playerData.HasInputAuthority;

        // chỉ player local được sửa
        playerNameText.interactable = isLocalPlayer;
        playerColor.GetComponent<Button>()?.gameObject.SetActive(isLocalPlayer);

        playerNameText.onEndEdit.AddListener(OnNameChanged);

        // Disable player movement input when typing
        playerNameText.onSelect.AddListener(OnInputFieldSelected);
        playerNameText.onDeselect.AddListener(OnInputFieldDeselected);
    }

    private void OnDestroy()
    {
        // Ensure input is re-enabled when this UI is destroyed
        if (PlayerInputHandler.Instance != null)
        {
            PlayerInputHandler.Instance.InputEnabled = true;
        }
    }

    private void OnInputFieldSelected(string _)
    {
        _isEditingName = true;
        if (PlayerInputHandler.Instance != null)
        {
            PlayerInputHandler.Instance.InputEnabled = false;
        }
    }

    private void OnInputFieldDeselected(string _)
    {
        _isEditingName = false;
        if (PlayerInputHandler.Instance != null)
        {
            PlayerInputHandler.Instance.InputEnabled = true;
        }
    }

    private void Update()
    {
        if (_playerData == null) return;

        UpdateUI();
    }

    private void UpdateUI()
    {
        // Don't update name text while user is editing
        if (!_isEditingName)
        {
            playerNameText.text = _playerData.PlayerName.ToString();
        }

        playerColor.color = PlayerColorDatabase.GetColor(_playerData.ColorID);

        readyStatusText.text = _playerData.IsReady ? $"<color=#{ColorUtility.ToHtmlStringRGB(PlayerColorDatabase.GetColor(_playerData.ColorID))}>Ready</color>" : $"<color=#{ColorUtility.ToHtmlStringRGB(PlayerColorDatabase.GetColor(_playerData.ColorID))}>Not Ready</color>";

        hostIndicator.SetActive(_playerData.HasStateAuthority);
    }

    private void OnNameChanged(string newName)
    {
        if (_playerData == null) return;

        if (!_playerData.HasInputAuthority) return;

        _playerData.SetPlayerName(newName);
    }

    public void OnColorClicked()
    {
        if (_playerData == null) return;

        if (!_playerData.HasInputAuthority) return;

        int nextColor = (_playerData.ColorID + 1) % PlayerColorDatabase.ColorCount;

        _playerData.SetColor(nextColor);
    }
}