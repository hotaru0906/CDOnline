using Fusion;
using UnityEngine;

public enum PlayerState
{
    Idle,
    Walking,
    Running,
    Jumping,
    Falling,
    Attacking,
    Crouching
}

public enum MinigameAction
{
    Move,
    Jump,
    Crouch,
    Attack,
    Run
}

[RequireComponent(typeof(NetworkCharacterController))]
[RequireComponent(typeof(PlayerAnimator))]
public class PlayerController : NetworkBehaviour
{
    [Header("Movement")]
    [SerializeField] private float walkSpeed = 5f;
    [SerializeField] private float runSpeed = 9f;
    [SerializeField] private float crouchSpeed = 2.5f;
    [SerializeField] private float rotationSpeed = 15f;

    [Header("Jump")]
    [SerializeField] private float groundBufferTime = 0.15f;

    [Header("Attack")]
    [SerializeField] private float attackDuration = 0.7f;

    [Header("Crouch")]
    [SerializeField] private float crouchScale = 0.75f;
    [SerializeField] private float crouchScaleSpeed = 10f;

    [Header("External Force")]
    [SerializeField] private float externalForceDrag = 5f;
    [SerializeField] private float externalForceThreshold = 0.1f;

    [Header("Hit Cooldown")]
    [SerializeField] private float hitCooldownDuration = 0.5f;

    [Header("UI")]
    [SerializeField] private GameObject crosshairUI;

    // ── Networked State ───────────────────────────────────
    [Networked] public PlayerState CurrentState { get; private set; }
    [Networked] private Vector3 ExternalVelocity { get; set; }
    [Networked] private float AttackTimer { get; set; }
    [Networked] private NetworkBool IsRunning { get; set; }
    [Networked] private NetworkBool IsCrouching { get; set; }
    [Networked] private NetworkBool IsMoving { get; set; }
    [Networked] private float GroundedTimer { get; set; }
    [Networked] private TickTimer HitCooldownTimer { get; set; }

    /// <summary>
    /// Đếm ngược thời gian knockback. Khi > 0, player không tự điều khiển được.
    /// </summary>
    [Networked] private float KnockbackTimer { get; set; }

    public bool IsInHitCooldown =>
        HitCooldownTimer.ExpiredOrNotRunning(Runner) == false;

    public bool IsKnockbacked => KnockbackTimer > 0f;

    public Vector3 Velocity =>
        _networkCC != null ? _networkCC.Velocity : Vector3.zero;

    // ── Private refs ─────────────────────────────────────
    private NetworkCharacterController _networkCC;
    private PlayerAnimator _playerAnimator;
    private PlayerSFXController _sfx;           // ← SFX (nhánh bạn)

    private CameraOrbit _cameraOrbit;
    private Transform _cameraTransform;

    private Vector3 _targetMoveDirection;
    private Vector3 _normalScale;
    private Vector3 _crouchScaleVec;

    private bool _isFrozen;
    public bool IsFrozen => _isFrozen;

    // ─────────────────────────────────────────────────────
    // Lifecycle
    // ─────────────────────────────────────────────────────

    private void Awake()
    {
        _networkCC      = GetComponent<NetworkCharacterController>();
        _playerAnimator = GetComponent<PlayerAnimator>();
        _sfx            = GetComponent<PlayerSFXController>(); // ← SFX (nhánh bạn)

        _normalScale  = transform.localScale;
        _crouchScaleVec = new Vector3(
            _normalScale.x,
            _normalScale.y * crouchScale,
            _normalScale.z
        );
    }

    public override void Spawned()
    {
        Debug.Log($"[PlayerController] Spawned - InputAuthority: {HasInputAuthority}");

        if (!HasInputAuthority)
        {
            if (crosshairUI != null)
                crosshairUI.SetActive(false);

            return;
        }

        if (CameraManager.Instance != null)
        {
            CameraManager.Instance.RegisterLocalPlayer(transform);
            CameraManager.Instance.SwitchToThirdPersonCamera();
            _cameraOrbit = CameraManager.Instance.CameraOrbit;
        }

        if (_cameraOrbit == null)
        {
            _cameraOrbit = Camera.main?.GetComponent<CameraOrbit>();
            _cameraOrbit?.SetTarget(transform);
        }

        _cameraTransform = Camera.main?.transform;

        UpdateCrosshairVisibility();
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (HasInputAuthority && CameraManager.Instance != null)
            CameraManager.Instance.UnregisterLocalPlayer();
    }

