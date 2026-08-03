using UnityEngine;

namespace RhythmGame
{
    /// <summary>
    /// Một note trên màn hình. Prefab cần: RectTransform + Image.
    /// Vị trí được TÍNH LẠI mỗi frame từ thời gian, không cộng dồn Translate,
    /// nên dù frame drop note vẫn nằm đúng chỗ.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class NoteView : MonoBehaviour
    {
        public int Lane { get; private set; }
        public double TargetTime { get; private set; }

        private RectTransform rt;

        private void Awake()
        {
            rt = (RectTransform)transform;
        }

        public void Setup(int lane, double targetTime, RectTransform parent)
        {
            if (rt == null) rt = (RectTransform)transform;
            Lane = lane;
            TargetTime = targetTime;
            rt.SetParent(parent, false);
            rt.anchoredPosition = new Vector2(9999f, 0f); // đẩy ra ngoài cho tới frame vẽ đầu tiên
            gameObject.SetActive(true);
        }

        /// <summary>x = vị trí hitline + (thời gian còn lại) * tốc độ.</summary>
        public void Redraw(double visualSongPos, float pixelsPerSecond, float hitLineX)
        {
            float x = hitLineX + (float)(TargetTime - visualSongPos) * pixelsPerSecond;
            Vector2 p = rt.anchoredPosition;
            p.x = x;
            p.y = 0f;
            rt.anchoredPosition = p;
        }

        public void Recycle()
        {
            gameObject.SetActive(false);
        }
    }
}
