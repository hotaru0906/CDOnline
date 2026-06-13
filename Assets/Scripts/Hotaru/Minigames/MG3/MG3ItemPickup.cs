using Fusion;
using UnityEngine;

public class MG3ItemPickup : NetworkBehaviour
{
    [Header("Visual")]
    [SerializeField] private GameObject itemVisual;

    [Networked] public NetworkBool IsPickedUp { get; private set; } = false;

    public int SpawnPointIndex { get; set; } = -1;
    public System.Action<int> OnPickedUp;

    private void OnTriggerEnter(Collider other)
    {
        // Chỉ host xử lý
        if (Runner == null || !Runner.IsServer) return;
        if (IsPickedUp) return;
        if (!other.TryGetComponent(out PlayerController player)) return;

        var mgData = player.GetComponent<PlayerMinigameData>();
        if (mgData == null || !mgData.CanTakeDamage()) return;

        var brawlData = player.GetComponent<MG3PlayerBrawlData>();
        if (brawlData == null || brawlData.HasItem) return;

        IsPickedUp = true;

        // Host gọi pickup trực tiếp
        brawlData.PickupItem();

        // Broadcast xuống tất cả clients
        RPC_OnPickup(player.Object.InputAuthority);

        OnPickedUp?.Invoke(SpawnPointIndex);
        Runner.Despawn(Object);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_OnPickup(PlayerRef playerRef)
    {
        var playerObj = Runner.GetPlayerObject(playerRef);
        if (playerObj != null && playerObj.TryGetComponent(out MG3PlayerBrawlData brawlData))
        {
            brawlData.PickupItem(); // sẽ set HasItem = true và trigger OnHasItemChanged trên client
        }
    }

}