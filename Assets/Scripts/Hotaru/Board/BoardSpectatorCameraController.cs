using UnityEngine;

public class BoardSpectatorCameraController : MonoBehaviour
{
    [Header("Dedicated Follow Camera")]
    [SerializeField] private Camera followCamera;
    [SerializeField] private Camera mainCamera;
    [SerializeField] private KeyCode toggleKey = KeyCode.R;

    [Header("View Settings")]
    [SerializeField] private float cameraHeight = 12f;
    [SerializeField] private float overheadAngle = 50f;
    [SerializeField] private float cameraDistance = 8f;
    [SerializeField] private float cameraZOffset = 10f;
    [SerializeField] private float panSpeed = 3f;

    [Header("Free Move")]
    [SerializeField] private float freeMoveSpeed = 8f;
    [SerializeField] private float maxX = 40f;
    [SerializeField] private float minX = -40f;
    [SerializeField] private float maxZ = 40f;
    [SerializeField] private float minZ = -40f;
    [SerializeField] private bool showGizmo = true;

    private bool _isActive;
    private BoardManager _boardManager;
    private bool _introActive;
    private Transform _currentTarget;
    private Vector3 _desiredPosition;
    private Vector3 _currentVelocity;
    private Vector3 _initialPosition;
    private Quaternion _initialRotation;
    private bool _freeLookEngaged;

    private void Awake()
    {
        _boardManager = FindFirstObjectByType<BoardManager>();
        mainCamera = Camera.main;

        if (followCamera == null)
        {
            followCamera = GetComponent<Camera>();
        }

        if (followCamera == null)
        {
            GameObject cameraGo = new GameObject("BoardSpectatorCamera");
            cameraGo.transform.SetParent(transform, false);
            followCamera = cameraGo.AddComponent<Camera>();
            followCamera.clearFlags = CameraClearFlags.SolidColor;
            followCamera.backgroundColor = Color.black;
            followCamera.enabled = false;
            followCamera.depth = 10;
        }

        if (followCamera != null)
        {
            followCamera.transform.position = GetInitialCameraPosition();
            followCamera.transform.rotation = Quaternion.Euler(overheadAngle, 180f, 0f);
            followCamera.enabled = false;
        }

        if (followCamera != null)
        {
            _initialPosition = followCamera.transform.position;
            _initialRotation = followCamera.transform.rotation;
        }

        if (mainCamera == null)
            mainCamera = followCamera;

        if (_boardManager != null)
            _boardManager.OnTurnStarted += OnTurnStarted;
    }

    private void OnDestroy()
    {
        if (_boardManager != null)
            _boardManager.OnTurnStarted -= OnTurnStarted;
    }

    private void Start()
    {
        UpdateTargetFromBoard();

        if (followCamera != null)
        {
            if (_currentTarget != null)
            {
                followCamera.transform.position = CalculatePosition(_currentTarget.position);
            }
            else
            {
                followCamera.transform.position = GetInitialCameraPosition();
            }

            followCamera.transform.rotation = Quaternion.Euler(overheadAngle, 180f, 0f);
        }
    }
    private Vector3 ClampToAllowedArea(Vector3 pos)
    {
        pos.x = Mathf.Clamp(pos.x, transform.position.x + minX, transform.position.x + maxX);
        pos.z = Mathf.Clamp(pos.z, transform.position.z + minZ, transform.position.z + maxZ);
        return pos;
    }

    private void Update()
    {
        if (_introActive) return;

        if (Input.GetKeyDown(toggleKey))
        {
            Toggle();
            return;
        }

        if (!_isActive || followCamera == null) return;

        bool movedThisFrame = HandleFreeMove();
        if (movedThisFrame)
            _freeLookEngaged = true;   // một khi đã tự lái, không auto-follow nữa

        if (_currentTarget == null)
            UpdateTargetFromBoard();

        if (!_freeLookEngaged && _currentTarget != null)
        {
            _desiredPosition = ClampToAllowedArea(CalculatePosition(_currentTarget.position));
            followCamera.transform.position = Vector3.SmoothDamp(
                followCamera.transform.position,
                _desiredPosition,
                ref _currentVelocity,
                1f / panSpeed);
        }

        followCamera.transform.rotation = Quaternion.Euler(overheadAngle, 180f, 0f);
    }

