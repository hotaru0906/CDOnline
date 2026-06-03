using UnityEngine;

/// <summary>
/// Xúc xắc bàn cờ — Phase 0: random 1-6.
/// Chỉ host gọi Roll(), kết quả được sync tới tất cả clients qua RPC trong BoardManager.
/// </summary>
public class BoardDice : MonoBehaviour
{
    [SerializeField] private int numberOfDice = 2;
    [SerializeField] private int minValuePerDie = 1;
    [SerializeField] private int maxValuePerDie = 6;

    public int LastTotal { get; private set; }
    public int[] LastRolls { get; private set; } = new int[2] { 1, 1 };

    /// <summary>
    /// Tung nhiều xúc xắc và trả về tổng.
    /// Mặc định là 2 xúc xắc, mỗi viên trong [minValuePerDie, maxValuePerDie].
    /// Chỉ gọi trên host.
    /// </summary>
    public int Roll()
    {
        int diceCount = Mathf.Max(1, numberOfDice);

        if (LastRolls == null || LastRolls.Length != diceCount)
            LastRolls = new int[diceCount];

        LastTotal = 0;
        for (int i = 0; i < diceCount; i++)
        {
            int value = Random.Range(minValuePerDie, maxValuePerDie + 1);
            LastRolls[i] = value;
            LastTotal += value;
        }

        return LastTotal;
    }
}
