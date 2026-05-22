using UnityEngine;

public class InventoryInput : MonoBehaviour
{
    public InventoryManager inventoryManager;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.B))
        {
            inventoryManager.ToggleInventory();
        }
    }
}