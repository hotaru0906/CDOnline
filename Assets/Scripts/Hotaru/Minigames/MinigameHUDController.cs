using UnityEngine;
using System.Collections;

public class MinigameHUDController : MonoBehaviour
{
    public static MinigameHUDController Instance { get; private set; }

    [Header("Sub-panels")]
    [SerializeField] private MinigameTimeUI      timePanel;
    [SerializeField] private MinigamePlayerRankUI playerRankPanel;

    [Header("HUD Root")]
    [Tooltip("GameObject chứa toàn bộ HUD — ẩn trước khi game bắt đầu")]
    [SerializeField] private GameObject hudRoot;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // Ẩn HUD ban đầu
        if (hudRoot != null) hudRoot.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        BaseMinigameController.OnGameStarted -= OnGameStarted;
    }

    private void Start()
    {
        BaseMinigameController.OnGameStarted += OnGameStarted;
    }

    private void OnGameStarted()
    {
        // Hiện HUD khi game bắt đầu
        if (hudRoot != null) hudRoot.SetActive(true);

        playerRankPanel?.Refresh();
        StartCoroutine(AutoRefresh());
    }

    private IEnumerator AutoRefresh()
    {
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

    public void SetTime(float seconds)        => timePanel?.UpdateTime(seconds);
    public void RefreshPlayers()              => playerRankPanel?.Refresh();
    public void UpdatePlayerLives(int playerId, int lives) => playerRankPanel?.UpdateLivesForPlayer(playerId, lives);
    public void MarkPlayerEliminated(int playerId)         => playerRankPanel?.MarkEliminated(playerId);
}