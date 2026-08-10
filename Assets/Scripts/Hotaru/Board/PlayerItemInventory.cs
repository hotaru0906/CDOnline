using Fusion;
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Lưu trữ items của 1 player — attach vào player prefab cùng với PlayerNetworkData.
/// BoardItems (max 4 slots): nhận từ tile bàn cờ, dùng trong Board phase.
/// RouletteItems (max 8 slots): nhận qua Board Race reward, dùng trong Roulette phase.
/// Host ghi dữ liệu, tất cả clients đọc qua Networked properties.
///
/// SETUP: Thêm component này vào player prefab trong Unity Editor.
/// </summary>
public class PlayerItemInventory : NetworkBehaviour
{
    private const int MAX_BOARD_SLOTS    = 4;
    private const int MAX_ROULETTE_SLOTS = 8;

    /// <summary>Board items (BoardItemEffect). -1 = slot trống.</summary>
    [Networked, Capacity(MAX_BOARD_SLOTS)]
    public NetworkArray<int> BoardItems => default;

    /// <summary>Roulette items (ItemEffect). -1 = slot trống.</summary>
    [Networked, Capacity(MAX_ROULETTE_SLOTS)]
    public NetworkArray<int> RouletteItems => default;

    public static System.Action<int, int> OnResourceChanged;
    public static System.Action<int> OnInventoryRegistered;
    public static System.Action<int> OnInventoryUnregistered;
    private int _lastRenderedChestCount = -1;

    // =====================================================================
    // STATIC REGISTRY — tra cứu nhanh theo PlayerId
    // =====================================================================

    private static readonly Dictionary<int, PlayerItemInventory> _registry = new();

    private static int _spawnedCount = 0;

    /// <summary>Tìm inventory của player theo PlayerId. Trả về null nếu chưa spawn.</summary>
    public static PlayerItemInventory GetForPlayer(int playerId)
    {
        _registry.TryGetValue(playerId, out var inv);
        return inv;
    }

    // =====================================================================
    // LIFECYCLE
    // =====================================================================

    public override void Spawned()
    {

        Debug.Log(
        $"Spawned InstanceID={GetInstanceID()} " +
        $"PlayerId={Object.InputAuthority.PlayerId}");

        int playerId = Object.InputAuthority.PlayerId;
        _registry[playerId] = this;

        OnInventoryRegistered?.Invoke(playerId);

        Debug.Log(
            $"Registry[{playerId}] = {GetInstanceID()}");

        if (HasStateAuthority)
        {
            Debug.Log("INITIALIZE INVENTORY TO -1");

            for (int i = 0; i < MAX_BOARD_SLOTS; i++)
                BoardItems.Set(i, -1);

            for (int i = 0; i < MAX_ROULETTE_SLOTS; i++)
                RouletteItems.Set(i, -1);

            if (GameManager.Instance != null)
            {
                GameManager.Instance.TryRestorePlayerResourceState(playerId, this);

                if (GameManager.Instance.CurrentState == GameState.Board)
                    GameManager.Instance.TryRestoreBoardItemsForPlayer(playerId, this);
            }
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        int playerId = Object.InputAuthority.PlayerId;
        _registry.Remove(playerId);
        OnInventoryUnregistered?.Invoke(playerId);
    }

    public override void Render()
    {
        base.Render();

        int playerId = Object.InputAuthority.PlayerId;
    }

    // =====================================================================
    // BOARD ITEMS API — chỉ gọi trên host
    // =====================================================================

    /// <summary>
    /// Thêm Board item vào slot trống đầu tiên.
    /// Nếu đầy (4 items): từ chối, trả về false.
    /// </summary>
    public bool AddBoardItem(BoardItemEffect effect)
    {
        Debug.Log($"AddBoardItem HasStateAuthority = {HasStateAuthority}");

        if (!HasStateAuthority)
        {
            Debug.LogWarning("[PlayerItemInventory] AddBoardItem chỉ gọi được trên host!");
            return false;
        }

        for (int i = 0; i < MAX_BOARD_SLOTS; i++)
        {
            Debug.Log($"Slot {i} = {BoardItems.Get(i)}");

            if (BoardItems.Get(i) == -1)
            {
                BoardItems.Set(i, (int)effect);

                Debug.Log($"VERIFY Slot {i} = {BoardItems.Get(i)}");

                Debug.Log($"[Inventory] P{Object.InputAuthority.PlayerId} +Board:{effect} -> slot {i}");
                return true;
            }
        }

        Debug.LogWarning($"[Inventory] FULL");
        return false;
    }

    /// <summary>Xóa Board item tại slot chỉ định. Set về -1.</summary>
    public void RemoveBoardItem(int slot)
    {
        if (!HasStateAuthority) return;
        if (slot < 0 || slot >= MAX_BOARD_SLOTS) return;
        Debug.Log($"[Inventory] P{Object.InputAuthority.PlayerId} -BoardSlot{slot} ({(BoardItemEffect)BoardItems.Get(slot)})");
        BoardItems.Set(slot, -1);
    }

    /// <summary>Số Board item đang giữ.</summary>
    public int GetBoardItemCount()
    {
        int count = 0;
        for (int i = 0; i < MAX_BOARD_SLOTS; i++)
            if (BoardItems.Get(i) != -1) count++;
        return count;
    }

    /// <summary>List BoardItemEffect đang giữ (bỏ qua slot trống).</summary>
    public List<BoardItemEffect> GetBoardItems()
    {
        var list = new List<BoardItemEffect>();
        for (int i = 0; i < MAX_BOARD_SLOTS; i++)
        {
            int v = BoardItems.Get(i);
            if (v != -1) list.Add((BoardItemEffect)v);
        }
        return list;
    }

    /// <summary>
    /// List (slot, effect) của Board items — cần slot index khi muốn remove chính xác.
    /// </summary>
    public List<(int slot, BoardItemEffect effect)> GetBoardItemsWithSlots()
    {
        var list = new List<(int, BoardItemEffect)>();
        for (int i = 0; i < MAX_BOARD_SLOTS; i++)
        {
            int v = BoardItems.Get(i);
            if (v != -1) list.Add((i, (BoardItemEffect)v));
        }
        return list;
    }
}

