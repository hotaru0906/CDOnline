using UnityEngine;
using Fusion;

public class Chest : BoardCollectable
{
    [SerializeField]
    private BoardNode boardNode;

    private Animator animator;

    public BoardNode BoardNode => boardNode;

    public bool IsOpened { get; private set; }

    public int ChestIndex { get; private set; } = -1;

    [Networked]
    public int BoardNodeID { get; set; } = -1;

    public void SetChestIndex(int index)
    {
        ChestIndex = index;
    }

    public void SetBoardNode(BoardNode node)
    {
        boardNode = node;

        BoardNodeID = node.nodeID;

        transform.position = node.transform.position + Vector3.up * 0.6f;
    }

    public void Open()
    {
        animator.Play("Take 001", 0, 0f);
    }

    public void MarkOpened()
    {
        IsOpened = true;
    }

    public bool IsOnNode(BoardNode node)
    {
        return boardNode == node;
    }

    public bool IsOnNode(int nodeId)
    {
        return boardNode != null && boardNode.nodeID == nodeId;
    }

    private void Awake()
    {
        animator = GetComponent<Animator>();
    }

    public override void Spawned()
    {
        base.Spawned();

        Debug.Log($"CHEST Spawned on {Runner.LocalPlayer.PlayerId}  Object={Object.Id}");

        BoardChestManager.Instance?.RegisterChest(this);
    }

    public override void Render()
    {
        base.Render();

        if (BoardNodeID < 0)
            return;

        BoardNode node = BoardNodePath.Instance.GetNodeByID(BoardNodeID);

        if (node == null)
            return;

        if (boardNode != node)
        {
            boardNode = node;

            transform.position =
                node.transform.position + Vector3.up * 0.6f;

            Debug.Log($"[Chest] Sync Node {BoardNodeID}");
        }
    }
}