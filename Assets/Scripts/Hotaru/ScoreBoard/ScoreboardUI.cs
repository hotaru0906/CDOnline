using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Quản lý hiển thị Scoreboard sau khi minigame kết thúc.
/// Gắn vào ScoreboardPanel (có UIPanel component với PanelType = Scoreboard).
///
/// Hierarchy gợi ý:
/// ScoreboardPanel (ScoreboardUI + UIPanel)
/// ├── TitleText          (TMP_Text)          → tên minigame vừa xong
/// ├── EntriesContainer   (RectTransform)     → parent chứa các row
/// │   ├── EntryRow_1     (ScoreboardEntry)   ← instantiate từ entryPrefab
/// │   ├── EntryRow_2     ...
/// │   └── ...
/// </summary>
public class ScoreboardUI : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Text hiển thị tên minigame vừa kết thúc (optional)")]
    [SerializeField] private TMP_Text titleText;

    [Tooltip("Parent transform chứa các ScoreboardEntry row")]
    [SerializeField] private Transform entriesContainer;

    [Tooltip("Prefab của 1 row (phải có ScoreboardEntry component)")]
    [SerializeField] private GameObject entryPrefab;

    // Cache các entry đang active
    private readonly List<ScoreboardEntry> _activeEntries = new();


    private void Start()
    {
        Debug.Log("SCOREBOARD START");
    }

    private void OnEnable()
    {
        Debug.Log("SCOREBOARD ENABLE");
        UpdateScoreboard();
    }

    private void OnDisable()
    {
        
    }

    /// <summary>
    /// Populate/refresh toàn bộ scoreboard — gọi bởi GameManager hoặc ScoreboardManager
    /// </summary>
    public void UpdateScoreboard()
    {
        Debug.Log("=== UPDATE SCOREBOARD ===");

        var rankedPlayers = ScoreboardManager.Instance != null
            ? ScoreboardManager.Instance.GetRankedPlayers()
            : new List<PlayerNetworkData>();

        Debug.Log("Player count = " + rankedPlayers.Count);


        ClearEntries();

        Debug.Log("After ClearEntries");

        for (int i = 0; i < rankedPlayers.Count; i++)
        {
            Debug.Log("Loop " + i);

            Debug.Log("Spawning row for " + rankedPlayers[i].name);

            SpawnEntry(i + 1, rankedPlayers[i]);
        }

        Debug.Log("END UPDATE SCOREBOARD");
    }

    private void SpawnEntry(int rank, PlayerNetworkData playerData)
    {
        Debug.Log("SPAWN ENTRY");
        
        if (entryPrefab == null || entriesContainer == null)
        {
            Debug.LogError("[ScoreboardUI] entryPrefab hoặc entriesContainer chưa được gán!");
            return;
        }

        var go = Instantiate(entryPrefab, entriesContainer);
        var entry = go.GetComponent<ScoreboardEntry>();

        if (entry == null)
        {
            Debug.LogError("[ScoreboardUI] entryPrefab không có ScoreboardEntry component!");
            Destroy(go);
            return;
        }

        entry.SetData(rank, playerData);
        _activeEntries.Add(entry);
    }

    private void ReleaseAllPortraits()
    {
        //khong can lam gi nua
    }

    private void ClearEntries()
    {
        foreach (var entry in _activeEntries)
        {
            if (entry != null)
                Destroy(entry.gameObject);
        }
        _activeEntries.Clear();
    }
}