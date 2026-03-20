using UnityEngine;

public class DeathZone : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        // Support both movement variants and any future respawn interface.
        var player1 = other.GetComponent<PlayerMovement1>();
        if (player1 != null)
        {
            player1.Respawn();
            return;
        }

        var player = other.GetComponent<PlayerMovement1>();
        if (player != null)
        {
            player.Respawn();
            return;
        }

        // Fallback: add support for an interface-based respawn component in the future.
        var respawnable = other.GetComponent<IRespawnable>();
        if (respawnable != null)
        {
            respawnable.Respawn();
        }
    }
}

public interface IRespawnable
{
    void Respawn();
}