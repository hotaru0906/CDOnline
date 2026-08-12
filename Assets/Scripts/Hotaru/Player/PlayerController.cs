using Fusion;
using UnityEngine;
using System.Collections;

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
[RequireComponent(typeof(PlayerModelSwitcher))]
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
    [SerializeField] private float attackDuration = 2f;

    [Header("Crouch")]
    [SerializeField] private float crouchScale = 0.75f;
    [SerializeField] private float crouchScaleSpeed = 10f;

    [Header("External Force")]
    [SerializeField] private float externalForceDrag = 5f;
    [SerializeField] private float externalForceThreshold = 0.1f;

    [Header("Hit Cooldown")]
    [SerializeField] private float hitCooldownDuration = 0.5f;

    [Header("Stun")]
    [SerializeField] private GameObject dizzyVFX; // Gắn object dizzy trong prefab, ban đầu inactive
    [SerializeField] private float stunDuration = 1f;
    [SerializeField] private GameObject freezeVFX;

    [Header("UI")]
    [SerializeField] private GameObject crosshairUI;

    [Networked] public PlayerState CurrentState { get; private set; }

    [Networked] private Vector3 ExternalVelocity { get; set; }
    [Networked] private float AttackTimer { get; set; }

    [Networked]
    public NetworkBool IsAttacking { get; private set; }

    [Networked] private NetworkBool IsRunning { get; set; }
    [Networked] private NetworkBool IsCrouching { get; set; }
    [Networked] private NetworkBool IsMoving { get; set; }

    [Networked] private float GroundedTimer { get; set; }
    [Networked] private NetworkBool IsJumpingByInput { get; set; }
    [Networked] private TickTimer HitCooldownTimer { get; set; }

    [Networked, OnChangedRender(nameof(OnStunChanged))]
    public NetworkBool IsStunned { get; private set; }

    [Networked] private TickTimer StunTimer { get; set; }
    [Networked] private float KnockbackTimer { get; set; }
    [Networked] private float SpeedMultiplier { get; set; } = 1f;
    [Networked] private float BoostTimer { get; set; }

    public bool IsInHitCooldown =>
        HitCooldownTimer.ExpiredOrNotRunning(Runner) == false;

    /// <summary>true khi player đang bị knockback (không thể input).</summary>
    public bool IsKnockbacked => KnockbackTimer > 0f;

    public Vector3 Velocity =>
        _networkCC != null ? _networkCC.Velocity : Vector3.zero;

    private NetworkCharacterController _networkCC;
    private PlayerAnimator _playerAnimator;
    private PlayerModelSwitcher _modelSwitcher;

    private CameraOrbit _cameraOrbit;

    private PlayerMinigameData _minigameData;
    private Transform _cameraTransform;

    private Vector3 _targetMoveDirection;

    private Vector3 _normalScale;
    private Vector3 _crouchScale;

    [Networked, OnChangedRender(nameof(OnFrozenChanged))]
    public NetworkBool IsFrozenNetworked { get; private set; }

    [Networked]
    private NetworkBool IsMovementLocked { get; set; }

    public bool IsFrozen => IsFrozenNetworked || IsMovementLocked;

    public void SetMovementLocked(bool locked)
    {
        if (!HasStateAuthority)
            return;

        IsMovementLocked = locked;
    }

    private void Awake()
    {
        _networkCC = GetComponent<NetworkCharacterController>();
        _playerAnimator = GetComponent<PlayerAnimator>();
        _modelSwitcher = GetComponent<PlayerModelSwitcher>();
        _minigameData = GetComponent<PlayerMinigameData>();

        _normalScale = transform.localScale;

        _crouchScale = new Vector3(
            _normalScale.x,
            _normalScale.y * crouchScale,
            _normalScale.z
        );
    }

    public override void Spawned()
    {
        if (freezeVFX != null)
        {
            freezeVFX.SetActive(false);
        }
        
        if (!HasInputAuthority)
        {
            if (crosshairUI != null)
                crosshairUI.SetActive(false);
            return;
        }

        if (CameraManager.Instance != null)
        {
            CameraManager.Instance.RegisterLocalPlayer(transform);

            if (GameManager.Instance == null || (GameManager.Instance.CurrentState != GameState.Tutorial && GameManager.Instance.CurrentState != GameState.Lobby))
            {
                CameraManager.Instance.SwitchToThirdPersonCamera();
            }

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
        {
            CameraManager.Instance.UnregisterLocalPlayer();
        }
    }

    public override void FixedUpdateNetwork()
    {

        if (AttackTimer > 0)
        {
            AttackTimer -= Runner.DeltaTime;

            if (AttackTimer <= 0)
            {
                AttackTimer = 0;
                IsAttacking = false;
            }
        }

        UpdateExternalVelocity();

        if (HasStateAuthority && KnockbackTimer > 0f)
            KnockbackTimer = Mathf.Max(0f, KnockbackTimer - Runner.DeltaTime); if (HasStateAuthority && BoostTimer > 0f)
        {
            BoostTimer -= Runner.DeltaTime;
            if (BoostTimer <= 0f)
            {
                BoostTimer = 0f;
                SpeedMultiplier = 1f;
            }
        }

        if (HasStateAuthority && IsStunned && StunTimer.Expired(Runner))
            IsStunned = false;

        if (GetInput(out PlayerInputData input))
        {
            if (!IsFrozen && !IsStunned) // THÊM: && !IsStunned
            {
                HandleAttack(input);

                Move(input);

                HandleJump(input);
            }
        }

        UpdateState();
    }

    private void Move(PlayerInputData input)
    {
        if (IsFrozen)
        {
            _networkCC.Move(Vector3.zero);
            IsMoving = false;
            IsRunning = false;
            return;
        }

        bool canMove = CanPerformAction(MinigameAction.Move);


        // Knockback: player không tự di chuyển được, nhưng ExternalVelocity vẫn tác động
        Vector3 moveDirection = (canMove && !IsKnockbacked)
            ? CalculateMoveDirection(input.MoveDirection, input.CameraForward)
            : Vector3.zero;

        IsMoving = moveDirection.magnitude > 0.01f;

        bool canRun = CanPerformAction(MinigameAction.Run);

        IsRunning =
            canRun &&
            input.IsButtonPressed(PlayerInputData.BUTTON_SLIDE);

        bool canCrouch = CanPerformAction(MinigameAction.Crouch);

        IsCrouching =
            canCrouch &&
            input.IsButtonPressed(PlayerInputData.BUTTON_CROUCH);

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
        targetSpeed *= SpeedMultiplier;
        Vector3 finalMovement =
            moveDirection.normalized * targetSpeed;

        finalMovement += ExternalVelocity;

        float totalSpeed = finalMovement.magnitude;

        _networkCC.maxSpeed =
            Mathf.Max(targetSpeed, totalSpeed);

        _networkCC.Move(finalMovement);

        _targetMoveDirection = moveDirection;

        // Luôn giữ player thẳng đứng — tránh NetworkCC / knockback làm nghiêng trục X/Z
        Vector3 euler = transform.eulerAngles;
        if (euler.x != 0f || euler.z != 0f)
            transform.rotation = Quaternion.Euler(0f, euler.y, 0f);
    }

    private void HandleJump(PlayerInputData input)
    {
        if (!CanPerformAction(MinigameAction.Jump))
            return;

        if (IsKnockbacked)
            return;

        bool canJump =
            _networkCC.Grounded ||
            GroundedTimer > 0;

        if (input.IsButtonPressed(PlayerInputData.BUTTON_JUMP) && canJump)
        {
            _networkCC.Jump();

            GroundedTimer = 0;

            IsJumpingByInput = true;

            // ép state Jump ngay lập tức
            CurrentState = PlayerState.Jumping;

            Debug.Log("[PlayerController] JUMP!");
        }
    }

    private void HandleAttack(PlayerInputData input)
    {
        if (!CanPerformAction(MinigameAction.Attack))
            return;

        if (IsKnockbacked) return;

        // Không spam attack
        if (IsAttacking)
            return;

        bool canAttack =
            _networkCC.Grounded ||
            GroundedTimer > 0;

        if (input.IsButtonPressed(PlayerInputData.BUTTON_PUNCH) && canAttack)
        {
            IsAttacking = true;

            AttackTimer = attackDuration;

            Debug.Log("[PlayerController] ATTACK!");
        }
    }

    /// <summary>
    /// Gọi bởi Animation Event trên clip đấm. Vì animation (và Animation Event) chạy cục bộ
    /// trên MÁY của bất kỳ client nào đang render nó (không riêng gì Host), ta KHÔNG được kiểm
    /// tra hit trực tiếp tại đây — chỉ máy có StateAuthority mới được phép áp dụng kết quả hit
    /// (xem TryApplyStun). Do đó chỉ gửi 1 RPC yêu cầu StateAuthority tự kiểm tra và xử lý.
    /// </summary>
    public void AttackHitEvent()
    {
        if (!IsAttacking)
            return;

        RPC_RequestAttackHit();
    }

    public void TriggerAttackHit()
    {
        AttackHitEvent();
    }

    /// <summary>
    /// Chạy trên máy có StateAuthority (Host) bất kể ai gửi request lên - đảm bảo
    /// CheckAttackHit()/TryApplyStun() luôn có đủ quyền để áp dụng stun cho player bị đánh.
    /// </summary>
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestAttackHit()
    {
        if (!IsAttacking)
            return;

        CheckAttackHit();
    }

    private void UpdateState()
    {

        bool isGrounded = _networkCC.Grounded;

        Vector3 velocity = _networkCC.Velocity;

        // Ground buffer
        if (isGrounded)
        {
            GroundedTimer = groundBufferTime;

            // Chỉ reset khi đã thực sự chạm đất
            if (velocity.y <= 0.1f)
            {
                IsJumpingByInput = false;
            }
        }
        else
        {
            GroundedTimer -= Runner.DeltaTime;
        }

        bool isBufferedGrounded =
            isGrounded || GroundedTimer > 0;

        // Crouch scale
        if (isBufferedGrounded)
        {
            UpdateCrouchHitbox(IsCrouching);
        }

        // ==========================================
        // ƯU TIÊN TRẠNG THÁI TRÊN KHÔNG
        // ==========================================

        if (velocity.y > 0.2f)
        {
            CurrentState = PlayerState.Jumping;
            return;
        }

        if (velocity.y < -0.2f && !isBufferedGrounded)
        {
            CurrentState = PlayerState.Falling;
            return;
        }

        // ==========================================
        // MẶT ĐẤT
        // ==========================================

        if (IsCrouching)
        {
            CurrentState = PlayerState.Crouching;
            return;
        }

        if (IsMoving)
        {
            CurrentState =
                IsRunning
                ? PlayerState.Running
                : PlayerState.Walking;

            return;
        }

        CurrentState = PlayerState.Idle;
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

        Vector3 right =
            Vector3.Cross(Vector3.up, forward).normalized;

        Vector3 moveDir =
            forward * input.y +
            right * input.x;

        return moveDir.normalized;
    }

    private void UpdateExternalVelocity()
    {
        if (
            ExternalVelocity.sqrMagnitude <
            externalForceThreshold * externalForceThreshold
        )
        {
            ExternalVelocity = Vector3.zero;
            return;
        }

        Vector3 decay =
            ExternalVelocity.normalized *
            externalForceDrag *
            Runner.DeltaTime;

        if (decay.sqrMagnitude >= ExternalVelocity.sqrMagnitude)
        {
            ExternalVelocity = Vector3.zero;
        }
        else
        {
            ExternalVelocity -= decay;
        }
    }

    public override void Render()
    {
        if (!HasInputAuthority)
            return;

        if (_minigameData != null && _minigameData.IsEliminated)
        {
            return;
        }

        UpdateCrosshairVisibility();

        if (CameraManager.Instance == null)
            return;

        if (CameraManager.Instance.CurrentMode == CameraMode.FirstPerson)
        {
            RotateToYaw(CameraManager.Instance.FPYaw);
            return;
        }

        if (CameraManager.Instance.CurrentMode == CameraMode.ThirdPerson)
        {
            if (
                IsMoving &&
                _targetMoveDirection.sqrMagnitude > 0.01f

            )
            {
                RotateTowards(_targetMoveDirection);
            }
        }
        RefreshFreezeVFX();
    }

    private void RotateToYaw(float yaw)
    {
        Quaternion targetRotation =
            Quaternion.Euler(0, yaw, 0);

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            rotationSpeed * 2f * Time.deltaTime
        );
    }

    private void RotateTowards(Vector3 direction)
    {
        Quaternion targetRotation =
            Quaternion.LookRotation(direction, Vector3.up);

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            rotationSpeed * Time.deltaTime
        );
    }

    private void UpdateCrosshairVisibility()
    {
        if (crosshairUI == null)
            return;

        bool shouldShow =
            CameraManager.Instance != null &&
            CameraManager.Instance.CurrentMode == CameraMode.FirstPerson;

        if (crosshairUI.activeSelf != shouldShow)
        {
            crosshairUI.SetActive(shouldShow);
        }
    }

    public void SetFrozen(bool frozen, bool showVFX = true)
    {
        if (!HasStateAuthority)
            return;

        IsFrozenNetworked = frozen;

        // Nếu chỉ muốn khóa điều khiển (ví dụ ngồi ghế)
        // thì tắt VFX ngay.
        if (!showVFX && freezeVFX != null)
        {
            freezeVFX.SetActive(false);
        }
    }
    private void OnFrozenChanged()
    {
        RefreshFreezeVFX();
    }

    private void RefreshFreezeVFX()
    {
        if (freezeVFX == null)
            return;

        bool show =
            GameManager.Instance != null &&
            GameManager.Instance.CurrentState == GameState.Playing &&
            IsFrozenNetworked;

        freezeVFX.SetActive(show);
    }

    public void ResetVelocity()
    {
        if (_networkCC != null)
            _networkCC.Move(Vector3.zero);
        ExternalVelocity = Vector3.zero;
    }

    private void UpdateCrouchHitbox(bool crouching)
    {
        Vector3 targetScale =
            crouching ? _crouchScale : _normalScale;

        transform.localScale = Vector3.Lerp(
            transform.localScale,
            targetScale,
            crouchScaleSpeed * Runner.DeltaTime
        );
    }

    public void ActivateRagdoll()
    {
        var ragdoll = GetActiveModelRagdoll();
        if (ragdoll == null)
        {
            ragdoll = GetComponentInChildren<Regdoll>(true);
            if (ragdoll == null)
            {
                Debug.LogWarning($"[PlayerController] Cannot activate ragdoll: no Regdoll component found on player {name}");
                return;
            }

            Debug.LogWarning($"[PlayerController] ActivateRagdoll fallback to any Regdoll on player {name}");
        }

        ragdoll.ActivateRagdoll();
    }

    public void DeactivateRagdoll()
    {
        var ragdoll = GetActiveModelRagdoll();
        if (ragdoll == null)
            ragdoll = GetComponentInChildren<Regdoll>(true);

        if (ragdoll == null)
            return;

        ragdoll.DeactivateRagdoll();
    }

    private Regdoll GetActiveModelRagdoll()
    {
        var activeModel = _modelSwitcher != null ? _modelSwitcher.GetActiveModel() : null;
        return activeModel != null ? activeModel.GetComponentInChildren<Regdoll>(true) : null;
    }

    /// <param name="force">Hướng và độ mạnh của lực.</param>
    /// <param name="duration">Thời gian block input (giây). 0 = không block.</param>
    /// <param name="overrideInput">Nếu true + duration > 0: block toàn bộ input trong thời gian duration.</param>
    public void ApplyExternalForce(Vector3 force, float duration = 0f, bool overrideInput = false)
    {
        if (!HasStateAuthority)
            return;

        ExternalVelocity += force;

        if (overrideInput && duration > 0f)
        {
            // Lấy giá trị lớn hơn để không cắt ngắn knockback đang chạy
            KnockbackTimer = Mathf.Max(KnockbackTimer, duration);
        }
    }

    /// <summary>
    /// Dùng cho JumpPad — phóng player lên cao với tốc độ Y tùy chỉnh.
    /// Dùng trực tiếp Velocity của NetworkCC thay vì ExternalVelocity để đảm bảo quỹ đạo tự nhiên.
    /// </summary>
    public void LaunchPad(float verticalSpeed)
    {
        if (!HasStateAuthority) return;
        var v = _networkCC.Velocity;
        _networkCC.Velocity = new Vector3(v.x, verticalSpeed, v.z);
    }

    public bool TryApplyHit(Vector3 knockbackForce)
    {
        if (!HasStateAuthority)
            return false;

        if (!HitCooldownTimer.ExpiredOrNotRunning(Runner))
        {
            return false;
        }

        ExternalVelocity += knockbackForce;

        HitCooldownTimer =
            TickTimer.CreateFromSeconds(
                Runner,
                hitCooldownDuration
            );

        return true;
    }
    public void ResetHitCooldown()
    {
        if (HasStateAuthority)
        {
            HitCooldownTimer = TickTimer.None;
        }
    }

    public float GetHorizontalSpeed()
    {
        if (_networkCC == null)
            return 0f;

        Vector3 velocity = _networkCC.Velocity;

        return new Vector3(
            velocity.x,
            0,
            velocity.z
        ).magnitude;
    }

    public bool IsInAir()
    {
        return _networkCC != null &&
               !_networkCC.Grounded;
    }

    public void Teleport(Vector3 position)
    {
        if (!HasStateAuthority)
            return;

        _networkCC.Teleport(position);
    }

    public void SetMovementEnabled(bool enabled)
    {
        if (_networkCC != null)
        {
            _networkCC.enabled = enabled;
        }
    }

    private bool CanPerformAction(MinigameAction action)
    {
        if (GameManager.Instance == null)
            return true;

        if (GameManager.Instance.CurrentState != GameState.Playing)
            return true;

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

    public bool TryApplyStun()
    {
        if (!HasStateAuthority) return false;

        if (!HitCooldownTimer.ExpiredOrNotRunning(Runner)) return false;

        IsStunned = true;
        StunTimer = TickTimer.CreateFromSeconds(Runner, stunDuration);
        HitCooldownTimer = TickTimer.CreateFromSeconds(Runner, hitCooldownDuration);

        return true;
    }

    private void OnStunChanged()
    {
        if (dizzyVFX != null)
            dizzyVFX.SetActive(IsStunned);
    }

    private void CheckAttackHit()
    {
        Collider[] hits = Physics.OverlapSphere(
            transform.position + transform.forward * 1.75f, 1f);

        foreach (Collider hit in hits)
        {
            if (hit.gameObject == gameObject) continue;

            var other = hit.GetComponentInParent<PlayerController>();
            if (other == null || other == this) continue;

            bool hitSuccess = other.TryApplyStun(); // SỬA: dùng stun thay knockback
            if (!hitSuccess) continue;

            other.ForceIdle();

            if (MG3BrawlController.Instance != null &&
                MG3BrawlController.Instance.IsGameStarted)
            {
                MG3BrawlController.Instance.OnPlayerHit(this, other);
            }

            if (MG5BombTagController.Instance != null &&
            MG5BombTagController.Instance.IsGameStarted)
            {
                GetComponent<MG5BombTagPlayer>()?.OnAttackHit(other);
            }
            if (MG7CrownController.Instance != null &&
            MG7CrownController.Instance.IsGameStarted)
            {
                GetComponent<MG7CrownPlayer>()?.OnAttackHit(other);
            }
            if (MG8Controller.Instance != null &&
            MG8Controller.Instance.IsGameStarted)
            {
                MG8Controller.Instance.OnPlayerHit(this, other);
            }
        }
    }
    public void ApplySpeedBoost(float multiplier, float duration)
    {
        if (!HasStateAuthority) return;
        SpeedMultiplier = multiplier;
        BoostTimer = duration;
    }

    public void ApplyTemporaryFreeze(float duration)
    {
        if (!HasStateAuthority)
            return;

        ResetVelocity();      // Dừng ngay lập tức
        ForceIdle();          // Hủy attack nếu đang đánh

        SetFrozen(true);

        StartCoroutine(UnfreezeAfter(duration));
    }

    private IEnumerator UnfreezeAfter(float duration)
    {
        yield return new WaitForSeconds(duration);
        SetFrozen(false);
    }

    public void ForceIdle()
    {
        if (!HasStateAuthority)
            return;

        IsAttacking = false;

        AttackTimer = 0;
    }

    private void OnDrawGizmosSelected()
    {
        bool grounded =
            _networkCC != null &&
            _networkCC.Grounded;

        Gizmos.color =
            grounded
            ? Color.green
            : Color.yellow;

        Vector3 origin =
            transform.position + Vector3.up * 0.1f;

        Gizmos.DrawWireSphere(
            origin + Vector3.down * 0.1f,
            0.3f
        );

        Gizmos.color = Color.red;

        Gizmos.DrawWireSphere(
            transform.position + transform.forward * 1.75f,
            1f
        );
    }

    public void RequestTeleport(Vector3 targetPosition)
    {
        if (_networkCC != null)
        {
            _networkCC.Teleport(targetPosition);
        }
    }
}