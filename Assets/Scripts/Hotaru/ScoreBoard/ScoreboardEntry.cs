using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Đại diện cho 1 dòng trong scoreboard.
/// Gắn vào EntryRow prefab bên trong ScoreboardPanel.
/// 
/// Hierarchy gợi ý:
/// EntryRow (ScoreboardEntry)
/// ├── RankText       (TMP_Text)        → "#1", "#2"...
/// ├── PortraitFrame  (RawImage)        → hiển thị RenderTexture
/// └── PlayerNameText (TMP_Text)        → tên player
/// </summary>
public class ScoreboardEntry : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_Text rankText;
    [SerializeField] private Image portraitImage;
    [SerializeField] private Sprite[] characterAvatars;
    [SerializeField] private TMP_Text playerNameText;

    [Header("Rank Colors (optional)")]
    [SerializeField] private Color rank1Color = new Color(1f, 0.84f, 0f);   // Gold
    [SerializeField] private Color rank2Color = new Color(0.75f, 0.75f, 0.75f); // Silver
    [SerializeField] private Color rank3Color = new Color(0.8f, 0.5f, 0.2f);   // Bronze
    [SerializeField] private Color defaultRankColor = Color.white;

    [Header("Portrait Placeholder")]
    [Tooltip("Texture hiển thị khi chưa có portrait (optional)")]
    [SerializeField] private Texture2D placeholderTexture;

    private PlayerPortraitCamera _portraitCamera;

    /// <summary>
    /// Populate dữ liệu cho entry này
    /// </summary>
    /// <param name="rank">Thứ hạng (1-based)</param>
    /// <param name="playerData">PlayerNetworkData của player đó</param>
    public void SetData(int rank, PlayerNetworkData playerData)
    {
        // --- Rank ---
        if (rankText != null)
        {
            rankText.text = $"#{rank}";
            rankText.color = GetRankColor(rank);
        }

        // --- Tên ---
        if (playerNameText != null)
        {
            playerNameText.text = playerData != null
                ? playerData.PlayerName.ToString()
                : $"Player {rank}";
        }
        
        if (playerData != null)
        {
            SetCharacterAvatar(playerData.CharacterIndex);
        }
    }

    private void SetupPortrait(PlayerNetworkData playerData)
    {
        // Tìm hoặc tạo PlayerPortraitCamera trên player
        _portraitCamera = playerData.GetComponent<PlayerPortraitCamera>();

        if (_portraitCamera == null)
        {
            // Tự động thêm nếu chưa có (fallback)
            _portraitCamera = playerData.gameObject.AddComponent<PlayerPortraitCamera>();
            Debug.Log($"[ScoreboardEntry] Added PlayerPortraitCamera to {playerData.gameObject.name}");
        }
   
    }

    private void SetCharacterAvatar(int characterIndex)
    {
        if (portraitImage == null)
            return;

        if (characterIndex < 0 ||
            characterIndex >= characterAvatars.Length)
            return;

        portraitImage.sprite = characterAvatars[characterIndex];
    }
    /// <summary>
    /// Tắt portrait camera khi entry bị ẩn (tiết kiệm tài nguyên)
    /// </summary>
   

    private Color GetRankColor(int rank)
    {
        return rank switch
        {
            1 => rank1Color,
            2 => rank2Color,
            3 => rank3Color,
            _ => defaultRankColor
        };
    }
}
