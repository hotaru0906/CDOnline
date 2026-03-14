using UnityEngine;
using Fusion;

/// <summary>
/// Side of the track where the obstacle is positioned.
/// </summary>
public enum ObstacleSide
{
    Left,
    Center,
    Right,
    Random
}

/// <summary>
/// Large obstacle that blocks most of the track width.
/// Players must dodge to the open side to avoid.
/// </summary>
public class DodgeableObstacle : Obstacle
{
    [Header("Dodgeable Settings")]
    [SerializeField] private float obstacleWidth = 3.5f;        // Width blocking track
    [SerializeField] private float obstacleHeight = 3f;         // Tall enough to prevent jumping
    [SerializeField] private float knockbackForce = 8f;         // Strong knockback when hit
    [SerializeField] private float stunDuration = 0.5f;         // Brief stun

    [Header("Positioning")]
    [SerializeField] private ObstacleSide side = ObstacleSide.Random;
    [SerializeField] private float lateralOffset = 1.5f;        // How far from center

    [Header("Audio")]
    [SerializeField] private AudioClip hitSound;
    [SerializeField] private AudioClip warningSound;

    [Networked] public ObstacleSide SpawnedSide { get; set; }

    private void Awake()
    {
        obstacleType = ObstacleType.Dodgeable;
    }

    public override void Spawned()
    {
        base.Spawned();

        if (Object.HasStateAuthority)
        {
            // Determine spawn side
            if (side == ObstacleSide.Random)
            {
                SpawnedSide = (ObstacleSide)Random.Range(0, 3); // Left, Center, or Right
            }
            else
            {
                SpawnedSide = side;
            }

            // Apply lateral offset based on side
            ApplyLateralOffset();
        }

        // Play warning sound for all clients
        if (warningSound != null)
        {
            AudioSource.PlayClipAtPoint(warningSound, transform.position, 0.5f);
        }
    }

    /// <summary>
    /// Apply lateral offset to position obstacle on one side.
    /// </summary>
    private void ApplyLateralOffset()
    {
        Vector3 rightDir = Vector3.Cross(MoveDirection, Vector3.up).normalized;
        
        switch (SpawnedSide)
        {
            case ObstacleSide.Left:
                transform.position -= rightDir * lateralOffset;
                break;
            case ObstacleSide.Right:
                transform.position += rightDir * lateralOffset;
                break;
            case ObstacleSide.Center:
                // No offset
                break;
        }
    }

    /// <summary>
    /// Initialize with specific side.
    /// </summary>
    public void Initialize(Vector3 direction, float speed, ObstacleSide spawnSide)
    {
        side = spawnSide;
        Initialize(direction, speed);
    }

    /// <summary>
    /// Apply knockback and stun when player fails to dodge.
    /// </summary>
    protected override void ApplyEffect(NetworkObject player)
    {
        var playerController = player.GetComponent<NetworkCharacterController>();
        if (playerController != null)
        {
            // Strong knockback
            Vector3 knockback = -MoveDirection * knockbackForce;
            playerController.Move(knockback * Runner.DeltaTime);
        }

        // Notify ObstacleManager to apply stun effect
        if (_obstacleManager != null)
        {
            _obstacleManager.ApplyStunEffect(player.InputAuthority, stunDuration);
        }

        Debug.Log($"[DodgeableObstacle] Player hit wall! Applying stun for {stunDuration}s");
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
        Gizmos.color = Color.magenta;
        Vector3 size = new Vector3(obstacleWidth, obstacleHeight, 1f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(Vector3.up * obstacleHeight * 0.5f, size);

        // Draw the open side indicator
        Gizmos.color = Color.green;
        Vector3 openSidePos = Vector3.zero;
        switch (side)
        {
            case ObstacleSide.Left:
                openSidePos = Vector3.right * (obstacleWidth * 0.5f + 1f);
                break;
            case ObstacleSide.Right:
                openSidePos = Vector3.left * (obstacleWidth * 0.5f + 1f);
                break;
        }
        if (side != ObstacleSide.Center)
        {
            Gizmos.DrawSphere(openSidePos + Vector3.up, 0.3f);
        }
    }
#endif
}
