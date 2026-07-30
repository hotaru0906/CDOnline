using UnityEngine;

public class BoardDiceVisual : MonoBehaviour
{
    public static BoardDiceVisual Instance;

    [Header("References")]
    [SerializeField] private Transform pivot;

    [Header("Follow")]
    [SerializeField] private float moveSpeed = 8f;

    [Header("Hover")]
    [SerializeField] private float hoverHeight = 0.8f;
    [SerializeField] private float hoverSpeed = 2.2f;

    [SerializeField] private GameObject diceMesh;

    [Header("Spin")]
    [SerializeField] private float spinSpeed = 900f;
    [SerializeField] private Vector3[] faceEulerRotations = new Vector3[12];

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
            float bob = Mathf.Sin(Time.time * hoverSpeed) * hoverHeight * 0.15f;
            Vector3 targetPos = targetAnchor.position + Vector3.up * (hoverHeight + bob);

            transform.position = Vector3.Lerp(
                transform.position,
                targetPos,
                Time.deltaTime * moveSpeed);
        }

        if (isSpinning && pivot != null)
        {
            Vector3 spinAxis = new Vector3(1f, 0.3f, 0.8f).normalized;
            pivot.Rotate(spinAxis, spinSpeed * Time.deltaTime, Space.Self);
        }
    }

    public void ShowAt(Transform anchor)
    {
        targetAnchor = anchor;
        isFollowing = true;
        diceMesh.SetActive(true);

        if (targetAnchor != null)
        {
            Vector3 targetPos = targetAnchor.position + Vector3.up * (hoverHeight + 0.05f);
            transform.position = targetPos;
        }
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
        StopSpin(1);
    }

    public void StopSpin(int faceValue)
    {
        isSpinning = false;

        if (pivot == null)
            return;

        int clampedFace = Mathf.Clamp(faceValue, 1, faceEulerRotations.Length);
        int index = clampedFace - 1;

        if (index >= 0 && index < faceEulerRotations.Length)
        {
            pivot.localRotation = Quaternion.Euler(faceEulerRotations[index]);
        }
        else
        {
            pivot.localRotation = Quaternion.identity;
        }
    }
}