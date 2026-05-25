using Fusion;
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Lưu trữ items của 1 player — attach vào player prefab cùng với PlayerNetworkData.
/// Max 8 slots, tất cả là Roulette items.
/// Host ghi dữ liệu, tất cả clients đọc qua Networked properties.
///
/// SETUP: Thêm component này vào player prefab trong Unity Editor.
/// </summary>
public class PlayerItemInventory : NetworkBehaviour
{
    private const int MAX_SLOTS = 8;

    /// <summary>
    /// Mảng items. Mỗi phần tử là (int)ItemEffect, -1 = slot trống.
    /// </summary>
    [Networked, Capacity(MAX_SLOTS)]
    public NetworkArray<int> HeldItems => default;

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

        // Host khởi tạo tất cả slots về -1 (trống)
        if (HasStateAuthority)
        {
            for (int i = 0; i < MAX_SLOTS; i++)
                HeldItems.Set(i, -1);
        }

        Debug.Log($"[PlayerItemInventory] Registered for player {playerId}");
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        int playerId = Object.InputAuthority.PlayerId;
        _registry.Remove(playerId);
    }

    // =====================================================================
    // PUBLIC API — chỉ gọi trên host (HasStateAuthority)
    // =====================================================================

    /// <summary>
    /// Thêm item vào slot trống đầu tiên.
    /// Nếu đầy (8 items): auto discard slot 0 (oldest), shift left, thêm vào cuối.
    /// </summary>
    public bool AddItem(ItemEffect effect)
    {
        if (!HasStateAuthority)
        {
            Debug.LogWarning("[PlayerItemInventory] AddItem chỉ gọi được trên host!");
            return false;
        }

        // Tìm slot trống
        for (int i = 0; i < MAX_SLOTS; i++)
        {
            if (HeldItems.Get(i) == -1)
            {
                HeldItems.Set(i, (int)effect);
                Debug.Log($"[Inventory] P{Object.InputAuthority.PlayerId} +{effect} → slot {i}");
                return true;
            }
        }

        // Đầy — discard oldest, shift left, thêm vào cuối
        Debug.Log($"[Inventory] P{Object.InputAuthority.PlayerId} full — discard {(ItemEffect)HeldItems.Get(0)}");
        for (int i = 0; i < MAX_SLOTS - 1; i++)
            HeldItems.Set(i, HeldItems.Get(i + 1));
        HeldItems.Set(MAX_SLOTS - 1, (int)effect);
        return true;
    }

    /// <summary>Xóa item tại slot chỉ định. Set về -1.</summary>
    public void RemoveItem(int slot)
    {
        if (!HasStateAuthority) return;
        if (slot < 0 || slot >= MAX_SLOTS) return;

        Debug.Log($"[Inventory] P{Object.InputAuthority.PlayerId} -slot{slot} ({(ItemEffect)HeldItems.Get(slot)})");
        HeldItems.Set(slot, -1);
    }

    /// <summary>Số item đang giữ (không tính slot -1).</summary>
    public int GetItemCount()
    {
        int count = 0;
        for (int i = 0; i < MAX_SLOTS; i++)
            if (HeldItems.Get(i) != -1) count++;
        return count;
    }

    /// <summary>Lấy list các ItemEffect đang giữ (bỏ qua slot trống).</summary>
    public List<ItemEffect> GetItems()
    {
        var list = new List<ItemEffect>();
        for (int i = 0; i < MAX_SLOTS; i++)
        {
            int v = HeldItems.Get(i);
            if (v != -1) list.Add((ItemEffect)v);
        }
        return list;
    }
}
