using UnityEngine;
using UnityEngine.EventSystems;

public class CharacterPreviewRotator : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
{
    [Header("Model cần xoay")]
    public Transform characterModel;          // Kéo Capsule (hoặc PlayerModel) vào đây

    [Header("Tốc độ xoay")]
    public float sensitivity = 0.8f;          // Càng lớn càng xoay nhanh
    public float smoothSpeed = 12f;           // Làm mượt

    private bool isDragging = false;
    private Vector2 lastMousePosition;
    private Vector3 targetRotation;

    void Start()
    {
        if (characterModel != null)
            targetRotation = characterModel.eulerAngles;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        isDragging = true;
        lastMousePosition = eventData.position;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isDragging = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging || characterModel == null) return;

        Vector2 delta = eventData.position - lastMousePosition;

        // Xoay ngang (trái/phải) - trục Y
        float rotationY = -delta.x * sensitivity;

        // Xoay dọc (lên/xuống) - trục X (nếu bạn muốn, comment nếu chỉ muốn xoay ngang)
        float rotationX = delta.y * sensitivity * 0.6f;

        targetRotation.y += rotationY;
        targetRotation.x = Mathf.Clamp(targetRotation.x + rotationX, -30f, 30f); // Giới hạn góc nhìn

        lastMousePosition = eventData.position;
    }

    void Update()
    {
        if (characterModel != null)
        {
            // Làm mượt khi xoay
            characterModel.rotation = Quaternion.Lerp(
                characterModel.rotation, 
                Quaternion.Euler(targetRotation), 
                Time.deltaTime * smoothSpeed
            );
        }
    }
}