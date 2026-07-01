// using Fusion;
// using UnityEngine;

// public class MG3ItemSpawner : NetworkBehaviour
// {
//     [Header("References")]
//     [SerializeField] private NetworkObject itemPrefab;
//     [SerializeField] private Transform[]   spawnPoints;

//     [Header("Settings")]
//     [SerializeField] private float spawnInterval = 5f;

//     // [Networked] private float _spawnTimer { get; set; } = 0f;

//     // Track xem slot nào đang có item
//     private readonly bool[] _occupied;

//     public MG3ItemSpawner()
//     {
//         _occupied = new bool[4];
//     }

//     public override void FixedUpdateNetwork()
//     {
//         if (!HasStateAuthority) return;
//         if (spawnPoints == null || spawnPoints.Length == 0) return;

//         _spawnTimer -= Runner.DeltaTime;
//         if (_spawnTimer > 0f) return;

//         _spawnTimer = spawnInterval;
//         SpawnItems();
//     }

//     private void SpawnItems()
//     {
//         for (int i = 0; i < spawnPoints.Length; i++)
//         {
//             if (_occupied[i]) continue;
//             if (spawnPoints[i] == null) continue;

//             var no = Runner.Spawn(itemPrefab, spawnPoints[i].position, spawnPoints[i].rotation, inputAuthority: null);

//             var pickup = no.GetComponent<MG3ItemPickup>();
//             if (pickup != null)
//             {
//                 pickup.SpawnPointIndex = i;
//                 pickup.OnPickedUp      = OnItemPickedUp;
//             }

//             _occupied[i] = true;
//             Debug.Log($"[MG3ItemSpawner] Spawned item at slot {i}");
//         }
//     }

//     private void OnItemPickedUp(int slotIndex)
//     {
//         if (slotIndex >= 0 && slotIndex < _occupied.Length)
//             _occupied[slotIndex] = false;
//     }
// }