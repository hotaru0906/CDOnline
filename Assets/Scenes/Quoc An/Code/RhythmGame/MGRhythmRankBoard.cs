using System.Collections.Generic;
using UnityEngine;

namespace RhythmGame
{
    /// <summary>
    /// Bảng xếp hạng dọc, tự sắp lại theo ĐIỂM mỗi frame.
    /// Ô hạng 1 ở trên cùng và thò ra phải nhiều nhất; xuống dưới thụt dần vào
    /// -> tạo hình bậc thang xéo.
    ///
    /// Gắn vào object cha chứa 4 MGRhythmRankRow. Bộ này chỉ lo SẮP THỨ TỰ và
    /// ĐẶT VỊ TRÍ; nội dung từng ô do MGRhythmRankRow tự cập nhật.
    /// </summary>
    public class MGRhythmRankBoard : MonoBehaviour
    {
        [SerializeField] private MGRhythmRankRow[] rows = new MGRhythmRankRow[4];

        [Header("Bố cục bậc thang")]
        [Tooltip("Khoảng cách dọc giữa hai hạng TRONG cùng cụm (âm = đi xuống).")]
        [SerializeField] private float rowSpacingY = -130f;
        [Tooltip("Mỗi hạng thấp hơn thụt/thò ngang bao nhiêu (tạo hình xéo).")]
        [SerializeField] private float diagonalStepX = 40f;
        [Tooltip("Vị trí X của hạng 1.")]
        [SerializeField] private float topX = 0f;
        [Tooltip("Vị trí Y của hạng 1.")]
        [SerializeField] private float topY = 0f;

        [Header("Chia cụm trên/dưới")]
        [Tooltip("Bật: hạng 1-2 thành cụm TRÊN, hạng 3-4 thành cụm DƯỚI. Tắt: một cột dài.")]
        [SerializeField] private bool twoGroups = true;
        [Tooltip("Mỗi cụm có mấy hạng. 2 = 1&2 trên, 3&4 dưới.")]
        [SerializeField] private int rowsPerGroup = 2;
        [Tooltip("Cụm dưới cách cụm trên bao nhiêu theo Y (âm = xuống sâu).")]
        [SerializeField] private float groupGapY = -320f;
        [Tooltip("Cụm dưới lệch ngang bao nhiêu so với cụm trên.")]
        [SerializeField] private float groupOffsetX = 0f;

        [Header("Chuyển động")]
        [Tooltip("Ô trượt tới vị trí mới mượt cỡ nào. Lớn hơn = nhanh hơn.")]
        [SerializeField] private float moveSpeed = 8f;

        private bool _bound;
        // Danh sách tạm để sắp, tránh cấp phát mỗi frame.
        private readonly List<MGRhythmRankRow> _sorted = new(4);

        public void BindRows()
        {
            var ctrl = MGRhythmController.Instance;
            if (ctrl == null) return;

            int localLane = ctrl.GetLocalLane();
            for (int i = 0; i < rows.Length; i++)
            {
                if (rows[i] == null) continue;
                var state = ctrl.GetStateForLane(i);
                rows[i].Bind(state, isLocal: i == localLane);
            }
            _bound = true;
        }

        private void Update()
        {
            if (!_bound)
            {
                // Thử bind cho tới khi controller sẵn sàng (lane đã replicate).
                if (MGRhythmController.Instance != null &&
                    MGRhythmController.Instance.GetLocalLane() >= 0)
                    BindRows();
                return;
            }

            // 1) Sắp các ô CÓ NGƯỜI theo điểm giảm dần.
            _sorted.Clear();
            for (int i = 0; i < rows.Length; i++)
                if (rows[i] != null && rows[i].Occupied) _sorted.Add(rows[i]);

            _sorted.Sort((a, b) => b.Score.CompareTo(a.Score)); // điểm cao lên trên

            // 2) Đặt mỗi ô vào vị trí theo hạng (trượt mượt).
            for (int rank = 0; rank < _sorted.Count; rank++)
            {
                var row = _sorted[rank];
                Vector2 target = PositionForRank(rank);

                row.Rt.anchoredPosition = Vector2.Lerp(
                    row.Rt.anchoredPosition, target, moveSpeed * Time.deltaTime);

                row.SetRankLabel(rank + 1);
                row.Rt.SetSiblingIndex(rank);
            }
        }

        /// <summary>
        /// Vị trí của một hạng. Có twoGroups: hạng 0..(rowsPerGroup-1) là cụm trên,
        /// các hạng sau là cụm dưới (lệch xuống groupGapY, ngang groupOffsetX).
        /// </summary>
        private Vector2 PositionForRank(int rank)
        {
            if (!twoGroups || rowsPerGroup <= 0)
            {
                return new Vector2(topX + diagonalStepX * rank,
                                   topY + rowSpacingY * rank);
            }

            int group = rank / rowsPerGroup;        // 0 = cụm trên, 1 = cụm dưới
            int inGroup = rank % rowsPerGroup;       // vị trí trong cụm

            float x = topX + diagonalStepX * inGroup + groupOffsetX * group;
            float y = topY + rowSpacingY * inGroup + groupGapY * group;
            return new Vector2(x, y);
        }
    }
}