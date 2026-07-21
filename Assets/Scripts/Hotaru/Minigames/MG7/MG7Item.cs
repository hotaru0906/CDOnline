using Fusion;
using UnityEngine;

/// <summary>
/// MG7 — Item rơi từ trên cao xuống đất, player nhặt bằng cách đi vào bán kính.
///
/// Rơi: mô phỏng gravity thủ công (không dùng NetworkRigidbody) — host tính vị trí,
/// đồng bộ qua NetworkPosition, client chỉ đọc và set transform trong Render().
///
/// Timer:
///   - Bomb: đếm fuse ngay từ lúc spawn (không cần chờ chạm đất) → hết giờ = nổ.
///   - Các loại khác: sau khi chạm đất mới bắt đầu đếm despawn; hết giờ chưa nhặt = biến mất.
///
/// Pickup: host OverlapSphere quanh vị trí hiện tại mỗi tick khi đã landed (trừ Bomb).
/// </summary>
public class MG7Item : NetworkBehaviour
{
    [Header("Type")]
    [SerializeField] private MG7ItemType itemType;

    [Header("Falling")]
    [SerializeField] private float gravity = 9.8f;
    [SerializeField] private float groundCheckDistance = 100f;
    [SerializeField] private LayerMask groundLayerMask = ~0;

    [Header("Pickup")]
    [SerializeField] private float pickupRadius = 1.2f;
    [SerializeField] private float despawnAfterLanding = 8f; // giây — không áp dụng cho Bomb

    [Header("Score Amount (chỉ dùng cho loại Score*)")]
    [SerializeField] private int scoreAmount = 1;

    [Header("Bomb Settings")]
    [SerializeField] private float bombFuseTime = 5f;
    [SerializeField] private float bombExplosionRadius = 3f;
    [SerializeField] private int bombScorePenalty = 5;

    [Header("Boost Settings")]
    [SerializeField] private float boostSpeedMultiplier = 1.6f;
    [SerializeField] private float boostDuration = 4f;

    [Header("Freeze Settings")]
    [SerializeField] private float freezeDuration = 2f;

    [Header("Visual")]
    [SerializeField] private GameObject visualMesh;
    [SerializeField] private ParticleSystem pickupVFX;
    [SerializeField] private ParticleSystem explosionVFX;
    [SerializeField] private AudioSource pickupOrExplodeAudio;

    // ----------------------------------------------------------------
    //  Networked State
    // ----------------------------------------------------------------

    [Networked, OnChangedRender(nameof(OnPositionChanged))]
    public Vector3 NetworkPosition { get; private set; }

    [Networked] private float VerticalVelocity { get; set; }
    [Networked] public NetworkBool HasLanded { get; private set; }
    [Networked] private NetworkBool Consumed { get; set; }
    [Networked] private float LifeTimer { get; set; } // fuse (Bomb) hoặc despawn countdown (others)

    // ----------------------------------------------------------------
    //  Lifecycle
    // ----------------------------------------------------------------

    public override void Spawned()
    {
        NetworkPosition = transform.position;

        if (HasStateAuthority && itemType == MG7ItemType.Bomb)
            LifeTimer = bombFuseTime;
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority || Consumed) return;

        // Bomb: fuse chạy ngay từ lúc spawn, độc lập với việc rơi/landed
        if (itemType == MG7ItemType.Bomb)
        {
            SimulateFallIfNeeded();

            LifeTimer -= Runner.DeltaTime;
            if (LifeTimer <= 0f)
                Explode();

            return;
        }

        if (!HasLanded)
        {
            SimulateFallIfNeeded();
            return;
        }

        LifeTimer -= Runner.DeltaTime;
        if (LifeTimer <= 0f)
        {
            DespawnUnpicked();
            return;
        }

