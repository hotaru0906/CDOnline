using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Script ĐỘC LẬP spawn VFX pháo hoa giấy (confetti) rơi từ trên xuống ở 2 bên ghế vàng (King Chair).
///
/// - KHÔNG đụng tới FinalManager / FinalCutsceneController. Bạn chỉ gắn script này vào 1 GameObject
///   bất kỳ trong scene (vd tạo empty "Confetti VFX Manager", hoặc gắn thẳng lên King Chair).
/// - VFX là cosmetic nên chạy LOCAL trên từng client (Instantiate thường, không spawn qua network).
///   Mỗi máy tự phát confetti của riêng mình -> không tốn băng thông, không cần NetworkObject.
/// - Prefab confetti của bạn tự lo phần "rơi từ trên xuống" (ParticleSystem). Script này chỉ đặt
///   đúng vị trí 2 bên ghế và bật đúng lúc.
///
/// Cách dùng nhanh:
///   1. Tạo 2 empty transform đặt ở bên trái + bên phải ghế vàng (đặt cao lên nếu muốn confetti
///      xuất hiện từ trên đỉnh rồi rơi xuống), gán vào spawnPoints.
///   2. Gán prefab confetti vào confettiPrefab.
///   3. Để playOnStart = true nếu muốn tự chạy ngay khi vào scene.
///      (Hoặc gọi PlayConfetti() từ chỗ khác nếu muốn trigger tay sau này.)
/// </summary>
public class KingChairConfettiVFX : MonoBehaviour
{
    [Header("VFX Prefab")]
    [Tooltip("Prefab pháo hoa giấy (confetti) - nên là ParticleSystem tự chạy rơi từ trên xuống.")]
    [SerializeField] private GameObject confettiPrefab;

    [Header("Spawn Points (2 ben ghe vang)")]
    [Tooltip("Các điểm spawn confetti. Đặt 2 điểm ở bên trái + bên phải ghế vàng. " +
             "Có thể thêm nhiều hơn 2 nếu muốn.")]
    [SerializeField] private Transform[] spawnPoints;

    [Tooltip("Nếu bật: confetti được đặt làm con của spawn point (đi theo point nếu point di chuyển). " +
             "Nếu tắt: confetti spawn ở world space, đứng yên tại vị trí point lúc spawn.")]
    [SerializeField] private bool parentToSpawnPoint = false;

    [Tooltip("Offset thêm theo trục Y (mét) so với spawn point. Tăng lên nếu muốn confetti bắt đầu " +
             "từ trên cao hơn rồi rơi xuống. Để 0 nếu prefab đã tự canh độ cao.")]
    [SerializeField] private float extraHeightOffset = 0f;

    [Header("Timing")]
    [Tooltip("Tự spawn confetti ngay khi vào scene (trong Start).")]
    [SerializeField] private bool playOnStart = true;

    [Tooltip("Delay (giây) trước khi spawn, tính từ lúc Start. Để 0 nếu muốn spawn ngay lập tức.")]
    [SerializeField] private float startDelay = 0f;

    [Header("Lifetime")]
    [Tooltip("Tự hủy các instance confetti sau ngần này giây (để dọn rác). " +
             "Đặt <= 0 nếu prefab tự có Stop Action = Destroy hoặc bạn muốn tự quản lý.")]
    [SerializeField] private float autoDestroyAfter = 8f;

    [Tooltip("Cho phép phát lại nhiều lần. Nếu tắt: gọi PlayConfetti() lần 2 trở đi sẽ bị bỏ qua.")]
    [SerializeField] private bool allowReplay = true;

    // Giữ tham chiếu các instance đang sống để có thể Stop/Clear khi cần.
    private readonly List<GameObject> _activeInstances = new List<GameObject>();
    private bool _hasPlayed = false;

    private void Start()
    {
        if (playOnStart)
        {
            if (startDelay > 0f)
                StartCoroutine(PlayAfterDelay(startDelay));
            else
                PlayConfetti();
        }
    }

    private IEnumerator PlayAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        PlayConfetti();
    }

    /// <summary>
    /// Spawn confetti tại tất cả spawn points. Có thể gọi từ script khác nếu muốn trigger tay
    /// (vd sau khi cutscene xong) thay vì tự chạy lúc Start.
    /// </summary>
    public void PlayConfetti()
    {
        if (!allowReplay && _hasPlayed)
        {
            Debug.Log("[KingChairConfettiVFX] Da phat roi va allowReplay=false -> bo qua.");
            return;
        }

        if (confettiPrefab == null)
        {
            Debug.LogError("[KingChairConfettiVFX] confettiPrefab chua duoc gan trong Inspector!");
            return;
        }

        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogError("[KingChairConfettiVFX] spawnPoints chua duoc gan (can it nhat 2 diem 2 ben ghe).");
            return;
        }

        _hasPlayed = true;

        int spawned = 0;
        foreach (var point in spawnPoints)
        {
            if (point == null) continue;

            Vector3 pos = point.position + Vector3.up * extraHeightOffset;
            Quaternion rot = point.rotation;

            GameObject fx = parentToSpawnPoint
                ? Instantiate(confettiPrefab, pos, rot, point)
                : Instantiate(confettiPrefab, pos, rot);

            // Bao dam ParticleSystem chay tu dau (phong khi prefab de Play On Awake = false).
            var systems = fx.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in systems)
                ps.Play(true);

            _activeInstances.Add(fx);
            spawned++;

            if (autoDestroyAfter > 0f)
                Destroy(fx, autoDestroyAfter);
        }

        Debug.Log($"[KingChairConfettiVFX] Da spawn confetti tai {spawned} diem.");
    }

    /// <summary>
    /// Dừng phát và dọn ngay các instance confetti đang sống (nếu cần tắt gấp).
    /// </summary>
    public void StopConfetti()
    {
        foreach (var fx in _activeInstances)
        {
            if (fx == null) continue;

            var systems = fx.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in systems)
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            Destroy(fx);
        }
        _activeInstances.Clear();
    }

    private void OnDestroy()
    {
        _activeInstances.Clear();
    }
}