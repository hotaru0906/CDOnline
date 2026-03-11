using Fusion;
using UnityEngine;

public enum PlayerState
{
    Idle,
    Moving,
    Jumping,
    Falling
}

[RequireComponent(typeof(NetworkCharacterController))]
public class PlayerController : NetworkBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float rotationSpeed = 15f;

    [Networked] public PlayerState CurrentState { get; private set; }

    public Vector3 Velocity => _networkCC != null ? _networkCC.Velocity : Vector3.zero;

    private Vector3 _targetMoveDirection;

    private NetworkCharacterController _networkCC;
    private Transform _cameraTransform;

    private void Awake()
    {
        _networkCC = GetComponent<NetworkCharacterController>();
    }

    public override void Spawned()
    {
        Debug.Log($"[PlayerController] Spawned. HasInputAuthority: {HasInputAuthority}, HasStateAuthority: {HasStateAuthority}");

        if (HasInputAuthority)
        {
            _cameraTransform = Camera.main?.transform;
            Debug.Log("[PlayerController] Local player spawned");
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (GetInput(out PlayerInputData input))
        {
            Move(input);
        }

        UpdateState();
    }

    private void Move(PlayerInputData input)
    {
        Vector3 moveDirection = CalculateMoveDirection(input.MoveDirection);

        if (input.IsButtonPressed(PlayerInputData.BUTTON_JUMP))
        {
            _networkCC.Jump();
        }

        _networkCC.Move(moveDirection);

        _targetMoveDirection = moveDirection;
    }

    private Vector3 CalculateMoveDirection(Vector2 input)
    {
        if (input.sqrMagnitude < 0.01f)
            return Vector3.zero;

        Vector3 forward;
        Vector3 right;

        if (_cameraTransform != null)
        {
            forward = _cameraTransform.forward;
            right = _cameraTransform.right;

            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();
        }
        else
        {
            forward = Vector3.forward;
            right = Vector3.right;
        }

        Vector3 moveDir = (forward * input.y + right * input.x);
        return moveDir.normalized;
    }

    private void UpdateState()
    {
        bool isGrounded = _networkCC.Grounded;
        Vector3 velocity = _networkCC.Velocity;

        if (!isGrounded)
        {
            CurrentState = velocity.y > 0 ? PlayerState.Jumping : PlayerState.Falling;
        }
        else if (_targetMoveDirection.sqrMagnitude > 0.01f)
        {
            CurrentState = PlayerState.Moving;
        }
        else
        {
            CurrentState = PlayerState.Idle;
        }
    }

    public override void Render()
    {
        if (_targetMoveDirection.sqrMagnitude > 0.01f)
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
        Vector3 velocity = _networkCC != null ? _networkCC.Velocity : Vector3.zero;
        Vector3 horizontalVelocity = new Vector3(velocity.x, 0, velocity.z);
        return horizontalVelocity.magnitude;
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
        // NetworkCharacterController handles this internally
        // You can disable the component if needed
        if (_networkCC != null)
        {
            _networkCC.enabled = enabled;
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