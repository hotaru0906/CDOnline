using Fusion;
using UnityEngine;

/// <summary>
/// MG5 — Lane quản lý box chạy và stack.
/// Mỗi lane có 5 spawn points cho 5 tầng.
/// Mỗi lane thuộc về 1 Player (OwnerPlayer), được gán bởi Controller.
/// </summary>
public class MG5Lane : NetworkBehaviour
{
    [Header("Lane Setup")]
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private Transform stackRoot;

    [Header("Prefabs")]
    [SerializeField] private NetworkObject movingBoxPrefab;

    [Header("Speed Settings")]
    [SerializeField] private float baseSpeed = 3f;
    [SerializeField] private float speedPerFloor = 0.8f;

    [Header("Lane Visuals")]
    [SerializeField] private GameObject[] visualObjects;

    [Networked] public int CurrentHeight { get; private set; } = 0;

    [Networked, OnChangedRender(nameof(OnOwnerChanged))]
    public PlayerRef OwnerPlayer { get; set; }

    [Networked] private int _lastPlacedCell { get; set; } = 0;
    [Networked] private NetworkBool _hasPlacedFirst { get; set; } = false;
    [Networked, Capacity(10)] private NetworkArray<int> _cellHeights { get; }

    private NetworkObject _currentMovingBox;

    public override void Spawned()
    {
        SetVisualsActive(false);
        if (OwnerPlayer != PlayerRef.None)
            SetVisualsActive(true);
    }

    public void AssignOwner(PlayerRef player)
    {
        OwnerPlayer = player;
    }

    private void OnOwnerChanged()
    {
        bool hasOwner = OwnerPlayer != PlayerRef.None;
        SetVisualsActive(hasOwner);
    }

    private void SetVisualsActive(bool active)
    {
        foreach (var obj in visualObjects)
            if (obj != null) obj.SetActive(active);
    }

    public void SpawnNewBox()
    {
        if (!HasStateAuthority) return;
        if (CurrentHeight >= spawnPoints.Length) return;

        if (GameManager.Instance == null || GameManager.Instance.CurrentState != GameState.Playing) return;

        var spawnPoint = spawnPoints[CurrentHeight];
        _currentMovingBox = Runner.Spawn(movingBoxPrefab, spawnPoint.position, Quaternion.identity, OwnerPlayer);

        var mover = _currentMovingBox.GetComponent<MG5MovingBox>();
        if (mover != null)
        {
            float speed = baseSpeed + CurrentHeight * speedPerFloor;
            mover.Initialize(this, speed);
        }

        var allForwarders = FindObjectsByType<MG5PlayerInputForwarder>(FindObjectsSortMode.None);
        foreach (var forwarder in allForwarders)
        {
            if (forwarder.Object.InputAuthority == OwnerPlayer)
            {
                forwarder.SetCurrentBox(mover);
                break;
            }
        }

        Debug.Log($"[MG5Lane] Spawn box tầng {CurrentHeight}, speed = {baseSpeed + CurrentHeight * speedPerFloor}");
    }

    public void PlaceBox(NetworkObject box)
    {
        if (!HasStateAuthority) return;

        if (CurrentHeight >= spawnPoints.Length)
        {
            Runner.Despawn(box);
            return;
        }

        var mover = box.GetComponent<MG5MovingBox>();
        int droppedCell = mover != null ? mover.CurrentCell : 0;

        int cellHeight = _cellHeights.Get(droppedCell);

        if (cellHeight >= spawnPoints.Length)
        {
            Runner.Despawn(box);
            SpawnNewBox();
            return;
        }

        // Snap box xuống đúng Y của cell đó
        var snapPos = stackRoot.position + Vector3.up * cellHeight;
        box.transform.position = new Vector3(stackRoot.position.x, snapPos.y, box.transform.position.z);
        _cellHeights.Set(droppedCell, cellHeight + 1);

        // Tầng đầu pass hết, từ tầng 2 phải match cell trước
        bool isCorrect = !_hasPlacedFirst || droppedCell == _lastPlacedCell;
        if (isCorrect)
        {
            _hasPlacedFirst = true;
            _lastPlacedCell = droppedCell;
            CurrentHeight++;
            Debug.Log($"[MG5Lane] Correct! Cell {droppedCell}. Height = {CurrentHeight}");

            if (CurrentHeight >= spawnPoints.Length)
            {
                var allPlayers = FindObjectsByType<PlayerMinigameData>(FindObjectsSortMode.None);
                foreach (var p in allPlayers)
                {
                    if (p.Object.InputAuthority == OwnerPlayer)
                    {
                        var stackData = p.GetComponent<MG5PlayerStackData>();
                        if (stackData != null)
                        {
                            stackData.IncreaseHeight();
                            if (stackData.CurrentStackHeight >= spawnPoints.Length)
                                MG5StackController.Instance?.PlayerFinished(OwnerPlayer);
                        }
                        break;
                    }
                }
                return;
            }
        }
        else
        {
            Debug.Log($"[MG5Lane] Wrong cell {droppedCell}, expected {_lastPlacedCell}.");
        }

        SpawnNewBox();
    }
}