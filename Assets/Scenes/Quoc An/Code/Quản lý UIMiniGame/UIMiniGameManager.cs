using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class UIMiniGameManager : MonoBehaviour
{
    public static UIMiniGameManager Instance;

    // ========================================================
    // INSPECTOR REFERENCES
    // ========================================================

    [Header("--- COUNTDOWN ---")]
    [Tooltip("Text hiển thị 3/2/1 Go! ở giữa màn hình")]
    public TextMeshProUGUI countdownText;

    [Header("--- ESC / SETTING ---")]
    [Tooltip("Nút ESC/Setting góc trên phải")]
    public Button escSettingButton;

    [Header("--- PLAYER SLOTS ---")]
    [Tooltip("Kéo 4 PlayerSlot vào đây theo thứ tự")]
    public PlayerSlotUI[] playerSlots;

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

        // Ẩn countdown lúc đầu
        if (countdownText != null)
            countdownText.gameObject.SetActive(false);
    }

    // ========================================================
    // BUTTON SETUP
    // ========================================================

    void SetupButtons()
    {
        if (escSettingButton != null)
            escSettingButton.onClick.AddListener(OnClick_ESCSetting);
        else
            Debug.LogWarning("[MiniGame] escSettingButton chưa được gán!");
    }

    // ========================================================
    // ESC / SETTING → Chuyển về UISetting qua UIManager
    // ========================================================

    void OnClick_ESCSetting()
    {
        Debug.Log("[MiniGame] ESC/Setting clicked → Chuyển về UISetting");

        if (UIManager.Instance != null)
            UIManager.Instance.NavigateTo(UIManager.Instance.UISetting);
        else
            Debug.LogWarning("[MiniGame] UIManager.Instance là null!");
    }

    // ========================================================
    // SETUP PLAYER SLOTS
    // ========================================================

    // Gọi hàm này khi bắt đầu minigame để setup thông tin player
    public void SetupPlayers(PlayerData[] players)
    {
        for (int i = 0; i < playerSlots.Length; i++)
        {
            if (i < players.Length)
            {
                // Hiện slot và setup thông tin
                playerSlots[i].gameObject.SetActive(true);
                playerSlots[i].Setup(
                    players[i].playerName,
                    players[i].icon,
                    players[i].score
                );
            }
            else
            {
                // Ẩn slot nếu không có player
                playerSlots[i].gameObject.SetActive(false);
            }
        }
    }

    // Cập nhật score của 1 player
    public void UpdateScore(int slotIndex, int newScore)
    {
        if (slotIndex < 0 || slotIndex >= playerSlots.Length) return;
        playerSlots[slotIndex].SetScore(newScore);
    }

    // ========================================================
    // COUNTDOWN 3/2/1 GO!
    // ========================================================

    public void StartCountdown()
    {
        StartCoroutine(CountdownCoroutine());
    }

    IEnumerator CountdownCoroutine()
    {
        if (countdownText == null) yield break;

        countdownText.gameObject.SetActive(true);

        // 3
        countdownText.text = "3";
        countdownText.color = Color.white;
        yield return new WaitForSeconds(1f);

        // 2
        countdownText.text = "2";
        yield return new WaitForSeconds(1f);

        // 1
        countdownText.text = "1";
        yield return new WaitForSeconds(1f);

        // GO!
        countdownText.text = "GO!";
        countdownText.color = new Color(74f/255f, 222f/255f, 128f/255f); // xanh lá
        yield return new WaitForSeconds(1f);

        // Ẩn countdown
        countdownText.gameObject.SetActive(false);

        Debug.Log("[MiniGame] Countdown xong → Bắt đầu game!");
    }

    // ========================================================
    // MỞ / ĐÓNG MINIGAME UI
    // ========================================================

    public void OpenMiniGameUI()
    {
        if (UIManager.Instance != null)
            UIManager.Instance.NavigateTo(UIManager.Instance.UIMiniGame);
    }

    public void CloseMiniGameUI()
    {
        if (UIManager.Instance != null)
            UIManager.Instance.NavigateBack();
    }
}

// ============================================================
// Data class cho Player
// ============================================================
[System.Serializable]
public class PlayerData
{
    public string playerName = "Player";
    public Sprite icon;       // Icon thay cho model 3D
    public int    score = 0;
}