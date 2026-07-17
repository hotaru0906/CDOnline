using Fusion;
using UnityEngine;

/// <summary>
/// Network state cho vũ khí của MG8.
/// Controller sẽ tự cấp vũ khí cho player được chọn làm Killer.
/// </summary>
public class MG8PlayerData : NetworkBehaviour
{
    [Header("Item In Hand — 1 per character model")]
    [Tooltip("Index khớp với PlayerNetworkData.CharacterIndex")]
    [SerializeField] private GameObject[] itemInHandObjects;

    [Networked, OnChangedRender(nameof(OnHasItemChanged))]
    public NetworkBool HasItem { get; private set; }

    public override void Spawned()
    {
        RefreshItemVisual();
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
        RefreshItemVisual();
    }

    private void RefreshItemVisual()
    {
        if (itemInHandObjects == null) return;

        foreach (var itemObject in itemInHandObjects)
        {
            if (itemObject != null)
            {
                itemObject.SetActive(false);
                SetItemCollidersEnabled(itemObject, false);
            }
        }

        var networkData = GetComponent<PlayerNetworkData>();
        int characterIndex = networkData != null ? networkData.CharacterIndex : -1;

        if (HasItem &&
            characterIndex >= 0 &&
            characterIndex < itemInHandObjects.Length &&
            itemInHandObjects[characterIndex] != null)
        {
            itemInHandObjects[characterIndex].SetActive(true);
            SetItemCollidersEnabled(itemInHandObjects[characterIndex], false);
        }

        Debug.Log($"[MG8PlayerData] HasItem={HasItem}, CharacterIndex={characterIndex}");
    }

    private void SetItemCollidersEnabled(GameObject itemObject, bool enabled)
    {
        if (itemObject == null) return;

        foreach (var collider in itemObject.GetComponentsInChildren<Collider>(true))
        {
            collider.enabled = enabled;
        }
    }
}