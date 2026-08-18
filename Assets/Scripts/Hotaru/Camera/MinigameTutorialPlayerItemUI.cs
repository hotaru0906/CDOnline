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

    [Tooltip("Icon hiển thị khi dữ liệu player CHƯA sync xong (placeholder). " +
             "Nếu để trống sẽ dùng characterIcons[0] làm mặc định.")]
    [SerializeField] private Sprite defaultIcon;

    [SerializeField] private Color readyColor = Color.green;
    [SerializeField] private Color notReadyColor = Color.gray;

    private PlayerNetworkData _playerData;

    public void SetData(PlayerNetworkData player)
    {
        _playerData = player;
        if (_playerData == null) return;

        // Không set tên/icon 1 lần ở đây nữa — UpdateData() sẽ tự lo,
        // kể cả khi dữ liệu network đến trễ (đến sau lần SetData ban đầu).
        UpdateData();
    }

    /// <summary>
    /// Refresh icon, tên, và status. Gọi định kỳ (vd mỗi 1s) từ list UI.
    /// Nếu dữ liệu player CHƯA thật sự sync (IsDataSynced == false):
    ///   - Hiện icon default, tên "Player {id}" (placeholder)
    ///   - LUÔN ép status = NOT READY, bất kể IsPlayerLoaded() trả về gì
    /// </summary>
    public void UpdateData()
    {
        if (_playerData == null || _playerData.Object == null) return;

        bool dataSynced = _playerData.IsDataSynced;
        int playerId = _playerData.Object.InputAuthority.PlayerId;

        // --- Tên ---
        if (playerNameText != null)
        {
            playerNameText.text = dataSynced
                ? _playerData.PlayerName.ToString()
                : $"Player {playerId}";
        }

        // --- Icon ---
        if (characterIcon != null && characterIcons != null && characterIcons.Length > 0)
        {
            int index = dataSynced ? _playerData.CharacterIndex : 0;

            if (!dataSynced && defaultIcon != null)
            {
                characterIcon.sprite = defaultIcon;
            }
            else if (index >= 0 && index < characterIcons.Length && characterIcons[index] != null)
            {
                characterIcon.sprite = characterIcons[index];
            }
        }

        // --- Status ---
        // Chưa sync dữ liệu => luôn NOT READY, không quan tâm PlayerController đã spawn hay chưa.
        bool isReady = dataSynced && IsPlayerLoaded(_playerData.Object.InputAuthority);

        if (statusText != null)
        {
            statusText.text = isReady ? "READY" : "NOT READY";
            statusText.color = isReady ? readyColor : notReadyColor;
        }
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