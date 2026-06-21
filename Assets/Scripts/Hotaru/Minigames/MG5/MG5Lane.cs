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
    [SerializeField] private Transform[] spawnPoints; // 5 điểm spawn cho 5 tầng
    [SerializeField] private Transform stackRoot;     // gốc stack (dùng để snap box)

    [Header("Prefabs")]
    [SerializeField] private NetworkObject movingBoxPrefab;

    [Networked] public int CurrentHeight { get; private set; } = 0;

    // MỚI — Player sở hữu lane này
    [Networked] public PlayerRef OwnerPlayer { get; set; }

    private NetworkObject _currentMovingBox;

    /// <summary>
    /// MỚI — Gọi bởi MG5StackController để gán Player cho lane này.
    /// Phải gọi TRƯỚC SpawnNewBox().
    /// </summary>
    public void AssignOwner(PlayerRef player)
    {
        OwnerPlayer = player;
    }

    /// <summary>
    /// Spawn box mới tại spawn point hiện tại.
    /// </summary>
    public void SpawnNewBox()
    {
        if (!HasStateAuthority) return;
        if (CurrentHeight >= spawnPoints.Length) return; // đã đủ tầng

        var spawnPoint = spawnPoints[CurrentHeight];
        var pos = spawnPoint.position;
        var rot = Quaternion.identity;

        _currentMovingBox = Runner.Spawn(movingBoxPrefab, pos, rot, OwnerPlayer);
        var mover = _currentMovingBox.GetComponent<MG5MovingBox>();
        if (mover != null)
        {
            mover.Initialize(this);
        }

        // MỚI — Tìm Player theo OwnerPlayer, forward box hiện tại cho nó
        var allForwarders = FindObjectsByType<MG5PlayerInputForwarder>(FindObjectsSortMode.None);
        foreach (var forwarder in allForwarders)
        {
            if (forwarder.Object.InputAuthority == OwnerPlayer)
            {
                forwarder.SetCurrentBox(mover);
                break;
            }
        }

        Debug.Log($"[MG5Lane] Spawn new box at height {CurrentHeight} for player {OwnerPlayer}");
    }

    /// <summary>
    /// Gọi khi box được đặt thành công.
    /// Snap box vào stackRoot theo CurrentHeight.
    /// </summary>
    public void PlaceBox(NetworkObject box)
    {
        if (!HasStateAuthority) return;

        var targetY = CurrentHeight;
        var snapPos = stackRoot.position + Vector3.up * targetY;
        box.transform.position = new Vector3(stackRoot.position.x, snapPos.y, stackRoot.position.z);

        CurrentHeight++;
        Debug.Log($"[MG5Lane] Box placed. Height = {CurrentHeight}");

        // Spawn box mới nếu chưa đủ tầng
        if (CurrentHeight < spawnPoints.Length)
        {
            SpawnNewBox();
        }
        else
        {
            // Player đạt target height → báo controller
            // SỬA — tìm theo OwnerPlayer thay vì GetComponentInParent
            // vì Lane và Player không còn quan hệ cha-con
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
                        {
                            MG5StackController.Instance?.PlayerFinished(OwnerPlayer);
                        }
                    }
                    break;
                }
            }
        }
    }
}