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

    [Header("Visual Feedback")]
    [SerializeField] private GameObject activatedVisual;
    [SerializeField] private GameObject deactivatedVisual;

    public int CheckpointIndex => checkpointIndex;
    public Vector3 RespawnPosition => respawnPoint != null ? respawnPoint.position : transform.position;

    private void Start()
    {
        // Ensure trigger is set
        var collider = GetComponent<Collider>();
        if (collider != null)
        {
            collider.isTrigger = true;
        }

        // Set initial visual
        UpdateVisual(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        // Chỉ xử lý local player
        var player = other.GetComponent<PlayerController>();
        if (player == null) return;

        // Check if this is the local player
        if (!player.Object.HasInputAuthority) return;

        // Get minigame data
        var minigameData = player.GetComponent<PlayerMinigameData>();
        if (minigameData == null)
        {
            Debug.LogWarning("[Checkpoint] Player không có PlayerMinigameData component!");
            return;
        }

        // Chỉ lưu checkpoint nếu index cao hơn checkpoint hiện tại
        if (checkpointIndex > minigameData.CurrentCheckpointIndex)
        {
            minigameData.RPC_SetCheckpoint(checkpointIndex, RespawnPosition);
            Debug.Log($"[Checkpoint] Player reached checkpoint {checkpointIndex}");

            // Visual feedback
            UpdateVisual(true);
        }
    }

    private void UpdateVisual(bool activated)
    {
        if (activatedVisual != null)
            activatedVisual.SetActive(activated);

        if (deactivatedVisual != null)
            deactivatedVisual.SetActive(!activated);
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
