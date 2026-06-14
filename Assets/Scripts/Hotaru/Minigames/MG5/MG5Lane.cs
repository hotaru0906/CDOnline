using Fusion;
using UnityEngine;

/// <summary>
/// MG5 — Lane quản lý box chạy và stack.
/// Mỗi lane có 5 spawn points cho 5 tầng.
/// </summary>
public class MG5Lane : NetworkBehaviour
{
    [Header("Lane Setup")]
    [SerializeField] private Transform[] spawnPoints; // 5 điểm spawn cho 5 tầng
    [SerializeField] private Transform stackRoot;     // gốc stack (dùng để snap box)

    [Header("Prefabs")]
    [SerializeField] private NetworkObject movingBoxPrefab;

    [Networked] public int CurrentHeight { get; private set; } = 0;

    private NetworkObject _currentMovingBox;

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

        _currentMovingBox = Runner.Spawn(movingBoxPrefab, pos, rot, Object.InputAuthority);
        var mover = _currentMovingBox.GetComponent<MG5MovingBox>();
        if (mover != null)
        {
            mover.Initialize(this);
        }

        Debug.Log($"[MG5Lane] Spawn new box at height {CurrentHeight}");
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
            var playerData = GetComponentInParent<PlayerMinigameData>();
            if (playerData != null)
            {
                var stackData = playerData.GetComponent<MG5PlayerStackData>();
                if (stackData != null)
                {
                    stackData.IncreaseHeight();
                    if (stackData.CurrentStackHeight >= spawnPoints.Length)
                    {
                        MG5StackController.Instance?.PlayerFinished(playerData.Object.InputAuthority);
                    }
                }
            }
        }
    }
}
