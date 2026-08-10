using UnityEngine;

namespace RhythmGame
{
    /// <summary>
    /// Hình ảnh của một HOLD NOTE trên MỘT cạnh hình thoi:
    ///   - Head: mũi tên đầu (thời điểm bắt đầu ấn).
    ///   - Body: thanh chữ nhật kéo dài giữa đầu và đuôi, co giãn khi note trượt.
    ///   - Tail: mũi tên đuôi (thời điểm thả). Có thể để trống.
    ///
    /// Prefab: root RectTransform, con Head (Image mũi tên), Body (Image chữ nhật),
    /// Tail (Image mũi tên, tuỳ chọn). Kéo ba cái vào script.
    ///
    /// Body dùng một Image trắng tô màu là đủ, không cần vẽ sprite riêng.
    /// </summary>
    public class DiamondHoldNoteView : MonoBehaviour
    {
        [SerializeField] private RectTransform head;
        [SerializeField] private RectTransform body;
        [SerializeField] private RectTransform tail;

        public int Side { get; private set; }        // 0 = trái, 1 = phải
        public double HeadTime { get; private set; }
        public double TailTime { get; private set; }

        /// <summary>
        /// Khi đang GIỮ, đầu note ghim tại đích, chỉ đuôi trôi tới -> thân ngắn dần.
        /// Playfield bật cờ này khi bắt đầu giữ.
        /// </summary>
        public bool Holding { get; set; }

        private RectTransform _rt;

        private void Awake() => _rt = (RectTransform)transform;

        public void Setup(int side, double headTime, double tailTime, RectTransform parent)
        {
            if (_rt == null) _rt = (RectTransform)transform;
            Side = side;
            HeadTime = headTime;
            TailTime = tailTime;

            _rt.SetParent(parent, false);
            _rt.anchorMin = _rt.anchorMax = _rt.pivot = new Vector2(0.5f, 0.5f);
            _rt.anchoredPosition = Vector2.zero;
            _rt.localScale = Vector3.one;
            _rt.localRotation = Quaternion.identity;

            gameObject.SetActive(true);
        }

        /// <summary>
        /// Đặt đầu và đuôi theo thời gian, kéo giãn body nối hai điểm.
        /// top, sideVertex là anchoredPosition của đỉnh trên và đỉnh đích (trong container).
        /// </summary>
        public void Redraw(double songPos, float travelTime, Vector2 top, Vector2 sideVertex)
        {
            // Đầu note: bình thường trôi theo thời gian. Khi đang GIỮ thì ghim tại
            // đích (sideVertex) — vì người chơi đã bắt được đầu và đang giữ ở đó.
            Vector2 headPos = Holding
                ? sideVertex
                : PointAt(HeadTime, songPos, travelTime, top, sideVertex);

            // Đuôi luôn trôi theo thời gian. Kẹp không cho vượt quá đích, để khi
            // đuôi tới nơi thì thân co về 0 chứ không lố qua.
            Vector2 tailPos = PointAt(TailTime, songPos, travelTime, top, sideVertex);
            if (Holding)
            {
                float tailProgress = 1f - (float)(TailTime - songPos) / Mathf.Max(0.0001f, travelTime);
                if (tailProgress > 1f) tailPos = sideVertex; // đuôi đã tới đích
            }

            if (head != null) head.anchoredPosition = headPos;
            if (tail != null) tail.anchoredPosition = tailPos;

            if (body != null)
            {
                Vector2 mid = (headPos + tailPos) * 0.5f;
                Vector2 dir = headPos - tailPos;
                float len = dir.magnitude;
                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

                body.anchoredPosition = mid;
                body.localRotation = Quaternion.Euler(0f, 0f, angle);
                body.sizeDelta = new Vector2(len, body.sizeDelta.y);
            }
        }

        private static Vector2 PointAt(double t, double songPos, float travelTime,
                                       Vector2 top, Vector2 sideVertex)
        {
            float timeUntilHit = (float)(t - songPos);
            float progress = 1f - timeUntilHit / Mathf.Max(0.0001f, travelTime);
            return Vector2.LerpUnclamped(top, sideVertex, progress);
        }

        public void Recycle() => gameObject.SetActive(false);
    }
}