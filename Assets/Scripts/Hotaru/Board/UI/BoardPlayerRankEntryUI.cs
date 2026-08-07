using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BoardPlayerRankEntryUI : MonoBehaviour
{
    [SerializeField] private TMP_Text   nameText;
    [SerializeField] private TMP_Text   keyText;
    [SerializeField] private Image[]    crownImages = new Image[2];
    [SerializeField] private GameObject turnTick;

    public void SetData(string playerName, bool isActiveTurn)
    {
        if (nameText != null)
            nameText.text = playerName;

        SetTurnActive(isActiveTurn);
    }

    public void SetTurnActive(bool active)
    {
        if (turnTick != null) turnTick.SetActive(active);
        if (nameText != null) nameText.color = active ? Color.yellow : Color.white;
    }
}

// Backward compatibility for existing scene/prefab references.
public class BoardPlayerEntryUI : BoardPlayerRankEntryUI
{
}