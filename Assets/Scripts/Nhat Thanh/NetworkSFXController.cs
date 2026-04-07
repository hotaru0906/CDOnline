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
/// Controller xử lý SFX sync qua network sử dụng Photon Fusion.
/// Gắn vào Player Prefab (có NetworkObject).
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public class NetworkSFXController : NetworkBehaviour
{
    [Header("Audio Sources")]
    [SerializeField] private AudioSource footstepSource;  // Loop cho walk/run
    [SerializeField] private AudioSource actionSource;     // One-shot cho jump/attack/kick

    [Header("3D Audio Settings")]
    [SerializeField] private float minDistance = 1f;
    [SerializeField] private float maxDistance = 20f;
    [SerializeField] private float spatialBlend = 1f;  // 1 = full 3D

    [Header("Footstep Settings")]
    [SerializeField] private float walkPitch = 1f;
    [SerializeField] private float runPitch = 1.3f;

    // Networked state để detect thay đổi
    [Networked] private PlayerSFXType CurrentFootstepType { get; set; }
    [Networked] private NetworkBool IsFootstepPlaying { get; set; }

    // Cache clips từ SFXManager
    private AudioClip _walkClip;
    private AudioClip _runClip;
    private AudioClip _jumpClip;
    private AudioClip _attackClip;
    private AudioClip _kickClip;

    private ChangeDetector _changeDetector;

    public override void Spawned()
    {
        _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);

        SetupAudioSources();
        CacheAudioClips();

        Debug.Log($"[NetworkSFXController] Spawned. HasInputAuthority: {HasInputAuthority}");
    }

    private void SetupAudioSources()
    {
        // Setup footstep source (looping)
        if (footstepSource == null)
        {
            GameObject footstepObj = new GameObject("FootstepAudio");
            footstepObj.transform.SetParent(transform);
            footstepObj.transform.localPosition = Vector3.zero;
            footstepSource = footstepObj.AddComponent<AudioSource>();
        }

        footstepSource.loop = true;
        footstepSource.playOnAwake = false;
        footstepSource.spatialBlend = spatialBlend;
        footstepSource.minDistance = minDistance;
        footstepSource.maxDistance = maxDistance;
        footstepSource.rolloffMode = AudioRolloffMode.Linear;

        // Setup action source (one-shot)
        if (actionSource == null)
        {
            GameObject actionObj = new GameObject("ActionAudio");
            actionObj.transform.SetParent(transform);
            actionObj.transform.localPosition = Vector3.zero;
            actionSource = actionObj.AddComponent<AudioSource>();
        }

        actionSource.loop = false;
        actionSource.playOnAwake = false;
        actionSource.spatialBlend = spatialBlend;
        actionSource.minDistance = minDistance;
        actionSource.maxDistance = maxDistance;
        actionSource.rolloffMode = AudioRolloffMode.Linear;
    }

    private void CacheAudioClips()
    {
        if (SFXManager.Instance != null)
        {
            _walkClip = SFXManager.Instance.WalkSound;
            _runClip = SFXManager.Instance.RunSound;
            _jumpClip = SFXManager.Instance.JumpSound;
            _attackClip = SFXManager.Instance.AttackSound;
            _kickClip = SFXManager.Instance.KickSound;
        }
    }

    public override void Render()
    {
        // Detect network state changes để update audio trên tất cả clients
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
        float volume = GetSFXVolume();
        if (footstepSource != null)
            footstepSource.volume = volume;
        if (actionSource != null)
            actionSource.volume = volume;
    }

    private void UpdateFootstepAudio()
    {
        if (!IsSFXEnabled())
        {
            StopFootstepLocal();
            return;
        }

        if (IsFootstepPlaying)
        {
            PlayFootstepLocal(CurrentFootstepType);
        }
        else
        {
            StopFootstepLocal();
        }
    }

    #region Public Methods - Gọi từ PlayerController (có InputAuthority)

    /// <summary>
    /// Bắt đầu phát footstep sound (walk/run).
    /// Chỉ gọi từ player có InputAuthority.
    /// </summary>
    public void StartFootstep(PlayerSFXType type)
    {
        if (!HasStateAuthority) return;
        if (type != PlayerSFXType.Walk && type != PlayerSFXType.Run) return;

        CurrentFootstepType = type;
        IsFootstepPlaying = true;
    }

    /// <summary>
    /// Dừng footstep sound.
    /// Chỉ gọi từ player có InputAuthority.
    /// </summary>
    public void StopFootstep()
    {
        if (!HasStateAuthority) return;

        IsFootstepPlaying = false;
    }

    /// <summary>
    /// Phát one-shot action sound (jump/attack/kick).
    /// Chỉ gọi từ player có InputAuthority.
    /// </summary>
    public void PlayAction(PlayerSFXType type)
    {
        if (!HasStateAuthority) return;
        if (type == PlayerSFXType.Walk || type == PlayerSFXType.Run) return;

        // Sử dụng RPC để sync tới tất cả clients
        RPC_PlayActionSound(type);
    }

    #endregion

    #region RPCs

    /// <summary>
    /// RPC phát action sound trên tất cả clients
    /// </summary>
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayActionSound(PlayerSFXType type)
    {
        if (!IsSFXEnabled()) return;

        AudioClip clip = GetActionClip(type);
        if (clip != null && actionSource != null)
        {
            actionSource.PlayOneShot(clip, GetSFXVolume());
        }
    }

    #endregion

    #region Local Audio Playback

    private void PlayFootstepLocal(PlayerSFXType type)
    {
        if (footstepSource == null) return;

        AudioClip clip = type == PlayerSFXType.Run ? _runClip : _walkClip;
        float pitch = type == PlayerSFXType.Run ? runPitch : walkPitch;

        if (clip == null) return;

        // Chỉ thay đổi nếu clip khác
        if (footstepSource.clip != clip)
        {
            footstepSource.clip = clip;
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
        {
            footstepSource.Stop();
        }
    }

    private AudioClip GetActionClip(PlayerSFXType type)
    {
        return type switch
        {
            PlayerSFXType.Jump => _jumpClip,
            PlayerSFXType.Attack => _attackClip,
            PlayerSFXType.Kick => _kickClip,
            _ => null
        };
    }

    #endregion

    #region Helper Methods

    private bool IsSFXEnabled()
    {
        return AudioManager.Instance != null && AudioManager.Instance.IsSFXOn;
    }

    private float GetSFXVolume()
    {
        return AudioManager.Instance != null ? AudioManager.Instance.SFXVolume : 1f;
    }

    #endregion

    #region Debug/Editor

#if UNITY_EDITOR
    [Header("Debug")]
    [SerializeField] private bool debugMode = false;

    private void OnGUI()
    {
        if (!debugMode || !HasInputAuthority) return;

        GUILayout.BeginArea(new Rect(10, 10, 200, 150));
        GUILayout.Label($"Footstep: {IsFootstepPlaying}");
        GUILayout.Label($"Type: {CurrentFootstepType}");

        if (GUILayout.Button("Walk")) StartFootstep(PlayerSFXType.Walk);
        if (GUILayout.Button("Run")) StartFootstep(PlayerSFXType.Run);
        if (GUILayout.Button("Stop")) StopFootstep();
        if (GUILayout.Button("Jump")) PlayAction(PlayerSFXType.Jump);
        if (GUILayout.Button("Attack")) PlayAction(PlayerSFXType.Attack);
        if (GUILayout.Button("Kick")) PlayAction(PlayerSFXType.Kick);

        GUILayout.EndArea();
    }
#endif

    #endregion
}
