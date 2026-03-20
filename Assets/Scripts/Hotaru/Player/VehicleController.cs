using Fusion;
using UnityEngine;

/// <summary>
/// Controller cho xe - chỉ sử dụng trong minigames
/// Gắn vào Player prefab, bật/tắt khi cần
/// </summary>
public class VehicleController : NetworkBehaviour
{
    [Header("Vehicle Settings")]
    [SerializeField] private float maxSpeed = 20f;
    [SerializeField] private float acceleration = 10f;
    [SerializeField] private float brakeForce = 15f;
    [SerializeField] private float turnSpeed = 100f;
    [SerializeField] private float driftFactor = 0.95f;

    [Header("References")]
    [SerializeField] private GameObject vehicleModel;
    [SerializeField] private Transform vehicleVisual;

    [Header("Ground Check")]
    [SerializeField] private float groundCheckDistance = 0.5f;
    [SerializeField] private LayerMask groundMask;

    [Networked] public bool IsActive { get; private set; }
    [Networked] public float CurrentSpeed { get; private set; }

    private Rigidbody _rb;
    private PlayerController _playerController;
    private float _currentTurnAngle;
    private bool _isGrounded;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _playerController = GetComponent<PlayerController>();

        if (_rb == null)
        {
            Debug.LogWarning("[VehicleController] Rigidbody not found. Vehicle physics will not work correctly.");
        }
    }

    public override void Spawned()
    {
        // Mặc định tắt vehicle mode
        SetVehicleActive(false);
    }

    public override void FixedUpdateNetwork()
    {
        if (!IsActive) return;

        if (GetInput(out PlayerInputData input))
        {
            HandleVehicleMovement(input);
        }

        CheckGround();
    }

    private void HandleVehicleMovement(PlayerInputData input)
    {
        float throttle = input.MoveDirection.y;
        float steering = input.MoveDirection.x;

        // Acceleration / Brake
        if (throttle > 0)
        {
            CurrentSpeed = Mathf.MoveTowards(CurrentSpeed, maxSpeed, acceleration * Runner.DeltaTime);
        }
        else if (throttle < 0)
        {
            CurrentSpeed = Mathf.MoveTowards(CurrentSpeed, -maxSpeed * 0.5f, brakeForce * Runner.DeltaTime);
        }
        else
        {
            // Slow down when no input
            CurrentSpeed = Mathf.MoveTowards(CurrentSpeed, 0, brakeForce * 0.5f * Runner.DeltaTime);
        }

        // Steering (chỉ khi đang di chuyển)
        if (Mathf.Abs(CurrentSpeed) > 0.1f)
        {
            float turnAmount = steering * turnSpeed * Runner.DeltaTime * Mathf.Sign(CurrentSpeed);
            transform.Rotate(0, turnAmount, 0);
        }

        // Apply movement
        Vector3 moveDirection = transform.forward * CurrentSpeed;
        
        if (_rb != null)
        {
            // Sử dụng Rigidbody cho physics
            Vector3 newVelocity = moveDirection;
            newVelocity.y = _rb.linearVelocity.y; // Giữ gravity
            _rb.linearVelocity = newVelocity;
        }
        else
        {
            // Fallback: direct transform movement
            transform.position += moveDirection * Runner.DeltaTime;
        }

        // Visual tilt khi rẽ
        if (vehicleVisual != null)
        {
            float targetTilt = -steering * 15f;
            _currentTurnAngle = Mathf.Lerp(_currentTurnAngle, targetTilt, 5f * Runner.DeltaTime);
            vehicleVisual.localRotation = Quaternion.Euler(0, 0, _currentTurnAngle);
        }
    }

    private void CheckGround()
    {
        _isGrounded = Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, 
            groundCheckDistance + 0.1f, groundMask);
    }

    /// <summary>
    /// Bật/tắt vehicle mode
    /// </summary>
    public void SetVehicleActive(bool active)
    {
        if (!HasStateAuthority) return;

        IsActive = active;
        
        // Bật/tắt model xe
        if (vehicleModel != null)
        {
            vehicleModel.SetActive(active);
        }

        // Tắt PlayerController khi dùng xe
        if (_playerController != null)
        {
            _playerController.SetMovementEnabled(!active);
        }

        // Reset speed khi tắt
        if (!active)
        {
            CurrentSpeed = 0;
            if (_rb != null)
            {
                _rb.linearVelocity = Vector3.zero;
            }
        }

        Debug.Log($"[VehicleController] Vehicle mode: {(active ? "ON" : "OFF")}");
    }

    /// <summary>
    /// Gọi từ minigame để bật vehicle mode
    /// </summary>
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_SetVehicleActive(bool active)
    {
        SetVehicleActive(active);
    }

    public bool IsVehicleGrounded() => _isGrounded;

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = _isGrounded ? Color.green : Color.red;
        Gizmos.DrawLine(transform.position + Vector3.up * 0.1f, 
            transform.position + Vector3.down * groundCheckDistance);
    }
}
