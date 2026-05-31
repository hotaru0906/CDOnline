/// <summary>
/// Loại effect của Board item — dùng trong Board phase.
/// Board items nhận từ tile Item/Jackpot, dùng trước khi đổ xúc xắc trong lượt đó.
/// </summary>
public enum BoardItemEffect
{
    None        = -1,
    PushBack    = 0,   // Chỉ định 1 player, player đó lùi 2 ô
    RushForward = 1,   // Bản thân di chuyển thêm 2 ô (cộng vào kết quả xúc xắc)
    EvenDice    = 2,   // Bản thân luôn đổ xúc xắc ra số chẵn trong lượt đó
}