    // ─────────────────────────────────────────────────────
    // Network Update
    // ─────────────────────────────────────────────────────

    public override void FixedUpdateNetwork()
    {
        // Attack timer
        if (AttackTimer > 0)
        {
            AttackTimer -= Runner.DeltaTime;
            if (AttackTimer <= 0)
                AttackTimer = 0;
        }

        // External force decay
        UpdateExternalVelocity();

        // Knockback timer countdown
        if (HasStateAuthority && KnockbackTimer > 0f)
            KnockbackTimer = Mathf.Max(0f, KnockbackTimer - Runner.DeltaTime);

        // Input
        if (GetInput(out PlayerInputData input))
        {
            HandleAttack(input);

            if (CurrentState != PlayerState.Attacking)
            {
                Move(input);
                HandleJump(input);
            }
        }

        UpdateState();
    }

    // ─────────────────────────────────────────────────────
    // Movement
    // ─────────────────────────────────────────────────────

    private void Move(PlayerInputData input)
    {
        if (_isFrozen)
        {
            _networkCC.Move(Vector3.zero);
            IsMoving = false;
            return;
        }

        bool canMove = CanPerformAction(MinigameAction.Move);

        Vector3 moveDirection = (canMove && !IsKnockbacked)
            ? CalculateMoveDirection(input.MoveDirection, input.CameraForward)
            : Vector3.zero;

        IsMoving = moveDirection.magnitude > 0.01f;

        bool canRun    = CanPerformAction(MinigameAction.Run);
        bool canCrouch = CanPerformAction(MinigameAction.Crouch);

        IsRunning   = canRun    && input.IsButtonPressed(PlayerInputData.BUTTON_SLIDE);
        IsCrouching = canCrouch && input.IsButtonPressed(PlayerInputData.BUTTON_CROUCH);

        float targetSpeed = 0f;
        if (IsMoving)
        {
            if (IsCrouching)      targetSpeed = crouchSpeed;
            else if (IsRunning)   targetSpeed = runSpeed;
            else                  targetSpeed = walkSpeed;
        }

        Vector3 finalMovement = moveDirection.normalized * targetSpeed;
        finalMovement += ExternalVelocity;

        float totalSpeed = finalMovement.magnitude;
        _networkCC.maxSpeed = Mathf.Max(targetSpeed, totalSpeed);

        _networkCC.Move(finalMovement);

        _targetMoveDirection = moveDirection;

        // Giữ player thẳng đứng
        Vector3 euler = transform.eulerAngles;
        if (euler.x != 0f || euler.z != 0f)
            transform.rotation = Quaternion.Euler(0f, euler.y, 0f);
    }

    private void HandleJump(PlayerInputData input)
    {
        if (!CanPerformAction(MinigameAction.Jump)) return;
        if (IsKnockbacked) return;

        bool canJump = _networkCC.Grounded || GroundedTimer > 0;

        if (input.IsButtonPressed(PlayerInputData.BUTTON_JUMP) && canJump)
        {
            _networkCC.Jump();
            GroundedTimer = 0;
            Debug.Log("[PlayerController] JUMP!");

            // SFX không gọi ở đây — driven by state trong Render()
        }
    }

    private void HandleAttack(PlayerInputData input)
    {
        if (!CanPerformAction(MinigameAction.Attack)) return;
        if (IsKnockbacked) return;
        if (CurrentState == PlayerState.Attacking) return;

        bool canAttack = _networkCC.Grounded || GroundedTimer > 0;

        if (input.IsButtonPressed(PlayerInputData.BUTTON_PUNCH) && canAttack)
        {
            CurrentState = PlayerState.Attacking;
            AttackTimer  = attackDuration;
            Debug.Log("[PlayerController] ATTACK!");
            CheckAttackHit();
        }
    }

    private Vector3 CalculateMoveDirection(Vector2 input, Vector3 cameraForward)
    {
        if (input.sqrMagnitude < 0.01f)
            return Vector3.zero;

        Vector3 forward = cameraForward;
        if (forward.sqrMagnitude < 0.01f)
            forward = Vector3.forward;

        forward.y = 0;
        forward.Normalize();

        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        return (forward * input.y + right * input.x).normalized;
    }

    // ─────────────────────────────────────────────────────
    // State
    // ─────────────────────────────────────────────────────

