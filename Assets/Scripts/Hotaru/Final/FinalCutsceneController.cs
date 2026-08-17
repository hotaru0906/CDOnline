using UnityEngine;
using System.Collections;

/// <summary>
/// Cutscene camera cho Final Scene, chạy hoàn toàn bằng code (giống pattern BoardIntroController),
/// không dùng Timeline/PlayableDirector nữa.
///
/// Trình tự:
///   1) Camera đứng yên tại orbitPoint, chỉ xoay quanh trục Y trong orbitDuration giây (mặc định 5s).
///   2) Di chuyển tới vị trí Top1 trong moveToTopOneDuration giây (mặc định 5s).
///   3) Di chuyển tới vị trí Cage trong moveToCageDuration giây (mặc định 5s).
///   4) Di chuyển tới vị trí cuối cùng (trung tâm) trong moveToFinalDuration giây, camera này
///      SẼ Ở LẠI làm gameplay camera luôn (không tắt/đổi camera nào khác sau đó).
///
/// FinalManager gọi PlayCutscene() (đã được trigger đồng bộ qua RPC cho tất cả client) và sau khi
/// coroutine đó hoàn tất trên host thì FinalManager tự advance sang phase tiếp theo + hiện UI.
/// </summary>
public class FinalCutsceneController : MonoBehaviour
{
    public static FinalCutsceneController Instance;

    [Header("Camera")]
    [Tooltip("Camera duy nhất dùng cho cutscene này - sau khi cutscene chạy xong sẽ ở lại làm gameplay camera, không tắt đi.")]
    [SerializeField] private Camera cutsceneCamera;

    [Header("Stage 1 - Xoay tai cho")]
    [Tooltip("Vị trí cố định của camera trong lúc xoay (camera đứng yên tại đây, chỉ xoay quanh trục Y).")]
    [SerializeField] private Transform orbitPoint;
    public float orbitDuration = 5f;
    [Tooltip("Số vòng xoay (360 độ) trong orbitDuration giây.")]
    public float orbitRotations = 1f;

    [Header("Stage 2 - Toi Top1")]
    [Tooltip("Transform vị trí + góc nhìn camera hướng vào Top1.")]
    [SerializeField] private Transform topOneViewPoint;
    public float moveToTopOneDuration = 3f;

    [Header("Stage 3 - Toi Cage")]
    [Tooltip("Transform vị trí + góc nhìn camera hướng vào Cage.")]
    [SerializeField] private Transform cageViewPoint;
    public float moveToCageDuration = 3f;

    [Header("Stage 4 - Vi tri cuoi (Gameplay Camera)")]
    [Tooltip("Transform vị trí + góc nhìn cuối cùng, camera sẽ ở lại đây làm gameplay camera cho cả Final Scene.")]
    [SerializeField] private Transform finalCameraPoint;
    public float moveToFinalDuration = 2f;

    public float TotalDuration =>
        orbitDuration + moveToTopOneDuration + moveToCageDuration + moveToFinalDuration;

    private void Awake()
    {
        Instance = this;

        // Camera nên để sẵn active/inactive tùy setup của FinalManager (FinalManager sẽ bật no lên
        // đúng lúc bắt đầu cutscene). Không ép trạng thái ở đây để tránh xung đột.
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// Chạy toàn bộ cutscene theo đúng 4 giai đoạn. Gọi từ FinalManager (đã được đồng bộ bằng RPC
    /// nên mọi client tự chạy coroutine này cùng lúc).
    /// </summary>
    public IEnumerator PlayCutscene()
    {
        if (cutsceneCamera == null)
        {
            Debug.LogError("[FinalCutsceneController] cutsceneCamera chua duoc gan trong Inspector!");
            yield break;
        }

        yield return RotateAtPoint();
        yield return MoveCameraToTransform(topOneViewPoint, moveToTopOneDuration, "topOneViewPoint");
        yield return MoveCameraToTransform(cageViewPoint, moveToCageDuration, "cageViewPoint");
        yield return MoveCameraToTransform(finalCameraPoint, moveToFinalDuration, "finalCameraPoint");

        // Camera hien dang o finalCameraPoint - giu nguyen lam gameplay camera, khong tat/doi nua.
    }

    private IEnumerator RotateAtPoint()
    {
        if (orbitPoint == null)
        {
            Debug.LogError("[FinalCutsceneController] orbitPoint chua duoc gan trong Inspector!");
            yield break;
        }

        // Camera dung yen tai orbitPoint, chi xoay quanh truc Y.
        cutsceneCamera.transform.position = orbitPoint.position;

        float startYaw = orbitPoint.eulerAngles.y;
        float elapsed = 0f;

        while (elapsed < orbitDuration)
        {
            elapsed += Time.deltaTime;

            float t = elapsed / orbitDuration;
            float yaw = startYaw + 360f * orbitRotations * t;

            cutsceneCamera.transform.rotation = Quaternion.Euler(
                orbitPoint.eulerAngles.x,
                yaw,
                orbitPoint.eulerAngles.z
            );

            yield return null;
        }

        cutsceneCamera.transform.rotation = orbitPoint.rotation;
    }

    // Di chuyen + xoay camera toi dung vi tri/rotation cua target transform (giu nguyen rotation goc,
    // khong LookAt, dung dung goc da dat san trong scene).
    private IEnumerator MoveCameraToTransform(Transform target, float duration, string debugName)
    {
        if (target == null)
        {
            Debug.LogError($"[FinalCutsceneController] {debugName} chua duoc gan trong Inspector!");
            yield break;
        }

        Vector3 startPos = cutsceneCamera.transform.position;
        Quaternion startRot = cutsceneCamera.transform.rotation;

        if (duration > 0f)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float p = Mathf.SmoothStep(0f, 1f, t / duration);

                cutsceneCamera.transform.position = Vector3.Lerp(startPos, target.position, p);
                cutsceneCamera.transform.rotation = Quaternion.Slerp(startRot, target.rotation, p);

                yield return null;
            }
        }

        cutsceneCamera.transform.position = target.position;
        cutsceneCamera.transform.rotation = target.rotation;
    }
}