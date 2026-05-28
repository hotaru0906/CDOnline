using UnityEngine;

/// <summary>
/// Helper relay: gắn lên child GameObject có BoxCollider (Is Trigger) trái/phải.
/// Tự động tìm TiltingPlatform ở parent nếu không gán tay.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class TiltZoneRelay : MonoBehaviour
{
    [SerializeField] private TiltingPlatform platform;
    [SerializeField] private int side = -1; // -1 = trái, +1 = phải

    private void Awake()
    {
        // Tự tìm platform ở parent nếu chưa gán trong Inspector
        if (platform == null)
            platform = GetComponentInParent<TiltingPlatform>();

        // Đảm bảo BoxCollider là trigger
        var col = GetComponent<BoxCollider>();
        if (col != null) col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (platform == null) return;
        // GetComponentInParent vì PlayerController nằm trên root, collider có thể ở child
        if (other.GetComponentInParent<PlayerController>() == null) return;
        platform.NotifyPlayerEntered(side);
    }

    private void OnTriggerExit(Collider other)
    {
        if (platform == null) return;
        if (other.GetComponentInParent<PlayerController>() == null) return;
        platform.NotifyPlayerExited(side);
    }
}
