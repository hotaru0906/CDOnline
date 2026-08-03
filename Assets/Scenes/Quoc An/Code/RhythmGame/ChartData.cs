using System;
using System.Collections.Generic;

namespace RhythmGame
{
    /// <summary>Một note trong beatmap.</summary>
    [Serializable]
    public class NoteEntry
    {
        /// <summary>Thời điểm note phải được ấn, tính bằng GIÂY từ lúc bài hát bắt đầu.</summary>
        public float time;

        /// <summary>Lane 0..3, đếm từ TRÊN xuống DƯỚI trên màn hình.</summary>
        public int lane;
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
