using Fusion;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class MG5MovingBox : NetworkBehaviour
{
    [Header("Grid Movement")]
    public int gridCount = 5;
    public float cellSize = 1f;

    [Networked] private float _networkedSpeed { get; set; }
    [Networked] private int _currentCell { get; set; }
    [Networked] private float _spawnZ { get; set; }
    [Networked] private NetworkBool _isMoving { get; set; }
    [Networked] private int _direction { get; set; }
    [Networked] private float _moveTimer { get; set; }

    // Lane không sync được → dùng NetworkId để tìm lại
    [Networked] private NetworkId _laneId { get; set; }
    private MG5Lane _lane;
    public int CurrentCell => _currentCell;

    public void Initialize(MG5Lane lane, float speed)
    {
        _lane = lane;
        _laneId = lane.Object.Id;
        _networkedSpeed = speed;
        _spawnZ = transform.position.z;
        _currentCell = 0;
        _direction = 1;
        _isMoving = true;
        _moveTimer = 0f;
        SnapToCell();
    }

    public override void Spawned()
    {
        // Tìm lại lane từ NetworkId sau khi spawn/rejoin
        if (_laneId != default && _lane == null)
        {
            var laneObj = Runner.FindObject(_laneId);
            if (laneObj != null)
                _lane = laneObj.GetComponent<MG5Lane>();
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;
        if (!_isMoving) return;

        _moveTimer += Runner.DeltaTime;
        float interval = 1f / _networkedSpeed;

        if (_moveTimer >= interval)
        {
            _moveTimer = 0f;
            MoveToNextCell();
        }
    }

    private void MoveToNextCell()
    {
        _currentCell += _direction;

        if (_currentCell >= gridCount)
        {
            _direction = -1;
            _currentCell = gridCount - 1;
        }
        else if (_currentCell < 0)
        {
            _direction = 1;
            _currentCell = 0;
        }

        SnapToCell();
    }

    private void SnapToCell()
    {
        var pos = transform.position;
        pos.z = _spawnZ + _currentCell * cellSize;
        transform.position = pos;
    }

    public void Drop()
    {
        if (!HasStateAuthority) return;
        if (!_isMoving) return;

        _isMoving = false;
        Debug.Log($"[MG5MovingBox] Dropped at cell {_currentCell}");

        // Tìm lại lane nếu null
        if (_lane == null && _laneId != default)
        {
            var laneObj = Runner.FindObject(_laneId);
            if (laneObj != null)
                _lane = laneObj.GetComponent<MG5Lane>();
        }

        _lane?.PlaceBox(Object);
    }
}