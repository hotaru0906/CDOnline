using UnityEngine;

/// <summary>
/// Xúc xắc bàn cờ — Phase 0: random 1-6.
/// Chỉ host gọi Roll(), kết quả được sync tới tất cả clients qua RPC trong BoardManager.
/// </summary>
public class BoardDice : MonoBehaviour
{
    [SerializeField] private int minValue = 1;
    [SerializeField] private int maxValue = 6;

    /// <summary>
    /// Tung xúc xắc, trả về giá trị ngẫu nhiên trong [minValue, maxValue].
    /// Chỉ gọi trên host.
    /// </summary>
    public int Roll()
    {
        return Random.Range(minValue, maxValue + 1);
    }
}
