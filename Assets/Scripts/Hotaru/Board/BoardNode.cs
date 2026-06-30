using UnityEngine;
using System.Collections.Generic;
using Unity.VisualScripting;
using Unity.VisualScripting.Antlr3.Runtime;
using System.Runtime.CompilerServices;


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
    [Header("Token Offsets (Spawn at Node)")]

    [SerializeField]
    private Vector3[] spawnOffsets = new Vector3[4]
    {
        new Vector3(-0.9f,0f,0f),   // Left
        new Vector3(0f,0f,0.9f),    // Top
        new Vector3(0.9f,0f,0f),    // Right
        new Vector3(0f,0f,-0.9f)    // Bottom
    };

    [SerializeField]
    private Vector3 centerOffset = Vector3.zero;

    public Vector3 GetSpawnPosition(int playerSlot)
    {
        playerSlot = Mathf.Clamp(playerSlot,0,3);

        return transform.position + spawnOffsets[playerSlot];
    }

    public Vector3 GetCenterPosition()
    {
        return transform.position + centerOffset;
    }
    
    [Header("Node Info")]
    public int nodeID;
    public TileType tileType = TileType.Empty;

    [Header("Connections")]
    [Tooltip("Ô tiếp theo (1 = linear, 2+ = nhánh - Phase 5)")]
    public List<BoardNode> nextNodes = new List<BoardNode>();

    [Tooltip("Tick nếu đây là ô CUỐI của một nhánh cụt (dead-end branch).")]
    public bool isDeadEnd = false;

    public Vector3 WorldPosition => transform.position;

    private static readonly Vector3[] PlayerOffsets =
    {
        new Vector3(-0.35f, 0f, -0.35f), // Slot 0
        new Vector3( 0.35f, 0f, -0.35f), // Slot 1
        new Vector3(-0.35f, 0f,  0.35f), // Slot 2
        new Vector3( 0.35f, 0f,  0.35f)  // Slot 3
    };

    public Vector3 GetPlayerPosition(int playerSlot)
    {
        playerSlot = Mathf.Clamp(playerSlot, 0, 3);

        float distance = 1.2f;   

        Vector3 offset = playerSlot switch
        {
            0 => Vector3.left * distance,
            1 => Vector3.forward * distance,
            2 => Vector3.right * distance,
            3 => Vector3.back * distance,
            _ => Vector3.zero
        };

        return transform.position + offset;
    }

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