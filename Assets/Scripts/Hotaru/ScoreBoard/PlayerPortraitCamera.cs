using Fusion;
using UnityEngine;

/// <summary>
/// Gắn vào Player prefab.
/// Tạo một Camera nhỏ chiếu vào model 3D của player, render ra RenderTexture.
/// ScoreboardEntry sẽ đọc Texture này để hiển thị trên UI.
/// 
/// Setup trong Unity:
/// 1. Tạo layer mới tên "PlayerPortrait" (hoặc đặt tên khác, nhớ update portraitLayer)
/// 2. Gắn script này vào Player prefab
/// 3. Đặt portraitCameraOffset phù hợp với chiều cao nhân vật
/// </summary>
public class PlayerPortraitCamera : NetworkBehaviour
{
    [Header("Portrait Camera Settings")]
    [Tooltip("Layer name dành riêng cho portrait. Tạo layer này trong Unity Tags & Layers.")]
    [SerializeField] private string portraitLayerName = "PlayerPortrait";

    [Tooltip("Offset của camera so với vị trí player (world space relative)")]
    [SerializeField] private Vector3 cameraOffset = new Vector3(0f, 1.2f, 2.5f);

    [Tooltip("Camera nhìn vào điểm này trên player")]
    [SerializeField] private Vector3 lookTargetOffset = new Vector3(0f, 1f, 0f);

    [Tooltip("Kích thước RenderTexture")]
    [SerializeField] private int textureSize = 256;

    [Header("Lighting")]
    [SerializeField] private bool addPortraitLight = true;
    [SerializeField] private Color lightColor = new Color(1f, 0.95f, 0.9f);
    [SerializeField] private float lightIntensity = 1.2f;

    // Public để ScoreboardEntry có thể lấy texture
    public RenderTexture PortraitTexture { get; private set; }

    private Camera _portraitCamera;
    private Light _portraitLight;
    private int _portraitLayer = -1;
    private bool _isSetup = false;

    public override void Spawned()
    {
        // Chỉ setup camera cho local player để tiết kiệm resource
        // Remote players sẽ được setup khi ScoreboardEntry request
        SetupPortraitCamera();
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        Cleanup();
    }

    private void OnDestroy()
    {
        Cleanup();
    }

