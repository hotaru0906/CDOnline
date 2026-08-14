using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Điều khiển 1 GameObject (có sẵn TrailRenderer + ParticleSystem) chạy dọc theo
/// đường đi của board, từ node hiện tại của player cho tới node kết thúc (isFinishNode).
/// Chỉ chạy local (client-side visual), không network-synced.
///
/// Cách dùng:
/// - Gắn component này lên GameObject trail (đã có TrailRenderer/ParticleSystem sẵn).
/// - Gọi StartFrom(nodeID) khi bắt đầu game để chạy trail từ node xuất phát.
/// - Gọi UpdateStartNode(nodeID) mỗi khi player tới node mới — trail sẽ rebuild lại
///   đường đi từ node đó tới cuối và restart ngay (không đợi vòng lặp hiện tại xong).
/// </summary>
public class BoardTrailRunner : MonoBehaviour
{
    [Header("Timing")]
    [Tooltip("Thời gian trail đi hết từ node bắt đầu tới node cuối")]
    [SerializeField] private float travelDuration = 10f;
    [Tooltip("Thời gian chờ sau khi tới node cuối trước khi chạy lại vòng lặp")]
    [SerializeField] private float loopRestartDelay = 5f;

    [Header("Height Offset")]
    [SerializeField] private float heightOffset = 0.5f;

    [Header("Auto Start")]
    [Tooltip("Tự động chạy trail từ node này ngay khi vào scene (không cần script khác gọi).")]
    [SerializeField] private bool autoStartOnAwake = true;
    [SerializeField] private int autoStartNodeID = 0;
    [Tooltip("Thời gian tối đa chờ BoardNodePath.Instance sẵn sàng trước khi bỏ cuộc")]
    [SerializeField] private float waitForPathTimeout = 5f;

    private int _currentStartNodeID = -1;
    private Coroutine _runRoutine;
    private List<Vector3> _pathPositions = new List<Vector3>();

    private void Start()
    {
        if (autoStartOnAwake)
            StartCoroutine(AutoStartRoutine());
    }

    private IEnumerator AutoStartRoutine()
    {
        float elapsed = 0f;
        while (BoardNodePath.Instance == null && elapsed < waitForPathTimeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (BoardNodePath.Instance == null)
        {
            Debug.LogWarning("[BoardTrailRunner] BoardNodePath.Instance không sẵn sàng — không thể auto-start trail.");
            yield break;
        }

        StartFrom(autoStartNodeID);
    }

    /// <summary>Bắt đầu chạy trail từ node chỉ định.</summary>
    public void StartFrom(int nodeID)
    {
        UpdateStartNode(nodeID);
    }

    /// <summary>
    /// Cập nhật lại node bắt đầu (khi player di chuyển tới node mới).
    /// Trail sẽ rebuild path và restart ngay lập tức.
    /// </summary>
    public void UpdateStartNode(int nodeID)
    {
        if (_currentStartNodeID == nodeID && _runRoutine != null)
            return;

        _currentStartNodeID = nodeID;

        if (!BuildPath(nodeID))
        {
            Debug.LogWarning($"[BoardTrailRunner] Không build được path từ node {nodeID}.");
            return;
        }

        if (_runRoutine != null)
            StopCoroutine(_runRoutine);

        _runRoutine = StartCoroutine(RunLoop());
    }

    public void Stop()
    {
        if (_runRoutine != null)
        {
            StopCoroutine(_runRoutine);
            _runRoutine = null;
        }
    }

    /// <summary>
    /// Đi theo nextNodes[0] liên tục từ startNode cho tới khi gặp node isFinishNode = true.
    /// Có bảo vệ vòng lặp vô hạn nếu graph bị nối vòng do lỗi setup.
    /// </summary>
    private bool BuildPath(int startNodeID)
    {
        var path = BoardNodePath.Instance;
        if (path == null)
            return false;

        BoardNode current = path.GetNodeByID(startNodeID);
        if (current == null)
            return false;

        _pathPositions.Clear();
        _pathPositions.Add(current.GetCenterPosition() + Vector3.up * heightOffset);

        var visited = new HashSet<int> { current.nodeID };
        int safety = 0;
        int maxSteps = 500; // giới hạn an toàn, tránh loop vô hạn nếu graph lỗi

        while (!current.isFinishNode && safety < maxSteps)
        {
            safety++;

            BoardNode next = (current.nextNodes != null && current.nextNodes.Count > 0)
                ? current.nextNodes[0]
                : null;

            if (next == null)
                break; // hết đường mà chưa gặp finish node — dừng lại tại đây

            if (visited.Contains(next.nodeID))
            {
                Debug.LogWarning("[BoardTrailRunner] Phát hiện vòng lặp trong graph node — dừng build path.");
                break;
            }

            visited.Add(next.nodeID);
            _pathPositions.Add(next.GetCenterPosition() + Vector3.up * heightOffset);

            current = next;
        }

        return _pathPositions.Count > 0;
    }

    private IEnumerator RunLoop()
    {
        while (true)
        {
            yield return TravelAlongPath();
            yield return new WaitForSeconds(loopRestartDelay);
        }
    }

    private IEnumerator TravelAlongPath()
    {
        if (_pathPositions.Count == 0)
            yield break;

        if (_pathPositions.Count == 1)
        {
            transform.position = _pathPositions[0];
            yield break;
        }

        // Chia thời gian theo tỉ lệ độ dài từng đoạn để tốc độ đều xuyên suốt path
        int segmentCount = _pathPositions.Count - 1;
        float[] segmentLengths = new float[segmentCount];
        float totalLength = 0f;

        for (int i = 0; i < segmentCount; i++)
        {
            float len = Vector3.Distance(_pathPositions[i], _pathPositions[i + 1]);
            segmentLengths[i] = len;
            totalLength += len;
        }

        bool useEqualSplit = totalLength <= 0.0001f;

        transform.position = _pathPositions[0];

        for (int i = 0; i < segmentCount; i++)
        {
            Vector3 from = _pathPositions[i];
            Vector3 to = _pathPositions[i + 1];

            float segDuration = useEqualSplit
                ? travelDuration / segmentCount
                : travelDuration * (segmentLengths[i] / totalLength);

            if (segDuration > 0f)
            {
                float t = 0f;
                while (t < segDuration)
                {
                    t += Time.deltaTime;
                    float p = Mathf.Clamp01(t / segDuration);

                    transform.position = Vector3.Lerp(from, to, p);

                    yield return null;
                }
            }

            transform.position = to;
        }
    }
}