using UnityEngine;
using UnityEngine.UI;
using RhythmGame;
using TMPro;

/// <summary>
/// Một lane trên màn hình, gắn với đúng một người chơi.
/// Icon lấy từ PlayerNetworkData.CharacterIndex qua CharacterIconDatabase.
/// </summary>
public class MGRhythmLaneUI : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private CharacterIconDatabase iconDatabase;

    [Header("Icon nhân vật (cột sát trái)")]
    [SerializeField] private Image characterIcon;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private Image laneFrame;

    [Header("Hitbox + note")]
    [SerializeField] private RectTransform noteContainer;
    [SerializeField] private RectTransform hitbox;
    [SerializeField] private GameObject hitEffect;

    [Header("Chỉ số")]
    [SerializeField, Tooltip("PHẢI khớp với startHP trong MGRhythmController.")]
    private float maxHP = 100f;
    [SerializeField] private Image hpFill;
    [SerializeField] private Image feverFill;
    [SerializeField] private TMP_Text comboText;
    [SerializeField] private TMP_Text judgeText;
    [SerializeField] private TMP_Text scoreText;

    [Header("Hiệu ứng")]
    [SerializeField] private GameObject feverBurstVfx;
    [SerializeField] private GameObject localHighlight;
    [SerializeField] private CanvasGroup rootGroup;

    public RectTransform NoteContainer => noteContainer;
    public bool IsOccupied => _state != null;
    public bool IsLocal { get; private set; }

    private MGRhythmPlayerState _state;
    private float _judgeTimer;
    private float _hitEffectTimer;
    private int _lastBurstSeq;

    // ----------------------------------------------------------------
    //  Bind
    // ----------------------------------------------------------------

    public void Bind(MGRhythmPlayerState state, bool isLocal)
    {
        Unbind();

        _state = state;
        IsLocal = isLocal;

        // Lane trống (phòng chỉ có 2-3 người) thì ẩn hẳn đi.
        bool occupied = state != null;
        if (rootGroup != null) rootGroup.alpha = occupied ? 1f : 0f;
        gameObject.SetActive(true);
        if (noteContainer != null) noteContainer.gameObject.SetActive(occupied);
        if (localHighlight != null) localHighlight.SetActive(isLocal);

        if (!occupied) return;

        // Icon + tên
        var net = _state.NetData;
        if (net != null)
        {
            if (characterIcon != null && iconDatabase != null)
            {
                characterIcon.sprite = iconDatabase.GetIcon(net.CharacterIndex);
                characterIcon.enabled = characterIcon.sprite != null;
            }
            if (laneFrame != null && iconDatabase != null)
                laneFrame.color = iconDatabase.GetThemeColor(net.CharacterIndex);
            if (nameText != null)
                nameText.text = net.PlayerName.ToString();
        }

        _state.OnStatsChangedRender += HandleStats;
        _state.OnFeverChangedRender += HandleFever;
        _state.OnFeverBurstRender += HandleBurst;

        // KHÔNG subscribe PlayerMinigameData.OnHPChangedHost ở đây.
        // Event đó chỉ Invoke bên trong SetHP/TakeDamage/ResetCheckpoint, mà cả ba
        // đều có "if (!HasStateAuthority) return;" — nghĩa là nó CHỈ chạy trên host.
        // Trên máy client thanh máu sẽ đứng im mãi.
        // Thay vào đó đọc thẳng HP (đã là [Networked], tự replicate) trong Update().

        RefreshAll();
    }

    private void Unbind()
    {
        if (_state == null) return;

        _state.OnStatsChangedRender -= HandleStats;
        _state.OnFeverChangedRender -= HandleFever;
        _state.OnFeverBurstRender -= HandleBurst;

        _state = null;
    }

    private void OnDestroy() => Unbind();

    // ----------------------------------------------------------------
    //  Cập nhật hiển thị
    // ----------------------------------------------------------------

    private void HandleStats(MGRhythmPlayerState s) => RefreshAll();
    private void HandleFever(MGRhythmPlayerState s) => RefreshFever();
    private void HandleBurst(MGRhythmPlayerState s) => PlayFeverBurst();

    private void RefreshAll()
    {
        RefreshHP();
        RefreshFever();

        if (_state == null) return;

        if (scoreText != null) scoreText.text = _state.RhythmScore.ToString("N0");

        // Lane cục bộ đã có combo cập nhật tức thì từ ShowLocalJudgement,
        // không ghi đè bằng giá trị mạng (trễ hơn ~100ms).
        if (!IsLocal && comboText != null)
            comboText.text = _state.Combo > 1 ? _state.Combo + "x" : "";
    }

    private void RefreshHP()
    {
        if (hpFill == null || _state == null) return;

        var mg = _state.MinigameData;
        if (mg == null) return;

        // HP là [Networked] nên đọc thẳng luôn đúng trên mọi máy.
        hpFill.fillAmount = Mathf.Clamp01(mg.HP / Mathf.Max(1f, maxHP));

        if (rootGroup != null)
            rootGroup.alpha = mg.IsEliminated ? 0.35f : 1f;
    }

    private void RefreshFever()
    {
        if (feverFill == null || _state == null) return;
        if (IsLocal) return; // lane mình dùng giá trị cục bộ, mượt hơn
        feverFill.fillAmount = _state.Fever01;
    }

    // ----------------------------------------------------------------
    //  Phản hồi tức thì cho lane cục bộ
    // ----------------------------------------------------------------

    public void ShowLocalJudgement(Judgement j, int combo, float fever01)
    {
        if (judgeText != null)
        {
            judgeText.text = j.ToString().ToUpper();
            _judgeTimer = 0.35f;
        }

        if (comboText != null)
            comboText.text = combo > 1 ? combo + "x" : "";

        if (feverFill != null)
            feverFill.fillAmount = fever01;

        if (j != Judgement.Miss && hitEffect != null)
        {
            hitEffect.SetActive(true);
            _hitEffectTimer = 0.15f;
        }

        if (hitbox != null)
            hitbox.localScale = Vector3.one * 1.15f;
    }

    public void PlayFeverBurst()
    {
        if (feverBurstVfx == null) return;
        feverBurstVfx.SetActive(false);
        feverBurstVfx.SetActive(true);
    }

    private void Update()
    {
        // HP đọc mỗi frame vì event OnHPChangedHost chỉ chạy trên host.
        // Đây là một phép chia và một phép gán, chi phí không đáng kể.
        if (_state != null) RefreshHP();

        if (_judgeTimer > 0f)
        {
            _judgeTimer -= Time.deltaTime;
            if (_judgeTimer <= 0f && judgeText != null) judgeText.text = "";
        }

        if (_hitEffectTimer > 0f)
        {
            _hitEffectTimer -= Time.deltaTime;
            if (_hitEffectTimer <= 0f && hitEffect != null) hitEffect.SetActive(false);
        }

        if (hitbox != null && hitbox.localScale.x > 1.001f)
            hitbox.localScale = Vector3.Lerp(hitbox.localScale, Vector3.one, Time.deltaTime * 12f);
    }
}
