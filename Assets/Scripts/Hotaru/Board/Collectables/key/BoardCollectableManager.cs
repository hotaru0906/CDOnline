using Fusion;
using UnityEngine;
using System.Collections.Generic;
using System.Collections;


public class BoardCollectableManager : NetworkBehaviour
{

        public static BoardCollectableManager Instance { get; private set; }

        [Header("Respawn")]

        [SerializeField]
        private float keyRespawnTime = 10f;

        [Header("Key Collectables")]

        [SerializeField]
        private List<KeyCollectable> keyCollectables = new();

        [Header("Key Spawn Nodes")]

        [SerializeField]
        private List<BoardNode> keySpawnNodes = new();
        private void Awake()
        {
                if (Instance != null && Instance != this)
                {
                        Destroy(gameObject);
                        return;
                }

                Instance = this;
        }

        public override void Spawned()
        {
                if (!HasStateAuthority)
                        return;

                if (GameManager.Instance == null)
                {
                        Debug.LogError("[BoardCollectableManager] GameManager not found!");
                        return;
                }

                if (GameManager.Instance.HasSavedKeyState())
                {
                        Debug.Log("[BoardCollectableManager] Restore Key State");
                        RestoreKeys();
                }
                else
                {
                        Debug.Log("[BoardCollectableManager] Spawn Initial Keys");
                        SpawnInitialKeys();
                        SaveKeys();
                }
        }

        private void SaveKeys()
        {
                if (GameManager.Instance == null)
                return;

                for (int i = 0; i < keyCollectables.Count; i++)
                {
                        KeyCollectable key = keyCollectables[i];

                        if (key == null || key.BoardNode == null)
                                continue;

                        GameManager.Instance.SaveKeyState(
                                i,
                                key.BoardNode.nodeID,
                                key.IsCollected);
                }

                Debug.Log("[BoardCollectableManager] Key states saved.");
        }

        private void RestoreKeys()
        {
                if (GameManager.Instance == null)
                        return;

                foreach (KeyCollectable key in keyCollectables)
                {
                        if (key == null)
                        continue;

                        int index = keyCollectables.IndexOf(key);

                        int nodeId = GameManager.Instance.GetKeyNode(index);

                        BoardNode node = BoardNodePath.Instance.GetNodeByID(nodeId);

                        if (node == null)
                        {
                                Debug.LogWarning($"[BoardCollectableManager] Cannot find BoardNode {nodeId}");
                                        continue;
                        }

                       key.SetBoardNode(node);

                        key.IsCollected = GameManager.Instance.GetKeyCollected(index);
                }

                Debug.Log("[BoardCollectableManager] Key states restored.");
        }

        /// <summary>
        /// Kiểm tra xem người chơi có đứng trên Key không.
        /// Nếu có thì thu thập Key.
        /// </summary>
        public void TryCollectKey(int playerId, int nodeId)
        {
                if (!HasStateAuthority)
                return;
                
                var inventory = PlayerItemInventory.GetForPlayer(playerId);

                if (inventory == null)
                        return;

                foreach (var key in keyCollectables)
                {
                        if (key == null)
                        continue;

                        if (key.IsCollected)
                        continue;

                        if (key.BoardNode == null)
                        continue;

                        if (key.BoardNode.nodeID != nodeId)
                        continue;

                        key.Collect(inventory);
                        break;
                }
        }

        /// <summary>
        /// Thông báo Key đã được thu thập.
        /// Manager sẽ xử lý Respawn.
        /// </summary>
        public void NotifyKeyCollected(KeyCollectable key)
        {
                if (!HasStateAuthority)
                return;

                if (key == null)
                return;

                SaveKeys();

                StartCoroutine(RespawnKeyCoroutine(key));
        }

        private IEnumerator RespawnKeyCoroutine(KeyCollectable key)
        {
                yield return new WaitForSeconds(keyRespawnTime);

                if (key == null)
                        yield break;

                BoardNode randomNode = GetRandomSpawnNode(key);

                if (randomNode == null)
                        yield break;

                key.SetBoardNode(randomNode);

                key.IsCollected = false;

                SaveKeys();

                Debug.Log($"[RESPAWN] {key.name} -> Node {randomNode.nodeID}");
        }

        /// <summary>
        /// Lấy ngẫu nhiên một node để spawn Key.
        /// </summary>
       private BoardNode GetRandomSpawnNode(KeyCollectable currentKey)
        {
                if (keySpawnNodes.Count == 0)
                        return null;

                List<BoardNode> availableNodes = new();

                foreach (BoardNode node in keySpawnNodes)
                {
                        // Không respawn vào đúng node hiện tại
                        if (currentKey.IsOnNode(node))
                        continue;

                        // Không respawn vào node đang có Key khác
                        if (IsNodeOccupied(node, currentKey))
                        continue;

                        availableNodes.Add(node);
                }

                if (availableNodes.Count == 0)
                        return currentKey.BoardNode;

                int randomIndex = Random.Range(0, availableNodes.Count);

                return availableNodes[randomIndex];
        }

        /// <summary>
        /// Kiểm tra node đã có Key khác đứng trên chưa.
        /// </summary>
        private bool IsNodeOccupied(BoardNode node, KeyCollectable ignoreKey = null)
        {
                foreach (KeyCollectable key in keyCollectables)
                {
                        if (key == null)
                        continue;

                        if (key == ignoreKey)
                        continue;

                        if (key.IsCollected)
                        continue;

                        if (key.BoardNode == node)
                        return true;
                }

                return false;
        }

        /// <summary>
        /// Spawn toàn bộ Key vào các vị trí ngẫu nhiên khi bắt đầu game.
        /// </summary>
        private void SpawnInitialKeys()
        {
                List<BoardNode> availableNodes = new(keySpawnNodes);

                foreach (KeyCollectable key in keyCollectables)
                {
                        if (key == null)
                        continue;

                        if (availableNodes.Count == 0)
                        break;

                        int randomIndex = Random.Range(0, availableNodes.Count);

                        BoardNode node = availableNodes[randomIndex];

                        availableNodes.RemoveAt(randomIndex);

                        key.IsCollected = false;
                        key.SetBoardNode(node);

                        Debug.Log($"{key.name} current BoardNode = {key.BoardNode.nodeID}");
                        Debug.Log($"{key.name} Position = {key.transform.position}");
                }
        }
}