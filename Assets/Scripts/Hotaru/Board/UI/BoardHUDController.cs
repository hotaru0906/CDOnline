using UnityEngine;
using System.Collections.Generic;

public class BoardHUDController : MonoBehaviour
{
    public static BoardHUDController Instance { get; private set; }

    [Header("Sub-panels")]
    [SerializeField] private BoardDiceDisplayUI diceDisplay;
    [SerializeField] private BoardPlayerRankUI playerRankPanel;
    [SerializeField] private BoardInventoryUI inventoryPanel;
    [SerializeField] private BoardCardDisplayUI cardDisplay;

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
    }

    public string GetPlayerName(int playerId)
    {
        if (_nameCache.TryGetValue(playerId, out string cached)) return cached;

        var all = Object.FindObjectsByType<PlayerNetworkData>(FindObjectsSortMode.None);
        foreach (var p in all)
            if (p.Object != null)
                _nameCache[p.Object.InputAuthority.PlayerId] = p.PlayerName.ToString();

        return _nameCache.TryGetValue(playerId, out string found) ? found : $"P{playerId}";
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