using UnityEngine;
using System.Collections;

public class MinigameHUDController : MonoBehaviour
{
    public static MinigameHUDController Instance { get; private set; }

    [Header("Sub-panels")]
    [SerializeField] private MinigameTimeUI timePanel;
    [SerializeField] private MinigamePlayerRankUI playerRankPanel;

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
}