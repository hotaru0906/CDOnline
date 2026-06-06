using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Danh sách Roulette items phân phối sau Board Race.
/// Tạo bằng Create > Board > Item Pool trong Project window.
/// Assign vào BoardManager Inspector để board dùng được.
/// Weight tự động tính từ item.rarity: Common=6, Rare=3, Legendary=1.
/// </summary>
[CreateAssetMenu(menuName = "Board/Item Pool")]
public class ItemPool : ScriptableObject
{
    [System.Serializable]
    public class Entry
    {
        public ItemData item;
    }

    [Header("Items")]
    public List<Entry> entries = new List<Entry>();

    // Static reference — set khi Unity load SO vào memory (OnEnable).
    public static ItemPool Current { get; private set; }

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
    public ItemData GetRandom()
    {
        if (entries == null || entries.Count == 0)
        {
            Debug.LogWarning("[ItemPool] Pool is empty!");
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

        // Fallback — lấy entry hợp lệ cuối cùng
        for (int i = entries.Count - 1; i >= 0; i--)
            if (entries[i].item != null) return entries[i].item;

        return null;
    }

    /// <summary>
    /// Random 1 item có rarity >= minRarity.
    /// Trả về null nếu không có item nào đủ điều kiện (fallback GetRandom()).
    /// </summary>
    public ItemData GetRandom(ItemRarity minRarity)
    {
        if (entries == null || entries.Count == 0) return null;

        int totalWeight = 0;
        foreach (var e in entries)
            if (e.item != null && e.item.rarity >= minRarity)
                totalWeight += RarityWeight(e.item.rarity);

        if (totalWeight == 0)
        {
            Debug.LogWarning($"[ItemPool] Không có item nào rarity >= {minRarity}, fallback GetRandom().");
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
    /// Tìm ItemData đầu tiên khớp effectType.
    /// Dùng để clients hiển thị tên item từ ItemEffect enum (không cần string qua RPC).
    /// </summary>
    public ItemData GetByEffect(ItemEffect effect)
    {
        if (entries == null) return null;
        foreach (var e in entries)
            if (e.item != null && e.item.effectType == effect)
                return e.item;
        return null;
    }
}
