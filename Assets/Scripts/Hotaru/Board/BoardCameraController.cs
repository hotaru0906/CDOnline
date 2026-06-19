using UnityEngine;

public class BoardCameraController : MonoBehaviour
{
    public static BoardCameraController Instance { get; private set; }

    [Header("Camera")]
    [SerializeField] private Camera boardCamera;

    [Header("Overhead Settings")]
    [SerializeField] private float cameraHeight = 12f;
    [SerializeField] private float overheadAngle = 50f;
    [SerializeField] private float cameraDistance = 8f;
    [SerializeField] private float cameraZOffset = 10f;

    [Header("Pan Settings")]
    [SerializeField] private float panSpeed = 3f;

    private Vector3 _desiredPosition;
    private Vector3 _currentVelocity;
    private Transform _currentFocusToken;
    private Transform _previousFocusToken;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (boardCamera == null) boardCamera = Camera.main;
    }

    private void Start()
    {
        if (CameraManager.Instance != null)
            CameraManager.Instance.SwitchToMinigameCamera();

        if (boardCamera != null)
        {
            boardCamera.transform.rotation = Quaternion.Euler(overheadAngle, 180f, 0f);
            _desiredPosition = boardCamera.transform.position;
        }

        // Hiện cursor suốt trong board
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void LateUpdate()
    {
        if (boardCamera == null || _currentFocusToken == null) return;

        _desiredPosition = CalcCameraPosition(_currentFocusToken.position);

        boardCamera.transform.position = Vector3.SmoothDamp(
            boardCamera.transform.position,
            _desiredPosition,
            ref _currentVelocity,
            1f / panSpeed
        );

        boardCamera.transform.rotation = Quaternion.Euler(overheadAngle, 180f, 0f);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // =====================================================================
    // PUBLIC API
    // =====================================================================

    public void FocusOnSlot(int slot)
    {
        var token = FindTokenBySlot(slot);
        if (token != null) SetFocus(token.transform);
    }

    public void FocusOnPlayer(int playerId)
    {
        var token = FindTokenByPlayerId(playerId);
        if (token != null) SetFocus(token.transform);
    }

    public void FocusOnTarget(Transform target, System.Action onComplete = null)
    {
        if (target == null) return;
        SetFocus(target);
        onComplete?.Invoke();
    }

    public void FocusOnTarget(int playerId, System.Action onComplete = null)
    {
        var token = FindTokenByPlayerId(playerId);
        if (token == null) { onComplete?.Invoke(); return; }
        FocusOnTarget(token.transform, onComplete);
    }

    public void ReturnToPreviousFocus()
    {
        if (_previousFocusToken != null)
            SetFocus(_previousFocusToken);
    }

    // =====================================================================
    // INTERNAL
    // =====================================================================

    private void SetFocus(Transform token)
    {
        if (_currentFocusToken != token)
            _previousFocusToken = _currentFocusToken;
        _currentFocusToken = token;
    }

    private Vector3 CalcCameraPosition(Vector3 targetWorldPos)
    {
        return targetWorldPos + new Vector3(
            0f,
            cameraHeight,
            -cameraDistance * Mathf.Cos(overheadAngle * Mathf.Deg2Rad) + cameraZOffset
        );
    }

    // =====================================================================
    // TOKEN LOOKUP
    // =====================================================================

    private BoardPlayerToken FindTokenBySlot(int slot)
    {
        var all = FindObjectsByType<BoardPlayerToken>(FindObjectsSortMode.None);
        foreach (var t in all)
            if (t.playerSlotIndex == slot) return t;
        return null;
    }

    private BoardPlayerToken FindTokenByPlayerId(int playerId)
    {
        var all = FindObjectsByType<BoardPlayerToken>(FindObjectsSortMode.None);
        foreach (var t in all)
            if (t.ownerPlayerId == playerId) return t;
        return null;
    }
}