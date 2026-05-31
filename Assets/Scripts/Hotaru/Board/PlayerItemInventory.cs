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

    // =====================================================================
    // STATIC REGISTRY — tra cứu nhanh theo PlayerId
    // =====================================================================

    private static readonly Dictionary<int, PlayerItemInventory> _registry = new();

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
        int playerId = Object.InputAuthority.PlayerId;
        _registry[playerId] = this;

        if (HasStateAuthority)
        {
            for (int i = 0; i < MAX_BOARD_SLOTS; i++)
                BoardItems.Set(i, -1);
            for (int i = 0; i < MAX_ROULETTE_SLOTS; i++)
                RouletteItems.Set(i, -1);
        }

        Debug.Log($"[PlayerItemInventory] Registered for player {playerId}");
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        int playerId = Object.InputAuthority.PlayerId;
        _registry.Remove(playerId);
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
        if (!HasStateAuthority)
        {
            Debug.LogWarning("[PlayerItemInventory] AddBoardItem chỉ gọi được trên host!");
            return false;
        }

        for (int i = 0; i < MAX_BOARD_SLOTS; i++)
        {
            if (BoardItems.Get(i) == -1)
            {
                BoardItems.Set(i, (int)effect);
                Debug.Log($"[Inventory] P{Object.InputAuthority.PlayerId} +Board:{effect} → slot {i}");
                return true;
            }
        }

        Debug.LogWarning($"[Inventory] P{Object.InputAuthority.PlayerId} BoardItems FULL — từ chối {effect}");
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

    // =====================================================================
    // ROULETTE ITEMS API — chỉ gọi trên host
    // =====================================================================

    /// <summary>
    /// Thêm Roulette item vào slot trống đầu tiên.
    /// Nếu đầy (8 items): auto discard oldest.
    /// </summary>
    public bool AddRouletteItem(ItemEffect effect)
    {
        if (!HasStateAuthority)
        {
            Debug.LogWarning("[PlayerItemInventory] AddRouletteItem chỉ gọi được trên host!");
            return false;
        }

        for (int i = 0; i < MAX_ROULETTE_SLOTS; i++)
        {
            if (RouletteItems.Get(i) == -1)
            {
                RouletteItems.Set(i, (int)effect);
                Debug.Log($"[Inventory] P{Object.InputAuthority.PlayerId} +Roulette:{effect} → slot {i}");
                return true;
            }
        }

        Debug.LogWarning($"[Inventory] P{Object.InputAuthority.PlayerId} RouletteItems FULL — từ chối {effect}");
        return false;
    }

    /// <summary>Xóa Roulette item tại slot chỉ định.</summary>
    public void RemoveRouletteItem(int slot)
    {
        if (!HasStateAuthority) return;
        if (slot < 0 || slot >= MAX_ROULETTE_SLOTS) return;
        Debug.Log($"[Inventory] P{Object.InputAuthority.PlayerId} -RouletteSlot{slot} ({(ItemEffect)RouletteItems.Get(slot)})");
        RouletteItems.Set(slot, -1);
    }

    /// <summary>Số Roulette item đang giữ.</summary>
    public int GetRouletteItemCount()
    {
        int count = 0;
        for (int i = 0; i < MAX_ROULETTE_SLOTS; i++)
            if (RouletteItems.Get(i) != -1) count++;
        return count;
    }

    /// <summary>List ItemEffect (Roulette) đang giữ.</summary>
    public List<ItemEffect> GetRouletteItems()
    {
        var list = new List<ItemEffect>();
        for (int i = 0; i < MAX_ROULETTE_SLOTS; i++)
        {
            int v = RouletteItems.Get(i);
            if (v != -1) list.Add((ItemEffect)v);
        }
        return list;
    }
}

