using Fusion;
using UnityEngine;

public class PopupSpikeTrap : BaseObstacle
{
    [Header("Spike Part")]
    [SerializeField] private Transform spikePart;
    [SerializeField] private Collider spikeCollider;
    [SerializeField] private Vector3 hiddenLocalPos;
    [SerializeField] private Vector3 activeLocalPos;
    [SerializeField] private float riseSpeed = 12f;
    [SerializeField] private float retractSpeed = 8f;

    [Header("Timing")]
    [SerializeField] private float activeDuration = 1.5f;
    [SerializeField] private float cooldownDuration = 2.5f;

    [Header("Visual")]
    [SerializeField] private Renderer spikeRenderer;
    [SerializeField] private Color activeColor = Color.red;
    [SerializeField] private Color hiddenColor = Color.gray;

    private enum TrapState : byte
    {
        Hidden,
        Rising,
        Active,
        Retracting
    }

    [Networked] private TrapState _state { get; set; }
    [Networked] private float _timer { get; set; }

    public override void Spawned()
    {
        if (Object.HasStateAuthority)
        {
            _state = TrapState.Hidden;
            _timer = cooldownDuration;
        }

        ApplyVisual(hiddenLocalPos, hiddenColor, false);
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority)
            return;

        _timer -= Runner.DeltaTime;

        switch (_state)
        {
            case TrapState.Hidden:
                if (_timer <= 0f)
                {
                    _state = TrapState.Rising;
                }
                break;

            case TrapState.Rising:
                if (MoveSpike(activeLocalPos, riseSpeed))
                {
                    _state = TrapState.Active;
                    _timer = activeDuration;
                }
                break;

            case TrapState.Active:
                if (_timer <= 0f)
                {
                    _state = TrapState.Retracting;
                }
                break;

            case TrapState.Retracting:
                if (MoveSpike(hiddenLocalPos, retractSpeed))
                {
                    _state = TrapState.Hidden;
                    _timer = cooldownDuration;
                }
                break;
        }
    }

    // ===================== VISUAL =====================

    public override void Render()
    {
        switch (_state)
        {
            case TrapState.Hidden:
                spikePart.localPosition =
                    Vector3.MoveTowards(
                        spikePart.localPosition,
                        hiddenLocalPos,
                        retractSpeed * Time.deltaTime);
                break;

            case TrapState.Rising:
            case TrapState.Active:
                spikePart.localPosition =
                    Vector3.MoveTowards(
                        spikePart.localPosition,
                        activeLocalPos,
                        riseSpeed * Time.deltaTime);
                break;

            case TrapState.Retracting:
                spikePart.localPosition =
                    Vector3.MoveTowards(
                        spikePart.localPosition,
                        hiddenLocalPos,
                        retractSpeed * Time.deltaTime);
                break;
        }

    spikeCollider.enabled = (_state == TrapState.Active);
}

    private bool MoveSpike(Vector3 target, float speed)
    {
        spikePart.localPosition = Vector3.MoveTowards(
            spikePart.localPosition,
            target,
            speed * Runner.DeltaTime
        );

        return Vector3.Distance(spikePart.localPosition, target) < 0.01f;
    }

    private void SetSpikeColor(Color color)
    {
        if (spikeRenderer == null) return;

        spikeRenderer.material.color = color;
    }

    private void ApplyVisual(Vector3 pos, Color color, bool active)
    {
        spikePart.localPosition = pos;
        spikeCollider.enabled = active;
        SetSpikeColor(color);
    }

    // ===================== DAMAGE =====================

    protected override void HandleHit(PlayerController player)
    {
        if (_state != TrapState.Active)
            return;

        if (!Object.HasStateAuthority)
            return;

        base.HandleHit(player);
    }

    protected override void ApplyEffect(PlayerController player)
    {
        var mgData = GetMinigameData(player);
        if (mgData != null && mgData.CanTakeDamage())
            mgData.Die();

        Debug.Log($"[PopupSpike Fusion] Killed {player.Object.InputAuthority}");
    }
}