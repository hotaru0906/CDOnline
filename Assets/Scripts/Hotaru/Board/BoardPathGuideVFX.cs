using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Hiển thị đường chỉ dẫn (Trail + Particle dust) từ ô hiện tại của LOCAL PLAYER
/// tới ô Finish của bàn cờ. Không ảnh hưởng gì tới logic di chuyển/network thật.
///
/// Cách hoạt động:
/// - Cứ mỗi <see cref="repeatInterval"/> giây, dựng lại path (nếu cần) và cho
///   1 object VFX bay dọc theo các node từ vị trí hiện tại -> Finish.
/// - Nếu phát hiện node hiện tại của player thay đổi (do họ vừa đi),
///   sẽ huỷ lượt chạy hiện tại, dựng lại path mới và chạy ngay lập tức,
///   rồi tiếp tục đếm lại chu kỳ 5s từ đó.
///
/// Cách dùng:
/// 1. Tạo 1 GameObject rỗng trong BoardScene (VD: "PathGuideVFX_Runner"),
///    gắn script này vào.
/// 2. Tạo 1 Prefab "VFX Object" gồm: TrailRenderer (đã style sẵn) + ParticleSystem
///    dust con (Play On Awake = false), gán vào field <see cref="vfxPrefab"/>.
/// </summary>
public class BoardPathGuideVFX : MonoBehaviour
{
    [Header("Refs")]
    [Tooltip("Prefab VFX gồm TrailRenderer + ParticleSystem dust. Sẽ được Instantiate 1 lần và tái sử dụng.")]
    [SerializeField] private GameObject vfxPrefab;

    [Header("Timing")]
    [Tooltip("Khoảng cách giữa 2 lần chạy VFX (giây).")]
    [SerializeField] private float repeatInterval = 5f;

    [Tooltip("Tốc độ bay dọc đường (units/giây). Vừa phải để thấy rõ từng ô.")]
    [SerializeField] private float moveSpeed = 4f;

    [Header("Visual")]
    [Tooltip("Độ cao nhấc VFX lên khỏi mặt bàn cờ, tránh bị che khuất bởi node/token.")]
    [SerializeField] private float yOffset = 0.15f;

    [Tooltip("Giới hạn an toàn số node tối đa khi dò đường (tránh vòng lặp vô hạn nếu path bị lỗi).")]
    [SerializeField] private int maxPathSteps = 200;

    [Header("Dust Particle (optional override)")]
    [Tooltip("Nếu gán, ParticleSystem này sẽ được Play() khi VFX bắt đầu bay và Stop() khi kết thúc. Có thể để trống nếu prefab tự lo việc này qua Play On Awake / trigger riêng.")]
    [SerializeField] private ParticleSystem dustParticle;

    private GameObject _vfxInstance;
    private TrailRenderer _trail;
    private Coroutine _loopRoutine;
    private Coroutine _flyRoutine;

    private int _lastKnownNodeID = int.MinValue;

    private void OnEnable()
    {
        _loopRoutine = StartCoroutine(MainLoop());
    }

    private void OnDisable()
    {
        if (_loopRoutine != null) StopCoroutine(_loopRoutine);
        if (_flyRoutine != null) StopCoroutine(_flyRoutine);
        HideVfxImmediate();
    }

    private void EnsureVfxInstance()
    {
        if (_vfxInstance != null) return;
        if (vfxPrefab == null)
        {
            Debug.LogWarning("[BoardPathGuideVFX] Chưa gán vfxPrefab!");
            return;
        }

        _vfxInstance = Instantiate(vfxPrefab);
        _vfxInstance.SetActive(false);
        _trail = _vfxInstance.GetComponent<TrailRenderer>();
        if (dustParticle == null)
            dustParticle = _vfxInstance.GetComponentInChildren<ParticleSystem>();
    }

