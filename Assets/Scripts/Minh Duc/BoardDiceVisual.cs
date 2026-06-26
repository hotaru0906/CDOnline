using System.Linq.Expressions;
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
    
    [SerializeField] private GameObject diceMesh;

    [Header("Spin")]
    [SerializeField] private float spinSpeed = 900f;

    private bool isSpinning = false;

    private Transform targetAnchor;

    private bool isFollowing = false;
    private void Awake()
    {
        Instance = this;
        diceMesh.SetActive(false);
    }

    private void Update()
    {
        if (isFollowing && targetAnchor != null)
        {
            Vector3 targetPos = targetAnchor.position;
            targetPos.y += Mathf.Sin(Time.time * hoverSpeed) * hoverHeight;

            transform.position = Vector3.Lerp(
                transform.position,
                targetPos,
                Time.deltaTime * moveSpeed);

            if (Vector3.Distance(transform.position, targetPos) < 0.05f)
            {
                transform.position = targetPos;
                isFollowing = false;
            }
        }

        if (isSpinning)
        {
            pivot.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.Self);
        }
    }

    public void ShowAt(Transform anchor)
    {
        targetAnchor = anchor;

        isFollowing = true;

        diceMesh.SetActive(true);
    }

    public void Hide()
    {
        targetAnchor = null;
        isFollowing = false;
        diceMesh.SetActive(false);
    }

    public void StartSpin()
    {
        isSpinning = true;
    }

    public void StopSpin()
    {
        isSpinning = false;
        pivot.localRotation = Quaternion.identity;
    }
}