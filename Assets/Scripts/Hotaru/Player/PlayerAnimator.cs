using Fusion;
using UnityEngine;

/// <summary>
/// Quản lý animation cho Player dựa trên PlayerState
/// Dùng SetBool/SetTrigger giống PlayerMovement1
/// </summary>
[RequireComponent(typeof(PlayerController))]
public class PlayerAnimator : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private Animator animator;

    [Header("Animation Parameters (giống PlayerMovement1)")]
    [SerializeField] private string isWalkingParam = "IsWalking";
    [SerializeField] private string isRunningParam = "IsRunning";
    [SerializeField] private string jumpTrigger = "Jump";
    [SerializeField] private string attackTrigger = "Attack";

    private PlayerController _playerController;
    private bool _isAttacking = false;

    private void Awake()
    {
        _playerController = GetComponent<PlayerController>();
    }

    /// <summary>
    /// Cập nhật Animator reference khi đổi model
    /// Gọi từ PlayerModelSwitcher khi model thay đổi
    /// </summary>
    public void UpdateAnimatorReference(GameObject activeModel)
    {
        if (activeModel == null)
        {
            animator = null;
            Debug.LogWarning("[PlayerAnimator] Active model is null!");
            return;
        }

        animator = activeModel.GetComponent<Animator>();
        if (animator == null)
        {
            animator = activeModel.GetComponentInChildren<Animator>();
        }

        if (animator != null)
        {
            Debug.Log($"[PlayerAnimator] Found Animator on: {animator.gameObject.name}");
        }
        else
        {
            Debug.LogWarning($"[PlayerAnimator] No Animator found on model: {activeModel.name}");
        }
    }

    public override void Render()
    {
        if (animator == null) return;

        UpdateAnimationState();
    }

    private void UpdateAnimationState()
    {
        var state = _playerController.CurrentState;
        float speed = _playerController.GetHorizontalSpeed();
        bool isMoving = speed > 0.1f;

        // Giống PlayerMovement1: SetBool cho Walking và Running
        bool isWalking = state == PlayerState.Walking;
        bool isRunning = state == PlayerState.Running;

        animator.SetBool(isWalkingParam, isWalking && !_isAttacking);
        animator.SetBool(isRunningParam, isRunning && !_isAttacking);
    }

    /// <summary>
    /// Trigger jump animation
    /// </summary>
    public void TriggerJump()
    {
        if (animator == null) return;
        animator.SetTrigger(jumpTrigger);
    }

    /// <summary>
    /// Trigger attack animation
    /// </summary>
    public void TriggerAttack()
    {
        if (animator == null) return;

        _isAttacking = true;
        animator.SetTrigger(attackTrigger);

        // Reset sau 0.7s giống PlayerMovement1
        Invoke(nameof(EndAttack), 0.7f);
    }

    private void EndAttack()
    {
        _isAttacking = false;
    }

    /// <summary>
    /// Reset animator về trạng thái ban đầu
    /// </summary>
    public void ResetAnimator()
    {
        if (animator == null) return;
        _isAttacking = false;
        animator.Rebind();
        animator.Update(0f);
    }
}
