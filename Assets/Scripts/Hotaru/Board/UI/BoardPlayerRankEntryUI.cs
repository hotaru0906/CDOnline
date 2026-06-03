using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Một dòng trong bảng xếp hạng Board — hiện rank, màu token, tên, vị trí.
///
/// SETUP TRONG UNITY EDITOR (1 prefab "PlayerRankEntry"):
///   PlayerRankEntry  (Horizontal Layout Group)
///     ├── RankText       (TMP_Text, width ~40)   ← rankText
///     ├── TokenColor     (Image, width ~24)       ← tokenColorImage
///     ├── NameText       (TMP_Text, flex width)   ← nameText
///     ├── NodeText       (TMP_Text, width ~70)    ← nodeText
///     └── TurnArrow      (Image / TMP_Text "▶")   ← turnIndicator
///
/// Màu token khớp với BoardPlayerToken.SlotColors.
/// </summary>
public class BoardPlayerRankEntryUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TMP_Text rankText;
    [SerializeField] private Image    tokenColorImage;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text nodeText;
    [SerializeField] private GameObject turnIndicator;   // hiện khi đến lượt player này
    [SerializeField] private Image    background;

    [Header("Colors")]
    [SerializeField] private Color currentTurnBg = new Color(1f, 0.9f, 0.1f, 0.35f);
    [SerializeField] private Color normalBg      = new Color(0f, 0f, 0f, 0.45f);

    // Màu token slot (khớp với BoardPlayerToken.SlotColors)
    private static readonly Color[] TokenColors =
    {
        new Color(0.9f, 0.2f, 0.2f),   // slot 0 — đỏ
        new Color(0.2f, 0.4f, 0.9f),   // slot 1 — xanh dương
        new Color(0.2f, 0.8f, 0.2f),   // slot 2 — xanh lá
        new Color(0.95f, 0.8f, 0.1f),  // slot 3 — vàng
    };

    // =====================================================================
    // PUBLIC API
    // =====================================================================

    /// <summary>
    /// Cập nhật hiển thị cho player này trong bảng.
    /// slot: chỉ số slot trong BoardManager (0-3), dùng để lấy màu token.
    /// </summary>
    public void SetData(int rank, int slot, string playerName, int nodeId, bool isCurrentTurn)
    {
        gameObject.SetActive(true);

        if (rankText != null)
            rankText.text = $"#{rank}";

        if (tokenColorImage != null)
        {
            Color c = (slot >= 0 && slot < TokenColors.Length) ? TokenColors[slot] : Color.white;
            tokenColorImage.color = c;
        }

        if (nameText != null)
            nameText.text = playerName;

        if (nodeText != null)
            nodeText.text = $"N{nodeId}";

        if (turnIndicator != null)
            turnIndicator.SetActive(isCurrentTurn);

        if (background != null)
            background.color = isCurrentTurn ? currentTurnBg : normalBg;
    }

    /// <summary>Ẩn entry này (slot trống).</summary>
    public void SetEmpty()
    {
        gameObject.SetActive(false);
    }
}
