using System.Collections.Generic;
using UnityEngine;

public class TabPanelController : MonoBehaviour
{
    [Header("=== Tab Panel ===")]
    [SerializeField] private GameObject tabPanel;
    [SerializeField] private Transform playerListParent;
    [SerializeField] private PlayerEntryUI playerEntryPrefab;

    [Header("=== Animation Settings ===")]
    [SerializeField] private float fadeSpeed = 8f;

    [Header("=== Test Data ===")]
    [SerializeField] private List<PlayerData> playerDataList = new List<PlayerData>();

    // ── Private ──
    private CanvasGroup _canvasGroup;
    private bool _isShowing;
    private readonly List<PlayerEntryUI> _spawnedEntries = new List<PlayerEntryUI>();

    // =============================================
    //                 UNITY EVENTS
    // =============================================
    private void Awake()
    {
        // Tự động thêm CanvasGroup nếu chưa có
        _canvasGroup = tabPanel.GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
            _canvasGroup = tabPanel.AddComponent<CanvasGroup>();

        // Ẩn panel lúc bắt đầu
        _canvasGroup.alpha = 0f;
        _canvasGroup.blocksRaycasts = false;
        _canvasGroup.interactable = false;
        tabPanel.SetActive(true); // Luôn active, điều khiển bằng alpha
    }

    private void Update()
    {
        HandleTabInput();
        HandleFadeAnimation();
    }

    // =============================================
    //              INPUT HANDLING
    // =============================================
    private void HandleTabInput()
    {
        // ✅ Giữ Tab → Hiện panel
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            ShowPanel();
        }

        // ✅ Thả Tab → Ẩn panel
        if (Input.GetKeyUp(KeyCode.Tab))
        {
            HidePanel();
        }
    }

    // =============================================
    //             SHOW / HIDE LOGIC
    // =============================================
    private void ShowPanel()
    {
        _isShowing = true;
        _canvasGroup.blocksRaycasts = true;

        // Refresh danh sách player mỗi lần mở
        RefreshPlayerList();
    }

    private void HidePanel()
    {
        _isShowing = false;
        _canvasGroup.blocksRaycasts = false;
        _canvasGroup.interactable = false;
    }

    // =============================================
    //             FADE ANIMATION
    // =============================================
    private void HandleFadeAnimation()
    {
        float targetAlpha = _isShowing ? 1f : 0f;

        // Lerp mượt mà
        _canvasGroup.alpha = Mathf.Lerp(
            _canvasGroup.alpha,
            targetAlpha,
            Time.unscaledDeltaTime * fadeSpeed
        );

        // Snap khi gần đích (tránh lerp vô hạn)
        if (Mathf.Abs(_canvasGroup.alpha - targetAlpha) < 0.01f)
        {
            _canvasGroup.alpha = targetAlpha;
        }
    }

    // =============================================
    //           PLAYER LIST MANAGEMENT
    // =============================================
    private void RefreshPlayerList()
    {
        ClearPlayerList();

        foreach (var data in playerDataList)
        {
            // Spawn prefab entry
            PlayerEntryUI entry = Instantiate(playerEntryPrefab, playerListParent);
            entry.Setup(data);
            _spawnedEntries.Add(entry);
        }
    }

    private void ClearPlayerList()
    {
        foreach (var entry in _spawnedEntries)
        {
            if (entry != null)
                Destroy(entry.gameObject);
        }
        _spawnedEntries.Clear();
    }

    // =============================================
    //           PUBLIC API (Multiplayer)
    // =============================================

    /// <summary>
    /// Thêm player mới vào danh sách (dùng cho Multiplayer)
    /// </summary>
    public void AddPlayer(PlayerData newPlayer)
    {
        playerDataList.Add(newPlayer);
    }

    /// <summary>
    /// Xóa player khỏi danh sách
    /// </summary>
    public void RemovePlayer(string playerName)
    {
        playerDataList.RemoveAll(p => p.playerName == playerName);
    }

    /// <summary>
    /// Cập nhật toàn bộ danh sách (VD: từ server)
    /// </summary>
    public void UpdatePlayerList(List<PlayerData> newList)
    {
        playerDataList = new List<PlayerData>(newList);
    }
}