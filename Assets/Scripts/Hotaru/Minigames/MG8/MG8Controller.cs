using Fusion;
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// MG8 - The Floor Is Lava.
/// Lava rises continuously. Touching it permanently eliminates a player.
/// </summary>
public class MG8Controller : BaseMinigameController
{
    public new static MG8Controller Instance =>
        BaseMinigameController.Instance as MG8Controller;

    [Header("Lava")]
    [SerializeField] private GameObject lava;
    [SerializeField] private float lavaRiseSpeed = 0.15f;

    [Networked, OnChangedRender(nameof(OnLavaScaleChanged))]
    private float LavaScaleY { get; set; }

    private readonly List<PlayerRef> _eliminationOrder = new();

    protected override void OnGamePlayingStarted()
    {
        if (!HasStateAuthority) return;

        _eliminationOrder.Clear();
        LavaScaleY = lava != null ? lava.transform.localScale.y : 0f;
        ApplyLavaScale();

        var allData = FindObjectsByType<PlayerMinigameData>(FindObjectsSortMode.None);
        foreach (var playerData in allData)
        {
            playerData.OnPlayerEliminated -= HandlePlayerEliminated;
            playerData.OnPlayerEliminated += HandlePlayerEliminated;
        }

        UpdateAlivePlayerCount();
        MinigameHUDController.Instance?.RefreshPlayers();
    }

    protected override void OnGameOver()
    {
        var allData = FindObjectsByType<PlayerMinigameData>(FindObjectsSortMode.None);
        foreach (var playerData in allData)
            playerData.OnPlayerEliminated -= HandlePlayerEliminated;
    }

    public override void FixedUpdateNetwork()
    {
        base.FixedUpdateNetwork();

        if (!HasStateAuthority || CurrentPhase != MinigamePhase.Playing || IsGameEnded)
            return;

        LavaScaleY += lavaRiseSpeed * Runner.DeltaTime;
        ApplyLavaScale();
    }

    public void EliminatePlayer(PlayerMinigameData playerData)
    {
        if (!HasStateAuthority || playerData == null || IsGameEnded) return;
        if (CurrentPhase != MinigamePhase.Playing || playerData.IsEliminated) return;

        playerData.EliminateImmediately();
    }

    public void OnPlayerHit(PlayerController attacker, PlayerController target)
    {
    }

    private void HandlePlayerEliminated(PlayerMinigameData data)
    {
        if (!HasStateAuthority || data == null) return;

        PlayerRef playerRef = data.Object.InputAuthority;
        if (!_eliminationOrder.Contains(playerRef))
            _eliminationOrder.Add(playerRef);

        UpdateAlivePlayerCount();
        RPC_SwitchEliminatedPlayerCamera(playerRef);
        CheckWinCondition();
    }

    protected override void CheckWinCondition()
    {
        if (!HasStateAuthority || IsGameEnded) return;

        var allData = FindObjectsByType<PlayerMinigameData>(FindObjectsSortMode.None);
        var alive = new List<PlayerMinigameData>();
        foreach (var playerData in allData)
        {
            if (!playerData.IsEliminated)
                alive.Add(playerData);
        }

        if (alive.Count == 1)
        {
            FinalizeRanks(allData);
            EndGame(alive[0].Object.InputAuthority);
        }
        else if (alive.Count == 0)
        {
            FinalizeRanks(allData);
            EndGame(PlayerRef.None);
        }
    }

    protected override void OnTimeUp()
    {
        if (!HasStateAuthority || IsGameEnded) return;

        var allData = FindObjectsByType<PlayerMinigameData>(FindObjectsSortMode.None);
        var ranking = BuildFinalRanking(allData);
        FinalizeRanks(allData, ranking);
        EndGame(ranking.Count > 0 ? ranking[0] : PlayerRef.None);
    }

    private void FinalizeRanks(PlayerMinigameData[] allData)
    {
        FinalizeRanks(allData, BuildFinalRanking(allData));
    }

    private void FinalizeRanks(PlayerMinigameData[] allData, List<PlayerRef> ranking)
    {
        for (int i = 0; i < ranking.Count; i++)
        {
            foreach (var playerData in allData)
            {
                if (playerData.Object.InputAuthority == ranking[i])
                {
                    playerData.SetFinished(i + 1, 0f);
                    break;
                }
            }
        }

        ApplyHiddenScores();
    }

    private List<PlayerRef> BuildFinalRanking(PlayerMinigameData[] allData)
    {
        var alive = new List<PlayerMinigameData>();
        var eliminated = new HashSet<PlayerRef>();

        foreach (var playerData in allData)
        {
            if (playerData.IsEliminated)
                eliminated.Add(playerData.Object.InputAuthority);
            else
                alive.Add(playerData);
        }

        alive.Sort((a, b) =>
        {
            int yCompare = b.transform.position.y.CompareTo(a.transform.position.y);
            return yCompare != 0
                ? yCompare
                : a.Object.InputAuthority.PlayerId.CompareTo(b.Object.InputAuthority.PlayerId);
        });

        var ranking = new List<PlayerRef>();
        foreach (var playerData in alive)
            ranking.Add(playerData.Object.InputAuthority);

        for (int i = _eliminationOrder.Count - 1; i >= 0; i--)
        {
            if (eliminated.Contains(_eliminationOrder[i]))
            {
                ranking.Add(_eliminationOrder[i]);
                eliminated.Remove(_eliminationOrder[i]);
            }
        }

        foreach (var playerRef in eliminated)
            ranking.Add(playerRef);

        return ranking;
    }

    private void ApplyLavaScale()
    {
        if (lava == null) return;

        Vector3 scale = lava.transform.localScale;
        scale.y = LavaScaleY;
        lava.transform.localScale = scale;
    }

    private void OnLavaScaleChanged() => ApplyLavaScale();

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_SwitchEliminatedPlayerCamera(PlayerRef eliminatedRef)
    {
        if (Runner.LocalPlayer != eliminatedRef) return;

        var targets = new List<PlayerController>();
        var allData = FindObjectsByType<PlayerMinigameData>(FindObjectsSortMode.None);
        foreach (var data in allData)
        {
            if (data.IsEliminated || data.Object.InputAuthority == eliminatedRef) continue;

            var player = data.GetComponent<PlayerController>();
            if (player != null) targets.Add(player);
        }

        if (targets.Count == 0 || CameraManager.Instance == null) return;

        targets.Sort((a, b) => a.Object.InputAuthority.PlayerId.CompareTo(b.Object.InputAuthority.PlayerId));
        CameraManager.Instance.UpdatePlayerTarget(targets[0].transform);
        CameraManager.Instance.SwitchToThirdPersonCamera();
    }

    protected override void BuildScoreboardResults()
    {
        var allData = FindObjectsByType<PlayerMinigameData>(FindObjectsSortMode.None);
        var sorted = new List<PlayerMinigameData>(allData);
        sorted.Sort((a, b) => a.FinishRank.CompareTo(b.FinishRank));

        for (int i = 0; i < ScoreboardResults.Length; i++)
            ScoreboardResults.Set(i, default);

        for (int i = 0; i < sorted.Count && i < ScoreboardResults.Length; i++)
        {
            var playerData = sorted[i];
            ScoreboardResults.Set(i, new MinigameResultData
            {
                Player = playerData.Object.InputAuthority,
                Rank = playerData.FinishRank > 0 ? playerData.FinishRank : i + 1,
                Score = Mathf.RoundToInt(playerData.transform.position.y),
                IsValid = true
            });
        }
    }
}
