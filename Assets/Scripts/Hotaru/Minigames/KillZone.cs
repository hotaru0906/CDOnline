using UnityEngine;

[RequireComponent(typeof(Collider))]
public class KillZone : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (!other.TryGetComponent(out PlayerController player)) return;
        if (!player.Object.HasInputAuthority) return;

        if (player.TryGetComponent(out PlayerMinigameData minigameData) 
            && minigameData.CanTakeDamage())
        {
            minigameData.Die();
        }
    }
}