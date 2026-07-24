using UnityEngine;

/// <summary>
/// Trigger placed on the MG8 lava GameObject.
/// </summary>
public class MG8Lava : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        HandlePlayerContact(other);
    }

    private void OnCollisionEnter(Collision collision)
    {
        HandlePlayerContact(collision.collider);
    }

    private void HandlePlayerContact(Collider other)
    {
        var playerData = other.GetComponentInParent<PlayerMinigameData>();
        if (playerData == null) return;

        MG8Controller.Instance?.EliminatePlayer(playerData);
    }
}
