using UnityEngine;
using Fusion;

/// <summary>
/// Component cho mỗi ghế trong Lobby
/// Kế thừa InteractableObject để tích hợp với PlayerInteractionHandler
/// </summary>
public class Seat : InteractableObject
{
    #region Properties
    [Header("Seat Settings")]
    [SerializeField] private Transform sitPoint;  // Vị trí ngồi
    
    /// <summary>
    /// Index của ghế này (được set bởi SeatManager)
    /// </summary>
    public int SeatIndex { get; private set; } = -1;
    
    /// <summary>
    /// Player slot đang ngồi (-1 nếu trống)
    /// </summary>
    public int OccupantSlot { get; private set; } = -1;
    
    /// <summary>
    /// Ghế có trống không
    /// </summary>
    public bool IsAvailable => OccupantSlot == -1;
    
    /// <summary>
    /// Vị trí ngồi
    /// </summary>
    public Vector3 SitPosition => sitPoint != null ? sitPoint.position : transform.position;
    
    /// <summary>
    /// Rotation khi ngồi
    /// </summary>
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
        
        // Setup default prompt text
        promptText = "Sit";
        interactionKey = KeyCode.E;
    }
    #endregion

    #region InteractableObject Overrides
    
    /// <summary>
    /// Chỉ có thể tương tác khi ghế trống và đang ở Lobby
    /// </summary>
    public override bool CanInteract()
    {
        bool baseCanInteract = base.CanInteract();
        if (!baseCanInteract)
        {
            Debug.Log($"[Seat {SeatIndex}] CanInteract: FALSE (base.CanInteract failed)");
            return false;
        }
        
        // Chỉ trong Lobby
        if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.Lobby)
        {
            Debug.Log($"[Seat {SeatIndex}] CanInteract: FALSE (Not in Lobby, state: {GameManager.Instance.CurrentState})");
            return false;
        }
        
        // Kiểm tra networked state từ SeatManager thay vì local state
        if (SeatManager.Instance != null && SeatIndex >= 0)
        {
            bool available = SeatManager.Instance.IsSeatAvailable(SeatIndex);
            Debug.Log($"[Seat {SeatIndex}] CanInteract: {available} (SeatManager check)");
            return available;
        }
        
        Debug.Log($"[Seat {SeatIndex}] CanInteract: {IsAvailable} (local IsAvailable, SeatManager: {(SeatManager.Instance != null ? "exists" : "NULL")}, SeatIndex: {SeatIndex})");
        return IsAvailable;
    }
    
    /// <summary>
    /// Khi player tương tác - ngồi xuống
    /// </summary>
    public override void Interact()
    {
        if (!CanInteract()) return;
        
        // Lấy local player ref
        if (PlayerNetworkData.Local != null)
        {
            var playerRef = PlayerNetworkData.Local.Object.InputAuthority;
            Debug.Log($"[Seat] Player {playerRef.PlayerId} trying to sit on seat {SeatIndex}");
            TrySit(playerRef);
        }
        else
        {
            Debug.LogWarning("[Seat] PlayerNetworkData.Local is null! Cannot interact.");
        }
        
        base.Interact();
    }
    #endregion

    #region Public Methods
    
    /// <summary>
    /// Initialize seat với index (gọi bởi SeatManager)
    /// </summary>
    public void Initialize(int index)
    {
        SeatIndex = index;
        Debug.Log($"[Seat] Initialized seat {index} at {transform.position}");
    }

    /// <summary>
    /// Cập nhật trạng thái occupied
    /// </summary>
    public void SetOccupied(bool occupied, int playerSlot)
    {
        OccupantSlot = occupied ? playerSlot : -1;
        
        // Update prompt text
        promptText = occupied ? "Occupied" : "Sit";
    }

    /// <summary>
    /// Player cố gắng ngồi vào ghế này
    /// </summary>
    public void TrySit(PlayerRef playerRef)
    {
        if (SeatManager.Instance == null)
        {
            Debug.LogWarning("[Seat] SeatManager not found!");
            return;
        }

        // Kiểm tra networked state thay vì local state
        if (!SeatManager.Instance.IsSeatAvailable(SeatIndex))
        {
            Debug.Log($"[Seat] Seat {SeatIndex} is occupied (networked check)");
            return;
        }

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
