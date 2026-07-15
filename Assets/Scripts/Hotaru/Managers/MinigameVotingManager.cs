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

    private const int InvalidIndex = -1;

    #region Networked Properties

    [Networked, Capacity(20)]
    private NetworkArray<int> PlayedMinigameIndices => default;

    [Networked]
    private int PlayedCount { get; set; }

    [Networked, Capacity(10)]
    private NetworkArray<int> AvailableMinigameIndices => default;

    [Networked]
    private int AvailableCount { get; set; }

    [Networked, OnChangedRender(nameof(OnAvailableListVersionChanged))]
    private int AvailableListVersion { get; set; }
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

    public int GetActualIndexByAvailableIndex(int availableIndex)
    {
        if (!IsReady) return InvalidIndex;
        if (availableIndex < 0 || availableIndex >= AvailableCount) return InvalidIndex;

        int actualIndex = AvailableMinigameIndices.Get(availableIndex);
        if (actualIndex < 0 || actualIndex >= allMinigames.Count)
            return InvalidIndex;

        return actualIndex;
    }

    public MinigameData GetMinigameByActualIndex(int actualIndex)
    {
        if (!IsReady) return null;
        if (actualIndex < 0 || actualIndex >= allMinigames.Count) return null;
        return allMinigames[actualIndex];
    }

    public void MarkMinigamePlayed(int availableIndex)
    {
        if (!HasStateAuthority)
        {
            Debug.LogWarning("[MinigameVotingManager] Only Host can mark minigame as played");
            return;
        }

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

        // Kiểm tra đã chơi bằng networked indices để đảm bảo đồng bộ tuyệt đối host/client.
        if (IsMinigamePlayed(actualIndex))
        {
            Debug.Log($"[MinigameVotingManager] Minigame index {actualIndex} already marked as played");
            return;
        }

        // Thêm vào danh sách đã chơi
        if (PlayedCount < PlayedMinigameIndices.Length)
        {
            PlayedMinigameIndices.Set(PlayedCount, actualIndex);
            PlayedCount++;
            playedMinigameSO.Add(allMinigames[actualIndex]);
            Debug.Log($"[MinigameVotingManager] Marked minigame {allMinigames[actualIndex].name} as played. Total played: {PlayedCount}");
        }
        else
        {
            Debug.LogWarning("[MinigameVotingManager] PlayedMinigameIndices is full, cannot track more played minigames");
        }
    }

    public void PrepareNextVotingRound()
    {
        if (!HasStateAuthority || !IsReady) return;

        Debug.Log("[MinigameVotingManager] Preparing next voting round...");

        var unplayedIndices = BuildUnplayedIndices();

        // Không còn lựa chọn hợp lệ thì trả list rỗng (không cho random lại game đã chơi).
        if (unplayedIndices.Count == 0)
        {
            Debug.LogWarning("[MinigameVotingManager] No unplayed minigames left for voting");
            ClearAvailableMinigameIndices();
            AvailableCount = 0;
            AvailableListVersion++;
            return;
        }

        if (shuffleMinigames)
        {
            ShuffleList(unplayedIndices);
        }

        int count = Mathf.Min(displayCount, unplayedIndices.Count, AvailableMinigameIndices.Length);
        ClearAvailableMinigameIndices();
        AvailableCount = count;

        for (int i = 0; i < count; i++)
        {
            AvailableMinigameIndices.Set(i, unplayedIndices[i]);
            Debug.Log($"[MinigameVotingManager] Available slot {i}: Minigame {unplayedIndices[i]}");
        }

        AvailableListVersion++;
        Debug.Log($"[MinigameVotingManager] Available: {AvailableCount} minigames (from {unplayedIndices.Count} unplayed)");
    }

    public void PrepareNextVotingRoundForRoulette()
    {
        PrepareNextVotingRound();
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
        for (int i = 0; i < PlayedMinigameIndices.Length; i++)
        {
            PlayedMinigameIndices.Set(i, InvalidIndex);
        }

        ClearAvailableMinigameIndices();
        AvailableCount = 0;
    }
    /// <summary>
    /// Lấy danh sách SO minigame đã chơi
    /// </summary>
    public List<MinigameData> GetPlayedMinigames()
    {
        RebuildPlayedCacheFromNetwork();
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

    private void OnAvailableListVersionChanged()
    {
        RebuildPlayedCacheFromNetwork();
        Debug.Log($"[MinigameVotingManager] Minigame list version {AvailableListVersion} received");
        OnMinigameListUpdated?.Invoke();
    }

    #region Helper Methods
    private List<int> BuildUnplayedIndices()
    {
        List<int> unplayedIndices = new List<int>();
        for (int i = 0; i < allMinigames.Count; i++)
        {
            if (!IsMinigamePlayed(i))
            {
                unplayedIndices.Add(i);
            }
        }
        return unplayedIndices;
    }

    private void RebuildPlayedCacheFromNetwork()
    {
        playedMinigameSO.Clear();

        if (!IsReady) return;

        for (int i = 0; i < PlayedCount; i++)
        {
            int index = PlayedMinigameIndices.Get(i);
            if (index >= 0 && index < allMinigames.Count)
            {
                playedMinigameSO.Add(allMinigames[index]);
            }
        }
    }

    private void ClearAvailableMinigameIndices()
    {
        for (int i = 0; i < AvailableMinigameIndices.Length; i++)
        {
            AvailableMinigameIndices.Set(i, InvalidIndex);
        }
    }

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
