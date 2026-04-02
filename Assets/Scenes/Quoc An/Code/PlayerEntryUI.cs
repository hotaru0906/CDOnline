using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerEntryUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI nameText;

    /// <summary>
    /// Cập nhật thông tin 1 dòng player trong bảng Tab
    /// </summary>
    public void Setup(PlayerData data)
    {
        if (data == null) return;

        // Gán icon nhân vật
        if (iconImage != null && data.icon != null)
        {
            iconImage.sprite = data.icon;
        }

        // Gán tên người chơi
        if (nameText != null)
        {
            nameText.text = data.playerName;
        }
    }
}