    private void UpdateState()
    {
        if (CurrentState == PlayerState.Attacking)
        {
            if (AttackTimer > 0) return;
            CurrentState = PlayerState.Idle;
        }

        bool isGrounded  = _networkCC.Grounded;
        Vector3 velocity = _networkCC.Velocity;

        if (isGrounded)
            GroundedTimer = groundBufferTime;
        else
            GroundedTimer -= Runner.DeltaTime;

        bool isBufferedGrounded = isGrounded || GroundedTimer > 0;

        if (isBufferedGrounded)
            UpdateCrouchHitbox(IsCrouching);

        if (!isBufferedGrounded)
        {
            CurrentState = velocity.y > 0.2f ? PlayerState.Jumping : PlayerState.Falling;
            return;
        }

        if (IsCrouching) { CurrentState = PlayerState.Crouching; return; }
        if (IsMoving)    { CurrentState = IsRunning ? PlayerState.Running : PlayerState.Walking; return; }

        CurrentState = PlayerState.Idle;
    }

    private void UpdateExternalVelocity()
    {
        if (ExternalVelocity.sqrMagnitude < externalForceThreshold * externalForceThreshold)
        {
            ExternalVelocity = Vector3.zero;
            return;
        }

        Vector3 decay = ExternalVelocity.normalized * externalForceDrag * Runner.DeltaTime;

        if (decay.sqrMagnitude >= ExternalVelocity.sqrMagnitude)
            ExternalVelocity = Vector3.zero;
        else
            ExternalVelocity -= decay;
    }

    // ─────────────────────────────────────────────────────
    // Render
    // ─────────────────────────────────────────────────────

    public override void Render()
    {
        if (!HasInputAuthority) return;

        UpdateCrosshairVisibility();

        if (CameraManager.Instance == null) return;

        if (CameraManager.Instance.CurrentMode == CameraMode.FirstPerson)
        {
            RotateToYaw(CameraManager.Instance.FPYaw);
            return;
        }

        if (CameraManager.Instance.CurrentMode == CameraMode.ThirdPerson)
        {
            if (
                IsMoving &&
                _targetMoveDirection.sqrMagnitude > 0.01f &&
                CurrentState != PlayerState.Attacking
            )
            {
                RotateTowards(_targetMoveDirection);
            }
        }

        // ── SFX driven by State ──────────────────────────
        UpdateSFXByState();
    }

