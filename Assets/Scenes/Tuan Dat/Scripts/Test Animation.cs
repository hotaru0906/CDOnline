using UnityEngine;

public class TestAnimation : MonoBehaviour
{
    private Animator animator;
    public float speed = 5f;
    public float jumpForce = 5f;

    private Rigidbody rb;
    private bool isGrounded = true;

    private bool isMoveCrouch = false;
    private bool isCrouching = false;

    [Header("VFX")]
    public GameObject stunVFX;

    void Start()
    {
        animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody>();

        // Ẩn VFX lúc đầu
        if (stunVFX != null)
            stunVFX.SetActive(false);
    }

    void Update()
    {
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        Vector3 move = new Vector3(h, 0, v);
        bool isMoving = move.magnitude > 0.1f;
        bool isRunning = Input.GetKey(KeyCode.LeftShift);

        // ===== HIT + STUN (NHẤN H) =====
        if (Input.GetKeyDown(KeyCode.H))
        {
            animator.SetTrigger("hit");

            // bật VFX stun
            if (stunVFX != null)
            {
                stunVFX.SetActive(true);
                Invoke(nameof(HideStunVFX), 2f); // tắt sau 1 giây
            }
        }

        // ===== CROUCH =====
        if (Input.GetKeyDown(KeyCode.X))
        {
            isMoveCrouch = !isMoveCrouch;
            isCrouching = false;
        }

        if (Input.GetKeyDown(KeyCode.LeftControl))
        {
            isCrouching = !isCrouching;
            isMoveCrouch = false;
        }

        // ===== ANIMATOR =====
        animator.SetBool("isMoveCrouch", isMoveCrouch);
        animator.SetBool("isCrouching", isCrouching);

        animator.SetFloat("moveX", h);
        animator.SetFloat("moveZ", v);

        animator.SetBool("isMoving", isMoving && !isMoveCrouch && !isCrouching);

        // ===== MOVE =====
        if (isMoving)
        {
            float currentSpeed;

            if (isMoveCrouch)
                currentSpeed = speed * 0.5f;
            else if (isRunning && !isCrouching)
                currentSpeed = speed * 2;
            else if (!isCrouching)
                currentSpeed = speed;
            else
                currentSpeed = 0;

            transform.Translate(move * currentSpeed * Time.deltaTime, Space.World);

            if (move != Vector3.zero)
                transform.forward = move;
        }

        // ===== JUMP =====
        if (Input.GetKeyDown(KeyCode.Space) && isGrounded && !isMoveCrouch && !isCrouching)
        {
            isGrounded = false;
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            animator.SetTrigger("jump");
            animator.SetBool("isJumping", true);
        }

        // ===== NORMAL ANIM =====
        animator.SetBool("isRunning", isMoving && isRunning && !isMoveCrouch && !isCrouching);
        animator.SetBool("isWalking", isMoving && !isRunning && !isMoveCrouch && !isCrouching);

        // ===== AIM =====
        bool isAiming = Input.GetMouseButton(1);
        animator.SetBool("isAiming", isAiming);

        // ===== SHOOT =====
        if (Input.GetMouseButtonDown(0) && isAiming)
        {
            animator.SetTrigger("shoot");
        }

        // ===== ATTACK =====
        if (Input.GetMouseButtonDown(0) && !isAiming && !isMoveCrouch && !isCrouching)
        {
            animator.SetTrigger("attack");
        }

        if (Input.GetKeyDown(KeyCode.K) && !isAiming && !isMoveCrouch && !isCrouching)
        {
            animator.SetTrigger("kick");
        }
    }

    void HideStunVFX()
    {
        if (stunVFX != null)
            stunVFX.SetActive(false);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
        {
            isGrounded = true;
            animator.SetBool("isJumping", false);
        }
    }
}