        CheckPickup();
    }

    public override void Render()
    {
        transform.position = NetworkPosition;
    }

    // ----------------------------------------------------------------
    //  Falling
    // ----------------------------------------------------------------

    private void SimulateFallIfNeeded()
    {
        if (HasLanded) return;

        VerticalVelocity -= gravity * Runner.DeltaTime;
        Vector3 pos = NetworkPosition + Vector3.up * (VerticalVelocity * Runner.DeltaTime);

        if (Physics.Raycast(NetworkPosition + Vector3.up * 0.1f, Vector3.down,
                out RaycastHit hit, groundCheckDistance, groundLayerMask))
        {
            if (pos.y <= hit.point.y)
            {
                pos.y = hit.point.y;
                HasLanded = true;
                VerticalVelocity = 0f;

                // Bomb không dùng despawn timer sau landing (đã có fuse riêng từ spawn)
                if (itemType != MG7ItemType.Bomb)
                    LifeTimer = despawnAfterLanding;
            }
        }

        NetworkPosition = pos;
    }

    // ----------------------------------------------------------------
    //  Pickup
    // ----------------------------------------------------------------

    private void CheckPickup()
    {
        Collider[] hits = Physics.OverlapSphere(NetworkPosition, pickupRadius);

        foreach (var col in hits)
        {
            var pc = col.GetComponent<PlayerController>();
            if (pc == null) continue;

            var data = pc.GetComponent<PlayerMinigameData>();
            if (data == null || data.IsEliminated) continue;

            ApplyPickupEffect(pc, data);
            Consumed = true;
            RPC_PlayPickupVFX();
            Runner.Despawn(Object);
            return; // chỉ 1 người nhặt được, ai chạm trước tính người đó
        }
    }

    private void ApplyPickupEffect(PlayerController pc, PlayerMinigameData data)
    {
        switch (itemType)
        {
            case MG7ItemType.ScoreSmall:
            case MG7ItemType.ScoreMedium:
            case MG7ItemType.ScoreLarge:
                data.AddScore(scoreAmount);
                Debug.Log($"[MG7Item] P{pc.Object.InputAuthority} nhặt {itemType} +{scoreAmount}");
                break;

            case MG7ItemType.Boost:
                // Yêu cầu PlayerController có method ApplySpeedBoost(multiplier, duration) — xem ghi chú patch bên dưới.
                pc.ApplySpeedBoost(boostSpeedMultiplier, boostDuration);
                Debug.Log($"[MG7Item] P{pc.Object.InputAuthority} nhặt Boost x{boostSpeedMultiplier} trong {boostDuration}s");
                break;

            case MG7ItemType.Freeze:
                // Yêu cầu PlayerController có method ApplyTemporaryFreeze(duration) — xem ghi chú patch bên dưới.
                pc.ApplyTemporaryFreeze(freezeDuration);
                Debug.Log($"[MG7Item] P{pc.Object.InputAuthority} nhặt Freeze — tự đóng băng {freezeDuration}s");
                break;
        }
    }

    // ----------------------------------------------------------------
    //  Bomb Explosion
    // ----------------------------------------------------------------

    private void Explode()
    {
        Consumed = true;

        Collider[] hits = Physics.OverlapSphere(NetworkPosition, bombExplosionRadius);
        foreach (var col in hits)
        {
            var pc = col.GetComponent<PlayerController>();
            if (pc == null) continue;

            var data = pc.GetComponent<PlayerMinigameData>();
            if (data == null || data.IsEliminated) continue;

            data.AddScore(-bombScorePenalty);
            Debug.Log($"[MG7Item] Bomb nổ trúng P{pc.Object.InputAuthority} -{bombScorePenalty}");
        }

        RPC_PlayExplosionVFX();
        Runner.Despawn(Object);
    }

    private void DespawnUnpicked()
    {
        Consumed = true;
        Runner.Despawn(Object);
    }

    // ----------------------------------------------------------------
    //  Visual callbacks
    // ----------------------------------------------------------------

    private void OnPositionChanged()
    {
        // Render() đã set transform mỗi frame nên không bắt buộc xử lý gì thêm ở đây.
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayPickupVFX()
    {
        if (visualMesh != null) visualMesh.SetActive(false);
        if (pickupVFX != null) pickupVFX.Play();
        if (pickupOrExplodeAudio != null) pickupOrExplodeAudio.Play();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayExplosionVFX()
    {
        if (visualMesh != null) visualMesh.SetActive(false);
        if (explosionVFX != null) explosionVFX.Play();
        if (pickupOrExplodeAudio != null) pickupOrExplodeAudio.Play();
    }
}
