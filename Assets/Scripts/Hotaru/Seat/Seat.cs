using UnityEngine;
using Fusion;

public class Seat : InteractableObject
{
    #region Properties
    [Header("Seat Settings")]
    [SerializeField] private Transform sitPoint;  // Vị trí ngồi

    public int SeatIndex { get; private set; } = -1;
    public bool IsAvailable =>
    SeatManager.Instance != null &&
    SeatManager.Instance.IsSeatAvailable(SeatIndex);
    public Vector3 SitPosition => sitPoint != null ? sitPoint.position : transform.position;

    public Quaternion SitRotation => sitPoint != null ? sitPoint.rotation : transform.rotation;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        // Nếu không có sitPoint, dùng transform của ghế
        if (sitPoint == null)
        {
            sitPoint = transform;
        }

        promptText = "Sit";
        interactionKey = KeyCode.E;
    }
    #endregion

    #region InteractableObject Overrides
    public override bool CanInteract()
    {
        if (SeatIndex < 0) return false;
        if (!base.CanInteract()) return false;

        if (GameManager.Instance != null &&
            GameManager.Instance.CurrentState != GameState.Lobby)
            return false;

        if (SeatManager.Instance == null) return false;

        return SeatManager.Instance.IsSeatAvailable(SeatIndex);
    }

    public override void Interact()
    {
        if (!CanInteract()) return;

        if (PlayerNetworkData.Local != null)
        {
            var playerRef = PlayerNetworkData.Local.Object.InputAuthority;

            if (SeatManager.Instance != null &&
                SeatManager.Instance.IsSeatAvailable(SeatIndex))
            {
                TrySit(playerRef);
                base.Interact();
            }
        }
        else
        {
            Debug.LogWarning("[Seat] PlayerNetworkData.Local is null! Cannot interact.");
        }
    }
    #endregion

    #region Public Methods
    public void Initialize(int index)
    {
        SeatIndex = index;
        Debug.Log($"[Seat] Initialized seat {index} at {transform.position}");
    }

    public void TrySit(PlayerRef playerRef)
    {
        if (SeatManager.Instance == null)
        {
            Debug.LogWarning("[Seat] SeatManager not found!");
            return;
        }

        if (!SeatManager.Instance.IsSeatAvailable(SeatIndex))
        {
            Debug.Log($"[Seat] Seat {SeatIndex} is occupied");
            return;
        }

        // Không tự xử lý gì thêm → để SeatManager quyết định
        SeatManager.Instance.TrySitDown(SeatIndex, playerRef);
    }
    #endregion

#if UNITY_EDITOR
    protected override void OnDrawGizmosSelected()
    {
        // Draw interaction range từ base class
        base.OnDrawGizmosSelected();

        // Draw sit point
        if (sitPoint != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(sitPoint.position, 0.2f);
            Gizmos.DrawLine(sitPoint.position, sitPoint.position + sitPoint.forward * 0.5f);
        }
    }
#endif
}
