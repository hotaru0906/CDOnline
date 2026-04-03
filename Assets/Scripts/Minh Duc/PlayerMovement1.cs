using UnityEngine;

public class PlayerMovement1 : MonoBehaviour, IRespawnable
{
    private CharacterController controller;
    private Animator animator;

    public Transform cameraTransform;
    public Transform spawnPoint;

    [Header("Movement")]
    public float walkSpeed = 5f;
    public float runSpeed = 9f;
    public float jumpHeight = 2f;
    public float gravity = -20f;

    private Vector3 velocity;

    // 🔥 STATE
    private bool isAttacking = false;
    private bool isSitting = false;

    // 🔥 CONTROL FLAGS
    public bool canMove = true;
    public bool canRun = true;
    public bool canJump = true;
    public bool canSit = true;
    public bool canAttack = true;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponentInChildren<Animator>();

        if (controller == null) Debug.LogError("❌ Missing CharacterController");
        if (animator == null) Debug.LogError("❌ Missing Animator");
        if (cameraTransform == null) Debug.LogError("❌ Missing Camera Transform");
    }

    void Update()
    {
        if (controller == null || cameraTransform == null) return;

        HandleGravity();
        HandleSit();
        HandleMovement();
        HandleJump();
        HandleAttack();
        UpdateAnimation();
    }

    // ================= MOVEMENT =================
    void HandleMovement()
    {
        if (!canMove || isAttacking || isSitting) return;

        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");

        Vector3 move = cameraTransform.forward * z + cameraTransform.right * x;
        move.y = 0;

        bool isMoving = move.magnitude > 0.01f;
        bool isRunning = Input.GetKey(KeyCode.LeftShift) && canRun;

        float speed = isRunning ? runSpeed : walkSpeed;

        if (isMoving)
        {
            controller.Move(move.normalized * speed * Time.deltaTime);

            Quaternion rot = Quaternion.LookRotation(move);
            transform.rotation = Quaternion.Slerp(transform.rotation, rot, 10f * Time.deltaTime);
        }

        // lưu lại để dùng cho animation
        animator.SetBool("IsRunning", isRunning && isMoving);
        animator.SetBool("IsWalking", !isRunning && isMoving);
    }

    // ================= JUMP =================
    void HandleJump()
    {
        if (!canJump || isAttacking || isSitting) return;

        if (Input.GetKeyDown(KeyCode.Space) && controller.isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            animator.SetTrigger("Jump");
        }
    }

    // ================= SIT =================
    void HandleSit()
    {
        if (!canSit || isAttacking) return;

        bool grounded = controller.isGrounded;

        if (grounded)
        {
            // giữ Ctrl để ngồi
            isSitting = Input.GetKey(KeyCode.LeftControl);
        }
        else
        {
            isSitting = false;
        }

        animator.SetBool("isSitting", isSitting);
    }

    // ================= ATTACK =================
    void HandleAttack()
    {
        if (!canAttack || isAttacking || isSitting) return;

        if (Input.GetMouseButtonDown(0) && controller.isGrounded)
        {
            isAttacking = true;
            animator.SetTrigger("Attack");

            Invoke(nameof(EndAttack), 0.7f);
        }
    }

    void EndAttack()
    {
        isAttacking = false;
    }

    // ================= GRAVITY =================
    void HandleGravity()
    {
        if (controller.isGrounded && velocity.y < 0)
            velocity.y = -2f;

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }

    // ================= ANIMATION =================
    void UpdateAnimation()
    {
        if (isAttacking)
        {
            animator.SetBool("IsRunning", false);
            animator.SetBool("IsWalking", false);
        }
    }

    // ================= MINI GAME =================
    public void SetMiniGame1()
    {
        canMove = false;
        canRun = false;
        canJump = true;
        canSit = true;
        canAttack = false;
    }

    public void SetMiniGame2()
    {
        canMove = true;
        canRun = true;
        canJump = true;
        canSit = true;
        canAttack = true;
    }

    // ================= RESPAWN =================
    public void Respawn()
    {
        controller.enabled = false;
        transform.position = spawnPoint.position;
        controller.enabled = true;

        velocity = Vector3.zero;
        isAttacking = false;
        isSitting = false;

        animator.Rebind();
    }
}