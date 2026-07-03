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
    // PLAYER RESOURCES
    // =====================================================================

    /// <summary>
    /// Number of keys player owns.
    /// Keys are resources, not board items.
    /// </summary>
    [Networked]
    public int KeyCount { get; set; }

    [Networked]
    public int ChestCount { get; set; }

    public static System.Action<int, int, int> OnResourceChanged;
    public static System.Action<int> OnInventoryRegistered;
    public static System.Action<int> OnInventoryUnregistered;

    private int _lastRenderedKeyCount = -1;
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

            KeyCount = 0;

            ChestCount = 0;

            if (GameManager.Instance != null)
                GameManager.Instance.TryRestorePlayerResourceState(playerId, this);
        }
        Debug.Log($"[PlayerItemInventory] Registered for player {playerId}");

        // Force one initial push so UI can pick up current values after scene transitions.
        _lastRenderedKeyCount = KeyCount;
        _lastRenderedChestCount = ChestCount;
        OnResourceChanged?.Invoke(playerId, KeyCount, ChestCount);
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

        if (_lastRenderedKeyCount == KeyCount && _lastRenderedChestCount == ChestCount)
            return;

        _lastRenderedKeyCount = KeyCount;
        _lastRenderedChestCount = ChestCount;

        OnResourceChanged?.Invoke(playerId, KeyCount, ChestCount);
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

    // =====================================================================
    // KEY API
    // =====================================================================

    /// <summary>
    /// Add keys to player.
    /// </summary>
    public void AddKey(int amount = 1)
    {
        if (!HasStateAuthority)
        {
            Debug.LogWarning("[PlayerItemInventory] AddKey chỉ được gọi trên Host!");
            return;
        }

        if (amount <= 0)
            return;

        KeyCount += amount;

        if (GameManager.Instance != null)
            GameManager.Instance.SavePlayerResourceState(Object.InputAuthority.PlayerId, KeyCount, ChestCount);

        Debug.Log($"[Inventory] P{Object.InputAuthority.PlayerId} +{amount} Key (Total={KeyCount})");
    }

    /// <summary>
    /// Check if player has at least one key.
    /// </summary>
    public bool HasKey()
    {
        return KeyCount > 0;
    }

    /// <summary>
    /// Consume keys.
    /// Returns false if player doesn't have enough keys.
    /// </summary>
    public bool ConsumeKey(int amount = 1)
    {
        if (!HasStateAuthority)
        {
            Debug.LogWarning("[PlayerItemInventory] ConsumeKey chỉ được gọi trên Host!");
            return false;
        }

        if (amount <= 0)
            return false;

        if (KeyCount < amount)
        {
            Debug.Log($"[Inventory] P{Object.InputAuthority.PlayerId} không đủ Key.");
            return false;
        }

        KeyCount -= amount;

        if (GameManager.Instance != null)
            GameManager.Instance.SavePlayerResourceState(Object.InputAuthority.PlayerId, KeyCount, ChestCount);

        Debug.Log($"[Inventory] P{Object.InputAuthority.PlayerId} -{amount} Key (Remain={KeyCount})");

        return true;
    }

    public int GetKeyCount()
    {
        return KeyCount;
    }

    public void AddChest()
    {
        if (!HasStateAuthority)
            return;

        ChestCount++;

        if (GameManager.Instance != null)
            GameManager.Instance.SavePlayerResourceState(Object.InputAuthority.PlayerId, KeyCount, ChestCount);

        Debug.Log($"[Inventory] P{Object.InputAuthority.PlayerId} Chest = {ChestCount}");
    }

    public void SetResourceCounts(int keyCount, int chestCount)
    {
        if (!HasStateAuthority)
            return;

        KeyCount = Mathf.Max(0, keyCount);
        ChestCount = Mathf.Max(0, chestCount);

        if (GameManager.Instance != null)
            GameManager.Instance.SavePlayerResourceState(Object.InputAuthority.PlayerId, KeyCount, ChestCount);
    }

    public int GetChestCount()
    {
        return ChestCount;
    }
}