    private void OnTurnStarted(int playerId)
    {
        if (!_isActive) return;
        _freeLookEngaged = false;
        UpdateTargetFromBoard(playerId);
    }

    public void Toggle()
    {
        SetActive(!_isActive);
    }

    public void SetActive(bool active)
    {
        _isActive = active;
        _freeLookEngaged = false;

        if (followCamera != null)
        {
            followCamera.enabled = active;
            followCamera.depth = 5;
        }

        if (mainCamera != null && mainCamera != followCamera)
        {
            mainCamera.enabled = !active;
            mainCamera.depth = 0;
        }

        if (active)
        {
            UpdateTargetFromBoard();
            if (_currentTarget != null)
            {
                followCamera.transform.position = ClampToAllowedArea(CalculatePosition(_currentTarget.position));
                followCamera.transform.rotation = Quaternion.Euler(overheadAngle, 180f, 0f);
            }
        }
        else
        {
            if (followCamera != null)
            {
                followCamera.transform.position = _initialPosition;
                followCamera.transform.rotation = _initialRotation;
            }
        }
    }

    public void SetIntroActive(bool active)
    {
        _introActive = active;

        if (!active)
        {
            if (followCamera != null)
                followCamera.enabled = _isActive;

            if (mainCamera != null && mainCamera != followCamera)
                mainCamera.enabled = !_isActive;
        }
        else
        {
            if (followCamera != null)
                followCamera.enabled = false;

            if (mainCamera != null && mainCamera != followCamera)
                mainCamera.enabled = true;
        }
    }

    private bool HandleFreeMove()
    {
        if (followCamera == null)
            return false;

        Vector3 move = Vector3.zero;
        bool hasInput = false;

        if (Input.GetKey(KeyCode.W)) { move += Vector3.forward; hasInput = true; }
        if (Input.GetKey(KeyCode.S)) { move += Vector3.back; hasInput = true; }
        if (Input.GetKey(KeyCode.A)) { move += Vector3.left; hasInput = true; }
        if (Input.GetKey(KeyCode.D)) { move += Vector3.right; hasInput = true; }

        if (!hasInput)
            return false;

        move.Normalize();
        Vector3 worldMove = followCamera.transform.TransformDirection(move);
        worldMove.y = 0f;
        if (worldMove.sqrMagnitude < 0.001f)
            return false;

        worldMove.Normalize();
        Vector3 newPos = followCamera.transform.position + worldMove * freeMoveSpeed * Time.deltaTime;
        newPos = ClampToAllowedArea(newPos); // dùng chung hàm clamp
        followCamera.transform.position = newPos;
        return true;
    }

    private void UpdateTargetFromBoard(int? playerId = null)
    {
        if (_boardManager != null && _boardManager.Object != null && _boardManager.Object.IsValid)
        {
            int targetPlayerId = playerId ?? _boardManager.CurrentPlayerID;
            _currentTarget = FindTokenByPlayerId(targetPlayerId)?.transform;
            return;
        }

        _currentTarget = null;
    }

    private BoardPlayerToken FindTokenByPlayerId(int playerId)
    {
        if (playerId < 0)
            return null;

        var allTokens = FindObjectsByType<BoardPlayerToken>(FindObjectsSortMode.None);
        foreach (var token in allTokens)
        {
            if (token.ownerPlayerId == playerId)
                return token;
        }

        return null;
    }

    private Vector3 CalculatePosition(Vector3 targetWorldPos)
    {
        return targetWorldPos + new Vector3(
            0f,
            cameraHeight,
            -cameraDistance * Mathf.Cos(overheadAngle * Mathf.Deg2Rad) + cameraZOffset
        );
    }

    private Vector3 GetInitialCameraPosition()
    {
        Vector3 fallback = transform.position + new Vector3(0f, cameraHeight, cameraZOffset);
        if (fallback == Vector3.zero)
        {
            fallback = new Vector3(0f, cameraHeight, cameraZOffset);
        }

        return fallback;
    }

    private void OnDrawGizmos()
    {
        if (!showGizmo)
            return;

        Gizmos.color = new Color(0f, 1f, 0.6f, 0.25f);
        Vector3 center = transform.position;
        Vector3 size = new Vector3(maxX - minX, 0f, maxZ - minZ);
        Gizmos.DrawCube(center, new Vector3(size.x, 0.01f, size.z));

        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(center, new Vector3(size.x, 0.02f, size.z));
    }
}
