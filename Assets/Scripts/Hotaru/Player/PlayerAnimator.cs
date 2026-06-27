using Fusion;
using UnityEngine;

[RequireComponent(typeof(PlayerController))]
public class PlayerAnimator : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private Animator animator;

    [Header("Animation Parameters")]
    [SerializeField] private string isWalkingParam = "IsWalking";
    [SerializeField] private string isRunningParam = "IsRunning";
    [SerializeField] private string jumpTrigger = "Jump";
    [SerializeField] private string attackTrigger = "Attack";
    [SerializeField] private string isSittingParam = "isSitting";

    private PlayerController _playerController;

    private bool _isAttacking = false;
    private bool _wasJumping = false;

    private void Awake()
    {
        _playerController = GetComponent<PlayerController>();
    }

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
        if (animator == null || _playerController == null)
            return;

        UpdateAnimationState();
    }

    private void UpdateAnimationState()
    {
        var state = _playerController.CurrentState;

        bool isWalking = state == PlayerState.Walking;
        bool isRunning = state == PlayerState.Running;
        bool isCrouching = state == PlayerState.Crouching;
        bool isJumping = state == PlayerState.Jumping;
        bool isAttacking = _playerController.IsAttacking;

        // WALK / RUN / CROUCH
        animator.SetBool(isWalkingParam, isWalking);
        animator.SetBool(isRunningParam, isRunning);
        animator.SetBool(isSittingParam, isCrouching);

        // JUMP
        if (isJumping && !_wasJumping)
        {
            _wasJumping = true;

            animator.ResetTrigger(jumpTrigger);
            animator.SetTrigger(jumpTrigger);
        }
        else if (!isJumping)
        {
            _wasJumping = false;
        }

        // ATTACK
        if (isAttacking && !_isAttacking)
        {
            _isAttacking = true;

            animator.ResetTrigger(attackTrigger);
            animator.SetTrigger(attackTrigger);
        }
        else if (!isAttacking)
        {
            _isAttacking = false;
        }
    }

    public void ResetAnimator()
    {
        if (animator == null) return;

        _isAttacking = false;
        _wasJumping = false;

        animator.Rebind();
        animator.Update(0f);
    }

    public void SetSittingOnChair(bool sitting)
    {
        if (animator == null) return;

        animator.SetBool("sitOnChair", sitting);
    }
}