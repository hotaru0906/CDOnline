using UnityEngine;
using System.Collections.Generic;
using Fusion;

/// <summary>
/// Quản lý danh sách minigame để vote theo cơ chế "hàng đợi vòng tròn" (rotation queue):
/// - KHÔNG random khi bắt đầu voting — thứ tự luôn theo đúng thứ tự khai báo trong allMinigames.
/// - Khi 1 minigame được CHỌN để chơi, nó bị đưa xuống CUỐI hàng đợi (tạm thời bỏ khỏi danh
///   sách vote), và minigame đang ở ĐẦU hàng đợi được kéo vào thế chỗ trong danh sách vote.
///
/// Ví dụ với 7 minigame, hiển thị 5 (displayCount = 5), thứ tự gốc 1-2-3-4-5-6-7:
///   Ban đầu:      [1,2,3,4,5]   (hàng đợi chờ: 6,7)
///   Chơi mg 1 ->  [2,3,4,5,6]   (hàng đợi chờ: 7,1)
///   Chơi mg 3 ->  [2,4,5,6,7]   (hàng đợi chờ: 1,3)
///   Chơi mg 5 ->  [1,2,4,6,7]   (hàng đợi chờ: 3,5)
/// </summary>
public class MinigameVotingManager : NetworkBehaviour
{
    #region Singleton
    public static MinigameVotingManager Instance { get; private set; }
    #endregion

    [Header("Minigame Configuration")]
    [SerializeField] private List<MinigameData> allMinigames = new List<MinigameData>();

    [Header("Settings")]
    [SerializeField] private int displayCount = 5; // Số minigame hiển thị để vote mỗi lần

    private const int InvalidIndex = -1;

