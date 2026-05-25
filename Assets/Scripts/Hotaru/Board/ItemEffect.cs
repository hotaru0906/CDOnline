/// <summary>
/// Loại effect của item — tất cả đều dùng trong Roulette phase.
/// </summary>
public enum ItemEffect
{
    None             = -1,
    RestoreLife      = 0,   // +1 mạng trong Roulette
    DoubleDamage     = 1,   // viên đạn tiếp theo gây 2 mạng
    SeeNextBullet    = 2,   // xem đạn tiếp theo là thật/giả
    SeeBulletOrder   = 3,   // xem toàn bộ thứ tự đạn
    SkipOpponentTurn = 4,   // skip lượt 1 người chơi trong Roulette
    ReverseRoulette  = 5,   // đảo chiều vòng bắn Roulette
}
