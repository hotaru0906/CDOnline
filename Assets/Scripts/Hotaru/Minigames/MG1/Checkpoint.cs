using Fusion;
using UnityEngine;

/// <summary>
/// Checkpoint trigger - lưu vị trí respawn cho player.
/// Đặt component này trên GameObject với Collider (isTrigger = true).
/// </summary>
[RequireComponent(typeof(Collider))]
public class Checkpoint : MonoBehaviour
{
    [Header("Checkpoint Settings")]
    [SerializeField] private int checkpointIndex = 0;
    [SerializeField] private Transform respawnPoint;

    public int CheckpointIndex => checkpointIndex;
    public Vector3 RespawnPosition => respawnPoint != null ? respawnPoint.position : transform.position;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.TryGetComponent(out PlayerController player)) return;
        if (!player.Object.HasStateAuthority) return;

        if (player.TryGetComponent(out PlayerMinigameData data))
        {
            data.SetCheckpoint(checkpointIndex, RespawnPosition);
        }
    }

    private void OnDrawGizmos()
    {
        // Draw checkpoint in editor
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, 0.5f);

        if (respawnPoint != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(respawnPoint.position, 0.3f);
            Gizmos.DrawLine(transform.position, respawnPoint.position);
        }

        // Draw checkpoint index
#if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up, $"Checkpoint {checkpointIndex}");
#endif
    }
}
