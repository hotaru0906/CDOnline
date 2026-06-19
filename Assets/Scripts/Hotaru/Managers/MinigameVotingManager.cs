using UnityEngine;
using System.Collections.Generic;
using Fusion;

public class MinigameVotingManager : NetworkBehaviour
{
    #region Singleton
    public static MinigameVotingManager Instance { get; private set; }
    #endregion

    [Header("Minigame Configuration")]
    [SerializeField] private List<MinigameData> allMinigames = new List<MinigameData>();
    private HashSet<MinigameData> playedMinigameSO = new HashSet<MinigameData>();

    [Header("Settings")]
    [SerializeField] private int displayCount = 3; // Số minigame hiển thị để vote mỗi lần
    [SerializeField] private bool shuffleMinigames = true;

    #region Networked Properties

    [Networked, Capacity(20)]
    private NetworkArray<int> PlayedMinigameIndices => default;

    [Networked]
    private int PlayedCount { get; set; }

    [Networked, Capacity(10)]
    private NetworkArray<int> AvailableMinigameIndices => default;

    [Networked]
    private int AvailableCount { get; set; }
    #endregion

    #region Events
    public event System.Action OnMinigameListUpdated;
    #endregion
    public bool IsReady { get; private set; } = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
    public override void Spawned()
    {
        IsReady = true;

        if (HasStateAuthority)
        {
            ResetPlayedMinigames();
            PrepareNextVotingRound();
        }
    }

    #region Public Methods
    public List<MinigameData> GetAvailableMinigames()
    {
        List<MinigameData> available = new List<MinigameData>();

        // Kiểm tra xem đã spawn chưa
        if (!IsReady) return available;

        for (int i = 0; i < AvailableCount; i++)
        {
            int index = AvailableMinigameIndices.Get(i);
            if (index >= 0 && index < allMinigames.Count)
            {
                available.Add(allMinigames[index]);
            }
        }

        return available;
    }

    public int GetAvailableMinigameCount()
    {
        // Kiểm tra xem đã spawn chưa trước khi truy cập Networked property
        if (!IsReady) return 0;
        return AvailableCount;
    }

    public MinigameData GetMinigameByAvailableIndex(int availableIndex)
    {
        // Kiểm tra xem đã spawn chưa
        if (!IsReady) return null;

        if (availableIndex < 0 || availableIndex >= AvailableCount)
            return null;

        int actualIndex = AvailableMinigameIndices.Get(availableIndex);
        if (actualIndex >= 0 && actualIndex < allMinigames.Count)
        {
            return allMinigames[actualIndex];
        }
        return null;
    }

    public void MarkMinigamePlayed(int availableIndex)
    {
        if (!IsReady)
        {
            Debug.LogWarning("[MinigameVotingManager] Cannot mark played - not spawned yet");
            return;
        }

        if (availableIndex < 0 || availableIndex >= AvailableCount)
        {
            Debug.LogWarning($"[MinigameVotingManager] Invalid available index: {availableIndex}");
            return;
        }

        int actualIndex = AvailableMinigameIndices.Get(availableIndex);
        if (actualIndex < 0 || actualIndex >= allMinigames.Count)
            return;

        // Kiểm tra đã chơi bằng SO
        var so = allMinigames[actualIndex];
        if (playedMinigameSO.Contains(so))
        {
            Debug.Log($"[MinigameVotingManager] Minigame {so.name} already marked as played");
            return;
        }

        // Thêm vào danh sách đã chơi
        if (PlayedCount < 20)
        {
            PlayedMinigameIndices.Set(PlayedCount, actualIndex);
            PlayedCount++;
            playedMinigameSO.Add(so);
            Debug.Log($"[MinigameVotingManager] Marked minigame {so.name} as played. Total played: {PlayedCount}");
        }
    }

