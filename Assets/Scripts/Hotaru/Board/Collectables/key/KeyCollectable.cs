// using Fusion;
// using UnityEngine;

// public class KeyCollectable : BoardCollectable
// {
//     [Networked]
//     public int CurrentNodeID { get; set; } = -1;
//     /// <summary>
//     /// Thu thập Key.
//     /// </summary>
//     public void Collect(PlayerItemInventory inventory)
//     {
//         if (IsCollected)
//             return;

//         IsCollected = true;

//         inventory.AddKey();


//         BoardCollectableManager.Instance.NotifyKeyCollected(this);
//     }

//     [SerializeField]
//     private BoardNode boardNode;

//     public BoardNode BoardNode => boardNode;

//     public bool IsOnNode(BoardNode node)
//     {
//         return boardNode == node;
//     }
//     public override void Render()
//     {
//         base.Render();

//         if (CurrentNodeID < 0)
//             return;

//         BoardNode node =
//             BoardNodePath.Instance.GetNodeByID(CurrentNodeID);

//         if (node == null)
//             return;

//         boardNode = node;

//         transform.position =
//             node.transform.position + Vector3.up * 0.6f;
//     }

//     // private void Start()
//     // {
//     //     SetBoardNode(boardNode);
//     // }

//     /// <summary>
//     /// Di chuyển Key sang BoardNode mới.
//     /// </summary>
//     public void SetBoardNode(BoardNode node)
//     {
//         if (node == null)
//             return;

//         boardNode = node;

//         CurrentNodeID = node.nodeID;
//     }
// }
    