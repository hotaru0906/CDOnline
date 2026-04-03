using UnityEngine;

/// <summary>
/// Component gắn vào minigame scene để quản lý camera của minigame đó
/// Tự động switch camera khi minigame bắt đầu/kết thúc
/// </summary>
public class MinigameCamera : MonoBehaviour
{
    [Header("Minigame Camera")]
    [Tooltip("Transform xác định VỊ TRÍ và ROTATION của camera (không phải Camera component!)\\n" +
             "Tạo một Empty GameObject ở vị trí muốn camera đứng, xoay nó đúng hướng, rồi kéo vào đây.")]
    [SerializeField] private Transform sharedCameraPosition;
    [SerializeField] private bool useSharedCamera = true;

    [Header("Auto Switch")]
    [Tooltip("Tự động switch sang shared camera khi Start()")]
    [SerializeField] private bool autoSwitchOnStart = true;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = true;

    private void Start()
    {
        if (autoSwitchOnStart && useSharedCamera)
        {
            // Delay một chút để đảm bảo CameraManager đã reinitialize xong
            StartCoroutine(SwitchToMinigameCameraDelayed());
        }
    }

    private System.Collections.IEnumerator SwitchToMinigameCameraDelayed()
    {
        // Đợi 2 frames để đảm bảo CameraManager.ReinitializeCameraDelayed() đã chạy xong
        yield return null;
        yield return null;
        
        SwitchToMinigameCamera();
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
            Debug.LogError("[MinigameCamera] Shared camera position not assigned! " +
                           "Hãy tạo một Empty GameObject ở vị trí muốn camera đứng và kéo vào field 'Shared Camera Position'.");
            return;
        }

        // Kiểm tra xem có vô tình gán Camera component thay vì Transform không
        if (sharedCameraPosition.GetComponent<Camera>() != null)
        {
            Debug.LogWarning("[MinigameCamera] 'Shared Camera Position' field đang trỏ đến một Camera! " +
                            "Nên dùng Empty GameObject để xác định vị trí camera, không phải Camera component.");
        }

        if (CameraManager.Instance != null)
        {
            if (showDebugInfo)
            {
                Debug.Log($"[MinigameCamera] Calling SwitchToSharedCamera with position: {sharedCameraPosition.name} " +
                         $"at {sharedCameraPosition.position}, rotation: {sharedCameraPosition.rotation.eulerAngles}");
            }
            
            CameraManager.Instance.SwitchToSharedCamera(sharedCameraPosition);
            Debug.Log($"[MinigameCamera] Switched to minigame camera: {sharedCameraPosition.name}");
        }
        else
        {
            Debug.LogError("[MinigameCamera] CameraManager.Instance is null! Cannot switch camera.");
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
        
        if (CameraManager.Instance != null && CameraManager.Instance.CurrentMode == CameraMode.Minigame)
        {
            CameraManager.Instance.SwitchToMinigameCamera(sharedCameraPosition);
        }
    }
}
