using UnityEngine;

public class Chest : BoardCollectable
{
    [SerializeField]
    private BoardNode boardNode;

    private Animator animator;

    public BoardNode BoardNode => boardNode;

    public bool IsOpened { get; private set; }

    public void SetBoardNode(BoardNode node)
    {
        boardNode = node;

        if (boardNode == null)
            return;

        transform.position = boardNode.transform.position + Vector3.up * 0.6f;
    }

    public void Open()
    {
        animator.Play("Take 001", 0, 0f);
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
}