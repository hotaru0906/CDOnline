using UnityEngine;
using System.Collections;

/// <summary>
/// Điều khiển camera trong Board phase.
/// Overhead camera, smooth pan đến token đang active.
///
/// SETUP trong BoardScene:
///   1. Tạo GameObject "BoardCameraController" trong scene
///   2. Attach component này
///   3. Assign mainCamera (hoặc để auto-find Camera.main)
///   4. Góc nhìn: overhead 45° hoặc tùy chỉnh qua overheadAngle + cameraHeight
///
/// BoardManager gọi:
///   - FocusOnSlot(slot)         → pan đến token của slot đó
///   - FocusOnPlayer(playerId)   → pan đến token của player đó  
///   - FocusOnTarget(target, onComplete) → pan đến target, callback khi xong
///   - ReturnToPreviousFocus()   → quay về token trước đó
/// </summary>
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
    [Tooltip("Tốc độ smooth pan (cao hơn = nhanh hơn)")]
    [SerializeField] private float panSpeed = 3f;
    [Tooltip("Ngưỡng distance để coi là đã đến đích (dừng lerp)")]
    [SerializeField] private float snapThreshold = 0.05f;

    [Header("Timing")]
    [Tooltip("Thời gian giữ camera tại target trước khi callback (giây)")]
    [SerializeField] private float holdDuration = 0.8f;

    // =====================================================================
    // RUNTIME STATE
    // =====================================================================

    private Vector3 _desiredPosition;   // vị trí camera muốn đến
    private Vector3 _currentVelocity;   // dùng cho SmoothDamp

    private Transform _currentFocusToken;   // token đang focus
    private Transform _previousFocusToken;  // token trước đó (để ReturnToPrevious)

    private Coroutine _focusRoutine;

    // =====================================================================
    // LIFECYCLE
    // =====================================================================

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (boardCamera == null)
            boardCamera = Camera.main;
    }

    private void Start()
    {
        if (CameraManager.Instance != null)
            CameraManager.Instance.SwitchToMinigameCamera();

        if (boardCamera != null)
        {
            boardCamera.transform.rotation = Quaternion.Euler(overheadAngle, 180f, 0f); // ← thêm 180f
            _desiredPosition = boardCamera.transform.position;
        }
    }

    private void LateUpdate()
    {
        if (boardCamera == null || _currentFocusToken == null) return;

        // Tính desired position theo góc overhead
        _desiredPosition = CalcCameraPosition(_currentFocusToken.position);

        // Smooth pan
        boardCamera.transform.position = Vector3.SmoothDamp(
            boardCamera.transform.position,
            _desiredPosition,
            ref _currentVelocity,
            1f / panSpeed
        );

        // Giữ rotation cố định
        // Giữ rotation cố định
        boardCamera.transform.rotation = Quaternion.Euler(overheadAngle, 180f, 0f); // ← thêm 180f    
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // =====================================================================
    // PUBLIC API — gọi từ BoardManager
    // =====================================================================

    /// <summary>
    /// Pan camera đến token của slot chỉ định.
    /// Dùng khi bắt đầu lượt (StartTurn).
    /// </summary>
    public void FocusOnSlot(int slot)
    {
        var token = FindTokenBySlot(slot);
        if (token == null) return;

        SetFocus(token.transform);
    }

    /// <summary>
    /// Pan camera đến token của playerId chỉ định.
    /// </summary>
    public void FocusOnPlayer(int playerId)
    {
        var token = FindTokenByPlayerId(playerId);
        if (token == null) return;

        SetFocus(token.transform);
    }

    /// <summary>
    /// Pan camera đến target, giữ holdDuration giây, rồi gọi onComplete.
    /// Dùng cho Steal/PushBack: pan sang target → chờ animation xong → callback.
    /// onComplete: thường là ReturnToPreviousFocus() hoặc logic tiếp theo.
    /// </summary>
    public void FocusOnTarget(Transform target, System.Action onComplete = null)
    {
        if (_focusRoutine != null) StopCoroutine(_focusRoutine);
        _focusRoutine = StartCoroutine(FocusAndHoldRoutine(target, onComplete));
    }

    /// <summary>
    /// Overload tiện lợi: focus theo playerId.
    /// </summary>
    public void FocusOnTarget(int playerId, System.Action onComplete = null)
    {
        var token = FindTokenByPlayerId(playerId);
        if (token == null) { onComplete?.Invoke(); return; }
        FocusOnTarget(token.transform, onComplete);
    }

    /// <summary>
    /// Quay camera về token trước đó (active player).
    /// Gọi sau khi animation target xong.
    /// </summary>
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

    private IEnumerator FocusAndHoldRoutine(Transform target, System.Action onComplete)
    {
        // Lưu focus hiện tại rồi pan sang target
        _previousFocusToken = _currentFocusToken;
        _currentFocusToken = target;

        // Chờ camera pan đến gần target (hoặc timeout 2s)
        float timeout = 2f;
        float elapsed = 0f;

        while (elapsed < timeout)
        {
            float dist = Vector3.Distance(
                boardCamera.transform.position,
                CalcCameraPosition(target.position)
            );

            if (dist < snapThreshold) break;

            elapsed += Time.deltaTime;
            yield return null;
        }

        // Giữ camera tại target trong holdDuration
        yield return new WaitForSeconds(holdDuration);

        onComplete?.Invoke();
        _focusRoutine = null;
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