    private void RotateToYaw(float yaw)
    {
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            Quaternion.Euler(0, yaw, 0),
            rotationSpeed * 2f * Time.deltaTime
        );
    }

    private void RotateTowards(Vector3 direction)
    {
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            Quaternion.LookRotation(direction, Vector3.up),
            rotationSpeed * Time.deltaTime
        );
    }

    private void UpdateCrosshairVisibility()
    {
        if (crosshairUI == null) return;

        bool shouldShow =
            CameraManager.Instance != null &&
            CameraManager.Instance.CurrentMode == CameraMode.FirstPerson;

        if (crosshairUI.activeSelf != shouldShow)
            crosshairUI.SetActive(shouldShow);
    }

    // ─────────────────────────────────────────────────────
    // SFX — driven by CurrentState
    // Không gọi trực tiếp trong Handle functions
    // → tránh conflict với animation state machine
    // ─────────────────────────────────────────────────────

    private PlayerState _lastSFXState = PlayerState.Idle;

    private void UpdateSFXByState()
    {
        if (_sfx == null) return;
        if (CurrentState == _lastSFXState) return;

        PlayerState prev = _lastSFXState;
        _lastSFXState = CurrentState;

        switch (CurrentState)
        {
            case PlayerState.Walking:
                _sfx.StartFootstep(PlayerSFXType.Walk);
                break;

            case PlayerState.Running:
                _sfx.StartFootstep(PlayerSFXType.Run);
                break;

            case PlayerState.Jumping:
                _sfx.StopFootstep();
                if (prev == PlayerState.Idle    ||
                    prev == PlayerState.Walking  ||
                    prev == PlayerState.Running)
                {
                    _sfx.PlayAction(PlayerSFXType.Jump);
                }
                break;

            case PlayerState.Idle:
            case PlayerState.Falling:
            case PlayerState.Crouching:
            case PlayerState.Attacking:
                _sfx.StopFootstep();
                break;
        }
    }

    // ─────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────

    public void ApplyExternalForce(Vector3 force, float duration = 0f, bool overrideInput = false)
    {
        if (!HasStateAuthority) return;

        ExternalVelocity += force;

        if (overrideInput && duration > 0f)
            KnockbackTimer = Mathf.Max(KnockbackTimer, duration);
    }

    public void LaunchPad(float verticalSpeed)
    {
        if (!HasStateAuthority) return;
        var v = _networkCC.Velocity;
        _networkCC.Velocity = new Vector3(v.x, verticalSpeed, v.z);
    }

    public bool TryApplyHit(Vector3 knockbackForce)
    {
        if (!HasStateAuthority) return false;

        if (!HitCooldownTimer.ExpiredOrNotRunning(Runner))
            return false;

        ExternalVelocity += knockbackForce;
        HitCooldownTimer = TickTimer.CreateFromSeconds(Runner, hitCooldownDuration);
        return true;
    }

    public void ResetHitCooldown()
    {
        if (HasStateAuthority)
            HitCooldownTimer = TickTimer.None;
    }

    public void SetFrozen(bool frozen)
    {
        _isFrozen = frozen;
        if (frozen) ResetVelocity();
    }

    public void ResetVelocity()
    {
        if (_networkCC != null)
            _networkCC.Move(Vector3.zero);
        ExternalVelocity = Vector3.zero;
    }

    public void ForceIdle()
    {
        if (!HasStateAuthority) return;
        CurrentState = PlayerState.Idle;
        AttackTimer  = 0;
    }

    public float GetHorizontalSpeed()
    {
        if (_networkCC == null) return 0f;
        Vector3 v = _networkCC.Velocity;
        return new Vector3(v.x, 0, v.z).magnitude;
    }

    public bool IsInAir() =>
        _networkCC != null && !_networkCC.Grounded;

    public void Teleport(Vector3 position)
    {
        if (!HasStateAuthority) return;
        _networkCC.Teleport(position);
    }

    public void RequestTeleport(Vector3 targetPosition)
    {
        if (_networkCC != null)
            _networkCC.Teleport(targetPosition);
    }

    public void SetMovementEnabled(bool enabled)
    {
        if (_networkCC != null)
            _networkCC.enabled = enabled;
    }

    // ─────────────────────────────────────────────────────
    // Minigame
    // ─────────────────────────────────────────────────────

    private bool CanPerformAction(MinigameAction action)
    {
        if (GameManager.Instance == null) return true;
        if (GameManager.Instance.CurrentState != GameState.Playing) return true;

        return action switch
        {
            MinigameAction.Move   => GameManager.Instance.MG_CanMove,
            MinigameAction.Jump   => GameManager.Instance.MG_CanJump,
            MinigameAction.Crouch => GameManager.Instance.MG_CanCrouch,
            MinigameAction.Attack => GameManager.Instance.MG_CanAttack,
            MinigameAction.Run    => GameManager.Instance.MG_CanRun,
            _                     => true
        };
    }

    // ─────────────────────────────────────────────────────
    // Combat
    // ─────────────────────────────────────────────────────

    private void CheckAttackHit()
    {
        Collider[] hits = Physics.OverlapSphere(
            transform.position + transform.forward * 1.5f, 1f
        );

        foreach (Collider hit in hits)
        {
            if (hit.gameObject == gameObject) continue;

            PlayerController other = hit.GetComponent<PlayerController>();
            if (other == null) continue;

            Vector3 knockback = transform.forward * 8f + Vector3.up * 2f;
            bool success = other.TryApplyHit(knockback);

            if (success)
            {
                other.ForceIdle();
                Debug.Log($"[FUSION HIT] {Object.InputAuthority} hit {other.Object.InputAuthority}");
            }
        }
    }

    // ─────────────────────────────────────────────────────
    // Crouch
    // ─────────────────────────────────────────────────────

    private void UpdateCrouchHitbox(bool crouching)
    {
        Vector3 targetScale = crouching ? _crouchScaleVec : _normalScale;
        transform.localScale = Vector3.Lerp(
            transform.localScale,
            targetScale,
            crouchScaleSpeed * Runner.DeltaTime
        );
    }

    // ─────────────────────────────────────────────────────
    // Gizmos
    // ─────────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        bool grounded = _networkCC != null && _networkCC.Grounded;
        Gizmos.color  = grounded ? Color.green : Color.yellow;

        Vector3 origin = transform.position + Vector3.up * 0.1f;
        Gizmos.DrawWireSphere(origin + Vector3.down * 0.1f, 0.3f);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position + transform.forward * 1.5f, 1f);
    }
}