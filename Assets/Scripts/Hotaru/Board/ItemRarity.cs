/// <summary>
/// Độ hiếm của item — áp dụng cho cả Board items và Roulette items.
/// Trọng số mặc định: Common=6, Rare=3, Legendary=1.
/// Trọng số được tính tự động trong BoardItemPool và ItemPool.
/// </summary>
public enum ItemRarity
{
    Common    = 0,   // Drop rate ~60%
    Rare      = 1,   // Drop rate ~30%
    Legendary = 2,   // Drop rate ~10%
}
