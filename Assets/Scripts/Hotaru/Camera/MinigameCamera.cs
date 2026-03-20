using UnityEngine;

/// <summary>
/// Component gắn vào minigame scene để quản lý camera của minigame đó
/// Tự động switch camera khi minigame bắt đầu/kết thúc
/// </summary>
public class MinigameCamera : MonoBehaviour
{
    [Header("Minigame Camera")]
    [Tooltip("Vị trí và góc nhìn của shared camera")]
    [SerializeField] private Transform sharedCameraPosition;
    [SerializeField] private bool useSharedCamera = true;

    [Header("Auto Switch")]
    [Tooltip("Tự động switch sang shared camera khi Start()")]
    [SerializeField] private bool autoSwitchOnStart = true;

    private void Start()
    {
        if (autoSwitchOnStart && useSharedCamera)
        {
            SwitchToMinigameCamera();
        }
    }

    private void OnDestroy()
    {
        // Khi minigame kết thúc, quay về player camera
        if (useSharedCamera)
        {
            SwitchToPlayerCamera();
        }
    }

    /// <summary>
    /// Chuyển sang camera của minigame (shared camera)
    /// </summary>
    public void SwitchToMinigameCamera()
    {
        if (sharedCameraPosition == null)
        {
            Debug.LogWarning("[MinigameCamera] Shared camera position not assigned!");
            return;
        }

        if (CameraManager.Instance != null)
        {
            CameraManager.Instance.SwitchToSharedCamera(sharedCameraPosition);
            Debug.Log($"[MinigameCamera] Switched to minigame camera: {sharedCameraPosition.name}");
        }
    }

    /// <summary>
    /// Chuyển về camera của player
    /// </summary>
    public void SwitchToPlayerCamera()
    {
        if (CameraManager.Instance != null)
        {
            CameraManager.Instance.SwitchToPlayerCamera();
            Debug.Log("[MinigameCamera] Switched back to player camera");
        }
    }

    /// <summary>
    /// Đổi shared camera position runtime (nếu minigame có nhiều camera angles)
    /// </summary>
    public void SetSharedCameraPosition(Transform newPosition)
    {
        sharedCameraPosition = newPosition;
        
        if (CameraManager.Instance != null && CameraManager.Instance.CurrentMode == CameraMode.Shared)
        {
            CameraManager.Instance.SwitchToSharedCamera(sharedCameraPosition);
        }
    }
}
