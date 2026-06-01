using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class BoardGameUIManager : MonoBehaviour
{
    public static BoardGameUIManager Instance;

    // ========================================================
    // INSPECTOR REFERENCES
    // ========================================================

    [Header("--- PLAYER ROWS ---")]
    [Tooltip("Kéo 4 PlayerRow vào đây theo thứ tự")]
    public PlayerRowUI[] playerRows;

    [Header("--- TURN ORDER ---")]
    [Tooltip("Text hiển thị thứ tự lượt")]
    public TextMeshProUGUI turnOrderText;

    [Header("--- INVENTORY BUTTON ---")]
    public Button inventoryButton;

    [Header("--- DEBUG / TEST INSPECTOR ---")]
    [Tooltip("Index player đang có lượt (0-3), chỉnh trong Inspector để test")]
    [Range(0, 3)]
    public int debugCurrentTurnIndex = 0;

    [Tooltip("Ấn nút này trong Inspector để test chuyển lượt")]
    public bool debugNextTurn = false;

    [Tooltip("Data test cho 4 player")]
    public List<DebugBoardPlayerData> debugPlayers = new List<DebugBoardPlayerData>()
    {
        new DebugBoardPlayerData { playerName = "Player 1", itemCount = 0 },
        new DebugBoardPlayerData { playerName = "Player 2", itemCount = 0 },
        new DebugBoardPlayerData { playerName = "Player 3", itemCount = 0 },
        new DebugBoardPlayerData { playerName = "Player 4", itemCount = 0 },
    };

    // ========================================================
    // RUNTIME
    // ========================================================
    private int currentTurnIndex = 0;
    private int totalPlayers = 4;

    // ========================================================
    // UNITY LIFECYCLE
    // ========================================================

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        SetupButtons();
        RefreshAllPlayers();
        UpdateTurnOrder(debugCurrentTurnIndex);
    }

    void Update()
    {
        // Test bằng Inspector
        if (debugNextTurn)
        {
            debugNextTurn = false;
            NextTurn();
        }
    }

    // OnValidate chạy khi thay đổi giá trị trong Inspector
    void OnValidate()
    {
        // Khi kéo debugCurrentTurnIndex → tự cập nhật highlight
        if (Application.isPlaying)
        {
            UpdateTurnOrder(debugCurrentTurnIndex);
            RefreshAllPlayers();
        }
    }

    // ========================================================
    // BUTTON SETUP
    // ========================================================

    void SetupButtons()
    {
        if (inventoryButton != null)
            inventoryButton.onClick.AddListener(OnClick_Inventory);
        else
            Debug.LogWarning("[BoardGameUI] inventoryButton chưa được gán!");
    }

    void OnClick_Inventory()
    {
        Debug.Log("[BoardGameUI] Mở Inventory");
        // TODO: Mở panel inventory
    }

    // ========================================================
    // SETUP TẤT CẢ PLAYER
    // ========================================================

    public void RefreshAllPlayers()
    {
        if (playerRows == null) return;

        for (int i = 0; i < playerRows.Length; i++)
        {
            if (playerRows[i] == null) continue;

            if (i < debugPlayers.Count)
            {
                playerRows[i].gameObject.SetActive(true);
                playerRows[i].Setup(
                    debugPlayers[i].playerName,
                    debugPlayers[i].icon,
                    debugPlayers[i].itemCount,
                    i == currentTurnIndex  // Highlight nếu đang có lượt
                );
            }
            else
            {
                playerRows[i].gameObject.SetActive(false);
            }
        }
    }

    // ========================================================
    // TURN ORDER
    // ========================================================

    public void UpdateTurnOrder(int turnIndex)
    {
        currentTurnIndex = turnIndex;
        debugCurrentTurnIndex = turnIndex;
        totalPlayers = debugPlayers.Count;

        // Cập nhật highlight
        for (int i = 0; i < playerRows.Length; i++)
        {
            if (playerRows[i] != null)
                playerRows[i].SetHighlight(i == currentTurnIndex);
        }

        // Cập nhật text thứ tự lượt
        if (turnOrderText != null)
        {
            string order = "Turn Order:\n";
            for (int i = 0; i < debugPlayers.Count; i++)
            {
                // Đánh dấu player đang có lượt bằng dấu ►
                string prefix = (i == currentTurnIndex) ? "► " : "    ";
                order += prefix + debugPlayers[i].playerName;
                if (i < debugPlayers.Count - 1) order += ", ";
            }
            turnOrderText.text = order;
        }

        Debug.Log($"[BoardGameUI] Lượt của: {debugPlayers[currentTurnIndex].playerName}");
    }

    // Chuyển sang lượt tiếp theo
    public void NextTurn()
    {
        int next = (currentTurnIndex + 1) % totalPlayers;
        UpdateTurnOrder(next);

        // --- ONLINE (Photon PUN2) ---
        // ExitGames.Client.Photon.Hashtable props = new ExitGames.Client.Photon.Hashtable();
        // props["CurrentTurn"] = next;
        // PhotonNetwork.CurrentRoom.SetCustomProperties(props);
    }

    // ========================================================
    // CẬP NHẬT ITEM COUNT
    // ========================================================

    // Tăng item cho player
    public void AddItemToPlayer(int playerIndex, int amount = 1)
    {
        if (playerIndex < 0 || playerIndex >= debugPlayers.Count) return;

        debugPlayers[playerIndex].itemCount += amount;

        // Cập nhật UI row tương ứng
        if (playerIndex < playerRows.Length && playerRows[playerIndex] != null)
            playerRows[playerIndex].SetItemCount(debugPlayers[playerIndex].itemCount);

        Debug.Log($"[BoardGameUI] {debugPlayers[playerIndex].playerName} " +
                  $"nhận item → Tổng: {debugPlayers[playerIndex].itemCount}");

        // --- ONLINE (Photon PUN2) ---
        // ExitGames.Client.Photon.Hashtable props = new ExitGames.Client.Photon.Hashtable();
        // props[$"ItemCount_{playerIndex}"] = debugPlayers[playerIndex].itemCount;
        // PhotonNetwork.CurrentRoom.SetCustomProperties(props);
    }

    // Set thẳng item count
    public void SetItemCount(int playerIndex, int count)
    {
        if (playerIndex < 0 || playerIndex >= debugPlayers.Count) return;

        debugPlayers[playerIndex].itemCount = count;

        if (playerIndex < playerRows.Length && playerRows[playerIndex] != null)
            playerRows[playerIndex].SetItemCount(count);
    }

    // ========================================================
    // ONLINE — Photon PUN2
    // ========================================================

    // public override void OnRoomPropertiesUpdate(ExitGames.Client.Photon.Hashtable props)
    // {
    //     // Cập nhật lượt
    //     if (props.ContainsKey("CurrentTurn"))
    //         UpdateTurnOrder((int)props["CurrentTurn"]);
    //
    //     // Cập nhật item count
    //     for (int i = 0; i < 4; i++)
    //     {
    //         string key = $"ItemCount_{i}";
    //         if (props.ContainsKey(key))
    //             SetItemCount(i, (int)props[key]);
    //     }
    // }
}

// ============================================================
// Data class debug player
// ============================================================
[System.Serializable]
public class DebugBoardPlayerData
{
    public string playerName = "Player";
    public Sprite icon;
    public int    itemCount  = 0;
}