using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Danh sách items có thể xuất hiện từ tile, kèm rarity weight.
/// Tạo bằng Create > Board > Item Pool trong Project window.
/// Assign vào BoardManager Inspector để board dùng được.
/// </summary>
[CreateAssetMenu(menuName = "Board/Item Pool")]
public class ItemPool : ScriptableObject
{
    [System.Serializable]
    public class Entry
    {
        public ItemData item;
        [Range(1, 10)]
        [Tooltip("Xác suất xuất hiện — số càng cao càng dễ ra")]
        public int weight = 1;
    }

    [Header("Items")]
    public List<Entry> entries = new List<Entry>();

    // Static reference — set khi Unity load SO vào memory (OnEnable).
    // Dùng để clients tra cứu tên item mà không cần truyền string qua RPC.
    public static ItemPool Current { get; private set; }

    private void OnEnable()  { Current = this; }
    private void OnDisable() { if (Current == this) Current = null; }

    /// <summary>
    /// Random 1 item theo weight. Trả về null nếu pool rỗng hoặc không có entry hợp lệ.
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
        {
            if (e.item != null) totalWeight += Mathf.Max(1, e.weight);
        }

        if (totalWeight == 0) return null;

        int roll = Random.Range(0, totalWeight);
        int acc  = 0;

        foreach (var e in entries)
        {
            if (e.item == null) continue;
            acc += Mathf.Max(1, e.weight);
            if (roll < acc) return e.item;
        }

        // Fallback — lấy entry hợp lệ cuối cùng
        for (int i = entries.Count - 1; i >= 0; i--)
            if (entries[i].item != null) return entries[i].item;

        return null;
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
