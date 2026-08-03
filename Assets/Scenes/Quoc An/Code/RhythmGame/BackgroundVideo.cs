using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace RhythmGame
{
    /// <summary>
    /// Video nền chạy loop, vẽ qua RenderTexture lên một RawImage nằm dưới cùng Canvas.
    ///
    /// CỰC KỲ QUAN TRỌNG: audioOutputMode phải là None.
    /// Nếu để video phát tiếng, Unity sẽ trộn thêm một luồng audio nữa và bài nhạc chính
    /// có thể bị lệch pha, phá nát toàn bộ timing của game.
    /// </summary>
    public class BackgroundVideo : MonoBehaviour
    {
        [Header("Tham chiếu")]
        public VideoPlayer videoPlayer;

        [Tooltip("RawImage phủ toàn màn hình, đặt ở vị trí ĐẦU TIÊN trong Canvas để nằm dưới cùng.")]
        public RawImage targetImage;

        [Tooltip("Tạo bằng Assets > Create > Render Texture. Kích thước nên bằng video, ví dụ 1920x1080.")]
        public RenderTexture renderTexture;

        [Header("Tuỳ chọn")]
        [Range(0f, 1f)]
        [Tooltip("Làm tối video để note dễ nhìn. 0.35 - 0.6 là hợp lý.")]
        public float darkenAmount = 0.45f;

        [Tooltip("Bỏ frame khi máy chậm, để video không kéo tụt framerate gameplay. Luôn nên bật.")]
        public bool skipOnDrop = true;

        [Tooltip("Tối đa chờ bao lâu cho video decode xong trước khi bỏ qua.")]
        public float prepareTimeout = 10f;

        private void Awake()
        {
            if (videoPlayer == null) videoPlayer = GetComponent<VideoPlayer>();
            if (videoPlayer == null)
            {
                Debug.LogError("[BackgroundVideo] Chưa gán VideoPlayer.");
                return;
            }

            videoPlayer.playOnAwake = false;
            videoPlayer.isLooping = true;
            videoPlayer.skipOnDrop = skipOnDrop;
            videoPlayer.audioOutputMode = VideoAudioOutputMode.None; // đừng bao giờ đổi dòng này
            videoPlayer.renderMode = VideoRenderMode.RenderTexture;
            videoPlayer.targetTexture = renderTexture;

            if (targetImage != null)
            {
                targetImage.texture = renderTexture;
                targetImage.color = new Color(1f - darkenAmount, 1f - darkenAmount, 1f - darkenAmount, 1f);
                targetImage.raycastTarget = false;
            }

            ClearRenderTexture();
        }

        /// <summary>Decode sẵn rồi mới phát. Gọi bằng yield return từ coroutine.</summary>
        public IEnumerator PrepareAndPlay()
        {
            if (videoPlayer == null) yield break;

            videoPlayer.Prepare();

            float waited = 0f;
            while (!videoPlayer.isPrepared && waited < prepareTimeout)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            if (!videoPlayer.isPrepared)
            {
                Debug.LogWarning("[BackgroundVideo] Video decode quá lâu, bỏ qua và chơi không có nền.");
                yield break;
            }

            videoPlayer.Play();

            // Chờ thêm 1 frame cho frame đầu tiên kịp vẽ vào RenderTexture,
            // tránh nhìn thấy một khung hình rác.
            yield return null;
        }

        /// <summary>Xoá RenderTexture về đen, tránh hiện rác của lần chạy trước.</summary>
        private void ClearRenderTexture()
        {
            if (renderTexture == null) return;
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = renderTexture;
            GL.Clear(true, true, Color.black);
            RenderTexture.active = prev;
        }

        private void OnDisable()
        {
            if (videoPlayer != null) videoPlayer.Stop();
        }
    }
}