    private IEnumerator MainLoop()
    {
        EnsureVfxInstance();

        while (true)
        {
            // Kiểm tra node hiện tại của local player, nếu đổi thì chạy lại ngay
            int currentNodeID = GetLocalPlayerCurrentNodeID();

            if (currentNodeID != int.MinValue && currentNodeID != _lastKnownNodeID)
            {
                _lastKnownNodeID = currentNodeID;

                if (_flyRoutine != null)
                {
                    StopCoroutine(_flyRoutine);
                    HideVfxImmediate();
                }

                yield return RunOnce();

                float elapsed = 0f;
                while (elapsed < repeatInterval)
                {
                    // Nếu trong lúc chờ mà node lại đổi tiếp -> thoát sớm để restart loop
                    if (GetLocalPlayerCurrentNodeID() != _lastKnownNodeID)
                        break;

                    elapsed += Time.deltaTime;
                    yield return null;
                }

                continue;
            }

            yield return RunOnce();

            yield return new WaitForSeconds(repeatInterval);
        }
    }

    private IEnumerator RunOnce()
    {
        var waypoints = BuildPathToFinish();
        if (waypoints == null || waypoints.Count < 2)
            yield break;

        _flyRoutine = StartCoroutine(FlyAlongPath(waypoints));
        yield return _flyRoutine;
        _flyRoutine = null;
    }

    /// <summary>
    /// Dò đường từ node hiện tại của local player tới Finish node,
    /// luôn đi theo nextNodes[0] (khớp với hướng đi mặc định "Phase 0").
    /// </summary>
    private List<Vector3> BuildPathToFinish()
    {
        var boardManager = BoardManager.Instance;
        var boardPath = BoardNodePath.Instance;
        if (boardManager == null || boardPath == null) return null;

        int startNodeID = GetLocalPlayerCurrentNodeID();
        if (startNodeID == int.MinValue) return null;

        var startNode = boardPath.GetNodeByID(startNodeID);
        if (startNode == null) return null;

        var waypoints = new List<Vector3> { GetVfxPosition(startNode) };

        var cursor = startNode;
        int guard = 0;

        while (!cursor.isFinishNode && guard < maxPathSteps)
        {
            guard++;

            BoardNode next = null;
            if (cursor.nextNodes != null && cursor.nextNodes.Count > 0)
                next = cursor.nextNodes[0];

            if (next == null)
                break; // hết đường đi được (path lỗi hoặc cụt) — dừng lại tại đây

            waypoints.Add(GetVfxPosition(next));
            cursor = next;
        }

        return waypoints;
    }

    private Vector3 GetVfxPosition(BoardNode node)
    {
        return node.GetCenterPosition() + Vector3.up * yOffset;
    }

    private IEnumerator FlyAlongPath(List<Vector3> waypoints)
    {
        EnsureVfxInstance();
        if (_vfxInstance == null) yield break;

        _vfxInstance.transform.position = waypoints[0];
        _vfxInstance.SetActive(true);

        _trail?.Clear();
        dustParticle?.Play();

        for (int i = 1; i < waypoints.Count; i++)
        {
            Vector3 from = waypoints[i - 1];
            Vector3 to = waypoints[i];
            float distance = Vector3.Distance(from, to);
            float duration = moveSpeed > 0f ? distance / moveSpeed : 0f;

            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                _vfxInstance.transform.position = Vector3.Lerp(from, to, duration > 0f ? t / duration : 1f);
                yield return null;
            }

            _vfxInstance.transform.position = to;
        }

        dustParticle?.Stop();

        // Đợi trail tự mờ dần hết rồi mới ẩn hẳn object (tránh cắt trail đột ngột)
        float fadeWait = _trail != null ? _trail.time : 0f;
        if (fadeWait > 0f)
            yield return new WaitForSeconds(fadeWait);

        _vfxInstance.SetActive(false);
    }

    private void HideVfxImmediate()
    {
        if (_vfxInstance == null) return;
        _trail?.Clear();
        dustParticle?.Stop();
        _vfxInstance.SetActive(false);
    }

    /// <summary>
    /// Lấy nodeID hiện tại của local player, dựa trên slot map trong BoardManager.
    /// Trả về int.MinValue nếu chưa xác định được (chưa vào board, mất kết nối, v.v.)
    /// </summary>
    private int GetLocalPlayerCurrentNodeID()
    {
        var boardManager = BoardManager.Instance;
        if (boardManager == null || boardManager.Runner == null) return int.MinValue;

        int localId = boardManager.Runner.LocalPlayer.PlayerId;

        for (int slot = 0; slot < 4; slot++)
        {
            if (boardManager.GetPlayerIDAtSlot(slot) == localId)
                return boardManager.GetNodeIDAtSlot(slot);
        }

        return int.MinValue;
    }
}