using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace RhythmGame
{
    /// <summary>
    /// Một ô trong bảng xếp hạng dọc: icon + tên + máu + combo + điểm.
    /// Bind với một MGRhythmPlayerState; đọc điểm/combo (đã networked) và HP.
    /// Vị trí ô do MGRhythmRankBoard đặt theo hạng, không tự đặt.
    /// </summary>
    public class MGRhythmRankRow : MonoBehaviour
    {
        [SerializeField] private CharacterIconDatabase iconDatabase;

        [Header("UI")]
        [SerializeField] private Image characterIcon;
        [SerializeField] private TMP_Text nameText;   // đổi TMP_Text nếu dùng TextMeshPro
        [SerializeField] private Image hpFill;     // Filled, Horizontal, Left
        [SerializeField] private TMP_Text comboText;
        [SerializeField] private TMP_Text scoreText;
        [SerializeField] private TMP_Text rankText;    // "1", "2"... (tuỳ chọn)
        [SerializeField] private Image frame;
        [SerializeField] private GameObject localMarker;
        [SerializeField] private CanvasGroup rootGroup;

        [SerializeField, Tooltip("Phải khớp startHP của MGRhythmController.")]
        private float maxHP = 100f;

        public RectTransform Rt { get; private set; }
        public MGRhythmPlayerState State { get; private set; }
        public int Score => State != null ? State.RhythmScore : -1;
        public bool Occupied => State != null;

        private void Awake() => Rt = (RectTransform)transform;

        public void Bind(MGRhythmPlayerState state, bool isLocal)
        {
            State = state;
            if (Rt == null) Rt = (RectTransform)transform;

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
                if (nameText != null) nameText.text = net.PlayerName.ToString();
            }
        }

        public void SetRankLabel(int rank)
        {
            if (rankText != null) rankText.text = rank.ToString();
        }

        private void Update()
        {
            if (State == null) return;

            if (scoreText != null) scoreText.text = State.RhythmScore.ToString("N0");
            if (comboText != null) comboText.text = State.Combo > 1 ? State.Combo + "x" : "";

            var mg = State.MinigameData;
            if (mg != null)
            {
                if (hpFill != null) hpFill.fillAmount = Mathf.Clamp01(mg.HP / Mathf.Max(1f, maxHP));
                if (rootGroup != null) rootGroup.alpha = mg.IsEliminated ? 0.35f : 1f;
            }
        }
    }
}