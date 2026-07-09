using Fusion;
using UnityEngine;

/// <summary>
/// MG7 — Crown visual object.
/// Đơn giản hơn MG5Bomb: không có blink/explosion, chỉ attach theo holder
/// và có thể thêm hiệu ứng tỏa sáng nhẹ (glow/rotate) qua VFX/Animator riêng.
/// </summary>
public class MG7Crown : NetworkBehaviour
{
    public static MG7Crown Instance { get; private set; }

    [Header("References")]
    [SerializeField] private GameObject crownMesh;
    [SerializeField] private ParticleSystem glowVFX; // optional — hiệu ứng tỏa sáng liên tục

    [Header("Attach Settings")]
    [SerializeField] private Vector3 attachOffset = new Vector3(0f, 2.4f, 0f);
    [SerializeField] private float rotateSpeed = 90f; // độ/giây — xoay nhẹ cho đẹp

    [Networked, OnChangedRender(nameof(OnVisibleChanged))]
    public NetworkBool IsVisible { get; private set; }

    private Transform _attachTarget;
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
        if (_attachTarget != null)
            transform.position = _attachTarget.position + attachOffset;

        if (_lastVisible != IsVisible)
        {
            _lastVisible = IsVisible;
            ApplyVisible(IsVisible);
        }

        if (IsVisible && rotateSpeed != 0f)
            transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime, Space.World);
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

    public void SetVisible(bool visible)
    {
        if (!HasStateAuthority) return;
        IsVisible = visible;
        ApplyVisible(visible);
    }

    // ----------------------------------------------------------------
    //  Visual
    // ----------------------------------------------------------------

    private void OnVisibleChanged()
    {
        _lastVisible = IsVisible;
        ApplyVisible(IsVisible);
    }

    private void ApplyVisible(bool visible)
    {
        if (crownMesh != null) crownMesh.SetActive(visible);

        if (glowVFX != null)
        {
            if (visible) glowVFX.Play();
            else glowVFX.Stop();
        }
    }
}