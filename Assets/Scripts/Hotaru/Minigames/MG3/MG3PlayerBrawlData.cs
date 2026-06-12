using Fusion;
using UnityEngine;

/// <summary>
/// Dữ liệp brawl của player — item trên tay.
/// Attach vào player prefab cùng với PlayerController.
///
/// SETUP:
///   - itemInHandObjects[0] = item vị trí tay model 0
///   - itemInHandObjects[1] = item vị trí tay model 1
///   - itemInHandObjects[2] = item vị trí tay model 2
///   - Tất cả ban đầu inactive
/// </summary>
public class MG3PlayerBrawlData : NetworkBehaviour
{
    [Header("Item In Hand — 1 per character model")]
    [Tooltip("Index khớp với PlayerNetworkData.CharacterIndex")]
    [SerializeField] private GameObject[] itemInHandObjects;

    [Networked, OnChangedRender(nameof(OnHasItemChanged))]
    public NetworkBool HasItem { get; private set; } = false;

    private int _characterIndex = -1;

    public override void Spawned()
    {
        // Lấy CharacterIndex của player này
        var netData = GetComponent<PlayerNetworkData>();
        if (netData != null)
            _characterIndex = netData.CharacterIndex;

        // Đảm bảo tất cả inactive ban đầu
        foreach (var obj in itemInHandObjects)
            if (obj != null) obj.SetActive(false);
    }

    public void PickupItem()
    {
        if (!HasStateAuthority) return;
        HasItem = true;
    }

    public void DropItem()
    {
        if (!HasStateAuthority) return;
        HasItem = false;
    }

    private void OnHasItemChanged()
    {
        // Refresh CharacterIndex nếu chưa có
        if (_characterIndex < 0)
        {
            var netData = GetComponent<PlayerNetworkData>();
            if (netData != null) _characterIndex = netData.CharacterIndex;
        }

        // Ẩn tất cả trước
        foreach (var obj in itemInHandObjects)
            if (obj != null) obj.SetActive(false);

        // Hiện đúng model theo CharacterIndex
        if (HasItem && _characterIndex >= 0 && _characterIndex < itemInHandObjects.Length)
            if (itemInHandObjects[_characterIndex] != null)
                itemInHandObjects[_characterIndex].SetActive(true);
    }
}