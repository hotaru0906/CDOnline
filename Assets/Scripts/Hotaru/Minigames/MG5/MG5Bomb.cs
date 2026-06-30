using Fusion;
using UnityEngine;

public class MG5Bomb : NetworkBehaviour
{
    public static MG5Bomb Instance { get; private set; }

    [Header("References")]
    [SerializeField] private GameObject bombMesh;
    [SerializeField] private Light bombLight;
    [SerializeField] private ParticleSystem explosionVFX;

    [Header("Light Settings")]
    [SerializeField] private float baseIntensity = 1f;
    [SerializeField] private float maxBlinkIntensity = 5f;
    [SerializeField] private float blinkThreshold = 7f;
    [SerializeField] private float blinkSpeedMin = 2f;
    [SerializeField] private float blinkSpeedMax = 15f;

    [Header("Attach Settings")]
    [SerializeField] private Vector3 attachOffset = new Vector3(0f, 2.2f, 0f);

    [Networked, OnChangedRender(nameof(OnVisibleChanged))]
    public NetworkBool IsVisible { get; private set; }

    private Transform _attachTarget;
    private float _displayTimer;
    private bool _lastVisible;

    // ----------------------------------------------------------------
    //  Lifecycle
    // ----------------------------------------------------------------

    public override void Spawned()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        _lastVisible = IsVisible;
        ApplyVisible(IsVisible);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public override void Render()
    {
        Debug.Log($"[MG5Bomb] Render — IsVisible={IsVisible}, HasStateAuthority={HasStateAuthority}, Object.IsValid={Object.IsValid}");
        if (_attachTarget != null)
            transform.position = _attachTarget.position + attachOffset;

        // Luôn check mỗi frame thay vì chỉ dựa vào OnChangedRender
        if (_lastVisible != IsVisible)
        {
            _lastVisible = IsVisible;
            ApplyVisible(IsVisible);
        }

        UpdateBlinkLight();
    }

    // ----------------------------------------------------------------
    //  Public API
    // ----------------------------------------------------------------

    public void AttachToPlayer(Transform playerTransform)
    {
        _attachTarget = playerTransform;
        if (playerTransform != null)
            transform.position = playerTransform.position + attachOffset;
    }

    public void Detach()
    {
        _attachTarget = null;
    }

    public void SetDisplayTimer(float timer)
    {
        _displayTimer = timer;
    }

    // Chỉ host gọi — replicate xuống clients qua OnVisibleChanged
    public void SetVisible(bool visible)
    {
        if (!HasStateAuthority) return;
        IsVisible = visible;
        ApplyVisible(visible); // apply ngay trên host, không chờ replication
    }

    public void PlayExplosion()
    {
        if (!HasStateAuthority) return;
        RPC_PlayExplosion();
    }

    // ----------------------------------------------------------------
    //  Visual
    // ----------------------------------------------------------------

    // Chạy trên clients khi IsVisible thay đổi qua network
    private void OnVisibleChanged()
    {
        _lastVisible = IsVisible;
        ApplyVisible(IsVisible);
    }

    private void ApplyVisible(bool visible)
    {
        if (bombMesh != null) bombMesh.SetActive(visible);
        if (bombLight != null) bombLight.enabled = visible;
    }

    private void UpdateBlinkLight()
    {
        if (bombLight == null || !IsVisible) return;

        if (_displayTimer <= 0f || _displayTimer > blinkThreshold)
        {
            bombLight.color = Color.red;
            bombLight.intensity = baseIntensity;
            return;
        }

        float dangerRatio = 1f - (_displayTimer / blinkThreshold);
        float blinkSpeed = Mathf.Lerp(blinkSpeedMin, blinkSpeedMax, dangerRatio);
        float intensity = Mathf.PingPong(Time.time * blinkSpeed, maxBlinkIntensity);

        bombLight.color = Color.red;
        bombLight.intensity = intensity;
    }

    // ----------------------------------------------------------------
    //  RPC
    // ----------------------------------------------------------------

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayExplosion()
    {
        if (bombMesh != null) bombMesh.SetActive(false);
        if (bombLight != null) bombLight.enabled = false;
        if (explosionVFX != null) explosionVFX.Play();
        Debug.Log("[MG5Bomb] BOOM!");
    }
}