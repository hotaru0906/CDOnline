using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;

public class UIAnim : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private Animator animator;
    private Vector3 originalScale;
    private Coroutine scaleCoroutine;

    [SerializeField] private float hoverScale = 1.1f;
    [SerializeField] private float scaleDuration = 0.2f;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        originalScale = transform.localScale;

        if (animator != null)
            animator.enabled = false;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (animator != null)
            animator.enabled = true;

        if (scaleCoroutine != null)
            StopCoroutine(scaleCoroutine);

        scaleCoroutine = StartCoroutine(ScaleButton(originalScale * hoverScale));
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (animator != null)
            animator.enabled = false;

        if (scaleCoroutine != null)
            StopCoroutine(scaleCoroutine);

        scaleCoroutine = StartCoroutine(ScaleButton(originalScale));
    }

    private IEnumerator ScaleButton(Vector3 targetScale)
    {
        Vector3 startScale = transform.localScale;
        float elapsedTime = 0f;

        while (elapsedTime < scaleDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.Clamp01(elapsedTime / scaleDuration);
            transform.localScale = Vector3.Lerp(startScale, targetScale, t);
            yield return null;
        }

        transform.localScale = targetScale;
    }
}