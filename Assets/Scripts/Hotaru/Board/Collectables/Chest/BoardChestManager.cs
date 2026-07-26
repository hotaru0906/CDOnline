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
    private List<Chest> spawnedChests = new();

    private const int CHEST_COUNT = 8;

    private Chest interactionChest;

    private const int CHEST_KEY_COST = 10;

    private PlayerItemInventory currentInventory;

    public bool IsInteractionActive { get; private set; }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ShowChestUI(int playerId, int keyCount)
    {
        if (Runner.LocalPlayer.PlayerId != playerId)
            return;

        ChestUI.Instance.Show(keyCount);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_HideChestUI()
    {
        ChestUI.Instance?.Hide();
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_EndInteraction()
    {
        EndInteraction();
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_OpenChest()
    {
        OnOpenButtonPressed();
    }
    private void Awake()
    {
        Instance = this;
    }

    public void RegisterChest(Chest chest)
    {
        if (chest == null)
            return;

        if (spawnedChests.Contains(chest))
            return;

        spawnedChests.Add(chest);
        chest.SetChestIndex(spawnedChests.Count - 1);

        Debug.Log($"[ChestManager] Register Chest {chest.ChestIndex}");
    }
    // 4. Spawned
    public override void Spawned()
    {
        // Client chỉ restore vị trí
        if (!HasStateAuthority)
        {
            if (GameManager.Instance.HasSavedChestState())
            {
                RestoreChest();
            }

            return;
        }

        // ===== Host =====

        if (chestSpawnNodes.Count == 0)
        {
            Debug.LogWarning("[BoardChestManager] No Chest Spawn Nodes!");
            return;
        }

        SpawnInitialChests();
    }

    // 5. SpawnChest
    private void SpawnChest(Chest chest, BoardNode node)
    {
        if (chest == null)
            return;

        if (node == null)
        {
            Debug.LogError("[BoardChestManager] Spawn Node is NULL!");
            return;
        }

        chest.IsCollected = false;

        chest.SetBoardNode(node);

        chest.Show();

        Debug.Log($"[BoardChestManager] Chest {chest.ChestIndex} moved to Node {node.nodeID}");
    }

    private void SpawnInitialChests()
    {
        
        spawnedChests.Clear();

        if (chestSpawnNodes.Count == 0)
            return;

        List<BoardNode> availableNodes = new(chestSpawnNodes);

        // Shuffle
        for (int i = 0; i < availableNodes.Count; i++)
        {
            int r = Random.Range(i, availableNodes.Count);

            (availableNodes[i], availableNodes[r]) =
                (availableNodes[r], availableNodes[i]);
        }

        int spawnCount = Mathf.Min(CHEST_COUNT, availableNodes.Count);

        for (int i = 0; i < spawnCount; i++)
        {
            Chest chest = Runner.Spawn(
                chestPrefab,
                Vector3.zero,
                Quaternion.identity);

            if (chest == null)
                continue;

            Debug.Log($"HOST Spawn: {chest.Object.Id}");

            RegisterChest(chest);
            chest.SetBoardNode(availableNodes[i]);
            chest.Show();
        }

        Debug.Log($"[BoardChestManager] Spawned {spawnedChests.Count} chests.");
        
    }

    private void SaveChest()
    {
        foreach (Chest chest in spawnedChests)
        {
            if (chest == null)
                continue;

            if (chest.BoardNode == null)
                continue;

            GameManager.Instance.SaveChestState(
                chest.ChestIndex,
                chest.BoardNode.nodeID);

            Debug.Log($"[Chest] Saved Chest {chest.ChestIndex} -> Node {chest.BoardNode.nodeID}");
        }
    }

    private void RestoreChest()
    {
        for (int i = 0; i < spawnedChests.Count; i++)
        {
            Chest chest = spawnedChests[i];

            if (chest == null)
                continue;

            int nodeId = GameManager.Instance.GetChestNode(chest.ChestIndex);

            if (nodeId < 0)
                continue;

            BoardNode node = BoardNodePath.Instance.GetNodeByID(nodeId);

            if (node == null)
            {
                Debug.LogWarning($"[Chest] Cannot restore node {nodeId}");
                continue;
            }

            SpawnChest(chest, node);
        }
    }

    public bool TryOpenChest(int playerId, int nodeId)
    {
        interactionChest = null;

        foreach (Chest chest in spawnedChests)
        {
            if (chest == null)
                continue;

            if (chest.IsOnNode(nodeId))
            {
                interactionChest = chest;
                break;
            }
        }

        if (interactionChest == null)
        {
            Debug.Log($"[Chest] No chest found at Node {nodeId}");
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

            RPC_ShowChestUI(playerId, inventory.GetKeyCount());

            Debug.Log($"[Chest] Player {playerId} needs {CHEST_KEY_COST} keys.");

            return true;
        }

        IsInteractionActive = true;

        RPC_ShowChestUI(playerId, inventory.GetKeyCount());

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

        RPC_HideChestUI();

        interactionChest = null;
        currentInventory = null;
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

            // Giữ UI mở.
            RPC_ShowChestUI(
                currentInventory.Object.InputAuthority.PlayerId,
                currentInventory.GetKeyCount());

            return;
        }

        Debug.Log("[Chest] Keys Consumed Successfully!");

        if (interactionChest == null)
        {
            Debug.LogError("[Chest] Interaction Chest is NULL");
            EndInteraction();
            return;
        }

        interactionChest.Open();

        currentInventory.AddChest();

        interactionChest.Hide();

        RPC_HideChestUI();

        if (currentInventory.GetChestCount() >= 2)
        {
            BoardManager.Instance.EndGame(
                currentInventory.Object.InputAuthority.PlayerId);

            EndInteraction();
            return;
        }

        BoardNode newNode = GetRandomSpawnNode();

        SpawnChest(interactionChest, newNode);

        SaveChest();

        EndInteraction();
            }

}