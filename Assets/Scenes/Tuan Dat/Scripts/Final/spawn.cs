using System.Collections;
using UnityEngine;

public class spawn : MonoBehaviour
{
    [Header("Final scene spawn points")]
    [SerializeField] private Transform top1SpawnPoint;
    [SerializeField] private Transform otherPlayersSpawnPoint;
    [SerializeField] private float otherPlayersCircleRadius = 2.2f;

    [Header("Optional override")]
    [SerializeField] private int forceWinnerPlayerId = -1;
    [SerializeField] private bool autoApplyOnStart = true;

    private void Start()
    {
        if (autoApplyOnStart)
        {
            StartCoroutine(ApplySpawnAfterOneFrame());
        }
    }

    private IEnumerator ApplySpawnAfterOneFrame()
    {
        yield return null;
        ApplyFinalSceneSpawn();
    }

    public void ApplyFinalSceneSpawn()
    {
        var playerDatas = FindObjectsByType<PlayerNetworkData>(FindObjectsSortMode.None);
        if (playerDatas == null || playerDatas.Length == 0)
        {
            Debug.LogWarning("[FinalSceneSpawn] Không tìm thấy PlayerNetworkData trong scene final.");
            return;
        }

        bool hasStateAuthority = false;
        foreach (var playerData in playerDatas)
        {
            if (playerData != null && playerData.Object != null && playerData.Object.HasStateAuthority)
            {
                hasStateAuthority = true;
                break;
            }
        }

        if (!hasStateAuthority)
        {
            Debug.Log("[FinalSceneSpawn] Không phải host/state authority, chờ host xử lý spawn.");
            return;
        }

        int winnerPlayerId = ResolveWinnerPlayerId();

        for (int i = 0; i < playerDatas.Length; i++)
        {
            var playerData = playerDatas[i];
            if (playerData == null || playerData.Object == null)
                continue;

            if (!playerData.Object.HasStateAuthority)
                continue;

            int playerId = playerData.Object.InputAuthority.PlayerId;
            var controller = playerData.GetComponent<PlayerController>();
            if (controller == null)
                continue;

            if (winnerPlayerId >= 0 && playerId == winnerPlayerId)
            {
                TeleportToPoint(controller, top1SpawnPoint, "Top 1");
            }
            else
            {
                Vector3 sharedPos = GetSharedOtherSpawnPosition(playerId, playerDatas.Length);
                Quaternion sharedRot = otherPlayersSpawnPoint != null ? otherPlayersSpawnPoint.rotation : Quaternion.identity;
                controller.Teleport(sharedPos);
                controller.transform.rotation = sharedRot;
                controller.SetFrozen(false);
                Debug.Log($"[FinalSceneSpawn] P{playerId} spawn ở điểm chung: {sharedPos}");
            }
        }
    }

    private int ResolveWinnerPlayerId()
    {
        if (forceWinnerPlayerId >= 0)
            return forceWinnerPlayerId;

        if (BoardManager.Instance != null && BoardManager.Instance.WinnerPlayerId >= 0)
            return BoardManager.Instance.WinnerPlayerId;

        if (GameManager.Instance != null && GameManager.Instance.FinalWinnerId >= 0)
            return GameManager.Instance.FinalWinnerId;

        Debug.LogWarning("[FinalSceneSpawn] Không có winner trong BoardManager/GameManager, mặc định spawn tất cả ở vị trí chung.");
        return -1;
    }

    private void TeleportToPoint(PlayerController controller, Transform spawnPoint, string label)
    {
        if (controller == null)
            return;

        if (spawnPoint == null)
        {
            controller.transform.position = Vector3.zero;
            controller.transform.rotation = Quaternion.identity;
            controller.SetFrozen(false);
            Debug.LogWarning($"[FinalSceneSpawn] {label} chưa gắn spawn point, dùng Vector3.zero.");
            return;
        }

        controller.Teleport(spawnPoint.position);
        controller.transform.rotation = spawnPoint.rotation;
        controller.SetFrozen(false);
        Debug.Log($"[FinalSceneSpawn] {label} spawn tại {spawnPoint.position}");
    }

    private Vector3 GetSharedOtherSpawnPosition(int playerId, int totalPlayers)
    {
        Vector3 origin = otherPlayersSpawnPoint != null ? otherPlayersSpawnPoint.position : Vector3.zero;

        if (totalPlayers <= 1)
            return origin;

        var playerDatas = FindObjectsByType<PlayerNetworkData>(FindObjectsSortMode.None);
        int index = 0;

        for (int i = 0; i < playerDatas.Length; i++)
        {
            if (playerDatas[i] == null || playerDatas[i].Object == null)
                continue;

            if (playerDatas[i].Object.InputAuthority.PlayerId == playerId)
            {
                index = i;
                break;
            }
        }

        float angle = index * (2f * Mathf.PI / totalPlayers);
        float x = origin.x + Mathf.Cos(angle) * otherPlayersCircleRadius;
        float z = origin.z + Mathf.Sin(angle) * otherPlayersCircleRadius;
        return new Vector3(x, origin.y, z);
    }
}
