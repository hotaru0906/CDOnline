using UnityEngine;

/// <summary>
/// Xúc xắc bàn cờ dùng 1 viên 12 mặt (1–12).
/// Chỉ host gọi Roll(), kết quả được sync tới tất cả clients qua RPC trong BoardManager.
/// </summary>
public class BoardDice : MonoBehaviour
{
    [SerializeField] private int minValuePerDie = 1;
    [SerializeField] private int maxValuePerDie = 12;

    public int LastTotal { get; private set; }
    public int[] LastRolls { get; private set; } = new int[1] { 1 };

    /// <summary>
    /// Tung 1 viên xúc xắc 12 mặt và trả về kết quả.
    /// Chỉ gọi trên host.
    /// </summary>
    public int Roll()
    {
        int result = Random.Range(minValuePerDie, maxValuePerDie + 1);
        LastTotal = result;
        LastRolls = new[] { result };
        return result;
    }
}
