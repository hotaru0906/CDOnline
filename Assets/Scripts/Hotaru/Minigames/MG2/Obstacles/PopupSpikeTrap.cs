using UnityEngine;

/// <summary>
/// C3 — Bẫy gai popup (Popup Spike Trap).
/// Chu kỳ: Ẩn → Cảnh báo (nhấp nháy) → Hiện ra (damage zone) → Ẩn lại.
/// Khi player chạm vào: player die → respawn tại checkpoint.
///
/// Setup prefab:
///   - Object gốc: base đứng yên + NetworkObject
///   - spikePart: con có Collider isTrigger
///     + Thêm ObstacleTriggerRelay → kéo root vào field 'obstacle'
///   - Gắn component này lên object GỐC
/// </summary>
public class PopupSpikeTrap : BaseObstacle
{
    [Header("Spike Part")]
    [SerializeField] private Transform spikePart;       // phần gai di chuyển lên/xuống
    [SerializeField] private Collider spikeCollider;    // Collider của spikePart
    [SerializeField] private Vector3 hiddenLocalPos;    // vị trí ẩn (local)
    [SerializeField] private Vector3 activeLocalPos;    // vị trí hiện ra (local)
    [SerializeField] private float riseSpeed = 12f;
    [SerializeField] private float retractSpeed = 8f;

    [Header("Timing")]
    [SerializeField] private float activeDuration = 1.5f;
    [SerializeField] private float cooldownDuration = 2.5f;
    [SerializeField] private float warningDuration = 0.5f;

    [Header("Warning Visual")]
    [SerializeField] private Renderer spikeRenderer;
    [SerializeField] private Color warningColor = Color.yellow;
    [SerializeField] private Color activeColor = Color.red;
    [SerializeField] private Color hiddenColor = Color.gray;

    private enum TrapState { Hidden, Warning, Rising, Active, Retracting }
    private TrapState _state = TrapState.Hidden;
    private float _stateTimer;

    private void Start()
    {
        _stateTimer = cooldownDuration;

        if (spikePart != null)
            spikePart.localPosition = hiddenLocalPos;

        if (spikeCollider != null)
            spikeCollider.enabled = false;

        SetSpikeColor(hiddenColor);
    }

    private void Update()
    {
        _stateTimer -= Time.deltaTime;

        switch (_state)
        {
            case TrapState.Hidden:
                if (_stateTimer <= 0f)
                {
                    _state = TrapState.Warning;
                    _stateTimer = warningDuration;
                    Debug.Log("[PopupSpike] WARNING");
                }
                break;

            case TrapState.Warning:
                // Nhấp nháy vàng
                float t = Mathf.PingPong(Time.time * 8f, 1f);
                SetSpikeColor(Color.Lerp(hiddenColor, warningColor, t));

                if (_stateTimer <= 0f)
                {
                    _state = TrapState.Rising;
                    if (spikeCollider != null) spikeCollider.enabled = false;
                }
                break;

            case TrapState.Rising:
                if (spikePart != null)
                {
                    spikePart.localPosition = Vector3.MoveTowards(
                        spikePart.localPosition, activeLocalPos, riseSpeed * Time.deltaTime);

                    if (Vector3.Distance(spikePart.localPosition, activeLocalPos) < 0.01f)
                    {
                        spikePart.localPosition = activeLocalPos;
                        _state = TrapState.Active;
                        _stateTimer = activeDuration;

                        if (spikeCollider != null) spikeCollider.enabled = true;
                        SetSpikeColor(activeColor);
                        Debug.Log("[PopupSpike] ACTIVE");
                    }
                }
                break;

            case TrapState.Active:
                if (_stateTimer <= 0f)
                {
                    _state = TrapState.Retracting;
                    if (spikeCollider != null) spikeCollider.enabled = false;
                }
                break;

            case TrapState.Retracting:
                if (spikePart != null)
                {
                    spikePart.localPosition = Vector3.MoveTowards(
                        spikePart.localPosition, hiddenLocalPos, retractSpeed * Time.deltaTime);

                    if (Vector3.Distance(spikePart.localPosition, hiddenLocalPos) < 0.01f)
                    {
                        spikePart.localPosition = hiddenLocalPos;
                        _state = TrapState.Hidden;
                        _stateTimer = cooldownDuration;
                        SetSpikeColor(hiddenColor);
                    }
                }
                break;
        }
    }

    // spikeCollider ở child spikePart → cần ObstacleTriggerRelay trên spikePart
    //   hoặc đặt Collider ngay trên root để BaseObstacle.OnTriggerEnter hoạt động.

    protected override void HandleHit(PlayerController player)
    {
        // Chỉ hit khi active
        if (_state != TrapState.Active) return;
        base.HandleHit(player);
    }

    protected override void ApplyEffect(PlayerController player)
    {
        // Cho player die → respawn tại checkpoint
        var mgData = GetMinigameData(player);
        if (mgData != null && mgData.CanTakeDamage())
            mgData.Die();

        Debug.Log($"[PopupSpike] Killed {player.Object.InputAuthority} → respawn");
    }

    private void SetSpikeColor(Color color)
    {
        if (spikeRenderer != null)
            spikeRenderer.material.color = color;
    }
}
