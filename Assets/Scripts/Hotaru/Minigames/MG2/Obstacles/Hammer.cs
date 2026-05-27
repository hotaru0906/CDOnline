using UnityEngine;

/// <summary>
/// C8 — Búa tạ (Hammer).
/// Vung theo chu kỳ, có cảnh báo trước khi đánh.
/// Khi player chạm vào vùng swing: văng mạnh theo hướng búa.
///
/// Setup prefab:
///   - hammerHead: GameObject có Collider isTrigger (đầu búa)
///   - Gắn component này lên parent của hammerHead
///   - Búa xoay quanh trục Z của parent (hoặc trục tùy chọn)
/// </summary>
public class Hammer : BaseObstacle
{
    [Header("Hammer — Force")]
    [SerializeField] private float hammerForce = 35f;
    [SerializeField] private float upForce = 10f;
    [SerializeField] private float knockbackDuration = 0.8f;

    [Header("Hammer — Swing")]
    [SerializeField] private Transform hammerHead;          // phần đầu búa (có Collider trigger)
    [SerializeField] private float swingInterval = 2.5f;   // giây giữa 2 lần vung
    [SerializeField] private float warningDuration = 0.6f; // thời gian cảnh báo trước khi vung
    [SerializeField] private float swingAngle = 120f;      // góc vung (độ)
    [SerializeField] private float swingSpeed = 300f;      // tốc độ vung (độ/giây)
    [SerializeField] private Vector3 swingAxis = Vector3.right; // trục vung

    [Header("Warning Visual")]
    [SerializeField] private Renderer warningRenderer;     // vật liệu đổi màu khi cảnh báo
    [SerializeField] private Color warningColor = Color.red;
    [SerializeField] private Color normalColor = Color.white;

    private enum HammerState { Idle, Warning, Swinging, Returning }

    private HammerState _state = HammerState.Idle;
    private float _stateTimer;
    private float _currentAngle;
    private float _targetAngle;
    private Quaternion _restRotation;
    private bool _canHit; // chỉ cho phép hit khi đang Swinging

    private void Start()
    {
        _stateTimer = swingInterval;

        if (hammerHead != null)
            _restRotation = hammerHead.localRotation;
    }

    private void Update()
    {
        _stateTimer -= Time.deltaTime;

        switch (_state)
        {
            case HammerState.Idle:
                if (_stateTimer <= 0f)
                {
                    EnterWarning();
                }
                break;

            case HammerState.Warning:
                // Nhấp nháy warning
                float blink = Mathf.PingPong(Time.time * 6f, 1f);
                SetWarningColor(Color.Lerp(normalColor, warningColor, blink));

                if (_stateTimer <= 0f)
                    EnterSwinging();
                break;

            case HammerState.Swinging:
                _currentAngle = Mathf.MoveTowards(_currentAngle, _targetAngle, swingSpeed * Time.deltaTime);

                if (hammerHead != null)
                    hammerHead.localRotation = _restRotation * Quaternion.AngleAxis(_currentAngle, swingAxis);

                if (Mathf.Abs(_currentAngle - _targetAngle) < 1f)
                    EnterReturning();
                break;

            case HammerState.Returning:
                _currentAngle = Mathf.MoveTowards(_currentAngle, 0f, swingSpeed * 0.5f * Time.deltaTime);

                if (hammerHead != null)
                    hammerHead.localRotation = _restRotation * Quaternion.AngleAxis(_currentAngle, swingAxis);

                if (Mathf.Abs(_currentAngle) < 1f)
                    EnterIdle();
                break;
        }
    }

    private void EnterWarning()
    {
        _state = HammerState.Warning;
        _stateTimer = warningDuration;
        _canHit = false;
        SetWarningColor(warningColor);
        Debug.Log("[Hammer] WARNING!");
    }

    private void EnterSwinging()
    {
        _state = HammerState.Swinging;
        _targetAngle = swingAngle;
        _canHit = true;
        SetWarningColor(normalColor);
        Debug.Log("[Hammer] SWINGING!");
    }

    private void EnterReturning()
    {
        _state = HammerState.Returning;
        _canHit = false;
    }

    private void EnterIdle()
    {
        _state = HammerState.Idle;
        _stateTimer = swingInterval;
        _currentAngle = 0f;

        if (hammerHead != null)
            hammerHead.localRotation = _restRotation;
    }

    private void SetWarningColor(Color color)
    {
        if (warningRenderer != null)
            warningRenderer.material.color = color;
    }

    protected override void HandleHit(PlayerController player)
    {
        if (!_canHit) return;
        base.HandleHit(player);
    }

    protected override void ApplyEffect(PlayerController player)
    {
        // Hướng đánh: từ gốc búa → player (ngang) + lên trên
        Vector3 toPlayer = (player.transform.position - transform.position).normalized;
        toPlayer.y = 0f;

        Vector3 force = toPlayer * hammerForce + Vector3.up * upForce;

        player.ApplyExternalForce(force, knockbackDuration, overrideInput: true);

        Debug.Log($"[Hammer] SMASHED {player.Object.InputAuthority} — force: {force.magnitude:F1}");
    }
}
