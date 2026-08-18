using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Thư viện SFX cho các sự kiện trên bàn cờ: dùng item, đáp tile, shield chặn, steal, gamble...
/// Tạo bằng Create > Board > Board Sfx Library trong Project window.
/// Không cần assign thủ công vào từng script — dùng qua BoardSfxLibrary.Current, giống BoardItemPool.
/// </summary>
[CreateAssetMenu(menuName = "Board/Board Sfx Library")]
public class BoardSfxLibrary : ScriptableObject
{
    [System.Serializable]
    public class ItemSfxEntry
    {
        public BoardItemEffect effect;
        public AudioClip useSfx;
    }

    [System.Serializable]
    public class TileSfxEntry
    {
        public TileType tileType;
        public AudioClip landSfx;
    }

    [Header("Item Use SFX")]
    public List<ItemSfxEntry> itemSfx = new List<ItemSfxEntry>();

    [Header("Tile Land SFX")]
    public List<TileSfxEntry> tileSfx = new List<TileSfxEntry>();

    [Header("Shield")]
    public AudioClip shieldBlockedSfx;

    [Header("Steal")]
    public AudioClip stealSuccessSfx;
    public AudioClip stealFailSfx;

    [Header("Gamble")]
    public AudioClip gambleWinSfx;
    public AudioClip gambleLoseSfx;

    [Header("Trap")]
    public AudioClip trapExplodeSfx;

    public static BoardSfxLibrary Current { get; private set; }

    private void OnEnable()  { Current = this; }
    private void OnDisable() { if (Current == this) Current = null; }

    public AudioClip GetItemSfx(BoardItemEffect effect)
    {
        foreach (var e in itemSfx)
            if (e.effect == effect) return e.useSfx;
        return null;
    }

    public AudioClip GetTileSfx(TileType tileType)
    {
        foreach (var e in tileSfx)
            if (e.tileType == tileType) return e.landSfx;
        return null;
    }

    private static void Play(AudioClip clip)
    {
        if (clip == null) return;
        if (SFXManager.Instance != null)
            SFXManager.Instance.PlaySFX(clip, 1f);
    }

    public void PlayItemUsed(BoardItemEffect effect) => Play(GetItemSfx(effect));
    public void PlayTileLanded(TileType tileType) => Play(GetTileSfx(tileType));
    public void PlayShieldBlocked() => Play(shieldBlockedSfx);
    public void PlayStealResult(bool success) => Play(success ? stealSuccessSfx : stealFailSfx);
    public void PlayGambleResult(bool win) => Play(win ? gambleWinSfx : gambleLoseSfx);
    public void PlayTrapExplode() => Play(trapExplodeSfx);
}