    public void PrepareNextVotingRound()
    {
        if (!HasStateAuthority || !IsReady) return;

        Debug.Log("[MinigameVotingManager] Preparing next voting round...");

        // Hiện tất cả minigame, không shuffle, không track played
        AvailableCount = allMinigames.Count;
        for (int i = 0; i < allMinigames.Count; i++)
            AvailableMinigameIndices.Set(i, i);

        RPC_NotifyMinigameListUpdated();
        Debug.Log($"[MinigameVotingManager] Available: {AvailableCount} minigames");
    }

    public void PrepareNextVotingRoundForRoulette()
    {
        if (!HasStateAuthority || !IsReady) return;

        Debug.Log("[MinigameVotingManager] Preparing next voting round for RouletteOrMinigame...");

        // Lấy danh sách minigame chưa chơi bằng SO
        List<int> unplayedIndices = new List<int>();
        for (int i = 0; i < allMinigames.Count; i++)
        {
            if (!playedMinigameSO.Contains(allMinigames[i]))
            {
                unplayedIndices.Add(i);
            }
        }

        Debug.Log($"[MinigameVotingManager] Unplayed minigames (Roulette): {unplayedIndices.Count}");

        // Nếu không còn minigame nào, không reset, không cho vote lại minigame đã chơi
        if (unplayedIndices.Count == 0)
        {
            Debug.LogWarning("[MinigameVotingManager] No unplayed minigames left for Roulette voting!");
            AvailableCount = 0;
            RPC_NotifyMinigameListUpdated();
            return;
        }

        // Shuffle nếu cần
        if (shuffleMinigames)
        {
            ShuffleList(unplayedIndices);
        }

        // Chọn số lượng minigame để hiển thị
        int count = Mathf.Min(displayCount, unplayedIndices.Count);
        AvailableCount = count;

        for (int i = 0; i < count; i++)
        {
            AvailableMinigameIndices.Set(i, unplayedIndices[i]);
            Debug.Log($"[MinigameVotingManager] Available slot {i}: Minigame {unplayedIndices[i]}");
        }

        // Notify clients
        RPC_NotifyMinigameListUpdated();
    }

    /// <summary>
    /// Reset danh sách minigame đã chơi (khi về Roulette hoặc new game)
    /// </summary>
    public void ResetPlayedMinigames()
    {
        if (!HasStateAuthority || !IsReady) return;

        Debug.Log("[MinigameVotingManager] Resetting played minigames");
        PlayedCount = 0;
        playedMinigameSO.Clear();

        // Clear array
        for (int i = 0; i < 20; i++)
        {
            PlayedMinigameIndices.Set(i, -1);
        }
    }
    /// <summary>
    /// Lấy danh sách SO minigame đã chơi
    /// </summary>
    public List<MinigameData> GetPlayedMinigames()
    {
        return new List<MinigameData>(playedMinigameSO);
    }

    /// <summary>
    /// Kiểm tra minigame đã được chơi chưa (theo actual index)
    /// </summary>
    public bool IsMinigamePlayed(int actualIndex)
    {
        if (!IsReady) return false;

        for (int i = 0; i < PlayedCount; i++)
        {
            if (PlayedMinigameIndices.Get(i) == actualIndex)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Lấy tổng số minigame
    /// </summary>
    public int TotalMinigameCount => allMinigames.Count;

    /// <summary>
    /// Lấy số minigame đã chơi
    /// </summary>
    public int PlayedMinigameCount => IsReady ? PlayedCount : 0;
    #endregion

    #region RPCs

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_NotifyMinigameListUpdated()
    {
        Debug.Log("[MinigameVotingManager] Minigame list updated notification received");
        OnMinigameListUpdated?.Invoke();
    }
    #endregion

    #region Helper Methods
    private void ShuffleList<T>(List<T> list)
    {
        int n = list.Count;
        for (int i = n - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            T temp = list[i];
            list[i] = list[j];
            list[j] = temp;
        }
    }
    #endregion
}
