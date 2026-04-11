using UnityEngine;

/// <summary>
/// Tủ đồ để customize character trong Lobby
/// </summary>
public class WardrobeInteractable : InteractableObject
{
    [Header("Wardrobe Settings")]
    [SerializeField] private CustomizationManager customizationManager;
    
    private bool _isCustomizing = false;

    private void Start()
    {
        promptText = "Customize";
        interactionKey = KeyCode.E;
        
        if (customizationManager == null)
            customizationManager = FindAnyObjectByType<CustomizationManager>();
    }

    public override bool CanInteract()
    {
        return base.CanInteract() && !_isCustomizing;
    }

    public override void Interact()
    {
        base.Interact();
        OpenCustomization();
    }

    private void OpenCustomization()
    {
        if (customizationManager == null)
        {
            Debug.LogWarning("[WardrobeInteractable] CustomizationManager not found!");
            return;
        }
        
        _isCustomizing = true;
        
        // Mở UI customization
        customizationManager.OpenCustomizationUI();
        
        Debug.Log("[WardrobeInteractable] Opening customization UI");
    }

    /// <summary>
    /// Gọi khi đóng UI customization
    /// </summary>
    public void CloseCustomization()
    {
        _isCustomizing = false;
        Debug.Log("[WardrobeInteractable] Closed customization UI");
    }
}
