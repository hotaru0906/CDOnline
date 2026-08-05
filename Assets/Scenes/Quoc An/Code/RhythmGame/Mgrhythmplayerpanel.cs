using UnityEngine;
using UnityEngine.UI;

namespace RhythmGame
{
    /// <summary>
    /// Một ô trong hàng 4 người ở ĐÁY màn hình: icon + tên + thanh HP.
    /// Hiện trên màn của MỌI người chơi (ai cũng theo dõi được cả 4).
    /// Bind với một MGRhythmPlayerState; HP đọc thẳng từ PlayerMinigameData (đã networked).
    /// </summary>
    public class MGRhythmPlayerPanel : MonoBehaviour
    {
        [SerializeField] private CharacterIconDatabase iconDatabase;

        [Header("UI")]
        [SerializeField] private Image characterIcon;
        [SerializeField] private Text nameText;   // đổi thành TMP_Text nếu dùng TextMeshPro
        [SerializeField] private Image hpFill;     // Image Type = Filled, Horizontal, Left
        [SerializeField] private Image frame;      // viền, đổi màu theo themeColor (tuỳ chọn)
        [SerializeField] private GameObject localMarker;   // đánh dấu "đây là bạn"
        [SerializeField] private GameObject feverBurstVfx; // nháy khi người này nổ fever
        [SerializeField] private CanvasGroup rootGroup;

        [SerializeField, Tooltip("Phải khớp startHP của MGRhythmController.")]
        private float maxHP = 100f;

        private MGRhythmPlayerState _state;

        public void Bind(MGRhythmPlayerState state, bool isLocal)
        {
            _state = state;

            bool occupied = state != null;
            if (rootGroup != null) rootGroup.alpha = occupied ? 1f : 0f;
            if (localMarker != null) localMarker.SetActive(isLocal && occupied);
            if (!occupied) return;

            var net = state.NetData;
            if (net != null)
            {
                if (characterIcon != null && iconDatabase != null)
                {
                    characterIcon.sprite = iconDatabase.GetIcon(net.CharacterIndex);
                    characterIcon.enabled = characterIcon.sprite != null;
                }
                if (frame != null && iconDatabase != null)
                    frame.color = iconDatabase.GetThemeColor(net.CharacterIndex);
                if (nameText != null)
                    nameText.text = net.PlayerName.ToString();
            }

            state.OnFeverBurstRender += HandleBurst;
        }

        private void OnDestroy()
        {
            if (_state != null) _state.OnFeverBurstRender -= HandleBurst;
        }

        private void HandleBurst(MGRhythmPlayerState s) => PlayBurst();

        public void PlayBurst()
        {
            if (feverBurstVfx == null) return;
            feverBurstVfx.SetActive(false);
            feverBurstVfx.SetActive(true);
        }

        private void Update()
        {
            if (_state == null || hpFill == null) return;

            var mg = _state.MinigameData;
            if (mg == null) return;

            // HP là [Networked] nên đọc mỗi frame là đúng trên mọi máy.
            hpFill.fillAmount = Mathf.Clamp01(mg.HP / Mathf.Max(1f, maxHP));

            if (rootGroup != null)
                rootGroup.alpha = mg.IsEliminated ? 0.35f : 1f;
        }
    }
}