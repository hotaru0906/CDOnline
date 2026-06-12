using UnityEngine;
using Fusion;

public class FloatingObstacle : BaseObstacle
{
    public enum FloatAxis { X, Z }

    [Header("Floating Motion")]
    [SerializeField] private FloatAxis axis           = FloatAxis.X;
    [SerializeField] private float     floatSpeed     = 1.5f;
    [SerializeField] private float     floatAmplitude = 2f;
    [SerializeField] private float     phaseOffset    = 0f;

    [Header("Knockback")]
    [SerializeField] private float knockbackForce    = 8f;
    [SerializeField] private float upForce           = 3f;
    [SerializeField] private float knockbackDuration = 0.3f;

    private Vector3 _startPosition;

    private void Start()
    {
        _startPosition = transform.position;
    }

    private void Update()
    {
        // Runner có sẵn từ NetworkBehaviour — SimulationTime sync cho tất cả clients
        float time   = Runner != null ? (float)Runner.SimulationTime : Time.time;
        float offset = Mathf.Sin((time + phaseOffset) * floatSpeed) * floatAmplitude;

        Vector3 pos = _startPosition;
        if (axis == FloatAxis.X) pos.x += offset;
        else                     pos.z += offset;

        transform.position = pos;
    }

    protected override void ApplyEffect(PlayerController player)
    {
        Vector3 toPlayer = (player.transform.position - transform.position).normalized;
        toPlayer.y = 0f;

        Vector3 force = toPlayer * knockbackForce + Vector3.up * upForce;
        player.ApplyExternalForce(force, knockbackDuration, overrideInput: true);

        Debug.Log($"[FloatingObstacle] Knocked {player.Object.InputAuthority}");
    }
}