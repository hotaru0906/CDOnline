using Fusion;
using UnityEngine;
using System.Collections;

/// <summary>
/// Controller cho Roulette scene.
/// Quản lý việc setup scene khi load xong và báo GameManager bắt đầu Roulette gameplay.
/// Đặt script này vào một GameObject trong scene "Roulette Test" và đảm bảo nó có NetworkObject.
/// </summary>
public class RouletteSceneController : NetworkBehaviour
{
    public static RouletteSceneController Instance { get; private set; }

    [Header("Spawn Settings")]
    [Tooltip("Các vị trí spawn cho players trong Roulette scene (6 vị trí tương ứng 6 seats)")]
    [SerializeField] private Transform[] spawnPoints;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public override void Spawned()
    {
        Debug.Log($"[RouletteSceneController] Spawned. IsHost: {HasStateAuthority}");

        if (HasStateAuthority)
        {
            // Đợi một chút để đảm bảo tất cả objects đã load
            StartCoroutine(WaitThenSetupRoulette());
        }
    }

    private IEnumerator WaitThenSetupRoulette()
    {
        Debug.Log("[RouletteSceneController] Waiting for scene to fully load...");

        // Đợi đến cuối frame để đảm bảo tất cả objects đã spawn
        yield return new WaitForEndOfFrame();
        yield return new WaitForSeconds(0.5f);

        // Kiểm tra GameManager và RouletteManager
        if (GameManager.Instance == null)
        {
            Debug.LogError("[RouletteSceneController] GameManager.Instance is NULL!");
            yield break;
        }

        if (RouletteManager.Instance == null)
        {
            Debug.LogError("[RouletteSceneController] RouletteManager.Instance is NULL!");
            yield break;
        }

        // Báo GameManager scene đã sẵn sàng
        Debug.Log("[RouletteSceneController] Scene ready, notifying GameManager");
        GameManager.Instance.OnRouletteSceneReady();
    }

    /// <summary>
    /// Lấy vị trí spawn cho seat index
    /// </summary>
    public Vector3 GetSpawnPosition(int seatIndex)
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogWarning("[RouletteSceneController] No spawn points configured!");
            return Vector3.zero;
        }

        if (seatIndex < 0 || seatIndex >= spawnPoints.Length)
        {
            Debug.LogWarning($"[RouletteSceneController] Invalid seat index: {seatIndex}");
            return spawnPoints[0].position;
        }

        return spawnPoints[seatIndex].position;
    }

    /// <summary>
    /// Lấy rotation spawn cho seat index
    /// </summary>
    public Quaternion GetSpawnRotation(int seatIndex)
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            return Quaternion.identity;
        }

        if (seatIndex < 0 || seatIndex >= spawnPoints.Length)
        {
            return spawnPoints[0].rotation;
        }

        return spawnPoints[seatIndex].rotation;
    }
}
