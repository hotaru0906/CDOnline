using UnityEngine;
using Fusion;

public class Hammer : BaseObstacle
{
    [Header("Hammer Swing")]
    [SerializeField] private Transform hammerHead;

    [SerializeField] private float swingAngle = 60f;
    [SerializeField] private float swingSpeed = 2f;

    private Quaternion _restRotation;

    private void Start()
    {
        if (hammerHead != null)
        {
            _restRotation = hammerHead.localRotation;
        }
    }

    private void Update()
    {
        if (hammerHead == null)
            return;

        float time = Runner != null
            ? (float)Runner.SimulationTime
            : Time.time;

        float angle =
            Mathf.Sin(time * swingSpeed) * swingAngle;

        // Lắc theo trục Z
        hammerHead.localRotation =
            _restRotation *
            Quaternion.Euler(0, 0, angle);
    }

    protected override void HandleHit(PlayerController player)
    {
        base.HandleHit(player);
    }

    protected override void ApplyEffect(PlayerController player)
    {
        var mgData = GetMinigameData(player);

        if (mgData != null && mgData.CanTakeDamage())
        {
            mgData.Die();
        }

        Debug.Log($"[Hammer] Killed {player.Object.InputAuthority}");
    }
}