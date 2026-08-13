using UnityEngine;

public class PlayerAnimationEventRelay : MonoBehaviour
{
    private PlayerController playerController;

    private void Awake()
    {
        playerController = GetComponentInParent<PlayerController>();
    }

    public void TriggerAttackHit()
    {
        Debug.Log($"[AnimationRelay] TriggerAttackHit received on {gameObject.name}");

        if (playerController == null)
        {
            Debug.LogWarning(
                $"[AnimationRelay] Cannot find PlayerController in parent of {gameObject.name}"
            );
            return;
        }

        playerController.AttackHitEvent();
    }
}