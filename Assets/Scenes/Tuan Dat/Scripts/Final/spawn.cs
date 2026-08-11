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
            StartCoroutine(ApplySpawnWhenPlayersReady());
        }
    }

    private IEnumerator ApplySpawnWhenPlayersReady()
    {
        float elapsed = 0f;
        const float maxWait = 2.0f;

        while (elapsed < maxWait)
        {
            var playerDatas = FindObjectsByType<PlayerNetworkData>(FindObjectsSortMode.None);
            if (playerDatas != null && playerDatas.Length > 0)
            {
                bool allReady = true;
                foreach (var playerData in playerDatas)
                {
                    if (playerData == null || playerData.Object == null || playerData.GetComponent<PlayerController>() == null)
                    {
                        allReady = false;
                        break;
                    }
                }

                if (allReady)
                {
                    ApplyFinalSceneSpawn();
                    yield break;
                }
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        Debug.LogWarning("[FinalSceneSpawn] Hết thời gian chờ, vẫn chưa có PlayerNetworkData đầy đủ. Chạy ApplyFinalSceneSpawn() dù vậy.");
        ApplyFinalSceneSpawn();
    }

    public void ApplyFinalSceneSpawn()
    {
        if (GameManager.Instance != null && !GameManager.Instance.HasStateAuthority)
        {
            Debug.Log("[FinalSceneSpawn] Chỉ host/state authority mới xử lý spawn final.");
            return;
        }
        var playerDatas = FindObjectsByType<PlayerNetworkData>(FindObjectsSortMode.None);
        if (playerDatas == null || playerDatas.Length == 0)
        {
            Debug.LogWarning("[FinalSceneSpawn] Không tìm thấy PlayerNetworkData trong scene final.");
            return;
        }

        int winnerPlayerId = ResolveWinnerPlayerId();
        Debug.Log($"[FinalSceneSpawn] WinnerPlayerId={winnerPlayerId}. Top1 point={(top1SpawnPoint != null ? top1SpawnPoint.position.ToString() : "NULL")}");

        // 1) Collect PlayerController instances (only where NetworkObject exists)
        var controllers = new System.Collections.Generic.List<PlayerController>();
        var playerIds = new System.Collections.Generic.List<int>();

        for (int i = 0; i < playerDatas.Length; i++)
        {
            var pd = playerDatas[i];
            if (pd == null || pd.Object == null) continue;
            var pc = pd.GetComponent<PlayerController>();
            if (pc == null) continue;
            controllers.Add(pc);
            playerIds.Add(pd.Object.InputAuthority.PlayerId);
        }

        if (controllers.Count == 0)
        {
            Debug.LogWarning("[FinalSceneSpawn] Không có PlayerController nào để teleport.");
            return;
        }

        // 2) Freeze all players on host/state authority to prevent client movement overwriting
        foreach (var pc in controllers)
        {
            try { pc.SetFrozen(true); } catch { }
        }

        // 3) Teleport winner and others
        for (int i = 0; i < controllers.Count; i++)
        {
            var pc = controllers[i];
            int pid = playerIds[i];

            if (winnerPlayerId >= 0 && pid == winnerPlayerId)
            {
                TeleportToPoint(pc, top1SpawnPoint, "Top 1");
            }
            else
            {
                Vector3 sharedPos = GetSharedOtherSpawnPosition(pid, controllers.Count);
                Quaternion sharedRot = otherPlayersSpawnPoint != null ? otherPlayersSpawnPoint.rotation : Quaternion.identity;
                pc.Teleport(sharedPos);
                pc.transform.rotation = sharedRot;
                Debug.Log($"[FinalSceneSpawn] P{pid} teleport to shared: {sharedPos}");
            }
        }

        // 4) Unfreeze players so they can receive input again
        foreach (var pc in controllers)
        {
            try { pc.SetFrozen(false); } catch { }
        }

        Debug.Log("[FinalSceneSpawn] Teleport complete for all players.");
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
