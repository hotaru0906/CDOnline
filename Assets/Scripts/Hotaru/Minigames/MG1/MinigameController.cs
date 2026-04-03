using Fusion;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Controller chính cho mỗi minigame scene.
/// Quản lý Tutorial -> Countdown -> Playing -> Scoreboard
/// </summary>
public class MinigameController : NetworkBehaviour
{
    public static MinigameController Instance { get; private set; }

    [Header("Spawn Settings")]
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private float spawnDelay = 1f;

    [Header("Tutorial Canvas")]
    [SerializeField] private GameObject tutorialCanvas;
    [SerializeField] private Button startButton; // Host only
    
    [Header("Countdown Canvas")]
    [SerializeField] private GameObject countdownCanvas;
    [SerializeField] private TMP_Text countdownText;
    [SerializeField] private float countdownTime = 3f;
    
    [Header("Scoreboard Canvas")]
    [SerializeField] private GameObject scoreboardCanvas;
    [SerializeField] private float scoreboardDuration = 5f;

    [Header("Game Settings")]
    [SerializeField] private bool freezePlayersOnStart = true;

    [Header("Trap Spawner (Optional)")]
    [SerializeField] private TrapSpawner trapSpawner;

    #region Networked Properties
    [Networked, OnChangedRender(nameof(OnPhaseChanged))]
    public MinigamePhase CurrentPhase { get; private set; } = MinigamePhase.WaitingForPlayers;

    [Networked]
    public NetworkBool IsGameStarted { get; private set; }

    [Networked]
    public NetworkBool IsGameEnded { get; private set; }

    [Networked]
    public PlayerRef Winner { get; private set; }

    [Networked, OnChangedRender(nameof(OnCountdownChanged))]
    public float Countdown { get; private set; }
    #endregion

    // Local reference
    private List<PlayerController> spawnedPlayers = new List<PlayerController>();
    private NetworkRunner _runner;

    public enum MinigamePhase
    {
        WaitingForPlayers,
        Tutorial,
        Countdown,
        Playing,
        GameOver,
        Scoreboard
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        
        // Hide all canvases initially
        SetCanvasActive(tutorialCanvas, false);
        SetCanvasActive(countdownCanvas, false);
        SetCanvasActive(scoreboardCanvas, false);
        
        // Disable trap spawner initially
        if (trapSpawner != null)
        {
            trapSpawner.enabled = false;
        }
    }

    public override void Spawned()
    {
        Debug.Log($"[MinigameController] Spawned. IsHost: {HasStateAuthority}");
        _runner = Runner;

        // Setup start button (host only)
        if (startButton != null)
        {
            startButton.onClick.RemoveAllListeners();
            startButton.onClick.AddListener(OnStartButtonClicked);
            startButton.gameObject.SetActive(HasStateAuthority);
        }

        if (HasStateAuthority)
        {
            // Host bắt đầu setup minigame
            StartCoroutine(SetupMinigame());
        }
    }

    private IEnumerator SetupMinigame()
    {
        yield return new WaitForSeconds(spawnDelay);
        yield return new WaitForSeconds(0.2f); // đảm bảo player spawn xong

        // Teleport players to spawn points
        TeleportPlayersToSpawnPoints();

        // Freeze players
        if (freezePlayersOnStart)
        {
            RPC_SetPlayersFrozen(true);
        }

        // Chuyển sang phase Tutorial
        CurrentPhase = MinigamePhase.Tutorial;
    }

    #region Phase Handlers
    private void OnPhaseChanged()
    {
        Debug.Log($"[MinigameController] Phase changed to: {CurrentPhase}");
        
        switch (CurrentPhase)
        {
            case MinigamePhase.Tutorial:
                HandleTutorialPhase();
                break;
            case MinigamePhase.Countdown:
                HandleCountdownPhase();
                break;
            case MinigamePhase.Playing:
                HandlePlayingPhase();
                break;
            case MinigamePhase.GameOver:
                HandleGameOverPhase();
                break;
            case MinigamePhase.Scoreboard:
                HandleScoreboardPhase();
                break;
        }
    }

    private void HandleTutorialPhase()
    {
        Debug.Log("[MinigameController] Tutorial phase started");
        
        SetCanvasActive(tutorialCanvas, true);
        SetCanvasActive(countdownCanvas, false);
        SetCanvasActive(scoreboardCanvas, false);
        
        // Update start button visibility
        if (startButton != null)
        {
            bool isHost = _runner != null && _runner.IsServer;
            startButton.gameObject.SetActive(isHost);
        }
    }

    private void HandleCountdownPhase()
    {
        Debug.Log("[MinigameController] Countdown phase started");
        
        SetCanvasActive(tutorialCanvas, false);
        SetCanvasActive(countdownCanvas, true);
        SetCanvasActive(scoreboardCanvas, false);
    }

