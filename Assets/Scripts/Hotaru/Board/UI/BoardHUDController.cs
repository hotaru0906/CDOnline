using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Master controller cho toàn bộ Board HUD.
/// Đặt vào GameObject "BoardHUD" trong BoardScene, gắn các sub-panel qua Inspector.
///
/// SETUP TRONG UNITY EDITOR:
///   1. Tạo Canvas (Screen Space - Overlay) trong BoardScene.
///   2. Tạo child: BoardHUD GameObject, gắn script này.
///   3. Tạo các sub-panel (xem từng script để biết cấu trúc).
///   4. Gán vào 3 field dưới đây.
/// </summary>
public class BoardHUDController : MonoBehaviour
{
    public static BoardHUDController Instance { get; private set; }

    [Header("Sub-panels")]
    [Tooltip("Panel hiện kết quả xúc xắc (fade in/out)")]
    [SerializeField] private BoardDiceDisplayUI diceDisplay;

    [Tooltip("Panel danh sách 4 player theo thứ tự vị trí trên bàn cờ")]
    [SerializeField] private BoardPlayerRankUI playerRankPanel;

    [Tooltip("Panel inventory Board items của local player")]
    [SerializeField] private BoardInventoryUI inventoryPanel;

    // Cache tên player: PlayerId -> tên
    private readonly Dictionary<int, string> _nameCache = new();

    // =====================================================================
    // LIFECYCLE
    // =====================================================================

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        // Đăng ký event từ BoardManager
        if (BoardManager.Instance != null)
            BoardManager.Instance.OnTurnStarted += OnTurnStarted;

        // Build name cache từ PlayerNetworkData objects hiện có trong scene
        RefreshNameCache();
    }

    private void OnDisable()
    {
        if (BoardManager.Instance != null)
            BoardManager.Instance.OnTurnStarted -= OnTurnStarted;
    }

    // =====================================================================
    // EVENTS
    // =====================================================================

    private void OnTurnStarted(int playerId)
    {
        playerRankPanel?.Refresh();
        inventoryPanel?.Refresh();
    }

    /// <summary>
    /// Gọi bởi BoardManager khi có kết quả xúc xắc (từ RPC_ShowDiceResult).
    /// </summary>
    public void OnDiceResult(int playerId, int result)
    {
        string name = GetPlayerName(playerId);
        diceDisplay?.ShowRoll(name, result);
    }

    // =====================================================================
    // NAME CACHE
    // =====================================================================

    /// <summary>Tìm tên hiển thị của player theo PlayerId.</summary>
    public string GetPlayerName(int playerId)
    {
        if (_nameCache.TryGetValue(playerId, out string cached))
            return cached;

        // Thử tìm trong scene
        var players = Object.FindObjectsByType<PlayerNetworkData>(FindObjectsSortMode.None);
        foreach (var p in players)
        {
            if (p.Object == null) continue;
            int pid = p.Object.InputAuthority.PlayerId;
            _nameCache[pid] = p.PlayerName.ToString();
        }

        return _nameCache.TryGetValue(playerId, out string found) ? found : $"P{playerId}";
    }

    private void RefreshNameCache()
    {
        _nameCache.Clear();
        var players = Object.FindObjectsByType<PlayerNetworkData>(FindObjectsSortMode.None);
        foreach (var p in players)
        {
            if (p.Object == null) continue;
            _nameCache[p.Object.InputAuthority.PlayerId] = p.PlayerName.ToString();
        }
    }
}
