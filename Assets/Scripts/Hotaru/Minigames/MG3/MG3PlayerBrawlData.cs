using Fusion;
using UnityEngine;

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
        var netData = GetComponent<PlayerNetworkData>();
        int characterIndex = netData != null ? netData.CharacterIndex : -1;

        foreach (var obj in itemInHandObjects)
            if (obj != null) obj.SetActive(false);

        if (HasItem && characterIndex >= 0 && characterIndex < itemInHandObjects.Length)
        {
            if (itemInHandObjects[characterIndex] != null)
                itemInHandObjects[characterIndex].SetActive(true);
        }

        Debug.Log($"[MG3PlayerBrawlData] OnHasItemChanged → HasItem={HasItem}, CharacterIndex={characterIndex}");
    }

}