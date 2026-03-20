using UnityEngine;

public class ThirdPersonCamera : MonoBehaviour
{
    // 🔥 đổi từ player -> target (quan trọng)
    public Transform target;

    public float mouseSensitivity = 200f;
    public float distance = 5f;
    public float height = 2f;

    float xRotation = 10f;
    float yRotation = 0f;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;

        // nếu quên gán target thì tự lấy Player
        if (target == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) target = p.transform;
        }
    }

    void LateUpdate()
    {
        if (target == null) return;

        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        yRotation += mouseX;
        xRotation -= mouseY;

        xRotation = Mathf.Clamp(xRotation, -30f, 60f);

        Quaternion rotation = Quaternion.Euler(xRotation, yRotation, 0);

        Vector3 position = target.position - rotation * Vector3.forward * distance + Vector3.up * height;

        transform.position = position;
        transform.LookAt(target.position + Vector3.up * 1.5f);
    }

    // 🔥 thêm hàm đổi target (cực quan trọng)
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }
}