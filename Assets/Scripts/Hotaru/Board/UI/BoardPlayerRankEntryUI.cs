using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 1 entry trong BoardPlayerRankUI.
/// SETUP (1 GameObject có Horizontal Layout Group):
///   ├── IconImage   (Image)       ← iconImage   (background màu slot)
///   ├── NameText    (TMP_Text)    ← nameText
///   ├── RankText    (TMP_Text)    ← rankText     (#1, #2...)
///   └── TurnTick    (GameObject)  ← turnTick     (ẩn/hiện theo lượt)
/// </summary>
public class BoardPlayerEntryUI : MonoBehaviour
{
    [SerializeField] private Image      iconImage;
    [SerializeField] private TMP_Text   nameText;
    [SerializeField] private TMP_Text   rankText;
    [SerializeField] private GameObject turnTick;

    public void SetData(string playerName, int rank, Color slotColor, bool isActiveTurn)
    {
        if (nameText  != null) nameText.text   = playerName;
        if (rankText  != null) rankText.text   = $"#{rank}";
        if (iconImage != null) iconImage.color = slotColor;
        SetTurnActive(isActiveTurn);
    }

    public void SetTurnActive(bool active)
    {
        if (turnTick != null) turnTick.SetActive(active);
        if (nameText != null) nameText.color = active ? Color.yellow : Color.white;
    }
}