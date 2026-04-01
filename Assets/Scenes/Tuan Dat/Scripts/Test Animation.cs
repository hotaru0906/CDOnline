using UnityEngine;

public class TestAnimation : MonoBehaviour
{
    private Animator animator;
    public float speed = 5f;
    public float jumpForce = 5f;

    private Rigidbody rb;
    private bool isGrounded = true;
    private bool isCrouching = false;

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
       

        // ===== DI CHUYỂN =====
        if (isMoving && !isCrouching)
        {
            float currentSpeed = isRunning ? speed * 2 : speed;
            transform.Translate(move * currentSpeed * Time.deltaTime, Space.World);
            transform.forward = move;
        }

        // ===== JUMP =====
      if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
{
    rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
    animator.SetTrigger("jump");
}

        // ===== CROUCH =====
        animator.SetBool("isCrouching", isCrouching);

        // ===== ANIMATION =====
        animator.SetBool("isRunning", isMoving && isRunning && !isCrouching);
        animator.SetBool("isWalking", isMoving && !isRunning && !isCrouching);
         if (Input.GetKeyDown(KeyCode.LeftControl))
    {
        isCrouching = !isCrouching; // toggle
        animator.SetBool("isCrouching", isCrouching);
    }
    
    
// ===== AIM =====
bool isAiming = Input.GetMouseButton(1); // giữ chuột phải
animator.SetBool("isAiming", isAiming);

// ===== SHOOT =====
if (Input.GetMouseButtonDown(0) && isAiming)
{
    animator.SetTrigger("shoot");
}

// ===== ATTACK (chỉ khi KHÔNG aim) =====
if (Input.GetMouseButtonDown(0) && !isAiming && !isCrouching)
{
    animator.SetTrigger("attack");
}
    }

    // ===== KIỂM TRA CHẠM ĐẤT =====
    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
        {
            isGrounded = true;
            animator.SetBool("isJumping", false);
        }
    }
}