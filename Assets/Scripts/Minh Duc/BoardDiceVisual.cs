using Unity.Cinemachine;
using UnityEngine;

public class BoardDiceVisual : MonoBehaviour
{
    public static BoardDiceVisual Instance;

    [Header("References")]
    [SerializeField] private Transform pivot;

    [Header("Follow")]
    [SerializeField] private float moveSpeed = 8f;

    [Header("Hover")]
    [SerializeField] private float hoverHeight = 0.4f;
    [SerializeField] private float hoverSpeed = 2f;

    private Transform targetAnchor;

    private bool isFollowing = false;
    private void Awake()
    {
        Instance = this;
        gameObject.SetActive(false);
    }

    private void Update()
    {
        if (!isFollowing || targetAnchor == null)
            return;

        Vector3 targetPos = targetAnchor.position;
        targetPos.y += Mathf.Sin(Time.time * hoverSpeed) * hoverHeight;

        transform.position = Vector3.Lerp(
            transform.position,
            targetPos,
            Time.deltaTime * moveSpeed);

        // Khi toi gan thi dung follow
        if (Vector3.Distance(transform.position, targetPos) < 0.05f)
        {
            transform.position = targetPos;
            isFollowing = false;
        }
        
    }

    public void ShowAt(Transform anchor)
    {
        targetAnchor = anchor;

        transform.position = anchor.position;

        isFollowing = false;

        gameObject.SetActive(true);
    }

    public void Hide()
    {
        targetAnchor = null;
        gameObject.SetActive(false);
    }
}