    private void HandlePlayingPhase()
    {
        Debug.Log("[MinigameController] Playing phase started");
        
        SetCanvasActive(tutorialCanvas, false);
        SetCanvasActive(countdownCanvas, false);
        SetCanvasActive(scoreboardCanvas, false);
        
        IsGameStarted = true;
        
        // Enable trap spawner
        if (trapSpawner != null)
        {
            trapSpawner.enabled = true;
            trapSpawner.StartSpawning();
        }
        
        // Notify GameManager to switch to Playing state
        if (HasStateAuthority && GameManager.Instance != null)
        {
            GameManager.Instance.StartPlayingState();
        }
    }

    private void HandleGameOverPhase()
    {
        Debug.Log("[MinigameController] GameOver phase");
        
        // Disable trap spawner
        if (trapSpawner != null)
        {
            trapSpawner.StopSpawning();
        }
        
        // Freeze all players
        RPC_SetPlayersFrozen(true);
    }

    private void HandleScoreboardPhase()
    {
        Debug.Log("[MinigameController] Scoreboard phase");
        
        SetCanvasActive(tutorialCanvas, false);
        SetCanvasActive(countdownCanvas, false);
        SetCanvasActive(scoreboardCanvas, true);
        
        // Debug scoreboard info
        LogScoreboardInfo();
        
        // Host: End game sau scoreboard duration
        if (HasStateAuthority)
        {
            StartCoroutine(EndGameAfterDelay(scoreboardDuration));
        }
    }

    private void LogScoreboardInfo()
    {
        Debug.Log("========== SCOREBOARD ==========");
        
        var players = FindObjectsByType<PlayerNetworkData>(FindObjectsSortMode.None);
        foreach (var player in players)
        {
            bool isWinner = Winner != PlayerRef.None && 
                           player.Object.InputAuthority == Winner;
            string status = isWinner ? "🏆 WINNER" : "❌ LOSER";
            Debug.Log($"[Scoreboard] {player.PlayerName}: {status}");
        }
        
        Debug.Log("================================");
    }
    #endregion

    #region Countdown Logic
    private void OnCountdownChanged()
    {
        if (countdownText != null && CurrentPhase == MinigamePhase.Countdown)
        {
            int displayCount = Mathf.CeilToInt(Countdown);
            if (displayCount > 0)
            {
                countdownText.text = displayCount.ToString();
            }
            else
            {
                countdownText.text = "GO!";
            }
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;

        // Countdown logic
        if (CurrentPhase == MinigamePhase.Countdown && Countdown > 0)
        {
            Countdown -= Runner.DeltaTime;

            if (Countdown <= 0)
            {
                Countdown = 0;
                // Delay nhỏ để hiện "GO!"
                StartCoroutine(StartPlayingAfterGo());
            }
        }
    }

    private IEnumerator StartPlayingAfterGo()
    {
        yield return new WaitForSeconds(0.5f);
        
        // Unfreeze players
        RPC_SetPlayersFrozen(false);
        
        // Switch to Playing phase
        CurrentPhase = MinigamePhase.Playing;
    }
    #endregion

    #region Button & Input
    private void OnStartButtonClicked()
    {
        if (!HasStateAuthority)
        {
            Debug.Log("[MinigameController] Only host can start");
            return;
        }

        if (CurrentPhase != MinigamePhase.Tutorial)
        {
            Debug.Log($"[MinigameController] Cannot start, current phase: {CurrentPhase}");
            return;
        }

        Debug.Log("[MinigameController] Host clicked Start - beginning countdown");
        
        // Start countdown
        Countdown = countdownTime;
        CurrentPhase = MinigamePhase.Countdown;
    }
    #endregion

    #region Player Management
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
            var spawnPoint = spawnPoints[spawnIndex % spawnPoints.Length];
            var targetPos = spawnPoint.position;

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
    #endregion

    #region Win Condition
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
        CurrentPhase = MinigamePhase.GameOver;

        // Notify all clients
        RPC_OnPlayerWon(playerRef);

        // Show scoreboard sau delay
        StartCoroutine(ShowScoreboardAfterDelay(2f));
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_OnPlayerWon(PlayerRef winnerRef)
    {
        Debug.Log($"[MinigameController] Player {winnerRef} WON!");
    }

    private IEnumerator ShowScoreboardAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        CurrentPhase = MinigamePhase.Scoreboard;
    }

    private IEnumerator EndGameAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        // Notify GameManager to go to Scoreboard state
        if (GameManager.Instance != null)
        {
            GameManager.Instance.EndMinigame(Winner.PlayerId);
        }
    }
    #endregion

    #region Helpers
    private void SetCanvasActive(GameObject canvas, bool active)
    {
        if (canvas != null)
        {
            canvas.SetActive(active);
        }
    }

    /// <summary>
    /// Lấy spawn point cho player
    /// </summary>
    public Vector3 GetSpawnPoint(int playerIndex)
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
            return Vector3.zero;

        int index = playerIndex % spawnPoints.Length;
        return spawnPoints[index].position;
    }

    /// <summary>
    /// Lấy spawn rotation cho player
    /// </summary>
    public Quaternion GetSpawnRotation(int playerIndex)
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
            return Quaternion.identity;

        int index = playerIndex % spawnPoints.Length;
        return spawnPoints[index].rotation;
    }
    #endregion
}
