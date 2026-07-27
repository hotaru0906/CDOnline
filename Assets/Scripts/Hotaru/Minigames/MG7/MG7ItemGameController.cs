using Fusion;
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// MG7 — Item Collection minigame controller.
/// Không có elimination — chơi hết GameTimer (từ BaseMinigameController).
/// Rank cuối = tổng điểm tích lũy, cao nhất = rank 1.
///
/// Spawn items ngẫu nhiên trong khu vực (spawnAreaCenter + spawnAreaSize) ở độ cao spawnHeight,
/// tốc độ spawn tăng dần đều từ initialSpawnInterval → minSpawnInterval trong rampDuration giây,
/// sau đó giữ nguyên ở minSpawnInterval.
/// </summary>
public class MG7ItemGameController : BaseMinigameController
{
    public new static MG7ItemGameController Instance =>
        BaseMinigameController.Instance as MG7ItemGameController;

    [System.Serializable]
    public class ItemSpawnEntry
    {
        public MG7ItemType type;
        public NetworkObject prefab;
        [Range(0f, 10f)] public float weight = 1f;
    }

    [Header("Item Spawn Area")]
    [Tooltip("Tâm khu vực spawn item trên cao. Kéo transform này trong scene để chỉnh vị trí.")]
    [SerializeField] private Transform spawnAreaCenter;
    [Tooltip("Kích thước khu vực spawn theo trục X/Z (độ rộng, độ sâu). Item spawn ngẫu nhiên trong phạm vi này.")]
    [SerializeField] private Vector2 spawnAreaSize = new Vector2(12f, 12f);
    [Tooltip("Độ cao spawn phía trên spawnAreaCenter — item sẽ rơi tự do từ đây xuống đất.")]
    [SerializeField] private float spawnHeight = 8f;

    [Header("Item Prefabs & Weights")]
    [Tooltip("Gán 6 prefab item (3 score + bomb + boost + freeze) và trọng số xuất hiện tương ứng.")]
    [SerializeField] private ItemSpawnEntry[] itemEntries;

    [Header("Spawn Rate Ramp (nhanh dần đều)")]
    [SerializeField] private float initialSpawnInterval = 3f;
    [SerializeField] private float minSpawnInterval = 0.8f;
    [SerializeField] private float rampDuration = 30f; // giây để đạt tốc độ spawn tối đa

    private float _spawnCountdown;
    private float _elapsedGameTime;
    private bool _spawnFreezeNext = true;
    private void OnDrawGizmos()
    {
        Vector3 center = spawnAreaCenter != null ? spawnAreaCenter.position : transform.position;
        Vector3 gizmoCenter = center + Vector3.up * spawnHeight;
        Vector3 size = new Vector3(spawnAreaSize.x, 0.01f, spawnAreaSize.y);

        Gizmos.color = new Color(0f, 0.8f, 1f, 0.75f);
        Gizmos.DrawWireCube(gizmoCenter, size);
        Gizmos.color = new Color(0f, 0.8f, 1f, 0.15f);
        Gizmos.DrawCube(gizmoCenter, size);
    }

    // ----------------------------------------------------------------
    //  Setup
    // ----------------------------------------------------------------

    protected override void OnGamePlayingStarted()
    {
        if (!HasStateAuthority) return;

        _elapsedGameTime = 0f;
        _spawnCountdown = initialSpawnInterval;

        Debug.Log("[MG7ItemGame] Game started — item spawning active");
    }

    protected override void OnGameOver()
    {
        // Item đang tồn tại trong scene sẽ tự despawn theo timer riêng, không cần dọn thủ công.
    }

    // ----------------------------------------------------------------
    //  FixedUpdateNetwork — spawn loop
    // ----------------------------------------------------------------

    public override void FixedUpdateNetwork()
    {
        base.FixedUpdateNetwork();

        if (!HasStateAuthority) return;
        if (!IsGameStarted || IsGameEnded) return;

        _elapsedGameTime += Runner.DeltaTime;
        _spawnCountdown -= Runner.DeltaTime;

        if (_spawnCountdown <= 0f)
        {
            if (_spawnFreezeNext)
            {
                SpawnFreezeItem();
            }
            else
            {
                SpawnRandomItem();
            }

            _spawnFreezeNext = !_spawnFreezeNext;

            float t = (rampDuration > 0f)
                ? Mathf.Clamp01(_elapsedGameTime / rampDuration)
                : 1f;

            _spawnCountdown = Mathf.Lerp(initialSpawnInterval, minSpawnInterval, t);
        }
    }

