using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ScoreboardManager : MonoBehaviour
{
    // ============================================
    //   DATA CLASS - Hiện trong Inspector để test
    // ============================================
    [System.Serializable]
    public class PlayerScoreData
    {
        [Header("--- Thông tin ---")]
        public string playerName  = "Player";
        public Sprite playerIcon;
        public Color  barColor    = Color.cyan;

        [Header("--- Điểm số (Chỉnh để test) ---")]
        [Range(0, 10000)]
        public int score = 0;
    }

    // ============================================
    //              INSPECTOR FIELDS
    // ============================================
    [Header("=== Prefab & Container ===")]
    [SerializeField] private ScoreboardColumn columnPrefab;
    [SerializeField] private Transform        columnsContainer;

    [Header("=== Bar Settings ===")]
    [SerializeField] private float maxBarHeight = 350f; // Chiều cao tối đa của cột
    [SerializeField] private float animSpeed    = 3f;   // Tốc độ tăng cột

    [Header("=== Test Players (Chỉnh điểm ở đây!) ===")]
    [SerializeField]
    private List<PlayerScoreData> testPlayers = new List<PlayerScoreData>()
    {
        new PlayerScoreData { playerName = "Player 1", score = 800,  barColor = new Color(0.2f, 0.6f, 1f)   },
        new PlayerScoreData { playerName = "Player 2", score = 1200, barColor = new Color(1f,   0.4f, 0.4f) },
        new PlayerScoreData { playerName = "Player 3", score = 500,  barColor = new Color(0.4f, 1f,   0.4f) },
        new PlayerScoreData { playerName = "Player 4", score = 950,  barColor = new Color(1f,   0.8f, 0f)   },
    };

    [Header("=== Debug ===")]
    [SerializeField] private bool autoRefreshInPlayMode = true; // Tự cập nhật khi thay đổi score
    [SerializeField] private float refreshInterval      = 0.2f; // Tần suất refresh

    // ============================================
    //              PRIVATE
    // ============================================
    private List<ScoreboardColumn> _spawnedColumns = new List<ScoreboardColumn>();
    private List<PlayerScoreData>  _previousScores = new List<PlayerScoreData>();

    // ============================================
    //              UNITY EVENTS
    // ============================================
    private void Start()
    {
        BuildScoreboard();

        if (autoRefreshInPlayMode)
            StartCoroutine(AutoRefreshRoutine());
    }

    // ============================================
    //          BUILD SCOREBOARD
    // ============================================
    private void BuildScoreboard()
    {
        ClearColumns();

        // Tìm điểm cao nhất để tính tỉ lệ
        int maxScore = GetMaxScore();
        if (maxScore == 0) maxScore = 1; // Tránh chia cho 0

        // Spawn từng cột
        foreach (var player in testPlayers)
        {
            ScoreboardColumn col = Instantiate(columnPrefab, columnsContainer);

            // Tính chiều cao theo tỉ lệ điểm
            float heightRatio  = (float)player.score / maxScore;
            float targetHeight = heightRatio * maxBarHeight;

            col.Setup(
                playerName:   player.playerName,
                icon:         player.playerIcon,
                barColor:     player.barColor,
                targetHeight: targetHeight,
                animSpeed:    animSpeed
            );

            col.SetScore(player.score);
            _spawnedColumns.Add(col);
        }

        SavePreviousScores();
    }

    // ============================================
    //       AUTO REFRESH (Detect thay đổi score)
    // ============================================
    private IEnumerator AutoRefreshRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(refreshInterval);

            // Kiểm tra xem có score nào thay đổi không
            if (HasScoreChanged())
            {
                RefreshScoreboard();
            }
        }
    }

    private bool HasScoreChanged()
    {
        if (_previousScores.Count != testPlayers.Count) return true;

        for (int i = 0; i < testPlayers.Count; i++)
        {
            if (_previousScores[i].score != testPlayers[i].score)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Refresh chiều cao các cột khi score thay đổi
    /// </summary>
    public void RefreshScoreboard()
    {
        int maxScore = GetMaxScore();
        if (maxScore == 0) maxScore = 1;

        for (int i = 0; i < _spawnedColumns.Count; i++)
        {
            if (i >= testPlayers.Count) break;

            float heightRatio  = (float)testPlayers[i].score / maxScore;
            float targetHeight = heightRatio * maxBarHeight;

            _spawnedColumns[i].SetTargetHeight(targetHeight);
            _spawnedColumns[i].SetScore(testPlayers[i].score);
        }

        SavePreviousScores();
        Debug.Log("🔄 Scoreboard refreshed!");
    }

    // ============================================
    //              HELPERS
    // ============================================
    private int GetMaxScore()
    {
        int max = 0;
        foreach (var p in testPlayers)
            if (p.score > max) max = p.score;
        return max;
    }

    private void ClearColumns()
    {
        foreach (var col in _spawnedColumns)
            if (col != null) Destroy(col.gameObject);

        _spawnedColumns.Clear();
    }

    private void SavePreviousScores()
    {
        _previousScores.Clear();
        foreach (var p in testPlayers)
        {
            _previousScores.Add(new PlayerScoreData
            {
                playerName = p.playerName,
                score      = p.score,
                barColor   = p.barColor
            });
        }
    }

    // ============================================
    //         PUBLIC API (Dùng sau khi có player)
    // ============================================

    /// <summary>
    /// Cập nhật điểm 1 người chơi theo index
    /// </summary>
    public void UpdateScore(int playerIndex, int newScore)
    {
        if (playerIndex < 0 || playerIndex >= testPlayers.Count) return;

        testPlayers[playerIndex].score = newScore;
        RefreshScoreboard();
    }

    /// <summary>
    /// Thêm người chơi mới vào scoreboard
    /// </summary>
    public void AddPlayer(PlayerScoreData newPlayer)
    {
        testPlayers.Add(newPlayer);
        BuildScoreboard(); // Rebuild toàn bộ
    }
}