using UnityEngine;

/// <summary>
/// Spawner cho C11 — RollingSpikeBall.
/// Spawn bóng gai theo interval, ngẫu nhiên từ một trong nhiều spawn point.
/// Bóng lăn theo hướng được cấu hình trong Inspector.
///
/// Setup:
///   - Gắn component này lên 1 GameObject quản lý
///   - spawnPoints: mảng các Transform điểm spawn (ví dụ: 3 làn trái/giữa/phải)
///   - spikeBallPrefab: prefab có RollingSpikeBall component
/// </summary>
public class RollingSpikeBallSpawner : MonoBehaviour
{
    [Header("Spawn Settings")]
    [SerializeField] private GameObject spikeBallPrefab;
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private float spawnInterval = 2f;
    [SerializeField] private int spawnCountPerWave = 1; // spawn bao nhiêu bóng mỗi lần

    [Header("Roll Direction")]
    [SerializeField] private Vector3 rollDirection = Vector3.left; // hướng bóng lăn

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
        if (spikeBallPrefab == null) return;

        // Chọn ngẫu nhiên không trùng nhau
        var indices = new System.Collections.Generic.List<int>();
        for (int i = 0; i < spawnPoints.Length; i++)
            indices.Add(i);

        int count = Mathf.Min(spawnCountPerWave, indices.Count);
        for (int i = 0; i < count; i++)
        {
            int pick = Random.Range(0, indices.Count);
            int idx = indices[pick];
            indices.RemoveAt(pick);

            var go = Instantiate(spikeBallPrefab, spawnPoints[idx].position, Quaternion.identity);
            if (go.TryGetComponent(out RollingSpikeBall ball))
                ball.SetRollDirection(rollDirection);
        }
    }

    public void StartSpawning() { _isSpawning = true; _timer = 0f; }
    public void StopSpawning()  { _isSpawning = false; }
}
