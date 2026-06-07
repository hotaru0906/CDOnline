using Fusion;
using UnityEngine;

/// <summary>
/// Loại âm thanh player action cần sync qua network
/// </summary>
public enum PlayerSFXType
{
    Walk,
    Run,
    Jump,
    Attack,
    Kick
}

/// <summary>
/// Controller xử lý SFX cho player — thay thế NetworkSFXController.
/// Toàn bộ AudioClip có thể kéo trực tiếp vào Inspector.
/// Gắn vào Player Prefab (có NetworkObject).
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public class PlayerSFXController : NetworkBehaviour
{
    // ─── Audio Sources ────────────────────────────────────────
    [Header("Audio Sources")]
    [Tooltip("AudioSource dùng để loop bước chân (walk/run)")]
    [SerializeField] private AudioSource footstepSource;

    [Tooltip("AudioSource dùng để phát one-shot (jump/attack/kick)")]
    [SerializeField] private AudioSource actionSource;

    // ─── Footstep Clips ───────────────────────────────────────
    [Header("Footstep SFX")]
    [Tooltip("Các clip bước đi — kéo bao nhiêu tùy ý, sẽ random mỗi lần")]
    [SerializeField] private AudioClip[] walkClips;

    [Tooltip("Các clip bước chạy — kéo bao nhiêu tùy ý, sẽ random mỗi lần")]
    [SerializeField] private AudioClip[] runClips;

    // ─── Action Clips ─────────────────────────────────────────
    [Header("Action SFX")]
    [Tooltip("Âm thanh nhảy — kéo nhiều clip để random")]
    [SerializeField] private AudioClip[] jumpClips;

    [Tooltip("Âm thanh tấn công — kéo nhiều clip để random")]
    [SerializeField] private AudioClip[] attackClips;

    [Tooltip("Âm thanh đá — kéo nhiều clip để random")]
    [SerializeField] private AudioClip[] kickClips;

    // ─── 3D Audio Settings ────────────────────────────────────
    [Header("3D Audio Settings")]
    [Tooltip("Khoảng cách gần nhất nghe full volume")]
    [SerializeField] private float minDistance = 1f;

    [Tooltip("Khoảng cách xa nhất còn nghe thấy")]
    [SerializeField] private float maxDistance = 20f;

    [Tooltip("0 = 2D hoàn toàn, 1 = 3D hoàn toàn")]
    [SerializeField][Range(0f, 1f)] private float spatialBlend = 1f;

    // ─── Footstep Settings ────────────────────────────────────
    [Header("Footstep Settings")]
    [Tooltip("Pitch khi đi bộ")]
    [SerializeField] private float walkPitch = 1f;

    [Tooltip("Pitch khi chạy — cao hơn để cảm giác nhanh hơn")]
    [SerializeField] private float runPitch = 1.3f;

    // ─── Networked State ──────────────────────────────────────
    [Networked] private PlayerSFXType CurrentFootstepType { get; set; }
    [Networked] private NetworkBool IsFootstepPlaying { get; set; }

    private ChangeDetector _changeDetector;

    // ─────────────────────────────────────────────────────────
    // Lifecycle
    // ─────────────────────────────────────────────────────────

    public override void Spawned()
    {
        _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
        SetupAudioSources();

        Debug.Log($"[PlayerSFXController] Spawned. HasInputAuthority: {HasInputAuthority}");
    }

    // ─────────────────────────────────────────────────────────
    // Setup
    // ─────────────────────────────────────────────────────────

    private void SetupAudioSources()
    {
        // Tự tạo FootstepAudio nếu chưa assign trong Inspector
        if (footstepSource == null)
        {
            GameObject go = new GameObject("FootstepAudio");
            go.transform.SetParent(transform);
            go.transform.localPosition = Vector3.zero;
            footstepSource = go.AddComponent<AudioSource>();
        }

        footstepSource.loop         = true;
        footstepSource.playOnAwake  = false;
        footstepSource.spatialBlend = spatialBlend;
        footstepSource.minDistance  = minDistance;
        footstepSource.maxDistance  = maxDistance;
        footstepSource.rolloffMode  = AudioRolloffMode.Linear;

        // Tự tạo ActionAudio nếu chưa assign trong Inspector
        if (actionSource == null)
        {
            GameObject go = new GameObject("ActionAudio");
            go.transform.SetParent(transform);
            go.transform.localPosition = Vector3.zero;
            actionSource = go.AddComponent<AudioSource>();
        }

        actionSource.loop         = false;
        actionSource.playOnAwake  = false;
        actionSource.spatialBlend = spatialBlend;
        actionSource.minDistance  = minDistance;
        actionSource.maxDistance  = maxDistance;
        actionSource.rolloffMode  = AudioRolloffMode.Linear;
    }

    // ─────────────────────────────────────────────────────────
    // Render — chạy mỗi frame, detect network state changes
    // ─────────────────────────────────────────────────────────

    public override void Render()
    {
        foreach (var change in _changeDetector.DetectChanges(this))
        {
            switch (change)
            {
                case nameof(IsFootstepPlaying):
                case nameof(CurrentFootstepType):
                    UpdateFootstepAudio();
                    break;
            }
        }

        UpdateVolume();
    }

    private void UpdateVolume()
    {
        float vol = GetSFXVolume();
        if (footstepSource != null) footstepSource.volume = vol;
        if (actionSource   != null) actionSource.volume   = vol;
    }

    private void UpdateFootstepAudio()
    {
        if (!IsSFXEnabled())
        {
            StopFootstepLocal();
            return;
        }

        if (IsFootstepPlaying)
            PlayFootstepLocal(CurrentFootstepType);
        else
            StopFootstepLocal();
    }

    // ─────────────────────────────────────────────────────────
    // Public API — gọi từ PlayerController (HasStateAuthority)
    // ─────────────────────────────────────────────────────────

    /// <summary>
    /// Bắt đầu phát footstep (walk hoặc run).
    /// Chỉ gọi từ HasStateAuthority.
    /// </summary>
    public void StartFootstep(PlayerSFXType type)
    {
        if (!HasStateAuthority) return;
        if (type != PlayerSFXType.Walk && type != PlayerSFXType.Run) return;

        CurrentFootstepType = type;
        IsFootstepPlaying   = true;
    }

    /// <summary>
    /// Dừng footstep.
    /// Chỉ gọi từ HasStateAuthority.
    /// </summary>
    public void StopFootstep()
    {
        if (!HasStateAuthority) return;
        IsFootstepPlaying = false;
    }

    /// <summary>
    /// Phát one-shot action sound (jump/attack/kick).
    /// Sync tới tất cả clients qua RPC.
    /// </summary>
    public void PlayAction(PlayerSFXType type)
    {
        if (!HasStateAuthority) return;
        if (type == PlayerSFXType.Walk || type == PlayerSFXType.Run) return;

        RPC_PlayActionSound(type);
    }

    // ─────────────────────────────────────────────────────────
    // RPCs
    // ─────────────────────────────────────────────────────────

    /// <summary>
    /// Phát action sound trên tất cả clients
    /// </summary>
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayActionSound(PlayerSFXType type)
    {
        if (!IsSFXEnabled()) return;

        AudioClip clip = GetRandomClip(GetActionClips(type));
        if (clip != null && actionSource != null)
            actionSource.PlayOneShot(clip, GetSFXVolume());
    }

    // ─────────────────────────────────────────────────────────
    // Local Audio Playback
    // ─────────────────────────────────────────────────────────

    private void PlayFootstepLocal(PlayerSFXType type)
    {
        if (footstepSource == null) return;

        AudioClip[] clips = type == PlayerSFXType.Run ? runClips : walkClips;
        float pitch       = type == PlayerSFXType.Run ? runPitch  : walkPitch;

        AudioClip clip = GetRandomClip(clips);
        if (clip == null) return;

        // Chỉ đổi clip nếu khác clip đang phát — tránh restart loop liên tục
        if (footstepSource.clip != clip)
        {
            footstepSource.clip  = clip;
            footstepSource.pitch = pitch;
        }

        if (!footstepSource.isPlaying)
        {
            footstepSource.volume = GetSFXVolume();
            footstepSource.Play();
        }
    }

    private void StopFootstepLocal()
    {
        if (footstepSource != null && footstepSource.isPlaying)
            footstepSource.Stop();
    }

    // ─────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────

    /// <summary>
    /// Lấy clip ngẫu nhiên từ array.
    /// Trả về null nếu array null hoặc rỗng.
    /// </summary>
    private AudioClip GetRandomClip(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0) return null;
        return clips[Random.Range(0, clips.Length)];
    }

    /// <summary>
    /// Map PlayerSFXType → đúng array clips
    /// </summary>
    private AudioClip[] GetActionClips(PlayerSFXType type)
    {
        return type switch
        {
            PlayerSFXType.Jump   => jumpClips,
            PlayerSFXType.Attack => attackClips,
            PlayerSFXType.Kick   => kickClips,
            _                    => null
        };
    }

    private bool IsSFXEnabled() =>
        AudioManager.Instance != null && AudioManager.Instance.IsSFXOn;

    private float GetSFXVolume() =>
        AudioManager.Instance != null ? AudioManager.Instance.SFXVolume : 1f;

    // ─────────────────────────────────────────────────────────
    // Debug
    // ─────────────────────────────────────────────────────────

#if UNITY_EDITOR
    [Header("Debug")]
    [SerializeField] private bool debugMode = false;

    private void OnGUI()
    {
        if (!debugMode || !HasInputAuthority) return;

        GUILayout.BeginArea(new Rect(10, 10, 200, 200));
        GUILayout.Label($"[PlayerSFXController]");
        GUILayout.Label($"Footstep Playing : {IsFootstepPlaying}");
        GUILayout.Label($"Current Type     : {CurrentFootstepType}");
        GUILayout.Space(5);
        if (GUILayout.Button("Walk"))   StartFootstep(PlayerSFXType.Walk);
        if (GUILayout.Button("Run"))    StartFootstep(PlayerSFXType.Run);
        if (GUILayout.Button("Stop"))   StopFootstep();
        if (GUILayout.Button("Jump"))   PlayAction(PlayerSFXType.Jump);
        if (GUILayout.Button("Attack")) PlayAction(PlayerSFXType.Attack);
        if (GUILayout.Button("Kick"))   PlayAction(PlayerSFXType.Kick);
        GUILayout.EndArea();
    }
#endif
}