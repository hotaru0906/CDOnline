using UnityEngine;
using System.Collections.Generic;
using TMPro;

public class BoardHUDController : MonoBehaviour
{
    public static BoardHUDController Instance { get; private set; }

    [Header("Sub-panels")]
    [SerializeField] private BoardDiceDisplayUI diceDisplay;
    [SerializeField] private BoardPlayerRankUI playerRankPanel;
    [SerializeField] private BoardInventoryUI inventoryPanel;
    [SerializeField] private BoardCardDisplayUI cardDisplay;
    [SerializeField] private TextMeshProUGUI stealSelectionText;

    private readonly Dictionary<int, string> _nameCache = new();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (BoardManager.Instance != null)
            BoardManager.Instance.OnTurnStarted -= OnTurnStarted;
    }

    private void Start()
    {
        if (BoardManager.Instance != null)
        {
            BoardManager.Instance.OnTurnStarted += OnTurnStarted;
            Debug.Log("[BoardHUD] Subscribed to OnTurnStarted");
        }
        else
        {
            Debug.LogError("[BoardHUD] BoardManager.Instance is NULL in Start!");
        }

        playerRankPanel?.Refresh();
    }


    // =====================================================================
    // PUBLIC API
    // =====================================================================

    public void OnDiceResult(int playerId, int result)
    {
        string name = GetPlayerName(playerId);
        diceDisplay?.ShowRoll(name, result);
    }

    public void OnItemUsed(int playerId, BoardItemEffect effect)
    {
        cardDisplay?.Show(effect);
        inventoryPanel?.OnItemUsed(playerId); // thêm dòng này
    }

    public void ShowStealSelectionPrompt(int stealerId, IReadOnlyList<int> eligibleTargets, int selectedIndex)
    {
        EnsureStealSelectionText();
        if (stealSelectionText == null || eligibleTargets == null || eligibleTargets.Count == 0)
        {
            HideStealSelectionPrompt();
            return;
        }

        if (selectedIndex < 0 || selectedIndex >= eligibleTargets.Count)
            selectedIndex = 0;

        string targetName = GetPlayerName(eligibleTargets[selectedIndex]);
        stealSelectionText.text = $"STEAL TARGET: {targetName}\nA / D to change • Space to confirm";
        stealSelectionText.gameObject.SetActive(true);
    }

    public void UpdateStealSelectionPrompt(IReadOnlyList<int> eligibleTargets, int selectedIndex)
    {
        ShowStealSelectionPrompt(-1, eligibleTargets, selectedIndex);
    }

    public void HideStealSelectionPrompt()
    {
        if (stealSelectionText != null)
        {
            stealSelectionText.text = string.Empty;
            stealSelectionText.gameObject.SetActive(false);
        }
    }

    private void EnsureStealSelectionText()
    {
        if (stealSelectionText != null) return;

        Canvas canvas = GetComponentInChildren<Canvas>(true);
        if (canvas == null) return;

        GameObject go = new GameObject("StealSelectionText");
        go.transform.SetParent(canvas.transform, false);

        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -70f);
        rt.sizeDelta = new Vector2(460f, 80f);

        stealSelectionText = go.AddComponent<TextMeshProUGUI>();
        stealSelectionText.alignment = TextAlignmentOptions.Center;
        stealSelectionText.fontSize = 26;
        stealSelectionText.color = Color.white;
        stealSelectionText.raycastTarget = false;
        stealSelectionText.enableAutoSizing = false;
        go.SetActive(false);
    }

    public string GetPlayerName(int playerId)
    {
        if (_nameCache.TryGetValue(playerId, out var cachedName) && !string.IsNullOrWhiteSpace(cachedName))
            return cachedName;

        var all = Object.FindObjectsByType<PlayerNetworkData>(FindObjectsSortMode.None);

        foreach (var p in all)
        {
            if (p?.Object == null)
                continue;

            if (p.Object.InputAuthority.PlayerId != playerId)
                continue;

            string playerName = p.PlayerName.ToString();

            if (!string.IsNullOrWhiteSpace(playerName))
            {
                _nameCache[playerId] = playerName;
                return playerName;
            }

            playerName = $"Player {playerId}";
            _nameCache[playerId] = playerName;
            return playerName;
        }

        return $"Player {playerId}";
    }

    public void RefreshPlayerNames()
    {
        _nameCache.Clear();

        var all = Object.FindObjectsByType<PlayerNetworkData>(FindObjectsSortMode.None);
        foreach (var p in all)
        {
            if (p?.Object == null)
                continue;

            int pid = p.Object.InputAuthority.PlayerId;
            string playerName = p.PlayerName.ToString();

            if (string.IsNullOrWhiteSpace(playerName))
                playerName = $"Player {pid}";

            _nameCache[pid] = playerName;
        }

        playerRankPanel?.Refresh();
    }

    // =====================================================================
    // EVENTS
    // =====================================================================

    private void OnTurnStarted(int playerId)
    {
        Debug.Log($"[BoardHUD] OnTurnStarted fired for P{playerId}");
        playerRankPanel?.SetActiveTurn(playerId);
    }
}