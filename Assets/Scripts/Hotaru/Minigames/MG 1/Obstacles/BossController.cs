using UnityEngine;
using Fusion;

/// <summary>
/// Boss entity that moves along the track ahead of players.
/// Spawns obstacles that travel backward toward players.
/// Stops when reaching the end of the track.
/// </summary>
public class BossController : NetworkBehaviour
{
    [Header("Movement")]
    [SerializeField] private float baseSpeed = 12f;             // Base movement speed
    [SerializeField] private float speedMultiplier = 1.2f;      // Faster than players (1.2 = 20% faster)
    [SerializeField] private float startDelay = 3f;             // Delay before boss starts moving

    [Header("Track")]
    [SerializeField] private TrackSystem trackSystem;
    [SerializeField] private float startDistance = 10f;         // Starting distance on track (near start)
    [SerializeField] private float stopBuffer = 50f;            // Stop this distance before track end

    [Header("Visual")]
    [SerializeField] private GameObject visualModel;
    [SerializeField] private ParticleSystem moveEffect;
    [SerializeField] private ParticleSystem stopEffect;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip moveLoopSound;
    [SerializeField] private AudioClip arriveSound;

    [Header("Debug")]
    [SerializeField] private bool debugMode = false;

    // Networked properties
    [Networked] public float CurrentDistance { get; set; }
    [Networked] public NetworkBool IsMoving { get; set; }
    [Networked] public NetworkBool HasReachedEnd { get; set; }
    [Networked] public TickTimer StartDelayTimer { get; set; }
    [Networked] public float CurrentSpeed { get; set; }

    // References
    private RaceManager _raceManager;
    private ObstacleManager _obstacleManager;
    private Vector3 _currentPosition;
    private Vector3 _currentDirection;

    /// <summary>
    /// Gets the boss's current position on the track.
    /// </summary>
    public Vector3 Position => _currentPosition;

    /// <summary>
    /// Gets the direction the boss is facing (track direction).
    /// </summary>
    public Vector3 Direction => _currentDirection;

    /// <summary>
    /// Gets the direction toward start (opposite of facing direction).
    /// </summary>
    public Vector3 DirectionTowardStart => -_currentDirection;

    public override void Spawned()
    {
        base.Spawned();

        // Find references
        if (trackSystem == null)
            trackSystem = FindAnyObjectByType<TrackSystem>();

        _raceManager = RaceManager.Instance;
        _obstacleManager = ObstacleManager.Instance;

        if (Object.HasStateAuthority)
        {
            // Initialize position
            CurrentDistance = startDistance;
            CurrentSpeed = baseSpeed * speedMultiplier;
            IsMoving = false;
            HasReachedEnd = false;
            StartDelayTimer = TickTimer.CreateFromSeconds(Runner, startDelay);

            UpdatePositionFromDistance();
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority) return;

        // Wait for race to be in Racing phase
        if (_raceManager != null && _raceManager.CurrentPhase != RacePhase.Racing)
        {
            // Reset timer while waiting for race to start
            StartDelayTimer = TickTimer.CreateFromSeconds(Runner, startDelay);
            return;
        }

        // Wait for start delay
        if (!StartDelayTimer.Expired(Runner))
        {
            return;
        }

        // Start moving after delay
        if (!IsMoving && !HasReachedEnd)
        {
            StartMoving();
        }

        // Move if active
        if (IsMoving && !HasReachedEnd)
        {
            Move();
        }

        // Always update visual position
        UpdatePositionFromDistance();
    }

    /// <summary>
    /// Start the boss movement.
    /// </summary>
    private void StartMoving()
    {
        IsMoving = true;
        RPC_OnStartMoving();
        Debug.Log("[BossController] Boss started moving!");
    }

