using UnityEngine;

public class PlayerSit : MonoBehaviour
{
    public float moveSpeed = 5f;

    private Animator anim;
    private PlayerMovement1 movement;

    private Transform targetSitPoint;

    private bool isNearChair = false;
    private bool isMovingToSit = false;
    private bool isSitting = false;

    private Chair currentChair;

    void Start()
    {
        anim = GetComponent<Animator>();
        movement = GetComponent<PlayerMovement1>();
    }

    void Update()
    {
        // Nhấn E để ngồi
        if (isNearChair && Input.GetKeyDown(KeyCode.E) && !isSitting)
        {
            targetSitPoint = currentChair.sitPoint;
            isMovingToSit = true;

            // Tắt di chuyển
            if (movement != null)
                movement.enabled = false;
        }

        // Di chuyển vào ghế
        if (isMovingToSit && targetSitPoint != null)
        {
            transform.position = Vector3.MoveTowards(
                transform.position,
                targetSitPoint.position,
                moveSpeed * Time.deltaTime
            );

            transform.rotation = Quaternion.Lerp(
                transform.rotation,
                targetSitPoint.rotation,
                moveSpeed * Time.deltaTime
            );

            // Khi tới nơi
            if (Vector3.Distance(transform.position, targetSitPoint.position) < 0.05f)
            {
                transform.position = targetSitPoint.position;
                transform.rotation = targetSitPoint.rotation;

                isMovingToSit = false;
                isSitting = true;

                // 🎬 Play animation
                anim.Play("Sit");
            }
        }
    }

    // Detect ghế
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Chair"))
        {
            currentChair = other.GetComponent<Chair>();
            isNearChair = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Chair"))
        {
            isNearChair = false;
            currentChair = null;
        }
    }
}