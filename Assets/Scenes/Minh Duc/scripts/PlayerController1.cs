using UnityEngine;

public class PlayerController1 : MonoBehaviour
{
    public Rigidbody rb;
    public float jumpForce = 5f;

    private bool isGrounded = true;
    private Vector3 originalScale;

    void Start()
    {
        originalScale = transform.localScale; // lưu scale gốc
    }

    void Update()
    {
        // Jump
        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
        {
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            isGrounded = false;
        }

        // Crouch
        if (Input.GetKey(KeyCode.LeftControl))
        {
            transform.localScale = new Vector3(
                originalScale.x,
                originalScale.y * 0.5f,
                originalScale.z
            );
        }
        else
        {
            transform.localScale = originalScale;
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
        {
            isGrounded = true;
        }
    }
}