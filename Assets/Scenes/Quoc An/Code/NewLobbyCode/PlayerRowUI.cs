using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Gắn lên PlayerRow prefab.
/// Nhận data và hiển thị avatar, tên, trạng thái ready.
/// </summary>
public class PlayerRowUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image avatarImage;
    [SerializeField] private TextMeshProUGUI playerNameText;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private GameObject hostBadge;

    // Màu trạng thái
    private static readonly Color colorReady    = new Color(0.29f, 0.87f, 0.5f);
    private static readonly Color colorNotReady = new Color(0.97f, 0.44f, 0.44f);

    /// <summary>
    /// Gọi từ LobbyUIManager để setup 1 dòng player.
    /// </summary>
    public void Setup(string playerName, bool isReady, bool isHost, PlayerModelData modelData)
    {
        // ── Tên ─────────────────────────────────────────────
        if (playerNameText != null)
            playerNameText.text = isHost ? $"{playerName}\n<size=70%>(Host)</size>" : playerName;

        // ── Trạng thái ───────────────────────────────────────
        if (statusText != null)
        {
            statusText.text  = isReady ? "Ready" : "Not Ready";
            statusText.color = isReady ? colorReady : colorNotReady;
        }

        // ── Avatar ───────────────────────────────────────────
        if (avatarImage != null)
        {
            if (modelData != null && modelData.avatarSprite != null)
            {
                avatarImage.sprite = modelData.avatarSprite;
                avatarImage.color  = Color.white; // hiển thị đúng màu sprite
            }
            else
            {
                // Fallback: màu theo tên nếu chưa có sprite
                avatarImage.sprite = null;
                avatarImage.color  = modelData != null
                    ? modelData.fallbackColor
                    : GetColorFromName(playerName);
            }
        }

        // ── Host Badge ───────────────────────────────────────
        if (hostBadge != null)
            hostBadge.SetActive(isHost);
    }

    /// <summary>
    /// Cập nhật trạng thái ready mà không rebuild cả row.
    /// </summary>
    public void SetReady(bool isReady)
    {
        if (statusText == null) return;
        statusText.text  = isReady ? "Ready" : "Not Ready";
        statusText.color = isReady ? colorReady : colorNotReady;
    }

    private Color GetColorFromName(string name)
    {
        Color[] palette = {
            new Color(0.23f, 0.51f, 0.96f),
            new Color(0.55f, 0.36f, 0.96f),
            new Color(0.13f, 0.77f, 0.37f),
            new Color(0.98f, 0.45f, 0.09f),
            new Color(0.93f, 0.28f, 0.60f),
        };
        return palette[Mathf.Abs(name.GetHashCode()) % palette.Length];
    }
}