    #region Networked Properties
    /// <summary>
    /// Hàng đợi vòng tròn chứa TẤT CẢ minigame theo actual index:
    /// - RotationQueue[0 .. AvailableCount-1]      = đang hiển thị để vote (window)
    /// - RotationQueue[AvailableCount .. Total-1]  = đang chờ (FIFO). Vị trí AvailableCount là
    ///   phần tử ĐẦU hàng đợi — sẽ được kéo vào window ngay khi có 1 minigame bị chơi.
    /// </summary>
    [Networked, Capacity(20)]
    private NetworkArray<int> RotationQueue => default;

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
        }
    }

    #region Public Methods
    public List<MinigameData> GetAvailableMinigames()
    {
        List<MinigameData> available = new List<MinigameData>();

        if (!IsReady) return available;

        for (int i = 0; i < AvailableCount; i++)
        {
            int index = RotationQueue.Get(i);
            if (index >= 0 && index < allMinigames.Count)
            {
                available.Add(allMinigames[index]);
            }
        }

        return available;
    }

    public int GetAvailableMinigameCount()
    {
        if (!IsReady) return 0;
        return AvailableCount;
    }

    public MinigameData GetMinigameByAvailableIndex(int availableIndex)
    {
        if (!IsReady) return null;
        if (availableIndex < 0 || availableIndex >= AvailableCount) return null;

        int actualIndex = RotationQueue.Get(availableIndex);
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

        int actualIndex = RotationQueue.Get(availableIndex);
        if (actualIndex < 0 || actualIndex >= allMinigames.Count)
            return InvalidIndex;

        return actualIndex;
    }

    public int GetAvailableIndexByActualIndex(int actualIndex)
    {
        if (!IsReady) return InvalidIndex;
        if (actualIndex < 0 || actualIndex >= allMinigames.Count) return InvalidIndex;

        for (int i = 0; i < AvailableCount; i++)
        {
            if (RotationQueue.Get(i) == actualIndex)
                return i;
        }

        return InvalidIndex;
    }

    public MinigameData GetMinigameByActualIndex(int actualIndex)
    {
        if (!IsReady) return null;
        if (actualIndex < 0 || actualIndex >= allMinigames.Count) return null;
        return allMinigames[actualIndex];
    }

    /// <summary>
    /// Đánh dấu minigame đã được CHỌN để chơi: đưa nó xuống CUỐI hàng đợi (tạm thời bỏ khỏi
    /// danh sách vote) và kéo minigame ở ĐẦU hàng đợi vào thế chỗ. Gọi ngay khi minigame được
    /// chọn để bắt đầu (GameManager.StartMinigameActual, trước Tutorial/Countdown) — giữ nguyên
    /// thời điểm gọi như code cũ.
    /// </summary>
    public void MarkMinigamePlayedByActualIndex(int actualIndex)
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

        int total = allMinigames.Count;
        if (actualIndex < 0 || actualIndex >= total)
        {
            Debug.LogWarning($"[MinigameVotingManager] Invalid actual index: {actualIndex}");
            return;
        }

        int windowIndex = -1;
        for (int i = 0; i < AvailableCount; i++)
        {
            if (RotationQueue.Get(i) == actualIndex)
            {
                windowIndex = i;
                break;
            }
        }

        if (windowIndex < 0)
        {
            Debug.LogWarning($"[MinigameVotingManager] actualIndex {actualIndex} không nằm trong danh sách vote hiện tại, bỏ qua rotation.");
            return;
        }

        // Không có minigame nào đang chờ (tất cả đều đang hiển thị) -> không có gì để thay thế
        if (AvailableCount >= total)
        {
            Debug.Log($"[MinigameVotingManager] Không có hàng đợi (AvailableCount >= total). Minigame {allMinigames[actualIndex].minigameName} vẫn hiển thị.");
            return;
        }

        // Kéo phần tử ĐẦU hàng đợi vào đúng chỗ vừa trống trong window
        int nextFromQueue = RotationQueue.Get(AvailableCount);
        RotationQueue.Set(windowIndex, nextFromQueue);

        // Dịch phần hàng đợi còn lại lên 1 vị trí (giữ đúng thứ tự FIFO)
        for (int i = AvailableCount; i < total - 1; i++)
        {
            RotationQueue.Set(i, RotationQueue.Get(i + 1));
        }

        // Đưa minigame vừa chơi xuống CUỐI hàng đợi
        RotationQueue.Set(total - 1, actualIndex);

        AvailableListVersion++;

        string nextName = (nextFromQueue >= 0 && nextFromQueue < total) ? allMinigames[nextFromQueue].minigameName : "?";
        Debug.Log($"[MinigameVotingManager] Minigame {allMinigames[actualIndex].minigameName} bị đưa xuống cuối hàng đợi. Thay thế trong danh sách vote bởi: {nextName}");
    }

    /// <summary>
    /// Giữ lại để tương thích ngược - map availableIndex sang actualIndex rồi gọi hàm chính.
    /// </summary>
    public void MarkMinigamePlayed(int availableIndex)
    {
        int actualIndex = GetActualIndexByAvailableIndex(availableIndex);
        if (actualIndex < 0)
        {
            Debug.LogWarning($"[MinigameVotingManager] Invalid available index: {availableIndex}");
            return;
        }
        MarkMinigamePlayedByActualIndex(actualIndex);
    }

    /// <summary>
    /// Lấy 1 minigame ngẫu nhiên không nằm trong danh sách loại trừ - chỉ dùng để lấp đầy các ô
    /// còn lại của vòng quay tie-break cho đủ số lượng hiển thị (không liên quan tới rotation).
    /// </summary>
    public int GetRandomEligibleActualMinigameIndexExcluding(HashSet<int> excludeActualIndices)
    {
        if (!IsReady) return InvalidIndex;

        List<int> eligible = new List<int>();
        for (int i = 0; i < allMinigames.Count; i++)
        {
            if (excludeActualIndices != null && excludeActualIndices.Contains(i))
                continue;

            eligible.Add(i);
        }

        if (eligible.Count == 0)
            return InvalidIndex;

        return eligible[UnityEngine.Random.Range(0, eligible.Count)];
    }

    /// <summary>
    /// Chuẩn bị vòng vote tiếp theo. Với cơ chế hàng đợi mới, danh sách vote luôn được cập nhật
    /// ngay khi có minigame bị đánh dấu đã chơi (MarkMinigamePlayedByActualIndex), nên hàm này
    /// chỉ khởi tạo hàng đợi nếu chưa có dữ liệu — KHÔNG reshuffle/reset lại mỗi vòng vote.
    /// </summary>
    public void PrepareNextVotingRound()
    {
        if (!HasStateAuthority || !IsReady) return;

        if (AvailableCount > 0) return; // đã có danh sách rồi, giữ nguyên (không random lại)

        InitializeRotationQueue();
    }

    public void PrepareNextVotingRoundForRoulette()
    {
        PrepareNextVotingRound();
    }

    /// <summary>
    /// Reset hàng đợi minigame về ĐÚNG thứ tự gốc trong allMinigames (không random).
    /// Gọi khi bắt đầu match mới (GameManager.StartMatch).
    /// </summary>
    public void ResetPlayedMinigames()
    {
        if (!HasStateAuthority || !IsReady) return;

        Debug.Log("[MinigameVotingManager] Reset rotation queue về đúng thứ tự gốc trong allMinigames");
        InitializeRotationQueue();
    }

    private void InitializeRotationQueue()
    {
        int total = allMinigames.Count;
        int capacity = RotationQueue.Length;
        int count = Mathf.Min(total, capacity);

        for (int i = 0; i < capacity; i++)
        {
            RotationQueue.Set(i, i < count ? i : InvalidIndex);
        }

        AvailableCount = Mathf.Min(displayCount, count);
        AvailableListVersion++;

        Debug.Log($"[MinigameVotingManager] Rotation queue khởi tạo. Total={count}, Window={AvailableCount} (đúng thứ tự trong allMinigames, không random)");
    }

    /// <summary>
    /// Không còn khái niệm "bị chặn vĩnh viễn" trong cơ chế hàng đợi mới.
    /// Giữ lại để tương thích ngược, luôn trả về danh sách rỗng.
    /// </summary>
    public List<MinigameData> GetPlayedMinigames()
    {
        return new List<MinigameData>();
    }

    /// <summary>
    /// Trả về true nếu minigame KHÔNG nằm trong danh sách vote hiện tại (đang ở hàng đợi chờ).
    /// Giữ tên hàm cũ để tương thích ngược với code gọi nó.
    /// </summary>
    public bool IsMinigamePlayed(int actualIndex)
    {
        if (!IsReady) return false;

        for (int i = 0; i < AvailableCount; i++)
        {
            if (RotationQueue.Get(i) == actualIndex)
                return false;
        }
        return true;
    }

    /// <summary>
    /// Lấy tổng số minigame
    /// </summary>
    public int TotalMinigameCount => allMinigames.Count;

    /// <summary>
    /// Không còn theo dõi "đã chơi vĩnh viễn" trong cơ chế mới. Giữ lại để tương thích ngược.
    /// </summary>
    public int PlayedMinigameCount => 0;
    #endregion

    private void OnAvailableListVersionChanged()
    {
        Debug.Log($"[MinigameVotingManager] Minigame list version {AvailableListVersion} received");
        OnMinigameListUpdated?.Invoke();
    }
}