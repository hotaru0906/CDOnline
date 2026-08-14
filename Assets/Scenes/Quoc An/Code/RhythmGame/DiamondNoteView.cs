using UnityEngine;

namespace RhythmGame
{
    /// <summary>
    /// Note chạy theo CẠNH HÌNH THOI: từ đỉnh trên xuống đỉnh trái (Side=0) hoặc
    /// đỉnh phải (Side=1). Vị trí nội suy tuyến tính giữa hai đỉnh theo thời gian,
    /// nên dù rớt frame note vẫn nằm đúng chỗ.
    ///
    /// Prefab cần: RectTransform + Image (hình mũi tên trái hoặc phải).
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class DiamondNoteView : MonoBehaviour
    {
        public int Side { get; private set; }        // 0 = trái, 1 = phải
        public double TargetTime { get; private set; }

        private RectTransform _rt;

        private void Awake() => _rt = (RectTransform)transform;

        public void Setup(int side, double targetTime, RectTransform parent)
        {
            if (_rt == null) _rt = (RectTransform)transform;
            Side = side;
            TargetTime = targetTime;
            _rt.SetParent(parent, false);

            // Ép anchor + pivot về TÂM, bất kể prefab được cấu hình thế nào.
            // Đây là chỗ note đỏ hay lệch: nếu sprite mũi tên phải có pivot khác
            // mũi tên trái, cùng một anchoredPosition sẽ hiển thị lệch nhau.
            // Ép ở đây thì hai bên luôn khớp track dù asset cấu hình khác.
            _rt.anchorMin = new Vector2(0.5f, 0.5f);
            _rt.anchorMax = new Vector2(0.5f, 0.5f);
            _rt.pivot = new Vector2(0.5f, 0.5f);
            _rt.localScale = Vector3.one;
            _rt.localRotation = Quaternion.identity;

            _rt.anchoredPosition = new Vector2(-9999f, 9999f); // giấu tới frame vẽ đầu
            gameObject.SetActive(true);
        }

        /// <summary>
        /// topVertex, sideVertex là anchoredPosition (toạ độ trong container) của
        /// đỉnh trên và đỉnh đích. progress 0 = ở đỉnh trên, 1 = chạm đích.
        /// </summary>
        public void Redraw(double songPos, float travelTime, Vector2 topVertex, Vector2 sideVertex)
        {
            float timeUntilHit = (float)(TargetTime - songPos);
            float progress = 1f - timeUntilHit / Mathf.Max(0.0001f, travelTime);
            _rt.anchoredPosition = Vector2.LerpUnclamped(topVertex, sideVertex, progress);
        }

        public void Recycle() => gameObject.SetActive(false);
    }
}