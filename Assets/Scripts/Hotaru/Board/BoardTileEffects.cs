using UnityEngine;

/// <summary>
/// Stub implementations cho mỗi TileType.
/// Phase 1: chỉ log + trả về display message.
/// Phase 2+: thay thế từng class bằng logic thật.
/// Item/Jackpot tile sử dụng BoardItemPool để cấp Board items.
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
    private readonly BoardItemPool _pool;

    public ItemTileEffect(BoardItemPool pool) { _pool = pool; }

    public string Resolve(int playerId)
    {
        if (_pool == null)
        {
            Debug.LogWarning("[Tile] BoardItemPool chưa được assign trong BoardManager Inspector.");
            return "GOT BOARD ITEM!";
        }

        var inv = PlayerItemInventory.GetForPlayer(playerId);
        if (inv == null)
        {
            Debug.LogWarning($"[Tile] Không tìm thấy PlayerItemInventory cho player {playerId}. Thêm component vào player prefab.");
            return "GOT BOARD ITEM!";
        }

        var item = _pool.GetRandom();
        if (item == null) return "GOT BOARD ITEM! (pool empty)";

        if (!inv.AddBoardItem(item.effectType))
        {
            Debug.Log($"[Tile] Player {playerId} Board items FULL — {item.itemName} bị từ chối.");
            return "[BOARD ITEMS FULL]";
        }

        Debug.Log($"[Tile] Player {playerId} nhận Board item: {item.itemName} ({item.effectType}) [{item.rarity}]");
        return $"GOT: {item.itemName} [{item.rarity}]";
    }
}

public class StealTileEffect : IBoardTileEffect
{
    public string Resolve(int playerId)
    {
        Debug.Log($"[Tile] Player {playerId} — STEAL: cướp Board item người khác (Phase 3).");
        return "STEAL!";
    }
}

public class TossTileEffect : IBoardTileEffect
{
    public string Resolve(int playerId)
    {
        Debug.Log($"[Tile] Player {playerId} — TOSS: mất random Board item (Phase 3).");
        return "TOSS ITEM!";
    }
}

public class JackpotTileEffect : IBoardTileEffect
{
    private readonly BoardItemPool _pool;

    public JackpotTileEffect(BoardItemPool pool) { _pool = pool; }

    public string Resolve(int playerId)
    {
        if (_pool == null)
        {
            Debug.LogWarning("[Tile] BoardItemPool chưa được assign trong BoardManager Inspector.");
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
            // Item đầu tiên luôn ít nhất Rare, item thứ 2 random bình thường
            var item = (i == 0) ? _pool.GetRandom(ItemRarity.Rare) : _pool.GetRandom();
            if (item == null) continue;

            if (!inv.AddBoardItem(item.effectType))
            {
                Debug.Log($"[Tile] JACKPOT Player {playerId} Board items FULL — {item.itemName} bị từ chối.");
                break; // nếu không còn chỗ, dừng luôn
            }

            Debug.Log($"[Tile] JACKPOT Player {playerId} nhận: {item.itemName} ({item.effectType}) [{item.rarity}]");
            granted++;
        }

        return granted > 0 ? $"JACKPOT! +{granted} [{(granted < 2 ? "FULL" : "OK")}]" : "JACKPOT! [BOARD ITEMS FULL]";
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

