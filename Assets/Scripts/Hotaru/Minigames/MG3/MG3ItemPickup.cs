using Fusion;
using UnityEngine;

/// <summary>
/// Item pickup trên map trong MG3 Brawl.
/// Khi player chạm vào → SetActive item trên tay player, despawn item trên map.
///
/// SETUP prefab:
///   - 1 GameObject với Collider isTrigger + NetworkObject
///   - Gắn script này lên root
///   - Assign itemVisual (MeshRenderer/model của item)
/// </summary>
public class MG3ItemPickup : NetworkBehaviour
{
    [Header("Visual")]
    [SerializeField] private GameObject itemVisual;

    [Networked] public NetworkBool IsPickedUp { get; private set; } = false;

    public int SpawnPointIndex { get; set; } = -1; // set bởi MG3ItemSpawner

    // Callback về spawner khi bị pickup
    public System.Action<int> OnPickedUp;

    private void OnTriggerEnter(Collider other)
    {
        if (!HasStateAuthority) return;
        if (IsPickedUp) return;

        if (!other.TryGetComponent(out PlayerController player)) return;

        var mgData = player.GetComponent<PlayerMinigameData>();
        if (mgData == null || !mgData.CanTakeDamage()) return;

        // Kiểm tra player chưa có item
        var brawlData = player.GetComponent<MG3PlayerBrawlData>();
        if (brawlData == null || brawlData.HasItem) return;

        IsPickedUp = true;
        RPC_OnPickup(player.Object.InputAuthority);
        OnPickedUp?.Invoke(SpawnPointIndex);

        Runner.Despawn(Object);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_OnPickup(PlayerRef playerRef)
    {
        // Tìm player và kích hoạt item trên tay
        var players = FindObjectsByType<MG3PlayerBrawlData>(FindObjectsSortMode.None);
        foreach (var p in players)
        {
            if (p.Object.InputAuthority == playerRef)
            {
                p.PickupItem();
                return;
            }
        }
    }
}