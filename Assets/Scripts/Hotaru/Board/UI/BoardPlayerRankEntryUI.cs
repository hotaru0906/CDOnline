using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 1 entry trong BoardPlayerRankUI.
/// SETUP (1 GameObject có Horizontal Layout Group):
///   ├── NameText    (TMP_Text)    ← nameText
///   ├── KeyText     (TMP_Text)    ← keyText
///   ├── Crown1      (Image)       ← crownImages[0]
///   ├── Crown2      (Image)       ← crownImages[1]
///   └── TurnTick    (GameObject)  ← turnTick     (ẩn/hiện theo lượt)
/// </summary>
public class BoardPlayerRankEntryUI : MonoBehaviour
{
    [SerializeField] private TMP_Text   nameText;
    [SerializeField] private TMP_Text   keyText;
    [SerializeField] private Image[]    crownImages = new Image[2];
    [SerializeField] private GameObject turnTick;

    public void SetData(string playerName, int keyCount, int chestCount, bool isActiveTurn)
    {
        if (nameText != null)
            nameText.text = playerName;

        if (keyText != null)
            keyText.text = keyCount.ToString();

        SetCrownState(chestCount);

        SetTurnActive(isActiveTurn);
    }

    public void SetResourceData(int keyCount, int chestCount)
    {
        if (keyText != null)
            keyText.text = keyCount.ToString();

        SetCrownState(chestCount);
    }

    public void SetTurnActive(bool active)
    {
        if (turnTick != null) turnTick.SetActive(active);
        if (nameText != null) nameText.color = active ? Color.yellow : Color.white;
    }

    private void SetCrownState(int chestCount)
    {
        if (crownImages == null)
            return;

        if (crownImages.Length > 0 && crownImages[0] != null)
            crownImages[0].enabled = chestCount >= 1;

        if (crownImages.Length > 1 && crownImages[1] != null)
            crownImages[1].enabled = chestCount >= 2;
    }
}

// Backward compatibility for existing scene/prefab references.
public class BoardPlayerEntryUI : BoardPlayerRankEntryUI
{
}