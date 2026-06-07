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

/// <summary>
/// Các hành động có thể bị giới hạn bởi MinigameData
/// </summary>
public enum MinigameAction
{
    Move,
    Jump,
    Crouch,
    Attack,
    Run
}

[RequireComponent(typeof(NetworkCharacterController))]
public class PlayerController : NetworkBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float walkSpeed = 5f;
    [SerializeField] private float runSpeed = 9f;
    [SerializeField] private float crouchSpeed = 2.5f;
    [SerializeField] private float rotationSpeed = 15f;

    [Header("Ground Check")]
    [SerializeField] private float groundBufferTime = 0.15f; // Thời gian buffer sau khi rời mặt đất

    [Header("Attack Settings")]
    [SerializeField] private float attackDuration = 0.7f;

    [Header("Crouch Settings")]
    [SerializeField] private float crouchScale = 0.75f; // Scale khi crouch (0.75 = 75% kích thước)
    [SerializeField] private float crouchScaleSpeed = 10f; // Tốc độ scale

    [Header("External Force Settings")]
    [SerializeField] private float externalForceDrag = 5f; // Tốc độ giảm dần external force
    [SerializeField] private float externalForceThreshold = 0.1f; // Ngưỡng để reset về 0

    [Header("Hit Cooldown Settings")]
    [SerializeField] private float hitCooldownDuration = 0.5f; // Thời gian cooldown sau khi bị hit

    [Header("UI")]
    [SerializeField] private GameObject crosshairUI;

    [Networked] public PlayerState CurrentState { get; private set; }
    [Networked] private Vector3 ExternalVelocity { get; set; } // Lực từ bên ngoài (obstacle, knockback)
    [Networked] private float AttackTimer { get; set; }
    [Networked] private NetworkBool IsRunning { get; set; }
    [Networked] private NetworkBool IsCrouching { get; set; }
    [Networked] private NetworkBool IsMoving { get; set; }
    [Networked] private float GroundedTimer { get; set; } // Timer để buffer ground check
    [Networked] private TickTimer HitCooldownTimer { get; set; } // Timer cho hit cooldown

    /// <summary>
    /// Kiểm tra player có đang trong hit cooldown không
    /// </summary>
    public bool IsInHitCooldown => HitCooldownTimer.ExpiredOrNotRunning(Runner) == false;

    public Vector3 Velocity => _networkCC != null ? _networkCC.Velocity : Vector3.zero;

    private Vector3 _targetMoveDirection;

    private NetworkCharacterController _networkCC;
    private Transform _cameraTransform;
    private CameraOrbit _cameraOrbit;
    private PlayerAnimator _playerAnimator;
    private PlayerSFXController _sfx;
    private Vector3 _normalScale;
    private Vector3 _crouchScale;

    private void Awake()
    {
        _networkCC = GetComponent<NetworkCharacterController>();
        _playerAnimator = GetComponent<PlayerAnimator>();
        _sfx = GetComponent<PlayerSFXController>(); 
        
        // Lưu scale gốc và tính scale crouch
        _normalScale = transform.localScale;
        _crouchScale = new Vector3(_normalScale.x, _normalScale.y * crouchScale, _normalScale.z);
    }

    public override void Spawned()
    {
        Debug.Log($"[PlayerController] Spawned. HasInputAuthority: {HasInputAuthority}, HasStateAuthority: {HasStateAuthority}");

        if (HasInputAuthority)
        {
            // Đăng ký với CameraManager
            if (CameraManager.Instance != null)
            {
                CameraManager.Instance.RegisterLocalPlayer(transform);

                // Chọn camera mode dựa trên GameState hiện tại
                if (GameManager.Instance != null)
                {
                    var state = GameManager.Instance.CurrentState;
                    if (state == GameState.Lobby || state == GameState.Voting || state == GameState.Roulette)
                    {
                        // First Person cho Lobby, Voting, Roulette
                        CameraManager.Instance.SwitchToFirstPersonCamera();
                    }
                    else if (state == GameState.Playing)
                    {
                        // Kiểm tra setting từ MinigameData
                        var minigameData = GameManager.Instance.CurrentMinigameData;
                        if (minigameData != null && !minigameData.useSharedCamera)
                        {
                            if (minigameData.useThirdPersonCamera)
                                CameraManager.Instance.SwitchToThirdPersonCamera();
                            else
                                CameraManager.Instance.SwitchToFirstPersonCamera();
                        }
                        else
                        {
                            // Default: Third Person cho Minigame
                            CameraManager.Instance.SwitchToThirdPersonCamera();
                        }
                    }
                    else
                    {
                        // Default: First Person
                        CameraManager.Instance.SwitchToFirstPersonCamera();
                    }
                }
                else
                {
                    // Fallback: First Person nếu không có GameManager
                    CameraManager.Instance.SwitchToFirstPersonCamera();
                }

                // Lấy reference từ CameraManager
                _cameraOrbit = CameraManager.Instance.CameraOrbit;
            }

            // Fallback: tìm trực tiếp trên Main Camera
            if (_cameraOrbit == null)
            {
                _cameraOrbit = Camera.main?.GetComponent<CameraOrbit>();
                _cameraOrbit?.SetTarget(transform);
            }

            _cameraTransform = Camera.main?.transform;

            // Crosshair sẽ được update trong Render() dựa trên camera mode
            UpdateCrosshairVisibility();

            Debug.Log("[PlayerController] Local player spawned and camera activated");
        }
        else
        {
            // Tắt crosshair cho player khác
            if (crosshairUI != null)
                crosshairUI.SetActive(false);
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (HasInputAuthority)
        {
            // Hủy đăng ký với CameraManager (sẽ tự chuyển về Fixed mode)
            if (CameraManager.Instance != null)
            {
                CameraManager.Instance.UnregisterLocalPlayer();
            }
        }
    }

    public override void FixedUpdateNetwork()
    {
        // Update attack timer
        if (AttackTimer > 0)
        {
            AttackTimer -= Runner.DeltaTime;
            if (AttackTimer <= 0)
            {
                AttackTimer = 0;
            }
        }

        // Update external velocity (decay over time)
        UpdateExternalVelocity();

        if (GetInput(out PlayerInputData input))
        {
            // Không di chuyển khi đang attack
            if (CurrentState != PlayerState.Attacking)
            {
                Move(input);
                HandleJump(input);
                HandleAttack(input);
            }
            else if (AttackTimer <= 0)
            {
                // Reset state sau khi attack xong
                CurrentState = PlayerState.Idle;
            }
        }

        UpdateState();
    }

    private void Move(PlayerInputData input)
    {
        // Không di chuyển khi bị frozen
        if (_isFrozen)
        {
            _networkCC.Move(Vector3.zero);
            IsMoving = false;   
            return;
        }

        // Kiểm tra minigame có cho phép di chuyển không
        bool canMoveInMinigame = CanPerformAction(MinigameAction.Move);

        // Dùng camera direction từ input (đã được client gửi lên)
        Vector3 moveDirection = canMoveInMinigame 
            ? CalculateMoveDirection(input.MoveDirection, input.CameraForward) 
            : Vector3.zero;

        // Check có input di chuyển không
        IsMoving = moveDirection.magnitude > 0.01f;

        // Check running (giữ Shift) - kiểm tra canRun
        bool canRunInMinigame = CanPerformAction(MinigameAction.Run);
        IsRunning = canRunInMinigame && input.IsButtonPressed(PlayerInputData.BUTTON_SLIDE);

        // Check crouching (giữ C hoặc Left Ctrl) - kiểm tra canCrouch
        bool canCrouchInMinigame = CanPerformAction(MinigameAction.Crouch);
        IsCrouching = canCrouchInMinigame && input.IsButtonPressed(PlayerInputData.BUTTON_CROUCH);

        // Tốc độ: ngồi < đi < chạy
        float targetSpeed = 0f;
        if (IsMoving)
        {
            if (IsCrouching)
                targetSpeed = crouchSpeed;
            else if (IsRunning)
                targetSpeed = runSpeed;
            else
                targetSpeed = walkSpeed;
        }

        // Tính final movement bao gồm external velocity
        Vector3 finalMovement = moveDirection.normalized * targetSpeed;

        // Thêm external velocity (lực đẩy từ obstacle)
        finalMovement += ExternalVelocity;

        // Update MaxSpeed để không bị clamp (cần cao hơn khi có external force)
        float totalSpeed = finalMovement.magnitude;
        _networkCC.maxSpeed = Mathf.Max(targetSpeed, totalSpeed);

        // Apply movement
        _networkCC.Move(finalMovement);
        // SFX footstep
        _targetMoveDirection = moveDirection;
    }

    /// <summary>
    /// Giảm dần external velocity theo thời gian
    /// </summary>
    private void UpdateExternalVelocity()
    {
        if (ExternalVelocity.sqrMagnitude < externalForceThreshold * externalForceThreshold)
        {
            ExternalVelocity = Vector3.zero;
            return;
        }

        // Decay external velocity
        Vector3 decay = ExternalVelocity.normalized * externalForceDrag * Runner.DeltaTime;

        if (decay.sqrMagnitude >= ExternalVelocity.sqrMagnitude)
        {
            ExternalVelocity = Vector3.zero;
        }
        else
        {
            ExternalVelocity -= decay;
        }
    }

    /// <summary>
    /// Áp dụng lực từ bên ngoài (obstacle, knockback) - HOST ONLY
    /// </summary>
    public void ApplyExternalForce(Vector3 force)
    {
        if (!HasStateAuthority) return;

        ExternalVelocity += force;
        Debug.Log($"[PlayerController] Applied external force: {force}, total: {ExternalVelocity}");
    }
    
    /// <summary>
    /// Áp dụng knockback - gọi từ local player, RPC đến host
    /// </summary>
    public void ApplyKnockback(Vector3 force)
    {
        if (Object.HasInputAuthority)
        {
            RPC_RequestKnockback(force);
        }
    }

    /// <summary>
    /// Áp dụng knockback với hit cooldown - ngăn spam hit
    /// </summary>
    public void ApplyKnockbackWithCooldown(Vector3 force)
    {
        if (Object.HasInputAuthority)
        {
            RPC_RequestKnockbackWithCooldown(force);
        }
    }

    /// <summary>
    /// Request teleport từ local player - gửi RPC đến host với cooldown
    /// </summary>
    public void RequestTeleport(Vector3 targetPosition)
    {
        if (Object.HasInputAuthority)
        {
            RPC_RequestTeleportWithCooldown(targetPosition);
        }
    }
    
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_RequestKnockback(Vector3 force)
    {
        ExternalVelocity += force;
        Debug.Log($"[PlayerController] Knockback applied: {force}, total: {ExternalVelocity}");
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_RequestKnockbackWithCooldown(Vector3 force)
    {
        // Kiểm tra cooldown trên host
        if (!HitCooldownTimer.ExpiredOrNotRunning(Runner))
        {
            Debug.Log($"[PlayerController] Knockback ignored - still in cooldown");
            return;
        }

        // Áp dụng knockback    
        ExternalVelocity += force;
        
        // Bắt đầu cooldown timer
        HitCooldownTimer = TickTimer.CreateFromSeconds(Runner, hitCooldownDuration);
        
        Debug.Log($"[PlayerController] Knockback with cooldown applied: {force}, cooldown: {hitCooldownDuration}s");
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_RequestTeleportWithCooldown(Vector3 targetPosition)
    {
        // Kiểm tra cooldown trên host
        if (!HitCooldownTimer.ExpiredOrNotRunning(Runner))
        {
            Debug.Log($"[PlayerController] Teleport ignored - still in cooldown");
            return;
        }

        // Teleport player
        _networkCC.Teleport(targetPosition);
        
        // Bắt đầu cooldown timer
        HitCooldownTimer = TickTimer.CreateFromSeconds(Runner, hitCooldownDuration);
        
        Debug.Log($"[PlayerController] Teleport with cooldown applied: {targetPosition}, cooldown: {hitCooldownDuration}s");
    }

    /// <summary>
    /// Kiểm tra và áp dụng hit từ bên ngoài (attack từ player khác, etc.)
    /// Trả về true nếu hit được áp dụng, false nếu đang trong cooldown
    /// </summary>
    public bool TryApplyHit(Vector3 knockbackForce)
    {
        if (!HasStateAuthority) return false;
        
        // Kiểm tra cooldown
        if (!HitCooldownTimer.ExpiredOrNotRunning(Runner))
        {
            return false;
        }

        // Áp dụng knockback
        ExternalVelocity += knockbackForce;
        
        // Bắt đầu cooldown
        HitCooldownTimer = TickTimer.CreateFromSeconds(Runner, hitCooldownDuration);
        
        Debug.Log($"[PlayerController] Hit applied with knockback: {knockbackForce}");
        return true;
    }

    /// <summary>
    /// Reset hit cooldown - dùng khi respawn hoặc round mới
    /// </summary>
    public void ResetHitCooldown()
    {
        if (HasStateAuthority)
        {
            HitCooldownTimer = TickTimer.None;
        }
    }

    private void HandleJump(PlayerInputData input)
    {
        // Kiểm tra minigame có cho phép nhảy không
        if (!CanPerformAction(MinigameAction.Jump)) return;
        
        // Coyote time - cho phép nhảy trong buffer time sau khi rời mặt đất
        bool canJump = _networkCC.Grounded || GroundedTimer > 0;

        if (input.IsButtonPressed(PlayerInputData.BUTTON_JUMP) && canJump)
        {
            _networkCC.Jump();
            GroundedTimer = 0; // Reset buffer khi đã nhảy

            // Trigger jump animation
            if (_playerAnimator != null)
            {
                _playerAnimator.TriggerJump();
            }
        }
    }

    private void HandleAttack(PlayerInputData input)
    {
        // Kiểm tra minigame có cho phép tấn công không
        if (!CanPerformAction(MinigameAction.Attack)) return;
        
        // Cho phép attack trong buffer time
        bool canAttack = _networkCC.Grounded || GroundedTimer > 0;

        if (input.IsButtonPressed(PlayerInputData.BUTTON_PUNCH) && canAttack)
        {
            CurrentState = PlayerState.Attacking;
            AttackTimer = attackDuration;

            // Trigger animation
            if (_playerAnimator != null)
            {
                _playerAnimator.TriggerAttack();
            }
        }
    }

    private Vector3 CalculateMoveDirection(Vector2 input, Vector3 cameraForward)
    {
        if (input.sqrMagnitude < 0.01f)
            return Vector3.zero;

        // Dùng camera forward từ input data (được client gửi lên)
        Vector3 forward = cameraForward;
        if (forward.sqrMagnitude < 0.01f)
            forward = Vector3.forward;

        forward.y = 0f;
        forward.Normalize();

        // Tính right từ forward
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

        Vector3 moveDir = (forward * input.y + right * input.x);
        return moveDir.normalized;
    }

    private void UpdateState()
    {
        // Không update state nếu đang attack
        if (CurrentState == PlayerState.Attacking && AttackTimer > 0)
            return;

        bool isGrounded = _networkCC.Grounded;
        Vector3 velocity = _networkCC.Velocity;

        // Update ground buffer timer
        if (isGrounded)
        {
            GroundedTimer = groundBufferTime;
        }
        else
        {
            GroundedTimer -= Runner.DeltaTime;
        }

        // Buffered ground check - vẫn coi như grounded nếu còn trong buffer time
        bool isBufferedGrounded = isGrounded || GroundedTimer > 0;

        // Cập nhật hitbox crouch - chỉ khi đang ở trên mặt đất và không nhảy/rơi
        if (isBufferedGrounded)
        {
            UpdateCrouchHitbox(IsCrouching);
        }

        if (!isBufferedGrounded)
        {
            CurrentState = velocity.y > 0.2f ? PlayerState.Jumping : PlayerState.Falling;
        }
        else if (IsCrouching)
        {
            // Crouching có priority cao hơn walking/running
            CurrentState = PlayerState.Crouching;
        }
        else if (IsMoving) // Dựa vào input, không dựa vào velocity
        {
            CurrentState = IsRunning ? PlayerState.Running : PlayerState.Walking;
        }
        else
        {
            CurrentState = PlayerState.Idle;
        }
    }

    public override void Render()
    {
        // CHỈ xử lý rotation cho LOCAL player (HasInputAuthority)
        // Remote players sẽ được sync rotation qua NetworkTransform hoặc không cần xoay local
        if (!HasInputAuthority) return;

        // Update crosshair visibility dựa trên camera mode
        UpdateCrosshairVisibility();

        if (CameraManager.Instance == null) return;

        // First Person: Player body luôn xoay theo hướng camera nhìn
        if (CameraManager.Instance.CurrentMode == CameraMode.FirstPerson)
        {
            RotateToYaw(CameraManager.Instance.FPYaw);
            return;
        }

        // Third Person: CHỈ xoay khi đang di chuyển (không xoay khi đứng yên)
        // Điều này cho phép xoay camera quanh player để ngắm model
        if (CameraManager.Instance.CurrentMode == CameraMode.ThirdPerson)
        {
            // Chỉ xoay nếu đang thực sự di chuyển (có input)
            if (IsMoving && _targetMoveDirection.sqrMagnitude > 0.01f && CurrentState != PlayerState.Attacking)
            {
                RotateTowards(_targetMoveDirection);
            }
            // Khi đứng yên: KHÔNG xoay model → có thể xoay camera xung quanh để ngắm
        } UpdateSFXByState();
    }

    /// <summary>
    /// Xoay player theo yaw angle (dùng cho First Person)
    /// </summary>
    private void RotateToYaw(float yaw)
    {
        Quaternion targetRotation = Quaternion.Euler(0, yaw, 0);
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            rotationSpeed * 2f * Time.deltaTime // Nhanh hơn để sync với camera
        );
    }

    private void RotateTowards(Vector3 direction)
    {
        Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            rotationSpeed * Time.deltaTime
        );
    }

    public float GetHorizontalSpeed()
    {
        if (_networkCC == null) return 0f;
        Vector3 velocity = _networkCC.Velocity;
        return new Vector3(velocity.x, 0, velocity.z).magnitude;
    }

    public bool IsInAir() => _networkCC != null && !_networkCC.Grounded;

    public void Teleport(Vector3 position)
    {
        if (!HasStateAuthority)
        {
            Debug.LogWarning("[PlayerController] Only state authority can teleport");
            return;
        }

        _networkCC.Teleport(position);
    }

    public void SetMovementEnabled(bool enabled)
    {
        if (_networkCC != null)
        {
            _networkCC.enabled = enabled;
        }
    }

    /// <summary>
    /// Freeze/Unfreeze player - dùng cho minigame countdown/win
    /// </summary>
    private bool _isFrozen;
    public bool IsFrozen => _isFrozen;

    /// <summary>
    /// Update crosshair visibility based on camera mode
    /// Crosshair chỉ hiển thị ở First Person mode
    /// </summary>
    private void UpdateCrosshairVisibility()
    {
        if (crosshairUI == null) return;

        bool shouldShow = CameraManager.Instance != null &&
                          CameraManager.Instance.CurrentMode == CameraMode.FirstPerson;

        if (crosshairUI.activeSelf != shouldShow)
        {
            crosshairUI.SetActive(shouldShow);
        }
    }

    public void SetFrozen(bool frozen)
    {
        _isFrozen = frozen;
        Debug.Log($"[PlayerController] Player {Object.InputAuthority} frozen: {frozen}");

        if (frozen)
        {
            // Reset velocity khi freeze
            ResetVelocity();
        }
    }

    /// <summary>
    /// Reset velocity - dùng khi respawn
    /// </summary>
    public void ResetVelocity()
    {
        if (_networkCC != null)
        {
            _networkCC.Move(Vector3.zero);
        }
        ExternalVelocity = Vector3.zero;
    }

    /// <summary>
    /// Điều chỉnh scale của player khi crouch
    /// Scale Y nhỏ lại sẽ làm CharacterController hitbox nhỏ theo
    /// </summary>
    private void UpdateCrouchHitbox(bool crouching)
    {
        Vector3 targetScale = crouching ? _crouchScale : _normalScale;
        
        // Lerp smooth scale
        transform.localScale = Vector3.Lerp(
            transform.localScale, 
            targetScale, 
            crouchScaleSpeed * Runner.DeltaTime
        );
    }

    #region Minigame Action Check
    /// <summary>
    /// Kiểm tra xem hành động có được phép trong minigame hiện tại không
    /// Nếu không trong minigame (Playing state), luôn trả về true
    /// </summary>
    private bool CanPerformAction(MinigameAction action)
    {
        // Không trong Playing state -> cho phép tất cả
        if (GameManager.Instance == null)
        {
            return true;
        }
        
        if (GameManager.Instance.CurrentState != GameState.Playing)
        {
            return true;
        }
        
        // Đọc từ synced Networked properties (đã được host sync)
        return action switch
        {
            MinigameAction.Move => GameManager.Instance.MG_CanMove,
            MinigameAction.Jump => GameManager.Instance.MG_CanJump,
            MinigameAction.Crouch => GameManager.Instance.MG_CanCrouch,
            MinigameAction.Attack => GameManager.Instance.MG_CanAttack,
            MinigameAction.Run => GameManager.Instance.MG_CanRun,
            _ => true
        };
    }
    #endregion

    private void OnDrawGizmosSelected()
    {
        bool grounded = _networkCC != null && _networkCC.Grounded;

        Gizmos.color = grounded ? Color.green : Color.yellow;
        Vector3 origin = transform.position + Vector3.up * 0.1f;
        Gizmos.DrawWireSphere(origin + Vector3.down * 0.1f, 0.3f);
    }
    private PlayerState _lastSFXState = PlayerState.Idle;

    private void UpdateSFXByState()
    {
        if (_sfx == null) return;
        if (CurrentState == _lastSFXState) return; // Không thay đổi → bỏ qua

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
                // Chỉ play jump sound khi chuyển từ state có thể nhảy
                if (prev == PlayerState.Idle   ||
                    prev == PlayerState.Walking ||
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
}