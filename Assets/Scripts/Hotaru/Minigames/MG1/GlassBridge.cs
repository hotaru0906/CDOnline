using Fusion;
using UnityEngine;

public class GlassBridge : NetworkBehaviour
{
    [Header("Bridge Settings")]
    [SerializeField] private int rowCount = 6;

    [Networked, Capacity(32)]
    private NetworkArray<NetworkBool> LeftIsSafe => default;

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

    private void RandomizeBridge()
    {
        for (int i = 0; i < rowCount && i < LeftIsSafe.Length; i++)
        {
            LeftIsSafe.Set(i, Random.value > 0.5f);
        }

        Debug.Log($"[GlassBridge] Randomized {rowCount} rows");
    }

    public bool IsPlatformSafe(int rowIndex, bool isLeft)
    {
        if (rowIndex < 0 || rowIndex >= rowCount || rowIndex >= LeftIsSafe.Length)
            return true;

        bool leftIsSafe = LeftIsSafe[rowIndex];
        return isLeft ? leftIsSafe : !leftIsSafe;
    }

    /// <summary>
    /// Client gọi method này để request Host kiểm tra platform
    /// </summary>
    public void RequestCheckPlatform(int rowIndex, bool isLeft)
    {
        Debug.Log($"[GlassBridge] RequestCheckPlatform row {rowIndex}, isLeft: {isLeft}");
        RPC_RequestCheck(rowIndex, isLeft);
    }

    /// <summary>
    /// RPC từ Client đến Host để kiểm tra và break platform nếu cần
    /// </summary>
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestCheck(int rowIndex, bool isLeft)
    {
        Debug.Log($"[GlassBridge] Host checking row {rowIndex}, isLeft: {isLeft}");

        bool isSafe = IsPlatformSafe(rowIndex, isLeft);

        if (!isSafe)
        {
            Debug.Log($"[GlassBridge] Platform NOT safe - breaking!");
            // Tìm platform và break qua RPC đến tất cả clients
            RPC_BreakPlatform(rowIndex, isLeft);
        }
        else
        {
            Debug.Log($"[GlassBridge] Platform is SAFE");
        }
    }

    public void BreakPlatform(GlassPlatform platform)
    {
        if (!Object.HasStateAuthority) return;

        // Chỉ gọi RPC, KHÔNG xử lý local ở đây
        RPC_BreakPlatform(platform.RowIndex, platform.IsLeft);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_BreakPlatform(int rowIndex, bool isLeft)
    {
        // Tìm đúng platform và break
        foreach (var platform in FindObjectsByType<GlassPlatform>(FindObjectsSortMode.None))
        {
            if (platform.RowIndex == rowIndex && platform.IsLeft == isLeft)
            {
                platform.Break();
                break;
            }
        }
    }

    public void ResetBridge()
    {
        if (!Object.HasStateAuthority) return;

        IsInitialized = false;
        RandomizeBridge();
        IsInitialized = true;

        Debug.Log("[GlassBridge] Bridge reset");
    }
}