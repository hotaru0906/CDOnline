using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// UI hiển thị thông tin tất cả người chơi trong lobby
/// </summary>
public class PlayerInfoUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Canvas canvas;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Transform contentParent;
    [SerializeField] private PlayerInfoItemUI itemPrefab;

    [Header("Animation")]
    [SerializeField] private float fadeSpeed = 10f;

    private List<PlayerInfoItemUI> _items = new List<PlayerInfoItemUI>();
    private bool _isVisible;
    private float _targetAlpha;
    private int _lastPlayerCount = -1;

    private static PlayerInfoUI _instance;
    public static PlayerInfoUI Instance => _instance;

    private void Awake()
    {
        _instance = this;

        if (canvas == null)
            canvas = GetComponent<Canvas>();
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        if (canvasGroup != null)
            canvasGroup.alpha = 0f;
    }

    private void Start()
    {
        Show();
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    private void Update()
    {
        // Fade animation
        if (canvasGroup != null)
        {
            canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, _targetAlpha, fadeSpeed * Time.deltaTime);

            if (!_isVisible && canvasGroup.alpha < 0.01f)
            {
                gameObject.SetActive(false);
            }
        }

        if (_isVisible)
        {
            // Tự refresh khi có người vào/rời lobby
            int currentCount = FindObjectsByType<PlayerNetworkData>(FindObjectsSortMode.None).Length;
            if (currentCount != _lastPlayerCount)
            {
                RefreshPlayerList();
            }

            UpdatePlayerData();
        }
    }

    /// <summary>
    /// Hiển thị Player Info UI
    /// </summary>
    public void Show()
    {
        gameObject.SetActive(true);
        _isVisible = true;
        _targetAlpha = 1f;

        RefreshPlayerList();

        Debug.Log("[PlayerInfoUI] Showing player info");
    }

    /// <summary>
    /// Ẩn Player Info UI
    /// </summary>
    public void Hide()
    {
        _isVisible = false;
        _targetAlpha = 0f;

        Debug.Log("[PlayerInfoUI] Hiding player info");
    }

    /// <summary>
    /// Refresh danh sách player
    /// </summary>
    private void RefreshPlayerList()
    {
        foreach (var item in _items)
        {
            if (item != null)
                Destroy(item.gameObject);
        }
        _items.Clear();

        var players = FindObjectsByType<PlayerNetworkData>(FindObjectsSortMode.None);

        foreach (var player in players)
        {
            if (itemPrefab != null && contentParent != null)
            {
                var item = Instantiate(itemPrefab, contentParent);
                item.SetData(player);
                _items.Add(item);
            }
        }

        _lastPlayerCount = players.Length;

        // Debug mode - show text if no prefab
        if (itemPrefab == null)
        {
            Debug.Log($"[PlayerInfoUI] === PLAYER LIST ({players.Length}) ===");
            foreach (var player in players)
            {
                string status = player.IsReady ? "[READY]" : "[NOT READY]";
                string local = player.Object.HasInputAuthority ? "(YOU)" : "";
                Debug.Log($"  - {player.PlayerName} {status} {local}");
            }
            Debug.Log("================================");
        }
    }

    /// <summary>
    /// Update dữ liệu realtime (Ready status, etc.)
    /// </summary>
    private void UpdatePlayerData()
    {
        foreach (var item in _items)
        {
            if (item != null)
                item.UpdateData();
        }
    }

    /// <summary>
    /// Static method để Show (tiện gọi từ event)
    /// </summary>
    public static void ShowPlayerInfo()
    {
        if (_instance != null)
            _instance.Show();
        else
            Debug.Log("[PlayerInfoUI] Instance not found - DEBUG MODE: Press Tab to see player list in Console");
    }

    /// <summary>
    /// Static method để Hide (tiện gọi từ event)
    /// </summary>
    public static void HidePlayerInfo()
    {
        if (_instance != null)
            _instance.Hide();
    }
}