    /// <summary>
    /// Move the boss along the track.
    /// </summary>
    private void Move()
    {
        if (trackSystem == null)
        {
            Debug.LogWarning("[BossController] TrackSystem is null!");
            return;
        }

        // Safety check: don't move if track isn't ready
        if (trackSystem.TrackLength <= 0f)
        {
            Debug.LogWarning($"[BossController] Track not ready yet. TrackLength = {trackSystem.TrackLength}");
            return;
        }

        // Calculate movement
        float movement = CurrentSpeed * Runner.DeltaTime;
        CurrentDistance += movement;

        // Check if reached end
        float endDistance = trackSystem.TrackLength - stopBuffer;
        
        // Debug: Log movement
        if (debugMode)
        {
            Debug.Log($"[BossController] Distance: {CurrentDistance:F1}/{trackSystem.TrackLength:F1}, EndAt: {endDistance:F1}");
        }
        
        if (CurrentDistance >= endDistance)
        {
            CurrentDistance = endDistance;
            StopMoving();
        }
    }

    /// <summary>
    /// Stop the boss at track end.
    /// </summary>
    private void StopMoving()
    {
        IsMoving = false;
        HasReachedEnd = true;
        RPC_OnReachedEnd();
        Debug.Log("[BossController] Boss reached track end and stopped!");
    }

    /// <summary>
    /// Update position and rotation from track distance.
    /// </summary>
    private void UpdatePositionFromDistance()
    {
        if (trackSystem == null) return;

        _currentPosition = trackSystem.GetPositionAtDistance(CurrentDistance);
        _currentDirection = trackSystem.GetDirectionAtDistance(CurrentDistance);

        // Apply to transform
        transform.position = _currentPosition;
        if (_currentDirection.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(_currentDirection, Vector3.up);
        }
    }

    /// <summary>
    /// Reset boss to start position.
    /// </summary>
    public void ResetBoss()
    {
        if (!Object.HasStateAuthority) return;

        CurrentDistance = startDistance;
        IsMoving = false;
        HasReachedEnd = false;
        StartDelayTimer = TickTimer.CreateFromSeconds(Runner, startDelay);

        UpdatePositionFromDistance();
        RPC_OnReset();

        Debug.Log("[BossController] Boss reset to start position.");
    }

    /// <summary>
    /// Set the boss speed multiplier.
    /// </summary>
    public void SetSpeedMultiplier(float multiplier)
    {
        speedMultiplier = multiplier;
        CurrentSpeed = baseSpeed * speedMultiplier;
    }

    /// <summary>
    /// Set track system reference.
    /// </summary>
    public void SetTrackSystem(TrackSystem track)
    {
        trackSystem = track;
        UpdatePositionFromDistance();
    }

    #region RPCs

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_OnStartMoving()
    {
        // Start move effect
        if (moveEffect != null)
        {
            moveEffect.Play();
        }

        // Start looping audio
        if (audioSource != null && moveLoopSound != null)
        {
            audioSource.clip = moveLoopSound;
            audioSource.loop = true;
            audioSource.Play();
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_OnReachedEnd()
    {
        // Stop move effect
        if (moveEffect != null)
        {
            moveEffect.Stop();
        }

        // Play stop effect
        if (stopEffect != null)
        {
            stopEffect.Play();
        }

        // Stop looping audio, play arrive sound
        if (audioSource != null)
        {
            audioSource.Stop();
            if (arriveSound != null)
            {
                audioSource.PlayOneShot(arriveSound);
            }
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_OnReset()
    {
        // Stop all effects
        if (moveEffect != null)
        {
            moveEffect.Stop();
        }
        if (stopEffect != null)
        {
            stopEffect.Stop();
        }
        if (audioSource != null)
        {
            audioSource.Stop();
        }
    }

    #endregion

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        // Draw boss position
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, 1f);

        // Draw direction arrow
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(transform.position, transform.forward * 3f);

        // Draw obstacle spawn direction (backward)
        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(transform.position, -transform.forward * 5f);

        // Label
        UnityEditor.Handles.Label(transform.position + Vector3.up * 2f, "BOSS");
    }

    private void OnDrawGizmosSelected()
    {
        if (trackSystem == null) return;

        // Draw stop position
        Gizmos.color = Color.red;
        float stopDistance = trackSystem.TrackLength - stopBuffer;
        Vector3 stopPos = trackSystem.GetPositionAtDistance(stopDistance);
        Gizmos.DrawWireCube(stopPos + Vector3.up, Vector3.one * 2f);
        UnityEditor.Handles.Label(stopPos + Vector3.up * 3f, "STOP POINT");
    }
#endif
}
