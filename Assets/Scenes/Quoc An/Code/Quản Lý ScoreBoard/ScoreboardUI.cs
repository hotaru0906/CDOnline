using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ScoreBoardUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Transform listContainer;

    [Header("Test Data — Inspector Only")]
    [SerializeField] private List<ScoreboardEntry> testEntries;

    // ─── TEST ─────────────────────────────────────────
    [ContextMenu("TEST — Preview Scoreboard")]
    private void PreviewInEditor() => Show(testEntries);

    [ContextMenu("TEST — Hide Scoreboard")]
    private void HideInEditor() => Hide();

    // ─── PUBLIC API ───────────────────────────────────
    public void Show(List<ScoreboardEntry> entries)
    {
        foreach (Transform child in listContainer)
        {
#if UNITY_EDITOR
            DestroyImmediate(child.gameObject);
#else
            Destroy(child.gameObject);
#endif
        }

        var sorted = entries.OrderByDescending(e => e.score).ToList();

        for (int i = 0; i < sorted.Count; i++)
            CreateRow(i + 1, sorted[i]);

        SetVisible(true);
    }

    public void Hide() => SetVisible(false);

    private void SetVisible(bool visible)
    {
        canvasGroup.alpha          = visible ? 1f : 0f;
        canvasGroup.interactable   = visible;
        canvasGroup.blocksRaycasts = visible;
    }

    // ─── TỰ TẠO ROW BẰNG CODE ────────────────────────
    private void CreateRow(int rank, ScoreboardEntry entry)
    {
        // Root row
        var row = new GameObject("Row_" + rank);
        row.transform.SetParent(listContainer, false);

        var rowRect = row.AddComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(0, 1);
        rowRect.anchorMax = new Vector2(1, 1);
        rowRect.pivot     = new Vector2(0.5f, 1f);
        rowRect.offsetMin = new Vector2(0, -60);
        rowRect.offsetMax = new Vector2(0, 0);

        // Background
        var bg = row.AddComponent<Image>();
        bg.color = rank == 1 ? new Color(1f, 0.85f, 0.1f, 0.08f)
                 : rank == 2 ? new Color(0.8f, 0.8f, 0.8f, 0.05f)
                 : rank == 3 ? new Color(0.8f, 0.5f, 0.2f, 0.06f)
                 : new Color(1f, 1f, 1f, 0.03f);

        // Horizontal Layout Group
        var hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment           = TextAnchor.MiddleLeft;
        hlg.childControlWidth        = true;
        hlg.childControlHeight       = true;
        hlg.childForceExpandWidth    = false;
        hlg.childForceExpandHeight   = true;
        hlg.spacing                  = 10;
        hlg.padding                  = new RectOffset(12, 12, 0, 0);

        // Rank
        var rankColor = rank == 1 ? new Color(1f, 0.85f, 0.1f)
                      : rank == 2 ? new Color(0.75f, 0.75f, 0.75f)
                      : rank == 3 ? new Color(0.8f, 0.5f, 0.2f)
                      : Color.white;
        AddText(row, rank.ToString(), rankColor, 20, FontStyles.Bold, 44);

        // Icon
        var iconGO = new GameObject("Icon");
        iconGO.transform.SetParent(row.transform, false);
        var iconImg = iconGO.AddComponent<Image>();
        iconImg.preserveAspect = true;
        if (entry.characterIcon != null)
            iconImg.sprite = entry.characterIcon;
        else
            iconImg.color = new Color(1, 1, 1, 0.15f);
        var iconLE = iconGO.AddComponent<LayoutElement>();
        iconLE.preferredWidth  = 48;
        iconLE.preferredHeight = 48;
        iconLE.flexibleWidth   = 0;

        // Name
        AddText(row, entry.playerName, Color.white, 15, FontStyles.Normal, 0, flexible: true);

        // Score
        AddText(row, entry.score.ToString("N0") + " pts", rankColor, 14, FontStyles.Bold, 110);
    }

    private void AddText(GameObject parent, string content,
                         Color color, float fontSize,
                         FontStyles style, float preferredWidth,
                         bool flexible = false)
    {
        var go = new GameObject("Text");
        go.transform.SetParent(parent.transform, false);

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text      = content;
        tmp.color     = color;
        tmp.fontSize  = fontSize;
        tmp.fontStyle = style;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        tmp.overflowMode = TextOverflowModes.Ellipsis;

        var le = go.AddComponent<LayoutElement>();
        if (flexible)
        {
            le.flexibleWidth = 1;
        }
        else
        {
            le.preferredWidth = preferredWidth;
            le.flexibleWidth  = 0;
        }
    }

    // ─── PHOTON FUSION (stub) ─────────────────────────
    /*
    using Fusion;
    public void ShowFromNetwork(Dictionary<PlayerRef, int> playerScores,
                                Dictionary<PlayerRef, string> playerNames,
                                Dictionary<PlayerRef, Sprite> playerIcons)
    {
        var entries = playerScores.Select(kvp => new ScoreboardEntry
        {
            playerName    = playerNames.TryGetValue(kvp.Key, out var n) ? n : kvp.Key.ToString(),
            characterIcon = playerIcons.TryGetValue(kvp.Key, out var s) ? s : null,
            score         = kvp.Value
        }).ToList();
        Show(entries);
    }
    */
}