using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Pool Board items có thể xuất hiện từ tile Item/Jackpot trên bàn cờ.
/// Tạo bằng Create > Board > Board Item Pool trong Project window.
/// Assign vào BoardManager Inspector (field "Board Item Pool").
/// Weight tự động tính từ item.rarity: Common=6, Rare=3, Legendary=1.
/// </summary>
[CreateAssetMenu(menuName = "Board/Board Item Pool")]
public class BoardItemPool : ScriptableObject
{
    [System.Serializable]
    public class Entry
    {
        public BoardItemData item;
    }

    [Header("Board Items")]
    public List<Entry> entries = new List<Entry>();

    // Static reference — set khi Unity load SO vào memory.
    public static BoardItemPool Current { get; private set; }

    private void OnEnable()  { Current = this; }
    private void OnDisable() { if (Current == this) Current = null; }

    /// <summary>Trọng số mặc định theo độ hiếm.</summary>
    public static int RarityWeight(ItemRarity rarity) => rarity switch
    {
        ItemRarity.Common    => 6,
        ItemRarity.Rare      => 3,
        ItemRarity.Legendary => 1,
        _                    => 1,
    };

    /// <summary>
    /// Random 1 item theo rarity weight. Trả về null nếu pool rỗng.
    /// Chỉ gọi trên host.
    /// </summary>
    public BoardItemData GetRandom()
    {
        if (entries == null || entries.Count == 0)
        {
            Debug.LogWarning("[BoardItemPool] Pool is empty!");
            return null;
        }

        int totalWeight = 0;
        foreach (var e in entries)
            if (e.item != null) totalWeight += RarityWeight(e.item.rarity);

        if (totalWeight == 0) return null;

        int roll = Random.Range(0, totalWeight);
        int acc  = 0;

        foreach (var e in entries)
        {
            if (e.item == null) continue;
            acc += RarityWeight(e.item.rarity);
            if (roll < acc) return e.item;
        }

        // Fallback
        for (int i = entries.Count - 1; i >= 0; i--)
            if (entries[i].item != null) return entries[i].item;

        return null;
    }

    /// <summary>
    /// Random 1 item có rarity >= minRarity.
    /// Dùng cho Jackpot tile (đảm bảo ít nhất Rare).
    /// Trả về null nếu không có item nào đủ điều kiện.
    /// </summary>
    public BoardItemData GetRandom(ItemRarity minRarity)
    {
        if (entries == null || entries.Count == 0) return null;

        int totalWeight = 0;
        foreach (var e in entries)
            if (e.item != null && e.item.rarity >= minRarity)
                totalWeight += RarityWeight(e.item.rarity);

        if (totalWeight == 0)
        {
            Debug.LogWarning($"[BoardItemPool] Không có item nào rarity >= {minRarity}, fallback GetRandom().");
            return GetRandom();
        }

        int roll = Random.Range(0, totalWeight);
        int acc  = 0;

        foreach (var e in entries)
        {
            if (e.item == null || e.item.rarity < minRarity) continue;
            acc += RarityWeight(e.item.rarity);
            if (roll < acc) return e.item;
        }

        return GetRandom();
    }

    /// <summary>
    /// Tìm BoardItemData đầu tiên khớp effectType.
    /// Dùng để hiển thị tên item từ enum mà không cần truyền string qua RPC.
    /// </summary>
    public BoardItemData GetByEffect(BoardItemEffect effect)
    {
        if (entries == null) return null;
        foreach (var e in entries)
            if (e.item != null && e.item.effectType == effect)
                return e.item;
        return null;
    }
}
