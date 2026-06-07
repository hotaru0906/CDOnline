using Fusion;
using UnityEngine;

public class SpectatorCamera : MonoBehaviour
{
    public static SpectatorCamera Instance;

    [SerializeField] private Camera mainCamera;

    private Transform target;

    private void Awake()
    {
        Instance = this;
    }

    private void LateUpdate()
    {
        if (target == null) return;

        mainCamera.transform.position =
            target.position + new Vector3(0, 5, -8);

        mainCamera.transform.LookAt(target);
    }

    public void FollowTarget(Transform newTarget)
    {
        target = newTarget;
    }
}