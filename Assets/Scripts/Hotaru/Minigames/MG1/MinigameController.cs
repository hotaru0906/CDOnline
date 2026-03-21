using Fusion;
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Controller chính cho mỗi minigame scene.
/// Quản lý spawn players, win condition, và kết thúc game.
/// </summary>
public class MinigameController : NetworkBehaviour
{
    public static MinigameController Instance { get; private set; }

    [Header("Spawn Settings")]
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private float spawnDelay = 1f;

    [Header("Game Settings")]
    [SerializeField] private float countdownTime = 3f;
    [SerializeField] private bool freezePlayersOnStart = true;

    [Networked]
    public NetworkBool IsGameStarted { get; private set; }

    [Networked]
    public NetworkBool IsGameEnded { get; private set; }

    [Networked]
    public PlayerRef Winner { get; private set; }

    [Networked]
    public float Countdown { get; private set; }

    // Local reference
    private List<PlayerController> spawnedPlayers = new List<PlayerController>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public override void Spawned()
    {
        Debug.Log($"[MinigameController] Spawned. IsHost: {HasStateAuthority}");

        if (HasStateAuthority)
        {
            // Host bắt đầu setup minigame
            StartCoroutine(SetupMinigame());
        }
    }

    private System.Collections.IEnumerator SetupMinigame()
    {
        yield return new WaitForSeconds(spawnDelay);
        yield return new WaitForSeconds(0.2f); // đảm bảo player spawn xong

        // Spawn/Teleport players to spawn points
        TeleportPlayersToSpawnPoints();

        // Freeze players nếu cần
        if (freezePlayersOnStart)
        {
            RPC_SetPlayersFrozen(true);
        }

        // Start countdown
        Countdown = countdownTime;
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;

        // Countdown logic
        if (Countdown > 0 && !IsGameStarted)
        {
            Countdown -= Runner.DeltaTime;

            if (Countdown <= 0)
            {
                StartGame();
            }
        }
    }

    private void TeleportPlayersToSpawnPoints()
    {
        var players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        
        // Sort theo PlayerRef để spawn order deterministic
        System.Array.Sort(players, (a, b) =>
            a.Object.InputAuthority.PlayerId.CompareTo(b.Object.InputAuthority.PlayerId)
        );
        
        int spawnIndex = 0;

        foreach (var player in players)
        {
            // Dùng modulo để handle trường hợp ít spawn points hơn players
            var spawnPoint = spawnPoints[spawnIndex % spawnPoints.Length];
            var targetPos = spawnPoint.position;

            // 🔥 Host set trực tiếp
            var cc = player.GetComponent<CharacterController>();
            if (cc != null)
            {
                cc.enabled = false;
                player.transform.position = targetPos;
                cc.enabled = true;
            }
            else
            {
                player.transform.position = targetPos;
            }

            // Reset checkpoint - truyền vị trí spawn để đảm bảo đúng
            var minigameData = player.GetComponent<PlayerMinigameData>();
            if (minigameData != null)
            {
                minigameData.ResetCheckpoint(targetPos);
            }

            spawnIndex++;
        }

        Debug.Log($"[MinigameController] Teleported {spawnIndex} players to spawn points");
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_SetPlayersFrozen(bool frozen)
    {
        var players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        foreach (var player in players)
        {
            player.SetFrozen(frozen);
        }
        Debug.Log($"[MinigameController] Players frozen: {frozen}");
    }

    private void StartGame()
    {
        if (!HasStateAuthority) return;

        Debug.Log("[MinigameController] Game Started!");
        IsGameStarted = true;

        // Unfreeze players
        RPC_SetPlayersFrozen(false);
        RPC_OnGameStarted();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_OnGameStarted()
    {
        Debug.Log("[MinigameController] RPC: Game Started!");
        // UI có thể subscribe event ở đây
    }

    /// <summary>
    /// Gọi khi player về đích - chỉ host gọi
    /// </summary>
    public void PlayerFinished(PlayerRef playerRef)
    {
        if (!HasStateAuthority) return;
        if (IsGameEnded) return;

        Debug.Log($"[MinigameController] Player {playerRef} finished!");

        Winner = playerRef;
        IsGameEnded = true;

        // Notify all clients
        RPC_OnPlayerWon(playerRef);

        // End game sau 3 giây
        StartCoroutine(EndGameAfterDelay(3f));
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_OnPlayerWon(PlayerRef winnerRef)
    {
        Debug.Log($"[MinigameController] Player {winnerRef} WON!");

        // Freeze all players
        var players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        foreach (var player in players)
        {
            player.SetFrozen(true);
        }
    }

    private System.Collections.IEnumerator EndGameAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        // Notify GameManager to go to Scoreboard
        if (GameManager.Instance != null)
        {
            GameManager.Instance.EndMinigame();
        }
    }

    /// <summary>
    /// Lấy spawn point cho player (dùng cho BasicSpawner và checkpoint system)
    /// </summary>
    public Vector3 GetSpawnPoint(int playerIndex)
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
            return Vector3.zero;

        // Dùng modulo để handle nhiều players hơn spawn points
        int index = playerIndex % spawnPoints.Length;
        return spawnPoints[index].position;
    }
}
