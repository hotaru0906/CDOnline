using UnityEngine;
using System.Collections.Generic;

public enum TileType
{
    Empty,
    Item,
    Steal,
    Toss,
    Jackpot,
    Gamble
}

/// <summary>
/// Một ô (node) trên bàn cờ.
/// Đặt trong BoardScene, kết nối với nhau qua nextNodes.
/// </summary>
public class BoardNode : MonoBehaviour
{
    [Header("Node Info")]
    public int nodeID;
    public TileType tileType = TileType.Empty;

    [Header("Connections")]
    [Tooltip("Ô tiếp theo (1 = linear, 2+ = nhánh - Phase 5)")]
    public List<BoardNode> nextNodes = new List<BoardNode>();

    [Tooltip("Tick nếu đây là ô CUỐI của một nhánh cụt (dead-end branch).\n" +
             "Khi player tung xúc xắc QUÁ số bước cần để đến đây,\n" +
             "token sẽ đi ĐẾN ô này rồi BOUNCE BACK số bước còn dư.\n" +
             "Player sẽ KHÔNG nhận hiệu ứng của ô này khi overshoot.\n" +
             "Chỉ nhận hiệu ứng khi đứng ĐÚNG ô này (không dư bước).")]
    public bool isDeadEnd = false;

    public Vector3 WorldPosition => transform.position;

    /// <summary>
    /// Tạo effect tương ứng với TileType của ô này.
    /// Gọi trên host khi player đứng lên ô.
    /// pool: cần thiết cho Item và Jackpot tiles; có thể null với các tile khác.
    /// </summary>
    public IBoardTileEffect CreateEffect(BoardItemPool pool = null)
    {
        return tileType switch
        {
            TileType.Item    => new ItemTileEffect(pool),
            TileType.Steal   => new StealTileEffect(),
            TileType.Toss    => new TossTileEffect(),
            TileType.Jackpot => new JackpotTileEffect(pool),
            TileType.Gamble  => new GambleTileEffect(),
            _                => new EmptyTileEffect()
        };
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = GetTileColor();
        Gizmos.DrawSphere(transform.position, 0.35f);

        // Vẽ vòng tròn ngoài màu đen nếu là dead-end
        if (isDeadEnd)
        {
            Gizmos.color = Color.black;
            Gizmos.DrawWireSphere(transform.position, 0.5f);
        }

        Gizmos.color = Color.white;
        foreach (var next in nextNodes)
        {
            if (next != null)
                Gizmos.DrawLine(transform.position, next.WorldPosition);
        }
    }

    private Color GetTileColor()
    {
        return tileType switch
        {
            TileType.Empty   => new Color(0.6f, 0.6f, 0.6f),
            TileType.Item    => Color.green,
            TileType.Steal   => Color.red,
            TileType.Toss    => Color.magenta,
            TileType.Jackpot => new Color(1f, 0.85f, 0f),
            TileType.Gamble  => new Color(1f, 0.5f, 0f),
            _                => Color.white
        };
    }
}
