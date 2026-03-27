using Fusion;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Glass Bridge minigame - Quản lý random logic cho các platform.
/// Mỗi hàng có 2 ô: 1 an toàn, 1 sẽ biến mất khi đạp vào.
/// Random đảm bảo mỗi hàng luôn có 1 ô sống và 1 ô chết.
/// </summary>
public class GlassBridge : NetworkBehaviour
{
    [Header("Bridge Settings")]
    [SerializeField] private int rowCount = 6;
    
    [Networked, Capacity(32)] 
    private NetworkArray<NetworkBool> LeftIsSafe { get; }
    
    [Networked]
    private NetworkBool IsInitialized { get; set; }

    public override void Spawned()
    {
        if (Object.HasStateAuthority && !IsInitialized)
        {
            RandomizeBridge();
            IsInitialized = true;
        }
    }

    /// <summary>
    /// Random xác định ô nào an toàn trong mỗi hàng.
    /// Mỗi hàng: 1 sống - 1 chết, không random riêng lẻ.
    /// </summary>
    private void RandomizeBridge()
    {
        for (int i = 0; i < rowCount && i < LeftIsSafe.Length; i++)
        {
            // Random 50/50: true = left an toàn, false = right an toàn
            LeftIsSafe.Set(i, Random.value > 0.5f);
        }
        
        Debug.Log($"[GlassBridge] Randomized {rowCount} rows");
    }

    /// <summary>
    /// Kiểm tra platform có an toàn không.
    /// </summary>
    public bool IsPlatformSafe(int rowIndex, bool isLeft)
    {
        if (rowIndex < 0 || rowIndex >= rowCount || rowIndex >= LeftIsSafe.Length)
            return true; // Default safe nếu invalid
            
        bool leftIsSafe = LeftIsSafe[rowIndex];
        return isLeft ? leftIsSafe : !leftIsSafe;
    }

    /// <summary>
    /// Được gọi khi player đạp vào platform không an toàn.
    /// </summary>
    public void BreakPlatform(GlassPlatform platform)
    {
        if (Object.HasStateAuthority)
        {
            RPC_BreakPlatform(platform.RowIndex, platform.IsLeft);
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_BreakPlatform(int rowIndex, bool isLeft)
    {
        Debug.Log($"[GlassBridge] Platform broken at row {rowIndex}, isLeft: {isLeft}");
    }

    /// <summary>
    /// Reset và random lại bridge.
    /// </summary>
    public void ResetBridge()
    {
        if (!Object.HasStateAuthority) return;
        RandomizeBridge();
        Debug.Log("[GlassBridge] Bridge re-randomized");
    }

}
