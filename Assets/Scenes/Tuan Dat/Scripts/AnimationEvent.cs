using UnityEngine;

public class AnimationEvent : MonoBehaviour
{
    [Header("Gọi method trên object cha / root")]
    [SerializeField] private string methodName = "AttackHitEvent";

    public void TriggerParentMethod()
    {
        if (string.IsNullOrWhiteSpace(methodName))
        {
            Debug.LogWarning($"[{name}] methodName chưa được set.");
            return;
        }

        gameObject.SendMessageUpwards(methodName, SendMessageOptions.DontRequireReceiver);
    }

    public void TriggerAttackHit()
    {
        gameObject.SendMessageUpwards("AttackHitEvent", SendMessageOptions.DontRequireReceiver);
    }

    public void TriggerDamage()
    {
        TriggerParentMethod();
    }
}
