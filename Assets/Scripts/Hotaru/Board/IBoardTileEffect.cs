/// <summary>
/// Interface cho tile effect khi player đứng lên ô.
/// Phase 1: stub — mỗi type chỉ log và trả về display message.
/// Phase 2+: sẽ implement logic thật (item reward, steal, coin...).
/// </summary>
public interface IBoardTileEffect
{
    /// <summary>
    /// Xử lý effect. Trả về chuỗi hiển thị trên debug UI.
    /// playerId: PlayerId của người vừa đứng lên ô.
    /// </summary>
    string Resolve(int playerId);
}
