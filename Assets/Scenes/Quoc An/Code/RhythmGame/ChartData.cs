using System;
using System.Collections.Generic;

namespace RhythmGame
{
    /// <summary>Một note trong beatmap.</summary>
    [Serializable]
    public class NoteEntry
    {
        /// <summary>Thời điểm note phải được ấn (đầu note), tính bằng GIÂY từ lúc bài bắt đầu.</summary>
        public float time;

        /// <summary>Lane 0..3 (bản lane cũ) hoặc hướng 0=trái/1=phải (bản hình thoi).</summary>
        public int lane;

        /// <summary>
        /// Thời gian GIỮ, tính bằng giây. 0 = note thường (chỉ ấn một cái).
        /// > 0 = hold note: ấn đầu, giữ suốt duration, thả ở cuối.
        /// </summary>
        public float duration = 0f;

        /// <summary>
        /// 0 = note thường / hold một hướng (dùng lane để biết trái/phải).
        /// 1 = note ĐÔI: ấn giữ CẢ hai hướng cùng lúc (lane bị bỏ qua).
        /// </summary>
        public int type = 0;
    }

    /// <summary>Toàn bộ beatmap của một bài. Lưu/đọc bằng JsonUtility.</summary>
    [Serializable]
    public class ChartData
    {
        public string songName = "Untitled";

        /// <summary>BPM của bài. Dùng cho việc snap note về lưới nhịp khi thu.</summary>
        public float bpm = 120f;

        /// <summary>
        /// Thời điểm (giây) của beat đầu tiên tính từ đầu file audio.
        /// Hầu như bài nào cũng có một khoảng im lặng ở đầu -> giá trị này khác 0.
        /// </summary>
        public float firstBeatOffset = 0f;

        public List<NoteEntry> notes = new List<NoteEntry>();
    }
}