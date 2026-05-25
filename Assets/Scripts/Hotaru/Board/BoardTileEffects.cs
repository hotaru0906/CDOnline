using UnityEngine;

/// <summary>
/// Stub implementations cho mỗi TileType.
/// Phase 1: chỉ log + trả về display message.
/// Phase 2+: thay thế từng class bằng logic thật.
/// </summary>

public class EmptyTileEffect : IBoardTileEffect
{
    public string Resolve(int playerId)
    {
        Debug.Log($"[Tile] Player {playerId} — EMPTY: không có gì xảy ra.");
        return string.Empty;
    }
}

public class ItemTileEffect : IBoardTileEffect
{
    private readonly ItemPool _pool;

    public ItemTileEffect(ItemPool pool) { _pool = pool; }

    public string Resolve(int playerId)
    {
        if (_pool == null)
        {
            Debug.LogWarning("[Tile] ItemPool chưa được assign trong BoardManager Inspector.");
            return "GOT ITEM!";
        }

        var inv = PlayerItemInventory.GetForPlayer(playerId);
        if (inv == null)
        {
            Debug.LogWarning($"[Tile] Không tìm thấy PlayerItemInventory cho player {playerId}. Thêm component vào player prefab.");
            return "GOT ITEM!";
        }

        var item = _pool.GetRandom();
        if (item == null) return "GOT ITEM! (pool empty)";

        inv.AddItem(item.effectType);
        Debug.Log($"[Tile] Player {playerId} nhận: {item.itemName} ({item.effectType})");
        return $"GOT: {item.itemName}";
    }
}

public class StealTileEffect : IBoardTileEffect
{
    public string Resolve(int playerId)
    {
        Debug.Log($"[Tile] Player {playerId} — STEAL: cướp item người khác (Phase 3).");
        return "STEAL!";
    }
}

public class TossTileEffect : IBoardTileEffect
{
    public string Resolve(int playerId)
    {
        Debug.Log($"[Tile] Player {playerId} — TOSS: mất random item (Phase 3).");
        return "TOSS ITEM!";
    }
}

public class ShuffleTileEffect : IBoardTileEffect
{
    public string Resolve(int playerId)
    {
        Debug.Log($"[Tile] Player {playerId} — SHUFFLE: xáo trộn vị trí / item (Phase 3).");
        return "SHUFFLE!";
    }
}

public class JackpotTileEffect : IBoardTileEffect
{
    private readonly ItemPool _pool;

    public JackpotTileEffect(ItemPool pool) { _pool = pool; }

    public string Resolve(int playerId)
    {
        if (_pool == null)
        {
            Debug.LogWarning("[Tile] ItemPool chưa được assign trong BoardManager Inspector.");
            return "JACKPOT!";
        }

        var inv = PlayerItemInventory.GetForPlayer(playerId);
        if (inv == null)
        {
            Debug.LogWarning($"[Tile] Không tìm thấy PlayerItemInventory cho player {playerId}.");
            return "JACKPOT!";
        }

        int granted = 0;
        for (int i = 0; i < 2; i++)
        {
            var item = _pool.GetRandom();
            if (item == null) continue;
            inv.AddItem(item.effectType);
            Debug.Log($"[Tile] JACKPOT Player {playerId} nhận: {item.itemName} ({item.effectType})");
            granted++;
        }

        return $"JACKPOT! +{granted} items";
    }
}

public class GambleTileEffect : IBoardTileEffect
{
    public string Resolve(int playerId)
    {
        Debug.Log($"[Tile] Player {playerId} — GAMBLE: đánh cược item ngẫu nhiên (Phase 4).");
        return "GAMBLE!";
    }
}
