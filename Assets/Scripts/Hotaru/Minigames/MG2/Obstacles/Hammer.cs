using UnityEngine;
using Fusion;

public class Hammer : BaseObstacle
{
    [Header("Hammer Swing")]
    [SerializeField] private Transform hammerHead;
    [SerializeField] private float swingAngle = 60f;
    [SerializeField] private float swingSpeed = 2f;
    [Tooltip("Delay ban đầu — đặt khác nhau cho từng instance để vung lệch pha")]
    [SerializeField] private float phaseOffset = 0f;

    private Quaternion _restRotation;

    private void Start()
    {
        if (hammerHead != null)
            _restRotation = hammerHead.localRotation;
    }

    private void Update()
    {
        if (hammerHead == null) return;

        float time  = Runner != null ? (float)Runner.SimulationTime : Time.time;
        float angle = Mathf.Sin((time + phaseOffset) * swingSpeed) * swingAngle;

        hammerHead.localRotation = _restRotation * Quaternion.Euler(0, 0, angle);
    }

    protected override void ApplyEffect(PlayerController player)
    {
        if (!Object.HasStateAuthority) return;

        Vector3 pushDir = (player.transform.position - hammerHead.position).normalized;
        pushDir.y = 0f;

        Vector3 knockback = pushDir * 15f + Vector3.up * 3f;

        if (!player.TryApplyHit(knockback)) return;

        player.ForceIdle();
        Debug.Log($"[Hammer] Knockback {player.Object.InputAuthority}");
    }
}