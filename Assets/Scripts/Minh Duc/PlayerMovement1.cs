using UnityEngine;

public class PlayerMovement1 : MonoBehaviour, IRespawnable
{
    CharacterController controller;
    Animator animator;

    public Transform cameraTransform;

    public float walkSpeed = 5f;
    public float runSpeed = 9f;
    public float jumpHeight = 2f;
    public float gravity = -20f;
    public Transform spawnPoint;

    Vector3 velocity;

    bool isAttacking = false;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponentInChildren<Animator>();

        if (controller == null)
        {
            Debug.LogError("❌ THIEU CharacterController");
        }

        if (animator == null)
        {
            Debug.LogError("❌ KHONG TIM THAY ANIMATOR");
        }

        if (cameraTransform == null)
        {
            Debug.LogError("❌ CHUA GAN CAMERA TRANSFORM");
        }
    }

    void Update()
    {
        if (controller == null || cameraTransform == null) return;

        bool isGrounded = controller.isGrounded;

        if (isGrounded && velocity.y < 0)
            velocity.y = -2f;

        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");

        Vector3 move = cameraTransform.forward * z + cameraTransform.right * x;
        move.y = 0;

        bool isMoving = move.magnitude > 0.01f;
        bool isRunning = Input.GetKey(KeyCode.LeftShift);

        float speed = isRunning ? runSpeed : walkSpeed;

        // 🚀 DI CHUYỂN (chỉ khi KHÔNG attack)
        if (!isAttacking)
        {
            controller.Move(move.normalized * speed * Time.deltaTime);

            if (isMoving)
            {
                Quaternion rot = Quaternion.LookRotation(move);
                transform.rotation = Quaternion.Slerp(transform.rotation, rot, 10f * Time.deltaTime);
            }
        }

        // 🚀 JUMP
        if (Input.GetKeyDown(KeyCode.Space) && isGrounded && !isAttacking)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            animator.SetTrigger("Jump");
        }

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);

        // 🚀 ATTACK
        if (Input.GetMouseButtonDown(0) && !isAttacking && isGrounded)
        {
            isAttacking = true;
            animator.SetTrigger("Attack");

            // 🔥 FIX NHANH: tự reset sau 0.7s (tránh bị kẹt)
            Invoke(nameof(EndAttack), 0.7f);
        }

        // 🎯 ANIMATION
        animator.SetBool("IsRunning", isRunning && isMoving && !isAttacking);
        animator.SetBool("IsWalking", isMoving && !isRunning && !isAttacking);

        Debug.Log("Run: " + isRunning + " Move: " + isMoving + " Attack: " + isAttacking);
    }

    // ✅ RESET ATTACK
    public void EndAttack()
    {
        isAttacking = false;
    }

    //spawn
    public void Respawn()
    {
        controller.enabled = false;
        transform.position = spawnPoint.position;
        controller.enabled = true;

        velocity = Vector3.zero;
        isAttacking = false;

        animator.Rebind();
    }
}