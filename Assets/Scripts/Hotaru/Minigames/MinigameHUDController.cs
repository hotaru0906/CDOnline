using UnityEngine;
using System.Collections;
using TMPro;

public class MinigameHUDController : MonoBehaviour
{
    public static MinigameHUDController Instance { get; private set; }

    [Header("Sub-panels")]
    [SerializeField] private MinigameTimeUI timePanel;
    [SerializeField] private MinigamePlayerRankUI playerRankPanel;
    [SerializeField] private TMP_Text hammerTimerText;

    [Header("HUD Root")]
    [Tooltip("GameObject chứa toàn bộ HUD — ẩn trước khi game bắt đầu")]
    [SerializeField] private GameObject hudRoot;

    private void Awake()
    {
        Debug.Log("[HUD] Awake");

        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // Ẩn HUD ban đầu
        if (hudRoot != null)
        {
            Debug.Log("[HUD] Hide HUD");
            hudRoot.SetActive(false);
        }
        if (hammerTimerText != null)
        {
            hammerTimerText.enabled = false;
        }
        else
        {
            Debug.LogError("[HUD] hudRoot is NULL!");
        }
    }

    private void Start()
    {
        Debug.Log("[HUD] Start");

        BaseMinigameController.OnGameStarted += OnGameStarted;
    }

    private void OnDestroy()
    {
        Debug.Log("[HUD] OnDestroy");

        if (Instance == this)
            Instance = null;

        BaseMinigameController.OnGameStarted -= OnGameStarted;
    }

    private void OnGameStarted()
    {
        Debug.Log("[HUD] OnGameStarted");

        // Hiện HUD khi game bắt đầu
        if (hudRoot != null)
        {
            Debug.Log("[HUD] Enable HUD");
            hudRoot.SetActive(true);
        }
        else
        {
            Debug.LogError("[HUD] Cannot enable HUD because hudRoot is NULL!");
        }

        playerRankPanel?.Refresh();
        StartCoroutine(AutoRefresh());
    }

    private IEnumerator AutoRefresh()
    {
        Debug.Log("[HUD] AutoRefresh Started");

        while (true)
        {
            yield return new WaitForSeconds(0.5f);

            if (BaseMinigameController.Instance != null &&
                BaseMinigameController.Instance.Object != null &&
                BaseMinigameController.Instance.Object.IsValid &&
                BaseMinigameController.Instance.IsGameStarted)
            {
                playerRankPanel?.Refresh();
            }
        }
    }

    public void SetTime(float seconds)
    {
        timePanel?.UpdateTime(seconds);
    }

    public void RefreshPlayers()
    {
        playerRankPanel?.Refresh();
    }

    public void UpdatePlayerLives(int playerId, int lives)
    {
        playerRankPanel?.UpdateLivesForPlayer(playerId, lives);
    }

    public void MarkPlayerEliminated(int playerId)
    {
        playerRankPanel?.MarkEliminated(playerId);
    }
    public void UpdatePlayerHP(int playerId, int hp)
    {
        playerRankPanel?.UpdateHPForPlayer(playerId, hp);
    }

    public void UpdatePlayerScore(int playerId, int score)
    {
        playerRankPanel?.UpdateScoreForPlayer(playerId, score);
    }

    public void ShowHammerTimer()
    {
        if (hammerTimerText != null)
            hammerTimerText.enabled = true;
    }

    public void HideHammerTimer()
    {
        if (hammerTimerText != null)
            hammerTimerText.enabled = false;
    }

    public void UpdateHammerTimer(float seconds)
    {
        if (hammerTimerText == null)
            return;

        hammerTimerText.text = Mathf.CeilToInt(seconds).ToString();
    }
}