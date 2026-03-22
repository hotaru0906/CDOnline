using Fusion;
using UnityEngine;

public enum PlayerState
{
    Idle,
    Walking,
    Running,
    Jumping,
    Falling,
    Attacking
}

[RequireComponent(typeof(NetworkCharacterController))]
public class PlayerController : NetworkBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float walkSpeed = 5f;
    [SerializeField] private float runSpeed = 9f;
    [SerializeField] private float rotationSpeed = 15f;

    [Header("Ground Check")]
    [SerializeField] private float groundBufferTime = 0.15f; // Thời gian buffer sau khi rời mặt đất

    [Header("Attack Settings")]
    [SerializeField] private float attackDuration = 0.7f;

    [Networked] public PlayerState CurrentState { get; private set; }
    [Networked] private float AttackTimer { get; set; }
    [Networked] private NetworkBool IsRunning { get; set; }
    [Networked] private NetworkBool IsMoving { get; set; } // Có input di chuyển không
    [Networked] private float GroundedTimer { get; set; } // Timer để buffer ground check

    public Vector3 Velocity => _networkCC != null ? _networkCC.Velocity : Vector3.zero;

    private Vector3 _targetMoveDirection;

    private NetworkCharacterController _networkCC;
    private Transform _cameraTransform;
    private CameraOrbit _cameraOrbit;
    private PlayerAnimator _playerAnimator;

    private void Awake()
    {
        _networkCC = GetComponent<NetworkCharacterController>();
        _playerAnimator = GetComponent<PlayerAnimator>();
    }

    public override void Spawned()
    {
        Debug.Log($"[PlayerController] Spawned. HasInputAuthority: {HasInputAuthority}, HasStateAuthority: {HasStateAuthority}");

        if (HasInputAuthority)
        {
            // Đăng ký với CameraManager và chuyển sang Player camera mode
            if (CameraManager.Instance != null)
            {
                CameraManager.Instance.RegisterLocalPlayer(transform);
                CameraManager.Instance.SwitchToPlayerCamera(); // Bật CameraOrbit
                
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
            
            Debug.Log("[PlayerController] Local player spawned and camera activated");
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

        // Dùng camera direction từ input (đã được client gửi lên)
        Vector3 moveDirection = CalculateMoveDirection(input.MoveDirection, input.CameraForward);
        
        // Check có input di chuyển không
        IsMoving = moveDirection.magnitude > 0.01f;
        
        // Check running (giữ Shift)
        IsRunning = input.IsButtonPressed(PlayerInputData.BUTTON_SLIDE);
        
        // Tốc độ: chạy nếu đang giữ Shift VÀ đang di chuyển
        float targetSpeed = IsMoving ? (IsRunning ? runSpeed : walkSpeed) : 0f;

        // Update MaxSpeed của NetworkCharacterController để không bị clamp
        _networkCC.maxSpeed = targetSpeed;

        // Apply movement - truyền direction, NCC sẽ dùng MaxSpeed đã set
        _networkCC.Move(moveDirection.normalized * targetSpeed);

        _targetMoveDirection = moveDirection;
    }

    private void HandleJump(PlayerInputData input)
    {
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

        if (!isBufferedGrounded)
        {
            CurrentState = velocity.y > 0.2f ? PlayerState.Jumping : PlayerState.Falling;
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
        if (_targetMoveDirection.sqrMagnitude > 0.01f && CurrentState != PlayerState.Attacking)
        {
            RotateTowards(_targetMoveDirection);
        }
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
    }

    private void OnDrawGizmosSelected()
    {
        bool grounded = _networkCC != null && _networkCC.Grounded;

        Gizmos.color = grounded ? Color.green : Color.yellow;
        Vector3 origin = transform.position + Vector3.up * 0.1f;
        Gizmos.DrawWireSphere(origin + Vector3.down * 0.1f, 0.3f);
    }
}