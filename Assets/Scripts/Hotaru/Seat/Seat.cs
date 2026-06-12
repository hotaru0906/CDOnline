using UnityEngine;

public class Seat : MonoBehaviour
{
    #region Properties

    [Header("Seat Settings")]
    [SerializeField] private Transform sitPoint;

    public int SeatIndex { get; private set; } = -1;

    public Vector3 SitPosition =>
        sitPoint != null ? sitPoint.position : transform.position;

    public Quaternion SitRotation =>
        sitPoint != null ? sitPoint.rotation : transform.rotation;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (sitPoint == null)
        {
            sitPoint = transform;
        }
    }

    #endregion

    #region Public Methods

    public void Initialize(int index)
    {
        SeatIndex = index;

        Debug.Log(
            $"[Seat] Initialized seat {index} at {transform.position}"
        );
    }

    #endregion

#if UNITY_EDITOR

    private void OnDrawGizmosSelected()
    {
        if (sitPoint == null)
            return;

        Gizmos.color = Color.green;

        Gizmos.DrawSphere(
            sitPoint.position,
            0.2f
        );

        Gizmos.DrawLine(
            sitPoint.position,
            sitPoint.position + sitPoint.forward * 0.5f
        );
    }

#endif
}