using Fusion;
using UnityEngine;

/// <summary>
/// State rhythm của từng player, gắn lên PLAYER PREFAB (cùng chỗ với PlayerMinigameData).
///
/// Kiến trúc: chấm điểm hoàn toàn CỤC BỘ trên máy của người chơi đó
/// (vì dspTime của mỗi máy là độc lập, không thể đồng bộ note qua mạng).
/// Client chỉ báo cáo KẾT QUẢ lên host ở tần suất thấp để các máy khác vẽ được
/// thanh fever / combo / điểm của mình.
///
/// Máu KHÔNG nằm ở đây — dùng luôn PlayerMinigameData.HP đã có sẵn.
/// </summary>
public class MGRhythmPlayerState : NetworkBehaviour
{
    #region Networked (host ghi, mọi máy đọc)

    [Networked, OnChangedRender(nameof(OnStatsChanged))]
    public int RhythmScore { get; private set; }

    [Networked, OnChangedRender(nameof(OnStatsChanged))]
    public int Combo { get; private set; }

    [Networked, OnChangedRender(nameof(OnStatsChanged))]
    public int MaxCombo { get; private set; }

    /// <summary>Fever đã chuẩn hoá 0..1, dùng trực tiếp cho fillAmount.</summary>
    [Networked, OnChangedRender(nameof(OnFeverChanged))]
    public float Fever01 { get; private set; }

    [Networked] public int PerfectCount { get; private set; }
    [Networked] public int GoodCount { get; private set; }
    [Networked] public int MissCount { get; private set; }

    /// <summary>
    /// Tăng mỗi lần fever nổ. Client dùng OnChangedRender để phát hiệu ứng
    /// mà không cần RPC riêng — rẻ hơn và không bao giờ mất gói.
    /// </summary>
    [Networked, OnChangedRender(nameof(OnFeverBurstChanged))]
    public int FeverBurstSeq { get; private set; }

    #endregion

    /// <summary>Sự kiện cho LaneUI: (player này, seq). Chạy trên mọi máy.</summary>
    public event System.Action<MGRhythmPlayerState> OnStatsChangedRender;
    public event System.Action<MGRhythmPlayerState> OnFeverChangedRender;
    public event System.Action<MGRhythmPlayerState> OnFeverBurstRender;

    private PlayerMinigameData _mgData;
    public PlayerMinigameData MinigameData =>
        _mgData != null ? _mgData : (_mgData = GetComponent<PlayerMinigameData>());

    private PlayerNetworkData _netData;
    public PlayerNetworkData NetData =>
        _netData != null ? _netData : (_netData = GetComponent<PlayerNetworkData>());

    // ----------------------------------------------------------------
    //  Host API
    // ----------------------------------------------------------------

    public void ResetForRhythmRound()
    {
        if (!HasStateAuthority) return;
        RhythmScore = 0;
        Combo = 0;
        MaxCombo = 0;
        Fever01 = 0f;
        PerfectCount = GoodCount = MissCount = 0;
        FeverBurstSeq = 0;
    }

    // ----------------------------------------------------------------
    //  Client → Host
    // ----------------------------------------------------------------

    /// <summary>
    /// Client gọi ở tần suất thấp (~10 lần/giây) để host cập nhật state hiển thị.
    /// KHÔNG gọi mỗi note — 4 người x 4 note/giây sẽ tạo traffic vô ích.
    /// </summary>
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_ReportProgress(int score, int combo, int maxCombo, float fever01,
                                   int perfect, int good, int miss)
    {
        RhythmScore = score;
        Combo = combo;
        MaxCombo = maxCombo;
        Fever01 = Mathf.Clamp01(fever01);
        PerfectCount = perfect;
        GoodCount = good;
        MissCount = miss;
    }

    /// <summary>
    /// Client báo đã né được đòn tấn công. Client CÓ input authority trên state của
    /// chính mình nên RPC này hợp lệ; host nhận rồi chuyển cho controller xử lý.
    /// </summary>
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_ReportDodge(int attackId)
    {
        MGRhythmController.Instance?.NotifyDodge(attackId);
    }

    /// <summary>
    /// Client báo fever vừa đầy. Host là bên DUY NHẤT quyết định sát thương,
    /// client không bao giờ tự trừ máu người khác.
    /// </summary>
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_ReportFeverFull()
    {
        Fever01 = 0f;
        FeverBurstSeq++;
        MGRhythmController.Instance?.ApplyFeverAttack(Object.InputAuthority);
    }

    // ----------------------------------------------------------------
    //  Render callbacks
    // ----------------------------------------------------------------

    private void OnStatsChanged() => OnStatsChangedRender?.Invoke(this);
    private void OnFeverChanged() => OnFeverChangedRender?.Invoke(this);
    private void OnFeverBurstChanged() => OnFeverBurstRender?.Invoke(this);
}