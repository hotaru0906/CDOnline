using Fusion;
using UnityEngine;
using System.Collections.Generic;
using System.Collections;
public class BoardChestManager : NetworkBehaviour
{
    // 1. Singleton
    public static BoardChestManager Instance;

    // 2. Inspector
    [SerializeField]
    private Chest chestPrefab;

    [SerializeField]
    private List<BoardNode> chestSpawnNodes = new();

    // 3. Runtime
    [SerializeField]
    private Chest currentChest;

    private const int CHEST_KEY_COST = 10;

    private PlayerItemInventory currentInventory;

    public bool IsInteractionActive { get; private set; }

    private void Awake()
    {
        Instance = this;
    }

    // 4. Spawned
    public override void Spawned()
    {
        if (!HasStateAuthority)
            return;

        if (chestSpawnNodes.Count == 0)
        {
            Debug.LogWarning("[BoardChestManager] No Chest Spawn Nodes!");
            return;
        }

        if (GameManager.Instance.HasSavedChestState())
        {
            RestoreChest();
        }
        else
        {
            BoardNode randomNode = GetRandomSpawnNode();

            SpawnChest(randomNode);

            SaveChest();
        }
    }

    // 5. SpawnChest
    private void SpawnChest(BoardNode node)
    {
        if (currentChest == null)
            return;

        if (node == null)
        {
            Debug.LogError("[BoardChestManager] Spawn Node is NULL!");
            return;
        }

        currentChest.IsCollected = false;

        currentChest.SetBoardNode(node);

        currentChest.Show();   // <-- thêm dòng này

        Debug.Log($"[BoardChestManager] Chest moved to Node {node.nodeID}");
    }

    private void SaveChest()
    {
        if (currentChest == null)
            return;

        if (currentChest.BoardNode == null)
            return;

        GameManager.Instance.SaveChestState(currentChest.BoardNode.nodeID);

        Debug.Log($"[Chest] Saved Node {currentChest.BoardNode.nodeID}");
    }

    private void RestoreChest()
    {
        int nodeId = GameManager.Instance.GetChestNode();

        BoardNode node = BoardNodePath.Instance.GetNodeByID(nodeId);

        if (node == null)
        {
            Debug.LogError($"[Chest] Cannot restore node {nodeId}");
            return;
        }

        SpawnChest(node);

        Debug.Log($"[Chest] Restored Node {nodeId}");
    }

    public bool TryOpenChest(int playerId, int nodeId)
    {
        if (currentChest == null)
        {
            Debug.LogWarning("[Chest] Current Chest is NULL");
            return false;
        }

        if (!currentChest.IsOnNode(nodeId))
        {
            return false;
        }

        PlayerItemInventory inventory =
            PlayerItemInventory.GetForPlayer(playerId);

        currentInventory = inventory;

        if (inventory == null)
        {
            return false;
        }

        if (inventory.GetKeyCount() < CHEST_KEY_COST)
        {
            IsInteractionActive = true;

            Debug.Log($"ChestUI.Instance = {ChestUI.Instance}");

            ChestUI.Instance.Show(inventory.GetKeyCount());

            Debug.Log($"[Chest] Player {playerId} needs {CHEST_KEY_COST} keys.");

            return true;
        }

        IsInteractionActive = true;

        ChestUI.Instance.Show(inventory.GetKeyCount());

        Debug.Log($"[Chest] Player {playerId} can open the Chest.");

        return true;
        
    }

    private BoardNode GetRandomSpawnNode()
    {
        if (chestSpawnNodes.Count == 0)
            return null;

        int randomIndex = Random.Range(0, chestSpawnNodes.Count);

        return chestSpawnNodes[randomIndex];
    }

    private IEnumerator TestCloseInteraction()
    {
        yield return new WaitForSeconds(2f);

        EndInteraction();
    }

    public void EndInteraction()
    {
        IsInteractionActive = false;
    }
    
    public void OnOpenButtonPressed()
    {
        if (currentInventory == null)
        {
            Debug.LogError("[Chest] Inventory NULL");
            return;
        }

        if (!currentInventory.ConsumeKey(CHEST_KEY_COST))
        {
            Debug.Log("[Chest] Not enough Keys!");
            return;
        }

        Debug.Log("[Chest] Keys Consumed Successfully!");
        currentChest.Open();

        currentInventory.AddChest();

        currentChest.Hide();

        if (currentInventory.GetChestCount() >= 2)
        {
            BoardManager.Instance.EndGame(
                currentInventory.Object.InputAuthority.PlayerId);

            EndInteraction();
            return;
        }

        BoardNode newNode = GetRandomSpawnNode();

        SpawnChest(newNode);

        SaveChest();

        EndInteraction();
            }

}