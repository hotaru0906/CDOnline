/// <summary>
/// Kiểu kết quả của minigame — dùng để BaseMinigameController biết cách tính winner.
/// </summary>
public enum MinigameResultType
{
    SingleWinner,  // 1 người thắng duy nhất (MG1 - Glass Bridge)
    Ranked,        // Xếp hạng theo thứ tự về đích (MG2 - Racing)
    Team,          // Tính điểm theo team (future)
    Score          // Người nhiều điểm nhất thắng (future)
}

/// <summary>
/// Dữ liệu kết quả của 1 player sau khi minigame kết thúc.
/// Dùng cho Scoreboard và GameManager.EndMinigame().
/// </summary>
public struct MinigameResultData
{
    public Fusion.PlayerRef Player;
    public int   Rank;        // 1 = nhất, 2 = nhì... 0 = chưa xác định
    public float FinishTime;  // Thời điểm về đích (giây kể từ khi game bắt đầu)
    public int   Score;       // Điểm số (dùng cho Score mode)
}
