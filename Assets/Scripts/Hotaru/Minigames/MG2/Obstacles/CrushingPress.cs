using UnityEngine;
using Fusion;

/// <summary>
/// C10 — Máy ép (Crushing Press).
/// Tường A đứng yên, tường B di chuyển về phía A để ép player.
/// Có cảnh báo âm thanh 1s trước khi ép.
/// Khi player chạm wallB: teleport về checkpoint gần nhất.
///
/// Setup prefab:
///   - Object gốc: trung tâm + NetworkObject
///   - wallA: tường đứng yên (Collider thường, không trigger)
///   - wallB: tường di chuyển:
///       + Collider isTrigger = true
///       + Thêm ObstacleTriggerRelay → kéo root (CrushingPress) vào field 'obstacle'
///   - Gắn component này lên object GỐC
/// </summary>
public class CrushingPress : BaseObstacle
{
    [Header("Press Parts")]
    [SerializeField] private Transform wallB;               // tường di chuyển
    [SerializeField] private Vector3 openLocalPos;          // vị trí mở
    [SerializeField] private Vector3 closeLocalPos;         // vị trí đóng (gần tường A)
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private float returnSpeed = 6f;

    [Header("Timing")]
    [SerializeField] private float openDuration = 3f;       // thời gian ở trạng thái mở
    [SerializeField] private float closeDuration = 1.5f;    // thời gian ở trạng thái đóng
    [SerializeField] private float warningDuration = 1f;    // cảnh báo trước khi ép

    [Header("Crush Effect")]
    [SerializeField] private float knockbackForce = 5f;
    [SerializeField] private float knockbackDuration = 0.2f;

    [Header("Warning Visual")]
    [SerializeField] private Renderer wallBRenderer;
    [SerializeField] private Color warningColor = Color.yellow;
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color crushingColor = Color.red;

    private enum PressState { Open, Warning, Closing, Closed, Opening }
    private PressState _state = PressState.Open;
    private float _stateTimer;

    private void Start()
    {
        _stateTimer = openDuration;
        if (wallB != null) wallB.localPosition = openLocalPos;
        SetWallColor(normalColor);
    }

    private void Update()
    {
        _stateTimer -= Time.deltaTime;

        switch (_state)
        {
            case PressState.Open:
                if (_stateTimer <= 0f)
                    EnterWarning();
                break;

            case PressState.Warning:
                float t = Mathf.PingPong(Time.time * 5f, 1f);
                SetWallColor(Color.Lerp(normalColor, warningColor, t));

                if (_stateTimer <= 0f)
                    EnterClosing();
                break;

            case PressState.Closing:
                if (wallB != null)
                {
                    wallB.localPosition = Vector3.MoveTowards(
                        wallB.localPosition, closeLocalPos, moveSpeed * Time.deltaTime);

                    if (Vector3.Distance(wallB.localPosition, closeLocalPos) < 0.05f)
                    {
                        wallB.localPosition = closeLocalPos;
                        EnterClosed();
                    }
                }
                break;

            case PressState.Closed:
                if (_stateTimer <= 0f)
                    EnterOpening();
                break;

            case PressState.Opening:
                if (wallB != null)
                {
                    wallB.localPosition = Vector3.MoveTowards(
                        wallB.localPosition, openLocalPos, returnSpeed * Time.deltaTime);

                    if (Vector3.Distance(wallB.localPosition, openLocalPos) < 0.05f)
                    {
                        wallB.localPosition = openLocalPos;
                        EnterOpen();
                    }
                }
                break;
        }
    }

    private void EnterWarning()
    {
        _state = PressState.Warning;
        _stateTimer = warningDuration;
        Debug.Log("[CrushingPress] WARNING!");
    }

    private void EnterClosing()
    {
        _state = PressState.Closing;
        SetWallColor(crushingColor);
        Debug.Log("[CrushingPress] CLOSING");
    }

    private void EnterClosed()
    {
        _state = PressState.Closed;
        _stateTimer = closeDuration;
        Debug.Log("[CrushingPress] CLOSED");
    }

    private void EnterOpening()
    {
        _state = PressState.Opening;
        SetWallColor(normalColor);
    }

    private void EnterOpen()
    {
        _state = PressState.Open;
        _stateTimer = openDuration;
    }

    protected override void HandleHit(PlayerController player)
    {
        // Chỉ gây effect khi đang đóng hoặc closed
        if (_state != PressState.Closing && _state != PressState.Closed) return;
        base.HandleHit(player);
    }

    protected override void ApplyEffect(PlayerController player)
    {
        // Teleport player về checkpoint gần nhất
        TeleportToLastCheckpoint(player);

        // Knockback nhỏ ra khỏi tường để tránh bị kẹt
        Vector3 escape = (player.transform.position - wallB.position).normalized;
        escape.y = 0f;
        player.ApplyExternalForce(escape * knockbackForce, knockbackDuration);

        Debug.Log($"[CrushingPress] CRUSHED {player.Object.InputAuthority} → teleported to checkpoint");
    }

    private void TeleportToLastCheckpoint(PlayerController player)
    {
        var mgData = GetMinigameData(player);
        if (mgData == null) return;

        // Dùng vị trí checkpoint đã lưu trong PlayerMinigameData
        if (player.HasStateAuthority)
        {
            var cc = player.GetComponent<CharacterController>();
            if (cc != null)
            {
                cc.enabled = false;
                player.transform.position = mgData.CurrentRespawnPosition;
                cc.enabled = true;
            }
            else
            {
                player.transform.position = mgData.CurrentRespawnPosition;
            }
        }
    }

    private void SetWallColor(Color color)
    {
        if (wallBRenderer != null)
            wallBRenderer.material.color = color;
    }
}
