using UnityEngine;

/// <summary>
/// Spawner cho C4 — FallingSpikedLog.
/// Spawn khúc gỗ gai từ trên cao xuống, phân bố qua 3 làn.
///
/// Setup:
///   - spawnPoints: các Transform điểm spawn (trái/giữa/phải)
///   - spikedLogPrefab: prefab có FallingSpikedLog component
/// </summary>
public class SpikedLogSpawner : MonoBehaviour
{
    [Header("Spawn")]
    [SerializeField] private GameObject spikedLogPrefab;
    [SerializeField] private Transform[] spawnPoints;      // trái / giữa / phải
    [SerializeField] private float spawnInterval = 3f;
    [SerializeField] private int spawnCountPerWave = 1;

    private float _timer;
    private bool _isSpawning;

    private void Update()
    {
        if (!_isSpawning) return;

        _timer += Time.deltaTime;
        if (_timer >= spawnInterval)
        {
            _timer = 0f;
            SpawnWave();
        }
    }

    private void SpawnWave()
    {
        if (spawnPoints == null || spawnPoints.Length == 0) return;
        if (spikedLogPrefab == null) return;

        var indices = new System.Collections.Generic.List<int>();
        for (int i = 0; i < spawnPoints.Length; i++) indices.Add(i);

        int count = Mathf.Min(spawnCountPerWave, indices.Count);
        for (int i = 0; i < count; i++)
        {
            int pick = Random.Range(0, indices.Count);
            int idx = indices[pick];
            indices.RemoveAt(pick);

            Instantiate(spikedLogPrefab, spawnPoints[idx].position, Quaternion.identity);
        }
    }

    public void StartSpawning() { _isSpawning = true; _timer = 0f; }
    public void StopSpawning()  { _isSpawning = false; }
}
