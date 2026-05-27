using UnityEngine;

/// <summary>
/// Gắn lên child GameObject có Collider isTrigger.
/// Forward trigger event lên BaseObstacle cha.
///
/// Cần dùng cho:
///   - Hammer     → gắn lên hammerHead
///   - JumpPad    → gắn lên springPart (nếu collider ở đó)
///   - RotatingSpikeTrap → gắn lên spikePart
///   - SpikedWheel       → gắn lên wheelPart
///   - PopupSpikeTrap    → gắn lên spikePart
///   - CrushingPress     → gắn lên wallB
///
/// Setup trong Inspector:
///   obstacle → kéo GameObject GỐC (có BaseObstacle component) vào đây
/// </summary>
[RequireComponent(typeof(Collider))]
public class ObstacleTriggerRelay : MonoBehaviour
{
    [SerializeField] private BaseObstacle obstacle;

    private void OnTriggerEnter(Collider other)
    {
        obstacle?.OnChildTriggerEnter(other);
    }
}
