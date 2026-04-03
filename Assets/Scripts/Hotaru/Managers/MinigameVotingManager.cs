using UnityEngine;
using System.Collections.Generic;
using Fusion;

/// <summary>
/// Quản lý các minigame đã chơi và minigame khả dụng cho lần vote tiếp theo
/// - Track minigame đã chơi trong session
/// - Loại bỏ minigame đã chơi khỏi danh sách vote
/// - Reset khi tất cả minigame đã được chơi hoặc khi vào Roulette
/// </summary>
public class MinigameVotingManager : NetworkBehaviour
{
    #region Singleton
    public static MinigameVotingManager Instance { get; private set; }
    #endregion

    [Header("Minigame Configuration")]
    [SerializeField] private List<MinigameData> allMinigames = new List<MinigameData>();
    
    [Header("Settings")]
    [SerializeField] private int displayCount = 3; // Số minigame hiển thị để vote mỗi lần
    [SerializeField] private bool shuffleMinigames = true;
    
    #region Networked Properties
    /// <summary>
    /// Danh sách index của các minigame đã chơi trong session này
    /// </summary>
    [Networked, Capacity(20)]
    private NetworkArray<int> PlayedMinigameIndices => default;
    
    [Networked]
    private int PlayedCount { get; set; }
    
    /// <summary>
    /// Danh sách index của các minigame khả dụng cho lần vote hiện tại
    /// </summary>
    [Networked, Capacity(10)]
    private NetworkArray<int> AvailableMinigameIndices => default;
    
    [Networked]
    private int AvailableCount { get; set; }
    #endregion

    #region Events
    public event System.Action OnMinigameListUpdated;
    #endregion
    
    /// <summary>
    /// Kiểm tra xem manager đã spawn và sẵn sàng chưa
    /// Phải kiểm tra trước khi truy cập Networked properties
    /// </summary>
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

    public override void Spawned()
    {
        IsReady = true;
        Debug.Log($"[MinigameVotingManager] Spawned. Total minigames: {allMinigames.Count}");
        
        if (HasStateAuthority)
        {
            ResetPlayedMinigames();
            PrepareNextVotingRound();
        }
    }

    #region Public Methods
    /// <summary>
    /// Lấy danh sách minigame data khả dụng cho voting
    /// </summary>
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

    /// <summary>
    /// Lấy số lượng minigame khả dụng
    /// </summary>
    public int GetAvailableMinigameCount()
    {
        // Kiểm tra xem đã spawn chưa trước khi truy cập Networked property
        if (!IsReady) return 0;
        return AvailableCount;
    }

    /// <summary>
    /// Lấy MinigameData theo index trong danh sách available
    /// </summary>
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

    /// <summary>
    /// Đánh dấu minigame đã được chơi
    /// </summary>
    public void MarkMinigamePlayed(int availableIndex)
    {
        if (!IsReady)
        {
            Debug.LogWarning("[MinigameVotingManager] Cannot mark played - not spawned yet");
            return;
        }
        
        if (!HasStateAuthority)
        {
            RPC_RequestMarkPlayed(availableIndex);
            return;
        }

        if (availableIndex < 0 || availableIndex >= AvailableCount)
        {
            Debug.LogWarning($"[MinigameVotingManager] Invalid available index: {availableIndex}");
            return;
        }

        int actualIndex = AvailableMinigameIndices.Get(availableIndex);
        
        // Kiểm tra xem đã chơi chưa
        for (int i = 0; i < PlayedCount; i++)
        {
            if (PlayedMinigameIndices.Get(i) == actualIndex)
            {
                Debug.Log($"[MinigameVotingManager] Minigame {actualIndex} already marked as played");
                return;
            }
        }

        // Thêm vào danh sách đã chơi
        if (PlayedCount < 20)
        {
            PlayedMinigameIndices.Set(PlayedCount, actualIndex);
            PlayedCount++;
            Debug.Log($"[MinigameVotingManager] Marked minigame {actualIndex} as played. Total played: {PlayedCount}");
        }
    }

    /// <summary>
    /// Chuẩn bị danh sách minigame cho lần vote tiếp theo
    /// Loại bỏ các minigame đã chơi
    /// </summary>
    public void PrepareNextVotingRound()
    {
        if (!HasStateAuthority || !IsReady) return;

        Debug.Log("[MinigameVotingManager] Preparing next voting round...");

        // Lấy danh sách minigame chưa chơi
        List<int> unplayedIndices = new List<int>();
        for (int i = 0; i < allMinigames.Count; i++)
        {
            if (!IsMinigamePlayed(i))
            {
                unplayedIndices.Add(i);
            }
        }

        Debug.Log($"[MinigameVotingManager] Unplayed minigames: {unplayedIndices.Count}");

        // Nếu không còn minigame nào, reset danh sách
        if (unplayedIndices.Count == 0)
        {
            Debug.Log("[MinigameVotingManager] All minigames played! Resetting...");
            ResetPlayedMinigames();
            for (int i = 0; i < allMinigames.Count; i++)
            {
                unplayedIndices.Add(i);
            }
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
        
        // Clear array
        for (int i = 0; i < 20; i++)
        {
            PlayedMinigameIndices.Set(i, -1);
        }
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
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestMarkPlayed(int availableIndex)
    {
        MarkMinigamePlayed(availableIndex);
    }

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
