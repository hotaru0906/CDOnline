using UnityEngine;

public class TestAnimation : MonoBehaviour
{
    private Animator animator;
    public float speed = 5f;
    public float jumpForce = 5f;

    private Rigidbody rb;
    private bool isGrounded = true;

    private bool isMoveCrouch = false; // crouch di chuyển
    private bool isCrouching = false;  // crouch đứng yên

    void Start()
    {
        animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        Vector3 move = new Vector3(h, 0, v);
        bool isMoving = move.magnitude > 0.1f;
        bool isRunning = Input.GetKey(KeyCode.LeftShift);

        // ===== CROUCH DI CHUYỂN (X) =====
        if (Input.GetKeyDown(KeyCode.X))
        {
            isMoveCrouch = !isMoveCrouch;
            isCrouching = false; // không cho trùng trạng thái
        }

        // ===== CROUCH NGỒI (CTRL) =====
        if (Input.GetKeyDown(KeyCode.LeftControl))
        {
            isCrouching = !isCrouching;
            isMoveCrouch = false; // không cho trùng trạng thái
        }

        // ===== ANIMATOR =====
        animator.SetBool("isMoveCrouch", isMoveCrouch);
        animator.SetBool("isCrouching", isCrouching);

        animator.SetFloat("moveX", h);
        animator.SetFloat("moveZ", v);

        // chỉ tính moving khi KHÔNG crouch
        animator.SetBool("isMoving", isMoving && !isMoveCrouch && !isCrouching);

        // ===== DI CHUYỂN =====
        if (isMoving)
        {
            float currentSpeed;

            if (isMoveCrouch)
                currentSpeed = speed * 0.5f; // đi cúi
            else if (isRunning && !isCrouching)
                currentSpeed = speed * 2; // chạy
            else if (!isCrouching)
                currentSpeed = speed; // đi thường
            else
                currentSpeed = 0; // ngồi thì không di chuyển

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

        // ===== ANIMATION NORMAL =====
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

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
        {
            isGrounded = true;
            animator.SetBool("isJumping", false);
        }
    }
}