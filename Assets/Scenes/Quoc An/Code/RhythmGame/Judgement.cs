namespace RhythmGame
{
    /// <summary>
    /// Kết quả chấm một note. Để riêng một file vì cả bản offline lẫn bản
    /// multiplayer đều dùng — nếu để chung trong RhythmGameManager.cs thì khi
    /// xoá file đó để chuyển sang multiplayer, toàn bộ project sẽ không compile.
    /// </summary>
    public enum Judgement
    {
        Perfect,
        Good,
        Miss
    }
}
