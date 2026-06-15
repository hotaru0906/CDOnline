using Fusion;
using UnityEngine;
using System.Collections;

public class MG4LaserHazard : NetworkBehaviour
{
    public enum Side { Left, Right }
    public Side laneSide = Side.Left;

    [Header("Network Laser Prefab")]
    public NetworkObject laserNetworkPrefab; // MUST be a NetworkObject with LaserMoverNetworked

    [Header("Spawn Timing")]
    public float spawnIntervalPhase1 = 2.5f;
    public float spawnIntervalPhase2 = 2.0f;
    public float spawnIntervalPhase3 = 1.8f;
    public float spawnIntervalPhase4 = 1.2f;

    [Header("Movement")]
    public float baseSpeed = 4f;
    public float speedMultiplierPhase2 = 1.5f;
    public float speedMultiplierPhase4 = 2f;

    [Header("Spawn Points")]
    public Transform topSpawnPoint;
    public Transform bottomSpawnPoint;

    private Coroutine _spawnRoutine;
    private float _currentInterval = 2.5f;
    private float _currentSpeed = 4f;
    private int _currentPhase = 1;
    private bool _extraTime = false;

    // Called by controller (host) to set phase and difficulty
    public void SetPhase(int phase, bool extraTime)
    {
        if (!HasStateAuthority) return;

        _currentPhase = phase;
        _extraTime = extraTime;

        switch (phase)
        {
            case 1:
                _currentInterval = spawnIntervalPhase1;
                _currentSpeed = baseSpeed;
                break;
            case 2:
                _currentInterval = spawnIntervalPhase2;
                _currentSpeed = baseSpeed * speedMultiplierPhase2;
                break;
            case 3:
                _currentInterval = spawnIntervalPhase3;
                _currentSpeed = baseSpeed * speedMultiplierPhase2;
                break;
            default:
                _currentInterval = spawnIntervalPhase4;
                _currentSpeed = baseSpeed * speedMultiplierPhase4;
                break;
        }

        if (_extraTime)
        {
            _currentInterval = Mathf.Min(_currentInterval, spawnIntervalPhase4);
            _currentSpeed = baseSpeed * speedMultiplierPhase4;
        }

        RestartSpawn();
    }

    private void RestartSpawn()
    {
        if (!HasStateAuthority) return;

        if (_spawnRoutine != null) StopCoroutine(_spawnRoutine);
        _spawnRoutine = StartCoroutine(SpawnLoop());
    }

    private IEnumerator SpawnLoop()
    {
        // small jitter so multiple hazards don't spawn exactly same frame
        yield return new WaitForSeconds(Random.Range(0f, 0.25f));

        while (true)
        {
            SpawnNetworkLaser();
            yield return new WaitForSeconds(_currentInterval);
        }
    }

    private void SpawnNetworkLaser()
    {
        if (!HasStateAuthority || laserNetworkPrefab == null || Runner == null) return;

        Transform spawnPoint = (laneSide == Side.Left) ? topSpawnPoint : bottomSpawnPoint;
        Vector3 pos = spawnPoint != null ? spawnPoint.position : transform.position;
        Quaternion rot = Quaternion.identity;

        // Spawn on host; owner set to StateAuthority (null owner is fine)
        var laserObj = Runner.Spawn(laserNetworkPrefab, pos, rot, Object.InputAuthority);

        if (laserObj != null)
        {
            var mover = laserObj.GetComponent<LaserMoverNetworked>();
            if (mover != null)
            {
                Vector3 dir = (laneSide == Side.Left) ? Vector3.down : Vector3.up;
                mover.Initialize(dir, _currentSpeed);
            }
        }
    }

    private void OnDisable()
    {
        if (_spawnRoutine != null) StopCoroutine(_spawnRoutine);
    }
}