    /// <summary>
    /// Setup portrait camera và render texture
    /// </summary>
    public void SetupPortraitCamera()
    {
        if (_isSetup) return;

        // Lấy layer index
        _portraitLayer = LayerMask.NameToLayer(portraitLayerName);
        if (_portraitLayer == -1)
        {
            Debug.LogWarning($"[PlayerPortraitCamera] Layer '{portraitLayerName}' không tồn tại! " +
                             "Tạo layer này trong Edit > Project Settings > Tags and Layers. " +
                             "Portrait sẽ dùng layer Default.");
            _portraitLayer = 0;
        }

        // Tạo RenderTexture
        PortraitTexture = new RenderTexture(textureSize, textureSize, 16, RenderTextureFormat.ARGB32);
        PortraitTexture.name = $"PlayerPortrait_{gameObject.name}";
        PortraitTexture.antiAliasing = 2;
        PortraitTexture.Create();

        // Tạo Camera GameObject
        var camGO = new GameObject($"PortraitCam_{gameObject.name}");
        camGO.transform.SetParent(transform);

        // Đặt vị trí camera (phía trước và cao hơn player)
        camGO.transform.localPosition = cameraOffset;

        // Nhìn vào đầu player
        Vector3 lookTarget = transform.position + lookTargetOffset;
        camGO.transform.LookAt(lookTarget);

        // QUAN TRỌNG: KHÔNG đặt tag "MainCamera" — tránh CameraManager.ReinitializeCamera()
        // nhầm portrait camera là main camera khi gọi Camera.main
        // Tag mặc định là "Untagged" là đúng, không cần thay đổi
        camGO.tag = "Untagged";

        // Setup Camera component
        _portraitCamera = camGO.AddComponent<Camera>();
        _portraitCamera.targetTexture = PortraitTexture;
        _portraitCamera.clearFlags = CameraClearFlags.SolidColor;
        _portraitCamera.backgroundColor = new Color(0, 0, 0, 0); // Transparent
        _portraitCamera.fieldOfView = 40f;
        _portraitCamera.nearClipPlane = 0.1f;
        _portraitCamera.farClipPlane = 10f;

        // depth = -10: render trước main camera (thường depth=0)
        // Camera.main chỉ lấy camera có tag "MainCamera" nên portrait camera này
        // sẽ KHÔNG BAO GIỜ bị CameraManager.ReinitializeCamera() nhầm
        _portraitCamera.depth = -10;

        // Chỉ render layer của player này (tránh thấy player khác)
        if (_portraitLayer != 0)
        {
            _portraitCamera.cullingMask = 1 << _portraitLayer;
            // Đặt model của player vào portrait layer
            SetModelLayer(_portraitLayer);
        }
        else
        {
            // Fallback: render tất cả nhưng chỉ render default layer
            _portraitCamera.cullingMask = LayerMask.GetMask("Default");
        }

        // Thêm ánh sáng riêng cho portrait
        if (addPortraitLight)
        {
            var lightGO = new GameObject($"PortraitLight_{gameObject.name}");
            lightGO.transform.SetParent(camGO.transform);
            lightGO.transform.localPosition = Vector3.zero;

            _portraitLight = lightGO.AddComponent<Light>();
            _portraitLight.type = LightType.Directional;
            _portraitLight.color = lightColor;
            _portraitLight.intensity = lightIntensity;
            _portraitLight.cullingMask = _portraitCamera.cullingMask;
        }

        _isSetup = true;
        Debug.Log($"[PlayerPortraitCamera] Setup complete for {gameObject.name}. Layer: {_portraitLayer}");
    }

    /// <summary>
    /// Đặt model vào portrait layer để camera culling hoạt động đúng
    /// </summary>
    private void SetModelLayer(int layer)
    {
        var modelSwitcher = GetComponent<PlayerModelSwitcher>();
        if (modelSwitcher != null)
        {
            modelSwitcher.SetModelLayer(layer);
            // Lưu lại layer để áp dụng lại nếu model đổi (CharacterIndex changed)
            _appliedLayer = layer;
        }
    }

    // Layer đang được áp dụng (để reapply khi model thay đổi)
    private int _appliedLayer = -1;

    /// <summary>
    /// Gọi từ PlayerNetworkData.OnCharacterIndexChanged nếu cần reapply layer
    /// </summary>
    public void ReapplyModelLayer()
    {
        if (_appliedLayer >= 0)
            SetModelLayer(_appliedLayer);
    }

    /// <summary>
    /// Bật/tắt portrait camera (tắt khi không dùng để tiết kiệm)
    /// </summary>
    public void SetPortraitActive(bool active)
    {
        if (!_isSetup) SetupPortraitCamera();

        if (_portraitCamera != null)
            _portraitCamera.enabled = active;

        if (_portraitLight != null)
            _portraitLight.enabled = active;
    }

    private void Cleanup()
    {
        if (PortraitTexture != null)
        {
            PortraitTexture.Release();
            Destroy(PortraitTexture);
            PortraitTexture = null;
        }

        if (_portraitCamera != null)
        {
            Destroy(_portraitCamera.gameObject);
            _portraitCamera = null;
        }

        _isSetup = false;
    }

    /// <summary>
    /// Update camera position để luôn nhìn vào player (gọi nếu player xoay)
    /// </summary>
    private void LateUpdate()
    {
        if (!_isSetup || _portraitCamera == null) return;

        // Camera theo player rotation (luôn nhìn vào mặt trước)
        Vector3 lookTarget = transform.position + lookTargetOffset;
        _portraitCamera.transform.LookAt(lookTarget);
    }
}