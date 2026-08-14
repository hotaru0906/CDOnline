using Fusion;
using UnityEngine;

/// <summary>
/// Component battle rieng cho Final Scene (Phase D/E). File hoan toan moi, tach biet
/// khoi he thong Minigame - khong ke thua, khong goi API cua BaseMinigameController hay
/// bat ky class minigame nao. Chi giao tiep ra ngoai qua FinalManager.Instance.ReportPlayerEliminated().
///
/// Elimination dua tren PlayerNetworkData.BattleHP / IsBattleEliminated (them o buoc truoc).
/// Ragdoll + SetFrozen khi eliminated da duoc trigger san trong PlayerNetworkData.Render()
/// (giong pattern PlayerMinigameData) - component nay KHONG tu goi ActivateRagdoll/SetFrozen truc tiep.
/// </summary>
[RequireComponent(typeof(PlayerNetworkData))]
[RequireComponent(typeof(PlayerController))]
public class PlayerBattleController : NetworkBehaviour
{
    [Header("Battle Settings")]
    [SerializeField] private float maxBattleHP = 100f;

    /// <summary>
    /// True khi player dang trong battle va co the nhan damage.
    /// Duoc FinalManager bat len khi vao Phase Battle (RPC_UnlockBattleInput),
    /// va tat di sau khi battle ket thuc (PostBattleTeleport).
    /// </summary>
    [Networked] public NetworkBool IsBattleActive { get; private set; }

    private PlayerNetworkData _networkData;

    private void Awake()
    {
        _networkData = GetComponent<PlayerNetworkData>();
    }

    #region Battle Lifecycle (goi tu FinalManager - host only)

    /// <summary>
    /// Kich hoat battle cho player nay: reset BattleHP ve max, cho phep nhan damage.
    /// Goi boi FinalManager khi buoc vao FinalPhaseState.Battle cho tung battler.
    /// Host only.
    /// </summary>
    public void ActivateForBattle()
    {
        if (!HasStateAuthority) return;

        _networkData.ResetBattleHP(maxBattleHP);
        IsBattleActive = true;

        Debug.Log($"[PlayerBattleController] P{Object.InputAuthority.PlayerId} activated for battle. HP={maxBattleHP}");
    }

    /// <summary>
    /// Tat trang thai battle (khong nhan damage nua). Goi khi battle ket thuc,
    /// truoc buoc teleport theo rank (Phase F). Khong tu dong un-ragdoll o day —
    /// xem ResetAfterBattle() cho player da bi eliminated.
    /// Host only.
    /// </summary>
    public void DeactivateBattle()
    {
        if (!HasStateAuthority) return;

        IsBattleActive = false;
    }

    /// <summary>
    /// Danh cho player DA BI ELIMINATED trong battle: tra IsBattleEliminated ve false
    /// de PlayerNetworkData.Render() tu dong DeactivateRagdoll() + SetFrozen(false)
    /// tren moi client - dung truoc/cung luc voi buoc teleport theo rank (Phase F,
    /// FinalManager.TeleportPostBattle()).
    /// Host only.
    /// </summary>
    public void ResetAfterBattle()
    {
        if (!HasStateAuthority) return;

        IsBattleActive = false;
        _networkData.SetBattleEliminated(false);
    }

    #endregion

    #region Damage Entry Point (stub - combat mechanic thuc se cam vao day sau)

    /// <summary>
    /// Entry point duy nhat de gay damage cho player nay. Combat mechanic thuc
    /// (melee/hitbox/vu khi...) CHUA duoc thiet ke - se goi ham nay khi co san,
    /// khong can sua lai luong elimination/ragdoll/teleport da co.
    /// Host only. Khong hook vao PlayerController.CheckAttackHit() (chi danh cho minigame cu).
    /// </summary>
    public void ApplyDamage(float amount)
    {
        if (!HasStateAuthority) return;
        if (!IsBattleActive) return;
        if (_networkData.IsBattleEliminated) return;
        if (amount <= 0f) return;

        float newHP = _networkData.BattleHP - amount;
        _networkData.SetBattleHP(newHP);

        Debug.Log($"[PlayerBattleController] P{Object.InputAuthority.PlayerId} took {amount} battle damage — {_networkData.BattleHP} HP remaining");

        if (_networkData.BattleHP <= 0f)
        {
            HandleEliminated();
        }
    }

    #endregion

    #region Elimination

    /// <summary>
    /// Host only. HP ve 0 -> danh dau eliminated (trigger ragdoll qua PlayerNetworkData.Render()
    /// tren moi client) -> bao cho FinalManager de ghi nhan thu tu loai + check dieu kien ket thuc battle.
    /// </summary>
    private void HandleEliminated()
    {
        IsBattleActive = false;
        _networkData.SetBattleEliminated(true);

        int playerId = Object.InputAuthority.PlayerId;
        Debug.Log($"[PlayerBattleController] P{playerId} ELIMINATED (BattleHP=0)");

        FinalManager.Instance?.ReportPlayerEliminated(playerId);
    }

    #endregion
}