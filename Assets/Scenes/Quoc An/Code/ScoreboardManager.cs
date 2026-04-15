using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ScoreboardManager : MonoBehaviour
{
    // ============================================
    //              SINGLETON
    // ============================================
    public static ScoreboardManager Instance { get; private set; }
    
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
    
    [Header("=== Auto Find Settings ===")]
    [Tooltip("Tên của GameObject chứa các columns (dùng khi columnsContainer = null)")]
    [SerializeField] private string containerName = "Columns_Container";
    [Tooltip("Tag của GameObject chứa các columns (ưu tiên hơn name)")]
    [SerializeField] private string containerTag  = "ScoreboardContainer";

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
    [SerializeField] private bool useRealPlayerData     = true; // Dùng data thật từ PlayerNetworkData

    // ============================================
    //              PRIVATE
    // ============================================
    private List<ScoreboardColumn> _spawnedColumns = new List<ScoreboardColumn>();
    private List<PlayerScoreData>  _previousScores = new List<PlayerScoreData>();
    private List<PlayerScoreData>  _currentPlayers = new List<PlayerScoreData>(); // Danh sách player thật

    // ============================================
    //              UNITY EVENTS
    // ============================================
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        
        // Persist qua các scene
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void OnEnable()
    {
        // Đăng ký event khi scene load xong
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        // Reset columns khi chuyển scene
        _spawnedColumns.Clear();
        columnsContainer = null; // Reset để tìm lại trong scene mới
        
        Debug.Log($"[ScoreboardManager] Scene loaded: {scene.name}");
    }

    private void Start()
    {
        // Nếu dùng data thật, lấy từ players
        if (useRealPlayerData)
        {
            RefreshFromPlayers();
        }
        else
        {
            BuildScoreboard();
        }

        if (autoRefreshInPlayMode)
            StartCoroutine(AutoRefreshRoutine());
    }

    /// <summary>
    /// Tự động tìm columnsContainer trong scene
    /// </summary>
    private bool TryFindContainer()
    {
        if (columnsContainer != null) return true;

        // Ưu tiên tìm theo tag
        if (!string.IsNullOrEmpty(containerTag))
        {
            GameObject taggedObj = GameObject.FindGameObjectWithTag(containerTag);
            if (taggedObj != null)
            {
                columnsContainer = taggedObj.transform;
                Debug.Log($"[ScoreboardManager] Found container by tag: {containerTag}");
                return true;
            }
        }

        // Tìm theo tên
        if (!string.IsNullOrEmpty(containerName))
        {
            GameObject namedObj = GameObject.Find(containerName);
            if (namedObj != null)
            {
                columnsContainer = namedObj.transform;
                Debug.Log($"[ScoreboardManager] Found container by name: {containerName}");
                return true;
            }
        }

        Debug.LogWarning("[ScoreboardManager] Could not find columnsContainer!");
        return false;
    }

    // ============================================
    //          BUILD SCOREBOARD
    // ============================================
    private void BuildScoreboard()
    {
        if (!TryFindContainer()) return;
        
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

            if (useRealPlayerData)
            {
                // Với real data, refresh từ players
                RefreshFromPlayers();
            }
            else
            {
                // Với test data, kiểm tra xem có score nào thay đổi không
                if (HasScoreChanged())
                {
                    RefreshScoreboard();
                }
            }
        }
    }

    private bool HasScoreChanged()
    {
        var dataSource = useRealPlayerData ? _currentPlayers : testPlayers;
        
        if (_previousScores.Count != dataSource.Count) return true;

        for (int i = 0; i < dataSource.Count; i++)
        {
            if (_previousScores[i].score != dataSource[i].score)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Refresh chiều cao các cột khi score thay đổi
    /// </summary>
    public void RefreshScoreboard()
    {
        var dataSource = useRealPlayerData ? _currentPlayers : testPlayers;
        
        int maxScore = GetMaxScoreFromList(dataSource);
        if (maxScore == 0) maxScore = 1;

        for (int i = 0; i < _spawnedColumns.Count; i++)
        {
            if (i >= dataSource.Count) break;

            float heightRatio  = (float)dataSource[i].score / maxScore;
            float targetHeight = heightRatio * maxBarHeight;

            _spawnedColumns[i].SetTargetHeight(targetHeight);
            _spawnedColumns[i].SetScore(dataSource[i].score);
        }

        SavePreviousScores();
        Debug.Log("🔄 Scoreboard refreshed!");
    }

    // ============================================
    //       REFRESH FROM REAL PLAYERS
    // ============================================
    
    /// <summary>
    /// Lấy dữ liệu từ các PlayerNetworkData trong scene và build scoreboard
    /// </summary>
    public void RefreshFromPlayers()
    {
        // Tìm tất cả PlayerNetworkData trong scene
        var players = FindObjectsByType<PlayerNetworkData>(FindObjectsSortMode.None);
        
        if (players == null || players.Length == 0)
        {
            Debug.LogWarning("[ScoreboardManager] No players found!");
            return;
        }

        // Cập nhật danh sách _currentPlayers
        _currentPlayers.Clear();
        
        Color[] playerColors = new Color[]
        {
            new Color(0.2f, 0.6f, 1f),   // Blue
            new Color(1f,   0.4f, 0.4f), // Red
            new Color(0.4f, 1f,   0.4f), // Green
            new Color(1f,   0.8f, 0f),   // Yellow
            new Color(0.8f, 0.4f, 1f),   // Purple
            new Color(1f,   0.6f, 0.2f), // Orange
            new Color(0.2f, 1f,   0.8f), // Cyan
            new Color(1f,   0.4f, 0.8f), // Pink
        };

        int colorIndex = 0;
        foreach (var player in players)
        {
            var data = new PlayerScoreData
            {
                playerName = player.PlayerName.ToString(),
                score      = player.Score,
                barColor   = playerColors[colorIndex % playerColors.Length],
                playerIcon = null // Có thể thêm icon sau
            };
            
            _currentPlayers.Add(data);
            colorIndex++;
        }

        // Sort theo score giảm dần
        _currentPlayers.Sort((a, b) => b.score.CompareTo(a.score));

        Debug.Log($"[ScoreboardManager] Found {_currentPlayers.Count} players");

        // Build lại scoreboard với data thật
        BuildScoreboardFromList(_currentPlayers);
    }

    /// <summary>
    /// Build scoreboard từ một danh sách PlayerScoreData
    /// </summary>
    private void BuildScoreboardFromList(List<PlayerScoreData> playerList)
    {
        if (!TryFindContainer()) return;
        
        ClearColumns();

        if (playerList == null || playerList.Count == 0) return;

        // Tìm điểm cao nhất để tính tỉ lệ
        int maxScore = GetMaxScoreFromList(playerList);
        if (maxScore == 0) maxScore = 1; // Tránh chia cho 0

        // Spawn từng cột
        foreach (var player in playerList)
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

    private int GetMaxScoreFromList(List<PlayerScoreData> playerList)
    {
        int max = 0;
        foreach (var p in playerList)
            if (p.score > max) max = p.score;
        return max;
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
        var dataSource = useRealPlayerData ? _currentPlayers : testPlayers;
        
        _previousScores.Clear();
        foreach (var p in dataSource)
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
    /// Set container thủ công (dùng khi không tìm được tự động)
    /// </summary>
    public void SetContainer(Transform container)
    {
        columnsContainer = container;
        Debug.Log($"[ScoreboardManager] Container set manually: {container?.name}");
    }

    /// <summary>
    /// Hiển thị scoreboard - gọi khi minigame kết thúc
    /// </summary>
    public void ShowScoreboard()
    {
        Debug.Log("[ScoreboardManager] ShowScoreboard called");
        
        if (useRealPlayerData)
        {
            RefreshFromPlayers();
        }
        else
        {
            RefreshScoreboard();
        }
    }

    /// <summary>
    /// Cập nhật điểm 1 người chơi theo index (chỉ dùng cho test data)
    /// </summary>
    public void UpdateScore(int playerIndex, int newScore)
    {
        if (!useRealPlayerData)
        {
            if (playerIndex < 0 || playerIndex >= testPlayers.Count) return;
            testPlayers[playerIndex].score = newScore;
        }
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
    
    /// <summary>
    /// Reset tất cả columns (dùng khi chuyển scene)
    /// </summary>
    public void ResetScoreboard()
    {
        ClearColumns();
        _currentPlayers.Clear();
        _previousScores.Clear();
        columnsContainer = null;
        Debug.Log("[ScoreboardManager] Scoreboard reset");
    }
}