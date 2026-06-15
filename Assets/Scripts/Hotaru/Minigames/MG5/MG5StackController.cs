using Fusion;
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// MG5 — Stack minigame controller.
/// - Mỗi player có lane riêng.
/// - Win: Player nào đạt 5 tầng trước → Rank 1.
/// - EndGame: Khi 3/4 player hoàn thành hoặc hết 60s.
/// - Nếu hết giờ: xếp hạng theo chiều cao hiện tại.
/// </summary>
public class MG5StackController : BaseMinigameController
{
    public new static MG5StackController Instance => BaseMinigameController.Instance as MG5StackController;

    [Header("Gameplay Settings")]
    [SerializeField] private int targetHeight = 5; // số tầng cần đạt
    [SerializeField] private float timeLimit = 60f; // 1 phút

    [Header("Lane Setup")]
    [SerializeField] private MG5Lane[] lanes; // 4 lane
    private Dictionary<PlayerRef, MG5PlayerStackData> _playerData = new();

    // ----------------------------------------------------------------
    // Lifecycle
    // ----------------------------------------------------------------

    protected override void OnGamePlayingStarted()
    {
        if (!HasStateAuthority) return;

        _finishOrder.Clear();
        _playerData.Clear();

        var allPlayers = FindObjectsByType<PlayerMinigameData>(FindObjectsSortMode.None);
        foreach (var p in allPlayers)
        {
            var stackData = p.GetComponent<MG5PlayerStackData>();
            if (stackData != null)
            {
                stackData.ResetData();
                _playerData[p.Object.InputAuthority] = stackData;
            }
        }

        // Spawn box đầu tiên cho mỗi lane
        foreach (var lane in lanes)
            lane.SpawnNewBox();

        GameTimer = timeLimit;
        Debug.Log("[MG5StackController] Game started!");
    }

    protected override void OnGameOver()
    {
        Debug.Log("[MG5StackController] Game Over!");
    }

    // ----------------------------------------------------------------
    // Win Condition
    // ----------------------------------------------------------------

    protected override void CheckWinCondition()
    {
        int finishedCount = 0;
        PlayerRef lastWinner = PlayerRef.None;

        foreach (var kv in _playerData)
        {
            if (kv.Value.IsFinished)
            {
                finishedCount++;
                lastWinner = kv.Key;
            }
        }

        if (finishedCount >= 3)
        {
            FinalizeRanks();
            EndGame(lastWinner);
        }
    }

    protected override void OnTimeUp()
    {
        Debug.Log("[MG5StackController] Time's up! Ranking by current height...");

        // Sort theo chiều cao stack
        var sorted = new List<MG5PlayerStackData>(_playerData.Values);
        sorted.Sort((a, b) => b.CurrentStackHeight.CompareTo(a.CurrentStackHeight));

        foreach (var data in sorted)
        {
            var pRef = data.GetComponent<PlayerMinigameData>().Object.InputAuthority;
            if (!_finishOrder.Contains(pRef))
                _finishOrder.Add(pRef);
        }

        FinalizeRanks();
        PlayerRef winner = _finishOrder.Count > 0 ? _finishOrder[0] : PlayerRef.None;
        EndGame(winner);
    }

    // ----------------------------------------------------------------
    // Player Finished
    // ----------------------------------------------------------------

    public override void PlayerFinished(PlayerRef playerRef)
    {
        if (!HasStateAuthority) return;
        if (_finishOrder.Contains(playerRef)) return;

        _finishOrder.Add(playerRef);
        int rank = _finishOrder.Count;

        var data = _playerData[playerRef];
        data.CurrentRank = rank;
        data.IsFinished = true;

        Debug.Log($"[MG5StackController] Player {playerRef} finished — Rank {rank}");

        CheckWinCondition();
    }


    // ----------------------------------------------------------------
    // Ranking
    // ----------------------------------------------------------------

    private void FinalizeRanks()
    {
        int total = _finishOrder.Count;
        foreach (var kv in _playerData)
        {
            var pRef = kv.Key;
            var data = kv.Value;

            if (data.CurrentRank <= 0)
            {
                int rank = total + 1;
                data.CurrentRank = rank;
                _finishOrder.Add(pRef);
            }
        }

        // Debug log kết quả
        foreach (var kv in _playerData)
        {
            Debug.Log($"[MG5StackController] Player {kv.Key} — Rank {kv.Value.CurrentRank}, Height {kv.Value.CurrentStackHeight}");
        }
    }

    protected override int[] BuildBoardRanking(PlayerRef winner)
    {
        var ranking = new List<int>();
        foreach (var pRef in _finishOrder)
            ranking.Add(pRef.PlayerId);
        return ranking.ToArray();
    }
}
