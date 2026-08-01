using Fusion;
using UnityEngine;

public class MG3ItemPickup : NetworkBehaviour
{
    [Header("Audio")]
    [SerializeField] private AudioClip pickupSound;

    [Networked] public NetworkBool IsPickedUp { get; private set; } = false;

    private void OnTriggerEnter(Collider other)
    {
        if (Runner == null || !Runner.IsServer) return;
        if (IsPickedUp) return;
        if (!other.TryGetComponent(out PlayerController player)) return;

        var mgData = player.GetComponent<PlayerMinigameData>();
        if (mgData == null || !mgData.CanTakeDamage()) return;

        var brawlData = player.GetComponent<MG3PlayerBrawlData>();
        if (brawlData == null || brawlData.HasItem) return;

        IsPickedUp = true;

        MG3HammerManager.Instance.AssignHammer(player);

        RPC_OnPickup();
        Runner.Despawn(Object);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_OnPickup()
    {
        if (pickupSound != null)
            AudioSource.PlayClipAtPoint(pickupSound, transform.position);
    }
}