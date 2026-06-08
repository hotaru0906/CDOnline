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

public class BoardNode : MonoBehaviour
{
    [Header("Node Info")]
    public int nodeID;
    public TileType tileType = TileType.Empty;

    [Header("Connections")]
    [Tooltip("Ô tiếp theo (1 = linear, 2+ = nhánh - Phase 5)")]
    public List<BoardNode> nextNodes = new List<BoardNode>();

    [Tooltip("Tick nếu đây là ô CUỐI của một nhánh cụt (dead-end branch).")]
    public bool isDeadEnd = false;

    public Vector3 WorldPosition => transform.position;

    private void OnDrawGizmos()
    {
        Gizmos.color = GetTileColor();
        Gizmos.DrawSphere(transform.position, 0.35f);

        if (isDeadEnd)
        {
            Gizmos.color = Color.black;
            Gizmos.DrawWireSphere(transform.position, 0.5f);
        }

        Gizmos.color = Color.white;
        foreach (var next in nextNodes)
            if (next != null)
                Gizmos.DrawLine(transform.position, next.WorldPosition);
    }

    private Color GetTileColor() => tileType switch
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