    private void SpawnRandomItem()
    {
        if (itemEntries == null || itemEntries.Length == 0)
        {
            Debug.LogWarning("[MG7ItemGame] No item entries configured!");
            return;
        }

        float totalWeight = 0f;

        foreach (var e in itemEntries)
        {
            if (e.type == MG7ItemType.Freeze)
                continue;

            totalWeight += Mathf.Max(0f, e.weight);
        }

        if (totalWeight <= 0f) return;

        float roll = UnityEngine.Random.Range(0f, totalWeight);
        ItemSpawnEntry chosen = null;
        float cumulative = 0f;

        foreach (var e in itemEntries)
        {
            if (e.type == MG7ItemType.Freeze)
                continue;

            cumulative += Mathf.Max(0f, e.weight);

            if (roll <= cumulative)
            {
                chosen = e;
                break;
            }
        }

        if (chosen == null || chosen.prefab == null) return;

        Vector3 center = spawnAreaCenter != null ? spawnAreaCenter.position : transform.position;
        float x = UnityEngine.Random.Range(-spawnAreaSize.x * 0.5f, spawnAreaSize.x * 0.5f);
        float z = UnityEngine.Random.Range(-spawnAreaSize.y * 0.5f, spawnAreaSize.y * 0.5f);
        Vector3 spawnPos = center + new Vector3(x, spawnHeight, z);

        Runner.Spawn(chosen.prefab, spawnPos, Quaternion.identity);

        Debug.Log($"[MG7ItemGame] Spawned {chosen.type} at {spawnPos} — next in {_spawnCountdown:F2}s");
    }

    private void SpawnFreezeItem()
    {
        if (itemEntries == null || itemEntries.Length == 0)
            return;

        ItemSpawnEntry freezeEntry = null;

        foreach (var e in itemEntries)
        {
            if (e.type == MG7ItemType.Freeze)
            {
                freezeEntry = e;
                break;
            }
        }

        if (freezeEntry == null || freezeEntry.prefab == null)
        {
            Debug.LogWarning("[MG7ItemGame] Freeze prefab not found!");
            return;
        }

        Vector3 center = spawnAreaCenter != null ? spawnAreaCenter.position : transform.position;

        float x = Random.Range(-spawnAreaSize.x * 0.5f, spawnAreaSize.x * 0.5f);
        float z = Random.Range(-spawnAreaSize.y * 0.5f, spawnAreaSize.y * 0.5f);

        Vector3 spawnPos = center + new Vector3(x, spawnHeight, z);

        Runner.Spawn(freezeEntry.prefab, spawnPos, Quaternion.identity);

        Debug.Log("[MG7ItemGame] Spawned Freeze");
    }

    // ----------------------------------------------------------------
    //  Win Condition — không có, chỉ kết thúc theo thời gian
    // ----------------------------------------------------------------

    protected override void CheckWinCondition()
    {
        // MG7 không có elimination — game luôn kết thúc qua OnTimeUp().
    }

    protected override void OnTimeUp()
    {
        Debug.Log("[MG7ItemGame] Time's up!");

        FinalizeRanks();

        PlayerRef winner = GetTopScorer();
        EndGame(winner);
    }

    // ----------------------------------------------------------------
    //  Rank
    // ----------------------------------------------------------------

    private void FinalizeRanks()
    {
        var allData = new List<PlayerMinigameData>(
            FindObjectsByType<PlayerMinigameData>(FindObjectsSortMode.None));

        // Sort theo Score giảm dần — cao nhất = rank 1
        allData.Sort((a, b) => b.Score.CompareTo(a.Score));

        for (int i = 0; i < allData.Count; i++)
        {
            int rank = i + 1;
            allData[i].SetFinished(rank, 0f);
        }

        // BuildBoardRanking (base) tự sort theo HiddenScore — cần apply trước khi EndGame gọi tới.
        ApplyHiddenScores();
    }

    private PlayerRef GetTopScorer()
    {
        var allData = FindObjectsByType<PlayerMinigameData>(FindObjectsSortMode.None);
        PlayerMinigameData best = null;

        foreach (var p in allData)
        {
            if (best == null || p.Score > best.Score)
                best = p;
        }

        return best != null ? best.Object.InputAuthority : PlayerRef.None;
    }

    // ----------------------------------------------------------------
    //  Scoreboard
    // ----------------------------------------------------------------

    protected override void BuildScoreboardResults()
    {
        var allData = FindObjectsByType<PlayerMinigameData>(FindObjectsSortMode.None);
        var sorted = new List<PlayerMinigameData>(allData);
        sorted.Sort((a, b) => a.FinishRank.CompareTo(b.FinishRank));

        for (int i = 0; i < ScoreboardResults.Length; i++)
            ScoreboardResults.Set(i, default);

        for (int i = 0; i < sorted.Count && i < ScoreboardResults.Length; i++)
        {
            var p = sorted[i];
            ScoreboardResults.Set(i, new MinigameResultData
            {
                Player = p.Object.InputAuthority,
                Rank = p.FinishRank > 0 ? p.FinishRank : (i + 1),
                Score = p.Score,
                IsValid = true
            });
        }
    }

    protected override void LogScoreboardInfo()
    {
        Debug.Log("========== SCOREBOARD (MG7 Item Collection) ==========");
        var allData = FindObjectsByType<PlayerMinigameData>(FindObjectsSortMode.None);
        var sorted = new List<PlayerMinigameData>(allData);
        sorted.Sort((a, b) => a.FinishRank.CompareTo(b.FinishRank));

        foreach (var p in sorted)
        {
            var netData = p.GetComponent<PlayerNetworkData>();
            string name = netData != null
                ? netData.PlayerName.ToString()
                : $"P{p.Object.InputAuthority.PlayerId}";
            Debug.Log($"[Scoreboard] #{p.FinishRank}: {name} — {p.Score} pts");
        }
        Debug.Log("=======================================================");
    }
}
