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