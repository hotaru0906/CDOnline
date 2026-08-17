using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class BoardIntroController : MonoBehaviour
{
    [Header("Camera References")]
    [SerializeField] private Camera introCamera;
    [SerializeField] private Camera mainCamera;
    [SerializeField] private BoardSpectatorCameraController spectatorCameraController;
    public static BoardIntroController Instance;

    [Header("Orbit")]
    [SerializeField] private float orbitRadius = 35f;
    [SerializeField] private float orbitHeight = 20f;
    [SerializeField] private float orbitDuration = 5f;

    [Header("Billboard")]
    [Tooltip("Transform đặt sẵn trong scene — vị trí + góc camera nhìn vào các billboard thông tin player")]
    [SerializeField] private Transform billboardViewPoint;
    [SerializeField] private float billboardDuration = 5f;
    [SerializeField] private float billboardTransitionDuration = 1f;

    [Header("Camera Path (sau Billboard)")]
    [Tooltip("Danh sách các điểm camera sẽ đi qua theo thứ tự, từ điểm đầu đến điểm cuối. Sau khi tới điểm cuối thì bắt đầu chơi game.")]
    [SerializeField] private List<Transform> cameraPathPoints = new List<Transform>();
    [Tooltip("Tổng thời gian camera di chuyển qua hết các điểm trong Camera Path")]
    [SerializeField] private float cameraPathDuration = 10f;

    public float TotalDuration => orbitDuration + billboardDuration + cameraPathDuration;

    private void Awake()
    {
        Instance = this;

        if (introCamera != null)
            introCamera.gameObject.SetActive(false);
    }

    private void Start()
    {
        StartCoroutine(NotifyReadyRoutine());
    }

    private IEnumerator NotifyReadyRoutine()
    {
        while (BoardManager.Instance == null)
            yield return null;

        BoardManager.Instance.NotifyClientReadyForIntro();
    }

    private Vector3 GetBoardCenter()
    {
        BoardNode[] nodes =
            FindObjectsByType<BoardNode>(FindObjectsSortMode.None);

        if (nodes.Length == 0)
            return Vector3.zero;

        Vector3 center = Vector3.zero;

        foreach (BoardNode node in nodes)
        {
            center += node.transform.position;
        }

        return center / nodes.Length;
    }

    public IEnumerator PlayIntro()
    {
        spectatorCameraController?.SetIntroActive(true);
        introCamera.gameObject.SetActive(true);
        mainCamera.gameObject.SetActive(false);

        BoardBillboardUI.PopulateAll();

        // Player đi đầu tiên luôn là slot 0 (CurrentTurnIndex bắt đầu = 0)
        BoardBillboardUI.StartFirstTurnGlowAll(0);

        yield return OrbitAroundBoard();
        yield return BillboardStage();
        yield return CameraPathStage();

        BoardBillboardUI.StopFirstTurnGlowAll();

        introCamera.gameObject.SetActive(false);
        mainCamera.gameObject.SetActive(true);
        spectatorCameraController?.SetIntroActive(false);
    }

    private IEnumerator BillboardStage()
    {
        if (billboardViewPoint == null)
        {
            Debug.LogWarning("[BoardIntroController] Chưa gán Billboard View Point — bỏ qua bước billboard.");
            yield return new WaitForSeconds(billboardDuration);
            yield break;
        }

        yield return MoveCameraToTransform(billboardViewPoint, billboardTransitionDuration);

        float holdDuration = billboardDuration - billboardTransitionDuration;
        if (holdDuration > 0f)
            yield return new WaitForSeconds(holdDuration);
    }

    private IEnumerator CameraPathStage()
    {
        if (cameraPathPoints == null || cameraPathPoints.Count == 0)
        {
            Debug.LogWarning("[BoardIntroController] Chưa gán Camera Path Points — bỏ qua bước camera path.");
            yield break;
        }

        // Loại các phần tử null (phòng trường hợp gán thiếu trong Inspector)
        List<Transform> points = cameraPathPoints.FindAll(p => p != null);
        if (points.Count == 0)
            yield break;

        // Nếu camera chưa ở đúng điểm đầu tiên, coi vị trí hiện tại là điểm xuất phát
        // và chèn điểm đầu tiên trong danh sách làm mốc kế tiếp.
        Vector3 startPos = introCamera.transform.position;
        Quaternion startRot = introCamera.transform.rotation;

        // Tính độ dài từng đoạn A->B->C->D để chia thời gian theo tỉ lệ quãng đường,
        // giúp tốc độ camera đều xuyên suốt thay vì mỗi đoạn tốn thời gian bằng nhau.
        int segmentCount = points.Count; // đoạn 0: start->points[0], đoạn i: points[i-1]->points[i]
        float[] segmentLengths = new float[segmentCount];
        float totalLength = 0f;

        Vector3 prevPos = startPos;
        for (int i = 0; i < points.Count; i++)
        {
            float len = Vector3.Distance(prevPos, points[i].position);
            segmentLengths[i] = len;
            totalLength += len;
            prevPos = points[i].position;
        }

        // Trường hợp tất cả các điểm trùng vị trí (totalLength = 0) — chia đều thời gian
        bool useEqualSplit = totalLength <= 0.0001f;

        Vector3 segStartPos = startPos;
        Quaternion segStartRot = startRot;

        for (int i = 0; i < points.Count; i++)
        {
            Transform target = points[i];

            float segDuration = useEqualSplit
                ? cameraPathDuration / segmentCount
                : cameraPathDuration * (segmentLengths[i] / totalLength);

            if (segDuration > 0f)
            {
                float t = 0f;
                while (t < segDuration)
                {
                    t += Time.deltaTime;
                    float p = Mathf.SmoothStep(0f, 1f, t / segDuration);

                    introCamera.transform.position = Vector3.Lerp(segStartPos, target.position, p);
                    introCamera.transform.rotation = Quaternion.Slerp(segStartRot, target.rotation, p);

                    yield return null;
                }
            }

            introCamera.transform.position = target.position;
            introCamera.transform.rotation = target.rotation;

            segStartPos = target.position;
            segStartRot = target.rotation;
        }
    }

    private IEnumerator OrbitAroundBoard()
    {
        float elapsed = 0f;
        Vector3 center = GetBoardCenter();

        while (elapsed < orbitDuration)
        {
            elapsed += Time.deltaTime;

            float t = elapsed / orbitDuration;
            t = Mathf.SmoothStep(0f, 1f, t);

            float angle = Mathf.Lerp(0f, 360f, t);
            float rad = angle * Mathf.Deg2Rad;

            Vector3 position =
                center +
                new Vector3(
                    Mathf.Cos(rad) * orbitRadius,
                    orbitHeight,
                    Mathf.Sin(rad) * orbitRadius
                );

            introCamera.transform.position = position;
            introCamera.transform.LookAt(center);

            yield return null;
        }
    }

    private IEnumerator MoveCameraToTransform(Transform target, float duration)
    {
        Vector3 startPos = introCamera.transform.position;
        Quaternion startRot = introCamera.transform.rotation;

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float p = Mathf.SmoothStep(0f, 1f, t / duration);

            introCamera.transform.position = Vector3.Lerp(startPos, target.position, p);
            introCamera.transform.rotation = Quaternion.Slerp(startRot, target.rotation, p);

            yield return null;
        }

        introCamera.transform.position = target.position;
        introCamera.transform.rotation = target.rotation;
    }
}