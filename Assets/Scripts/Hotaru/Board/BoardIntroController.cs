using UnityEngine;
using System.Collections;

public class BoardIntroController : MonoBehaviour
{
    [Header("Camera References")]
    [SerializeField] private Camera introCamera;
    [SerializeField] private Camera mainCamera;
    [SerializeField] private BoardSpectatorCameraController spectatorCameraController;
    public static BoardIntroController Instance;

    [Header("Orbit")]
    [SerializeField] private float orbitRadius = 35f;
    [SerializeField] private float orbitHeight = 20f;
    [SerializeField] private float orbitDuration = 8f;

    private void Awake()
    {
        Instance = this;

        if (introCamera != null)
            introCamera.gameObject.SetActive(false);
    }

    private Vector3 GetBoardCenter()
    {
        BoardNode[] nodes =
            FindObjectsByType<BoardNode>(FindObjectsSortMode.None);

        if (nodes.Length == 0)
            return Vector3.zero;

        Vector3 center = Vector3.zero;

        foreach (BoardNode node in nodes)
        {
            center += node.transform.position;
        }

        return center / nodes.Length;
    }

    public IEnumerator PlayIntro()
    {
        spectatorCameraController?.SetIntroActive(true);
        introCamera.gameObject.SetActive(true);
        mainCamera.gameObject.SetActive(false);

        yield return OrbitAroundBoard();

        introCamera.gameObject.SetActive(false);
        mainCamera.gameObject.SetActive(true);
        spectatorCameraController?.SetIntroActive(false);
    }

    private IEnumerator OrbitAroundBoard()
    {
        float elapsed = 0f;
        Vector3 center = GetBoardCenter();

        while (elapsed < orbitDuration)
        {
            elapsed += Time.deltaTime;

            float t = elapsed / orbitDuration;

            t = Mathf.SmoothStep(0f, 1f, t);

            float angle = Mathf.Lerp(0f, 360f, t);

            float rad = angle * Mathf.Deg2Rad;

            Vector3 position =
                center +
                new Vector3(
                    Mathf.Cos(rad) * orbitRadius,
                    orbitHeight,
                    Mathf.Sin(rad) * orbitRadius
                );

            introCamera.transform.position = position;

            introCamera.transform.LookAt(center);

            yield return null;
        }
    }

    private IEnumerator MoveCameraTo(
    Vector3 targetPosition,
    Vector3 lookTarget,
    float duration)
    {
        Vector3 startPos = introCamera.transform.position;
        Quaternion startRot = introCamera.transform.rotation;

        Quaternion targetRot =
            Quaternion.LookRotation(
                lookTarget - targetPosition);

        float t = 0f;

        while (t < duration)
        {
            t += Time.deltaTime;

            float p = Mathf.SmoothStep(0f, 1f, t / duration);

            introCamera.transform.position =
                Vector3.Lerp(
                    startPos,
                    targetPosition,
                    p);

            introCamera.transform.rotation =
                Quaternion.Slerp(
                    startRot,
                    targetRot,
                    p);

            yield return null;
        }
    }
}