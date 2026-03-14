using UnityEngine;
using Fusion;

/// <summary>
/// Long obstacle that players can jump over.
/// Spans the width of the track but is short in height.
/// </summary>
public class JumpableObstacle : Obstacle
{
    [Header("Jumpable Settings")]
    [SerializeField] private float obstacleHeight = 0.8f;       // Height player must jump to clear
    [SerializeField] private float obstacleLength = 1.5f;       // Length along track direction
    [SerializeField] private float knockbackForce = 5f;         // Knockback when hit
    [SerializeField] private float slowDuration = 1f;           // How long player is slowed

    [Header("Audio")]
    [SerializeField] private AudioClip hitSound;

    private void Awake()
    {
        obstacleType = ObstacleType.Jumpable;
    }

    /// <summary>
    /// Apply slowdown effect when player fails to jump.
    /// </summary>
    protected override void ApplyEffect(NetworkObject player)
    {
        // Apply knockback in opposite direction of obstacle movement
        var playerController = player.GetComponent<NetworkCharacterController>();
        if (playerController != null)
        {
            // Small knockback
            Vector3 knockback = -MoveDirection * knockbackForce;
            playerController.Move(knockback * Runner.DeltaTime);
        }

        // Notify ObstacleManager to apply slow effect
        if (_obstacleManager != null)
        {
            _obstacleManager.ApplySlowEffect(player.InputAuthority, slowDuration, damageOrSlowAmount);
        }

        Debug.Log($"[JumpableObstacle] Player failed to jump! Applying slow for {slowDuration}s");
    }

    protected override void OnAfterPlayerHit(NetworkObject player)
    {
        // Play sound
        if (hitSound != null)
        {
            RPC_PlaySound();
        }

        // Don't destroy - obstacle continues moving
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlaySound()
    {
        if (hitSound != null)
        {
            AudioSource.PlayClipAtPoint(hitSound, transform.position);
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        // Draw the obstacle bounds
        Gizmos.color = Color.red;
        Vector3 size = new Vector3(5f, obstacleHeight, obstacleLength); // Wide, short, long
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(Vector3.up * obstacleHeight * 0.5f, size);
    }
#endif
}
