using UnityEngine;

/// <summary>
/// 1 object đường trong maze MG6 (VD 1 đoạn path/tile).
/// Cơ chế hiện/ẩn đơn giản: bật/tắt Renderer theo fade input.
/// KHÔNG đụng vào Collider nên player vẫn va chạm/đi qua bình thường.
/// </summary>
public class MG6PathSegment : MonoBehaviour
{
    [Tooltip("Renderer(s) sẽ hiện/ẩn. Nếu để trống sẽ tự lấy tất cả Renderer trong children.")]
    [SerializeField] private Renderer[] _renderers;

    [Tooltip("Ngưỡng để coi segment là visible. fade >= ngưỡng thì hiện Renderer.")]
    [SerializeField, Range(0f, 1f)] private float _visibleThreshold = 0.5f;

    private bool _isVisible;

    private void Awake()
    {
        EnsureInitialized();
    }

    private void OnEnable()
    {
        EnsureInitialized();
    }

    private void EnsureInitialized()
    {
        if (_renderers == null || _renderers.Length == 0)
            _renderers = GetComponentsInChildren<Renderer>(true);
    }

    /// <summary>
    /// fade: 0 = ẩn, 1 = hiện. Giá trị trung gian dùng để quyết định bật/tắt theo ngưỡng.
    /// </summary>
    public void SetFade(float fade)
    {
        EnsureInitialized();

        fade = Mathf.Clamp01(fade);

        bool shouldBeVisible = fade >= _visibleThreshold;
        if (shouldBeVisible == _isVisible) return;
        _isVisible = shouldBeVisible;

        for (int i = 0; i < _renderers.Length; i++)
        {
            var r = _renderers[i];
            if (r == null) continue;
            r.enabled = _isVisible;
        }
    }
}