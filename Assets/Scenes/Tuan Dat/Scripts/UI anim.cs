using UnityEngine;
using UnityEngine.EventSystems;

public class UIAnim : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private Animator animator;

    private void Awake()
    {
        animator = GetComponent<Animator>();

        // Vừa vào game: tắt animation
        animator.enabled = false;
    }

    // Chuột lia vào Button
    public void OnPointerEnter(PointerEventData eventData)
    {
        animator.enabled = true;
    }

    // Chuột lia ra khỏi Button
    public void OnPointerExit(PointerEventData eventData)
    {
        animator.enabled = false;
    }
}